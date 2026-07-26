/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of WWCP EEBUS <https://github.com/OpenChargingCloud/WWCP_EEBUS>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using cloud.charging.open.protocols.EEBUS.SPINE;
using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.CEVC
{

    /// <summary>
    /// The car of "Coordinated EV Charging" - the side which wants energy and
    /// says what it will do with it.
    ///
    /// It is the server of all four data scenarios, which is unusual and is the
    /// point: the demand, the power limits and the plan all live on the car's
    /// TimeSeries feature and the prices on its IncentiveTable feature, so
    /// everybody involved reads the same three curves from the same place rather
    /// than each holding their own copy.
    ///
    /// Being the server also means the car cannot ask anybody for anything - a
    /// SPINE server answers, it does not request. So when it needs a fresh power
    /// limit curve or a fresh incentive table it raises **updateRequired** on
    /// the description its clients are subscribed to, and lowers it when the
    /// write arrives ([CEVC-015], [CEVC-030]). That flag is the whole of the
    /// car's ability to start a conversation.
    /// </summary>
    public class CEVCElectricVehicle : AUseCase
    {

        #region Data

        private readonly UInt32  demandSeriesId;
        private readonly UInt32  constraintsSeriesId;
        private readonly UInt32  planSeriesId;

        private readonly UInt32  tariffId;

        #endregion

        #region Properties

        /// <summary>
        /// The time series server feature, which holds all three curves.
        /// </summary>
        public SPINELocalFeature   TimeSeries      { get; }

        /// <summary>
        /// The incentive table server feature, which holds the prices - or null
        /// when this car does not talk to an energy broker (scenario 3).
        /// </summary>
        public SPINELocalFeature?  IncentiveTable  { get; }

        /// <summary>
        /// The device diagnosis client feature, with which it watches the energy
        /// guard and the energy broker (scenarios 5 to 8).
        /// </summary>
        public SPINELocalFeature   Diagnosis       { get; }

        #endregion

        #region Events

        /// <summary>
        /// The energy guard wrote a new maximum power limitation curve
        /// (scenario 2).
        /// </summary>
        public event Action<CEVCElectricVehicle, IReadOnlyList<PowerSlot>>? OnPowerLimitsWritten;

        /// <summary>
        /// The energy broker wrote a new incentive table (scenario 3).
        /// </summary>
        public event Action<CEVCElectricVehicle, IReadOnlyList<IncentiveSlot>>? OnIncentivesWritten;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the EV of CEVC to an entity.
        /// </summary>
        /// <param name="Entity">The entity of the electric vehicle.</param>
        /// <param name="WithBroker">Whether this car talks to an energy broker as well (scenarios 3, 6 and 8).</param>
        public CEVCElectricVehicle(SPINELocalEntity  Entity,
                                   Boolean           WithBroker   = true)

            : base(Entity,
                   UseCaseActors.EV,
                   CoordinatedEVCharging.Name,
                   CoordinatedEVCharging.Version,
                   CoordinatedEVCharging.ElectricVehicleScenarios(WithBroker),
                   [ UseCaseActors.EnergyGuard, UseCaseActors.EnergyBroker, UseCaseActors.CEM ],
                   [ EntityTypeType.CEM ],
                   CoordinatedEVCharging.DocumentSubRevision)

        {

            #region The time series server: the three curves (scenarios 1, 2 and 4)

            TimeSeries = Entity.Feature(FeatureTypeType.TimeSeries, RoleType.Server)
                             ?? Entity.AddFeature(FeatureTypeType.TimeSeries, RoleType.Server);

            TimeSeries.AddFunction(CoordinatedEVCharging.TimeSeriesDescriptionListData,
                                   Read:         true,
                                   PartialRead:  true);

            TimeSeries.AddFunction(CoordinatedEVCharging.TimeSeriesConstraintsListData,
                                   Read:         true,
                                   PartialRead:  true);

            // Writeable because of the constraints series alone; the demand and
            // the plan are the car's own and nobody else may touch them, which
            // is what ApproveTimeSeries enforces entry by entry.
            TimeSeries.AddFunction(CoordinatedEVCharging.TimeSeriesListData,
                                   Read:          true,
                                   Write:         true,
                                   PartialRead:   true,
                                   PartialWrite:  true);

            var descriptions = TimeSeries.DataCopy<TimeSeriesDescriptionListDataType>(CoordinatedEVCharging.TimeSeriesDescriptionListData)?.
                                   TimeSeriesDescriptionData?.ToList() ?? [];

            var ids          = UseCaseIds.NextFree(descriptions.Select(description => description.TimeSeriesId),
                                                   Count: 3).ToList();

            demandSeriesId       = ids[0];
            constraintsSeriesId  = ids[1];
            planSeriesId         = ids[2];

            descriptions.Add(new TimeSeriesDescriptionDataType {
                                 TimeSeriesId         = demandSeriesId,
                                 TimeSeriesType       = CoordinatedEVCharging.Demand,
                                 TimeSeriesWriteable  = false,
                                 Unit                 = UnitOfMeasurementType.Wh
                             });

            descriptions.Add(new TimeSeriesDescriptionDataType {
                                 TimeSeriesId         = constraintsSeriesId,
                                 TimeSeriesType       = CoordinatedEVCharging.Constraints,
                                 TimeSeriesWriteable  = true,
                                 UpdateRequired       = false,
                                 Unit                 = UnitOfMeasurementType.W
                             });

            descriptions.Add(new TimeSeriesDescriptionDataType {
                                 TimeSeriesId         = planSeriesId,
                                 TimeSeriesType       = CoordinatedEVCharging.Plan,
                                 TimeSeriesWriteable  = false,
                                 Unit                 = UnitOfMeasurementType.W
                             });

            TimeSeries.FunctionData(CoordinatedEVCharging.TimeSeriesDescriptionListData)!.SetData(
                new TimeSeriesDescriptionListDataType { TimeSeriesDescriptionData = descriptions }
            );

            var approvedBySomeoneElse = TimeSeries.WriteApproval;
            TimeSeries.WriteApproval  = async (message, cancellationToken) =>
                await ApproveTimeSeries(message, cancellationToken)
                    ?? (approvedBySomeoneElse is not null
                            ? await approvedBySomeoneElse(message, cancellationToken)
                            : null);

            #endregion

            #region The incentive table server: the prices (scenario 3)

            if (WithBroker)
            {

                IncentiveTable = Entity.Feature(FeatureTypeType.IncentiveTable, RoleType.Server)
                                     ?? Entity.AddFeature(FeatureTypeType.IncentiveTable, RoleType.Server);

                IncentiveTable.AddFunction(CoordinatedEVCharging.IncentiveTableDescriptionData,
                                           Read:          true,
                                           Write:         true,
                                           PartialRead:   true,
                                           PartialWrite:  true);

                IncentiveTable.AddFunction(CoordinatedEVCharging.IncentiveTableConstraintsData,
                                           Read:         true,
                                           PartialRead:  true);

                IncentiveTable.AddFunction(CoordinatedEVCharging.IncentiveTableData,
                                           Read:          true,
                                           Write:         true,
                                           PartialRead:   true,
                                           PartialWrite:  true);

                var tables = IncentiveTable.DataCopy<IncentiveTableDescriptionDataType>(CoordinatedEVCharging.IncentiveTableDescriptionData)?.
                                 IncentiveTableDescription?.ToList() ?? [];

                tariffId = UseCaseIds.NextFree(tables.Select(table => table.TariffDescription?.TariffId));

                tables.Add(Tariff(tariffId));

                IncentiveTable.FunctionData(CoordinatedEVCharging.IncentiveTableDescriptionData)!.SetData(
                    new IncentiveTableDescriptionDataType { IncentiveTableDescription = tables }
                );

            }

            #endregion

            #region The device diagnosis client: watching both partners (scenarios 5 to 8)

            Diagnosis = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Client)
                            ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Client);

            #endregion

            Device.Events.Subscribe<SPINEDataChanged>(Watch, SPINEEventLevel.Core);

        }


        /// <summary>
        /// The tariff this car offers an energy broker to fill in
        /// (Table 9).
        ///
        /// One tier with one power boundary and one cost incentive - the
        /// "simpleIncentiveTable" scope, which is the shape the use case names
        /// and the only one a car has any reason to describe. The broker then
        /// writes what the energy costs when; it does not get to invent a
        /// different table shape.
        /// </summary>
        private static IncentiveTableDescriptionType Tariff(UInt32 TariffId)

            => new () {

                   TariffDescription = new TariffDescriptionDataType {
                                           TariffId        = TariffId,
                                           TariffWriteable = true,
                                           UpdateRequired  = false,
                                           ScopeType       = ScopeTypeType.SimpleIncentiveTable
                                       },

                   Tier = [
                       new IncentiveTableDescriptionTierType {

                           TierDescription = new TierDescriptionDataType {
                                                 TierId    = 1,
                                                 TierType  = TierTypeType.DynamicCost
                                             },

                           BoundaryDescription = [
                               new TierBoundaryDescriptionDataType {
                                   BoundaryId    = 1,
                                   BoundaryType  = TierBoundaryTypeType.PowerBoundary,
                                   BoundaryUnit  = UnitOfMeasurementType.W
                               }
                           ],

                           IncentiveDescription = [
                               new IncentiveDescriptionDataType {
                                   IncentiveId    = 1,
                                   IncentiveType  = IncentiveTypeType.AbsoluteCost,
                                   Currency       = CurrencyType.EUR,
                                   Unit           = UnitOfMeasurementType.Wh
                               }
                           ]

                       }
                   ]

               };

        #endregion


        #region Demand / SetDemand(Demand, ...)

        /// <summary>
        /// What this car is currently asking for (scenario 1).
        /// </summary>
        public ChargingDemand? Demand
        {
            get
            {

                var slot = Series(demandSeriesId)?.TimeSeriesSlot?.FirstOrDefault();

                if (slot is null)
                    return null;

                return new ChargingDemand(slot.Duration?.AsTimeSpan,
                                          slot.MinValue?.Value,
                                          slot.Value?.Value,
                                          slot.MaxValue?.Value,
                                          Series(demandSeriesId)?.TimePeriod?.StartTime?.AsTimeSpan);

            }
        }


        /// <summary>
        /// Say how much energy this car wants and by when, and tell whoever
        /// subscribed ([CEVC-001] to [CEVC-005]).
        ///
        /// One slot, because "singleDemand" means exactly that: "Only one
        /// timeslot SHALL be used."
        /// </summary>
        /// <param name="Demand">What the car wants.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="ArgumentException">When the demand says nothing at all.</exception>
        public async Task SetDemand(ChargingDemand     Demand,
                                    CancellationToken  CancellationToken   = default)
        {

            if (Demand.IsEmpty)
                throw new ArgumentException("A charging demand has to state at least one value; " +
                                            "section 2.4.1.1 asks for as much as the car can give.",
                                            nameof(Demand));

            await Write(new TimeSeriesDataType {

                            TimeSeriesId    = demandSeriesId,

                            TimePeriod      = Demand.Arrival.HasValue
                                                  ? new TimePeriodType {
                                                        StartTime = AbsoluteOrRelativeTimeType.Parse(Demand.Arrival.Value)
                                                    }
                                                  : null,

                            TimeSeriesSlot  = [
                                new TimeSeriesSlotType {
                                    TimeSeriesSlotId  = 1,
                                    Duration          = Demand.Departure.HasValue
                                                            ? DurationType.Parse(Demand.Departure.Value)
                                                            : null,
                                    Value             = Demand.OptimumEnergy is Decimal optimum ? ScaledNumberType.FromValue(optimum) : null,
                                    MinValue          = Demand.MinimumEnergy is Decimal minimum ? ScaledNumberType.FromValue(minimum) : null,
                                    MaxValue          = Demand.MaximumEnergy is Decimal maximum ? ScaledNumberType.FromValue(maximum) : null
                                }
                            ]

                        },
                        CancellationToken);

        }

        #endregion

        #region PowerLimits / RequestPowerLimits(...)

        /// <summary>
        /// The maximum power limitation curve the energy guard last wrote
        /// (scenario 2).
        /// </summary>
        public IReadOnlyList<PowerSlot> PowerLimits

            => CoordinatedEVCharging.Slots(Series(constraintsSeriesId));


        /// <summary>
        /// Ask the energy guard for a fresh power limitation curve
        /// ([CEVC-015]).
        ///
        /// A server cannot make a request, so this raises **updateRequired** on
        /// the description and waits. The flag goes down by itself when the
        /// curve arrives.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task RequestPowerLimits(CancellationToken CancellationToken = default)

            => SetUpdateRequired(constraintsSeriesId, true, CancellationToken);


        /// <summary>
        /// Whether this car is currently asking for a new power limitation
        /// curve.
        /// </summary>
        public Boolean PowerLimitsRequested

            => Description(constraintsSeriesId)?.UpdateRequired == true;

        #endregion

        #region Plan / SetPlan(Slots, ...)

        /// <summary>
        /// The charging plan this car last published (scenario 4).
        /// </summary>
        public IReadOnlyList<PowerSlot> Plan

            => CoordinatedEVCharging.Slots(Series(planSeriesId));


        /// <summary>
        /// Publish what this car intends to draw over time, and tell whoever
        /// subscribed ([CEVC-008]).
        ///
        /// This is the answer to everything else: given what it needs, what the
        /// connection allows and what the energy costs, this is what the car
        /// will actually do. The energy guard needs it in order to plan the rest
        /// of the building around the car rather than merely against it.
        /// </summary>
        /// <param name="Slots">The plan, slot by slot, in watts.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task SetPlan(IEnumerable<PowerSlot>  Slots,
                            CancellationToken       CancellationToken   = default)

            => Write(new TimeSeriesDataType {
                         TimeSeriesId    = planSeriesId,
                         TimePeriod      = new TimePeriodType {
                                               StartTime = AbsoluteOrRelativeTimeType.Parse(TimeSpan.Zero)
                                           },
                         TimeSeriesSlot  = CoordinatedEVCharging.Slots(Slots)
                     },
                     CancellationToken);

        #endregion

        #region Incentives / RequestIncentives(...)

        /// <summary>
        /// The prices the energy broker last wrote (scenario 3).
        /// </summary>
        public IReadOnlyList<IncentiveSlot> Incentives
        {
            get
            {

                var table = IncentiveTable?.DataCopy<IncentiveTableDataType>(CoordinatedEVCharging.IncentiveTableData)?.
                                IncentiveTable?.FirstOrDefault(entry => entry.Tariff?.TariffId == tariffId);

                var slots = new List<IncentiveSlot>();

                foreach (var slot in table?.IncentiveSlot ?? [])
                {

                    var cost = slot.Tier?.FirstOrDefault()?.
                                   Incentive?.FirstOrDefault()?.
                                   Value?.Value;

                    if (cost is not Decimal value ||
                        slot.TimeInterval?.StartTime?.Relative?.AsTimeSpan is not TimeSpan start)
                        continue;

                    slots.Add(new IncentiveSlot(start,
                                                slot.TimeInterval.EndTime?.Relative?.AsTimeSpan,
                                                value));

                }

                return slots;

            }
        }


        /// <summary>
        /// Ask the energy broker for a fresh incentive table ([CEVC-030]).
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">When this car does not talk to an energy broker.</exception>
        public async Task RequestIncentives(CancellationToken CancellationToken = default)
        {

            if (IncentiveTable is null)
                throw new InvalidOperationException("This electric vehicle does not support scenario 3 of the coordinated EV charging.");

            var data = IncentiveTable.DataCopy<IncentiveTableDescriptionDataType>(CoordinatedEVCharging.IncentiveTableDescriptionData)
                           ?? new IncentiveTableDescriptionDataType { IncentiveTableDescription = [] };

            var mine = data.IncentiveTableDescription?.
                           FirstOrDefault(table => table.TariffDescription?.TariffId == tariffId);

            if (mine?.TariffDescription is null)
                return;

            mine.TariffDescription.UpdateRequired = true;

            await IncentiveTable.SetData(CoordinatedEVCharging.IncentiveTableDescriptionData,
                                         data,
                                         CancellationToken: CancellationToken);

        }


        /// <summary>
        /// Whether this car is currently asking for a new incentive table.
        /// </summary>
        public Boolean IncentivesRequested

            => IncentiveTable?.DataCopy<IncentiveTableDescriptionDataType>(CoordinatedEVCharging.IncentiveTableDescriptionData)?.
                   IncentiveTableDescription?.
                   FirstOrDefault(table => table.TariffDescription?.TariffId == tariffId)?.
                   TariffDescription?.UpdateRequired == true;

        #endregion


        #region (private) Series(Id) / Description(Id) / Write(Data, ...) / SetUpdateRequired(...)

        /// <summary>
        /// One of the three curves, as it currently stands.
        /// </summary>
        private TimeSeriesDataType? Series(UInt32 Id)

            => TimeSeries.DataCopy<TimeSeriesListDataType>(CoordinatedEVCharging.TimeSeriesListData)?.
                   TimeSeriesData?.FirstOrDefault(series => series.TimeSeriesId == Id);


        /// <summary>
        /// The description of one of them.
        /// </summary>
        private TimeSeriesDescriptionDataType? Description(UInt32 Id)

            => TimeSeries.DataCopy<TimeSeriesDescriptionListDataType>(CoordinatedEVCharging.TimeSeriesDescriptionListData)?.
                   TimeSeriesDescriptionData?.FirstOrDefault(description => description.TimeSeriesId == Id);


        /// <summary>
        /// Replace one curve, leaving the other two and anybody else's alone.
        /// </summary>
        private async Task Write(TimeSeriesDataType  Data,
                                 CancellationToken   CancellationToken)
        {

            var all = TimeSeries.DataCopy<TimeSeriesListDataType>(CoordinatedEVCharging.TimeSeriesListData)
                          ?? new TimeSeriesListDataType { TimeSeriesData = [] };

            all.TimeSeriesData ??= [];
            all.TimeSeriesData.RemoveAll(series => series.TimeSeriesId == Data.TimeSeriesId);
            all.TimeSeriesData.Add(Data);

            await TimeSeries.SetData(CoordinatedEVCharging.TimeSeriesListData,
                                     all,
                                     CancellationToken: CancellationToken);

        }


        /// <summary>
        /// Raise or lower the update request on one of the curves.
        /// </summary>
        private async Task SetUpdateRequired(UInt32             Id,
                                             Boolean            Required,
                                             CancellationToken  CancellationToken)
        {

            var data = TimeSeries.DataCopy<TimeSeriesDescriptionListDataType>(CoordinatedEVCharging.TimeSeriesDescriptionListData);

            var mine = data?.TimeSeriesDescriptionData?.FirstOrDefault(description => description.TimeSeriesId == Id);

            if (mine is null || mine.UpdateRequired == Required)
                return;

            mine.UpdateRequired = Required;

            await TimeSeries.SetData(CoordinatedEVCharging.TimeSeriesDescriptionListData,
                                     data,
                                     CancellationToken: CancellationToken);

        }

        #endregion

        #region (private) ApproveTimeSeries(Message, CancellationToken)

        /// <summary>
        /// Somebody wrote a time series.
        ///
        /// Only the constraints curve is theirs to write. The demand is what the
        /// car wants and the plan is what the car intends, and neither becomes
        /// truer for somebody else having written it - which is what
        /// `timeSeriesWriteable: false` says in the description and what this
        /// enforces when a client ignores it.
        /// </summary>
        private Task<ResultDataType?> ApproveTimeSeries(SPINEMessage       Message,
                                                        CancellationToken  CancellationToken)
        {

            if (Message.Data is not TimeSeriesListDataType data)
                return Task.FromResult<ResultDataType?>(null);

            foreach (var series in data.TimeSeriesData ?? [])
            {

                if (series.TimeSeriesId is not UInt32 id)
                    continue;

                if (id == demandSeriesId || id == planSeriesId)
                    return Task.FromResult<ResultDataType?>(
                               ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                    $"The time series {id} of this EV is not writeable; " +
                                                    $"only its constraints curve ({constraintsSeriesId}) is.")
                           );

                if (id != constraintsSeriesId)
                    continue;

                foreach (var slot in series.TimeSeriesSlot ?? [])
                    if (slot.Duration?.AsTimeSpan is TimeSpan duration && duration <= TimeSpan.Zero)
                        return Task.FromResult<ResultDataType?>(
                                   ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                        "A time series slot \"SHALL only contain values greater than zero seconds\".")
                               );

            }

            return Task.FromResult<ResultDataType?>(null);

        }

        #endregion

        #region (private) Watch(Event)

        /// <summary>
        /// Something a client wrote has arrived. Lower whichever update request
        /// it answers, and tell the application.
        ///
        /// "The server SHALL set the updateRequired back to 'false', as soon as
        /// [the data] was updated successfully" - which is the other half of the
        /// only mechanism a SPINE server has for asking anybody anything.
        /// </summary>
        private void Watch(SPINEDataChanged Event)
        {

            if (Event.Change.CmdClassifier != CmdClassifierType.Write)
                return;

            if (Event.Change.LocalFeature == TimeSeries &&
                Event.Change.Function     == CoordinatedEVCharging.TimeSeriesListData)
            {

                _ = SetUpdateRequired(constraintsSeriesId, false, CancellationToken.None);

                OnPowerLimitsWritten?.Invoke(this, PowerLimits);

            }

            if (IncentiveTable is not null &&
                Event.Change.LocalFeature == IncentiveTable &&
                Event.Change.Function     == CoordinatedEVCharging.IncentiveTableData)
            {

                var data = IncentiveTable.DataCopy<IncentiveTableDescriptionDataType>(CoordinatedEVCharging.IncentiveTableDescriptionData);

                var mine = data?.IncentiveTableDescription?.
                               FirstOrDefault(table => table.TariffDescription?.TariffId == tariffId);

                if (mine?.TariffDescription?.UpdateRequired == true)
                {
                    mine.TariffDescription.UpdateRequired = false;
                    _ = IncentiveTable.SetData(CoordinatedEVCharging.IncentiveTableDescriptionData, data);
                }

                OnIncentivesWritten?.Invoke(this, Incentives);

            }

        }

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the time series feature.
        /// </summary>
        protected override SPINEFeature Feature()

            => TimeSeries;

        #endregion

    }

}
