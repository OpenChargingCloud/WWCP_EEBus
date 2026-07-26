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

namespace cloud.charging.open.protocols.EEBUS.UseCases.MPC
{

    /// <summary>
    /// The monitored unit of "Monitoring of Power Consumption" - the device
    /// which is being watched.
    ///
    /// It is the server actor and it does one thing: publish what it measures.
    /// There is nothing to write, nothing to agree and no state to fall back
    /// from - which makes it the use case where the whole difficulty is in the
    /// **descriptions**. A number without one is meaningless, and the three
    /// pieces which give it meaning live in two features: the measurement
    /// description says what kind of quantity it is and in which unit, the
    /// electrical connection parameter description says which phase it is on,
    /// and the measurement identifier is what joins them.
    /// </summary>
    public class MPCMonitoredUnit : AUseCase
    {

        #region Data

        private readonly UInt32                              electricalConnectionId  = 0;

        private readonly Dictionary<MPCQuantity, UInt32>     measurementIdOf         = [];

        private          UInt32                              nextMeasurementId;

        #endregion

        #region Properties

        /// <summary>
        /// The measurement server feature, which holds the values.
        /// </summary>
        public SPINELocalFeature                            Measurement    { get; }

        /// <summary>
        /// The electrical connection server feature, which says which
        /// measurement is on which phase.
        /// </summary>
        public SPINELocalFeature                            Electrical     { get; }

        /// <summary>
        /// The phases this unit measures.
        /// </summary>
        public IReadOnlyList<ElectricalConnectionPhaseNameType>  Phases     { get; }

        /// <summary>
        /// Which quantities this unit publishes, and under which measurement
        /// identifier.
        /// </summary>
        public IReadOnlyDictionary<MPCQuantity, UInt32>      Quantities
            => measurementIdOf;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the monitored unit of MPC to an entity.
        ///
        /// Scenario 1 is mandatory and always there; the other four are added
        /// when this device measures them. A meter which knows nothing but its
        /// total active power implements the use case completely.
        /// </summary>
        /// <param name="Entity">The entity which is being watched.</param>
        /// <param name="Phases">Which phases it measures. All three by default; an empty list means it measures only totals.</param>
        /// <param name="PowerPerPhase">Whether it measures the active power of each phase as well as the total (scenario 1).</param>
        /// <param name="Energy">Whether it measures energy (scenario 2).</param>
        /// <param name="Current">Whether it measures current (scenario 3).</param>
        /// <param name="Voltage">Whether it measures voltage (scenario 4).</param>
        /// <param name="Frequency">Whether it measures the grid frequency (scenario 5).</param>
        public MPCMonitoredUnit(SPINELocalEntity                                 Entity,
                                IEnumerable<ElectricalConnectionPhaseNameType>?  Phases          = null,
                                Boolean                                          PowerPerPhase   = false,
                                Boolean                                          Energy          = false,
                                Boolean                                          Current         = false,
                                Boolean                                          Voltage         = false,
                                Boolean                                          Frequency       = false)

            : base(Entity,
                   UseCaseActors.MonitoredUnit,
                   MonitoringOfPowerConsumption.Name,
                   MonitoringOfPowerConsumption.Version,
                   MonitoringOfPowerConsumption.Scenarios(
                       ForMonitoringAppliance: false,
                       Scenarios: Supported(Energy, Current, Voltage, Frequency)
                   ),
                   [ UseCaseActors.MonitoringAppliance ],
                   PartnerEntityTypes:   null,
                   DocumentSubRevision:  MonitoringOfPowerConsumption.DocumentSubRevision)

        {

            this.Phases  = [.. Phases ?? [ ElectricalConnectionPhaseNameType.A,
                                           ElectricalConnectionPhaseNameType.B,
                                           ElectricalConnectionPhaseNameType.C ]];

            Measurement  = Entity.Feature(FeatureTypeType.Measurement, RoleType.Server)
                               ?? Entity.AddFeature(FeatureTypeType.Measurement, RoleType.Server);

            Measurement.AddFunction(MonitoringOfPowerConsumption.MeasurementDescriptionListData);
            Measurement.AddFunction(MonitoringOfPowerConsumption.MeasurementListData,
                                    Read:         true,
                                    PartialRead:  true);

            Electrical   = Entity.Feature(FeatureTypeType.ElectricalConnection, RoleType.Server)
                               ?? Entity.AddFeature(FeatureTypeType.ElectricalConnection, RoleType.Server);

            Electrical.AddFunction(MonitoringOfPowerConsumption.ParameterDescriptionListData);

            #region Which quantities this unit publishes

            Declare(MonitoringOfPowerConsumption.PowerTotal);

            if (PowerPerPhase)
                foreach (var phase in this.Phases)
                    Declare(MonitoringOfPowerConsumption.Power(phase));

            if (Energy)
            {
                Declare(MonitoringOfPowerConsumption.EnergyConsumed);
                Declare(MonitoringOfPowerConsumption.EnergyProduced);
            }

            if (Current)
                foreach (var phase in this.Phases)
                    Declare(MonitoringOfPowerConsumption.Current(phase));

            if (Voltage)
                foreach (var phase in this.Phases)
                    Declare(MonitoringOfPowerConsumption.Voltage(phase));

            if (Frequency)
                Declare(MonitoringOfPowerConsumption.Frequency);

            Publish();

            #endregion

        }


        /// <summary>
        /// Which of the optional scenarios a unit with these measurements
        /// supports.
        /// </summary>
        private static IEnumerable<UInt32> Supported(Boolean Energy,
                                                     Boolean Current,
                                                     Boolean Voltage,
                                                     Boolean Frequency)
        {

            var scenarios = new List<UInt32>();

            if (Energy)     scenarios.Add(MonitoringOfPowerConsumption.ScenarioEnergy);
            if (Current)    scenarios.Add(MonitoringOfPowerConsumption.ScenarioCurrent);
            if (Voltage)    scenarios.Add(MonitoringOfPowerConsumption.ScenarioVoltage);
            if (Frequency)  scenarios.Add(MonitoringOfPowerConsumption.ScenarioFrequency);

            return scenarios;

        }

        #endregion


        #region Set(Quantity, Value, CancellationToken = default)

        /// <summary>
        /// Publish a measured value.
        ///
        /// Whoever subscribed to the measurement feature is told at once - which
        /// is how this use case is meant to be used: the general implementation
        /// guideline § 3.2.2 makes subscriptions the primary way of getting data
        /// and polling the exception.
        /// </summary>
        /// <param name="Quantity">Which quantity was measured.</param>
        /// <param name="Value">Its value, in the unit of the quantity.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="ArgumentException">When this unit does not publish that quantity.</exception>
        public Task Set(MPCQuantity        Quantity,
                        Decimal            Value,
                        CancellationToken  CancellationToken   = default)

            => Set([ (Quantity, Value) ], CancellationToken);


        /// <summary>
        /// Publish several measured values at once, which is one notify rather
        /// than several.
        /// </summary>
        /// <param name="Values">The quantities and their values.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task Set(IEnumerable<(MPCQuantity Quantity, Decimal Value)>  Values,
                              CancellationToken                                   CancellationToken   = default)
        {

            var data       = Measurement.DataCopy<MeasurementListDataType>(MonitoringOfPowerConsumption.MeasurementListData)
                                 ?? new MeasurementListDataType { MeasurementData = [] };

            data.MeasurementData ??= [];

            var timestamp  = AbsoluteOrRelativeTimeType.Parse(Device.TimeProvider.GetUtcNow());

            foreach (var (quantity, value) in Values)
            {

                if (!measurementIdOf.TryGetValue(quantity, out var measurementId))
                    throw new ArgumentException($"This monitored unit does not publish {quantity}.",
                                                nameof(Values));

                var entry = data.MeasurementData.FirstOrDefault(measurement => measurement.MeasurementId == measurementId);

                if (entry is null)
                {
                    entry = new MeasurementDataType { MeasurementId = measurementId };
                    data.MeasurementData.Add(entry);
                }

                entry.ValueType  = MeasurementValueTypeType.Value;
                entry.Value      = ScaledNumberType.FromValue(value);
                entry.Timestamp  = timestamp;

            }

            await Measurement.SetData(MonitoringOfPowerConsumption.MeasurementListData,
                                      data,
                                      CancellationToken: CancellationToken);

        }

        #endregion

        #region Get(Quantity)

        /// <summary>
        /// The value this unit last published for a quantity, or null when it
        /// has published none.
        /// </summary>
        /// <param name="Quantity">A quantity.</param>
        public Decimal? Get(MPCQuantity Quantity)

            => measurementIdOf.TryGetValue(Quantity, out var measurementId)
                   ? Measurement.DataCopy<MeasurementListDataType>(MonitoringOfPowerConsumption.MeasurementListData)?.
                         MeasurementData?.FirstOrDefault(measurement => measurement.MeasurementId == measurementId)?.
                         Value?.Value
                   : null;

        #endregion


        #region (private) Declare(Quantity) / Publish()

        /// <summary>
        /// Give a quantity a measurement identifier, so that its description and
        /// its values can refer to each other.
        /// </summary>
        private void Declare(MPCQuantity Quantity)
        {

            if (!measurementIdOf.ContainsKey(Quantity))
                measurementIdOf[Quantity] = nextMeasurementId++;

        }


        /// <summary>
        /// Write the descriptions of everything this unit measures.
        ///
        /// Two of them per quantity which is on a phase: the measurement
        /// description says what it is, and the electrical connection parameter
        /// description says where. A monitoring appliance which reads only one
        /// of the two learns a number without knowing which wire it came from.
        /// </summary>
        private void Publish()
        {

            var descriptions  = new List<MeasurementDescriptionDataType>();
            var parameters    = new List<ElectricalConnectionParameterDescriptionDataType>();

            foreach (var (quantity, measurementId) in measurementIdOf.OrderBy(entry => entry.Value))
            {

                descriptions.Add(new MeasurementDescriptionDataType {
                                     MeasurementId    = measurementId,
                                     MeasurementType  = quantity.Type,
                                     CommodityType    = CommodityTypeType.Electricity,
                                     Unit             = quantity.Unit,
                                     ScopeType        = quantity.Scope
                                 });

                parameters.Add(new ElectricalConnectionParameterDescriptionDataType {
                                   ElectricalConnectionId   = electricalConnectionId,
                                   ParameterId              = measurementId,
                                   MeasurementId            = measurementId,
                                   VoltageType              = ElectricalConnectionVoltageTypeType.Ac,

                                   // A total is measured across all the phases
                                   // this unit has; a per-phase quantity names
                                   // its phase.
                                   AcMeasuredPhases         = quantity.Phase ?? AllPhases(),
                                   AcMeasuredInReferenceTo  = quantity.Phase is not null
                                                                  ? ElectricalConnectionPhaseNameType.Neutral
                                                                  : null,
                                   AcMeasurementType        = ElectricalConnectionAcMeasurementTypeType.Real,
                                   AcMeasurementVariant     = ElectricalConnectionMeasurandVariantType.Rms,
                                   ScopeType                = quantity.Scope
                               });

            }

            Measurement.FunctionData(MonitoringOfPowerConsumption.MeasurementDescriptionListData)!.SetData(
                new MeasurementDescriptionListDataType { MeasurementDescriptionData = descriptions }
            );

            Electrical.FunctionData(MonitoringOfPowerConsumption.ParameterDescriptionListData)!.SetData(
                new ElectricalConnectionParameterDescriptionListDataType { ElectricalConnectionParameterDescriptionData = parameters }
            );

        }


        /// <summary>
        /// How SPINE names "all the phases of this unit at once".
        /// </summary>
        private ElectricalConnectionPhaseNameType AllPhases()

            => Phases.Count switch {
                   3  => ElectricalConnectionPhaseNameType.Abc,
                   2  => ElectricalConnectionPhaseNameType.Ab,
                   1  => Phases[0],
                   _  => ElectricalConnectionPhaseNameType.None
               };

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the measurement feature.
        /// </summary>
        protected override SPINEFeature Feature()

            => Measurement;

        #endregion

    }

}
