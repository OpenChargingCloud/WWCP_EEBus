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

namespace cloud.charging.open.protocols.EEBUS.UseCases.Monitoring
{

    /// <summary>
    /// The measured side of a monitoring use case - the device which is being
    /// watched.
    ///
    /// It is the server actor and it does one thing: publish what it measures.
    /// There is nothing to write, nothing to agree and no state to fall back
    /// from - which makes these the use cases where the whole difficulty is in
    /// the **descriptions**. A number without one is meaningless, and the three
    /// pieces which give it meaning live in two features: the measurement
    /// description says what kind of quantity it is and in which unit, the
    /// electrical connection parameter description says which phase it is on,
    /// and the measurement identifier is what joins them.
    ///
    /// Which quantities it publishes is the only thing a concrete use case has
    /// to decide.
    /// </summary>
    public abstract class AMonitoredDevice : AUseCase
    {

        #region Data

        private readonly UInt32                                   electricalConnectionId  = 0;

        private readonly Dictionary<MonitoringQuantity, UInt32>   measurementIdOf         = [];

        private readonly Dictionary<MonitoringQuantity, UInt32>   parameterIdOf           = [];

        #endregion

        #region Properties

        /// <summary>
        /// Which of the monitoring use cases this is.
        /// </summary>
        public MonitoringProfile                                 Profile        { get; }

        /// <summary>
        /// The measurement server feature, which holds the values.
        /// </summary>
        public SPINELocalFeature                                 Measurement    { get; }

        /// <summary>
        /// The electrical connection server feature, which says which
        /// measurement is on which phase - or null for a use case whose
        /// measurements are not measurements of an electrical connection.
        /// </summary>
        public SPINELocalFeature?                                Electrical     { get; }

        /// <summary>
        /// The phases this device measures.
        /// </summary>
        public IReadOnlyList<ElectricalConnectionPhaseNameType>  Phases         { get; }

        /// <summary>
        /// Which quantities this device publishes, and under which measurement
        /// identifier.
        /// </summary>
        public IReadOnlyDictionary<MonitoringQuantity, UInt32>   Quantities
            => measurementIdOf;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the measured side of a monitoring use case to an entity.
        /// </summary>
        /// <param name="Entity">The entity which is being watched.</param>
        /// <param name="Profile">Which of the monitoring use cases this is.</param>
        /// <param name="Quantities">What this device measures.</param>
        /// <param name="Phases">Which phases it measures. All three by default; an empty list means it measures only totals.</param>
        /// <param name="AlsoSupports">Scenarios which are supported without being measured, e.g. one which publishes a configuration value.</param>
        protected AMonitoredDevice(SPINELocalEntity                                 Entity,
                                   MonitoringProfile                                Profile,
                                   IEnumerable<MonitoringQuantity>                  Quantities,
                                   IEnumerable<ElectricalConnectionPhaseNameType>?  Phases         = null,
                                   IEnumerable<UInt32>?                             AlsoSupports   = null)

            : base(Entity,
                   Profile.ServerActor,
                   Profile.UseCaseName,
                   Profile.Version,
                   Profile.SupportedScenarios(
                       ForClient:  false,
                       Scenarios:  [.. Quantities.Select(quantity => quantity.Scenario),
                                    .. AlsoSupports ?? []]
                   ),
                   Profile.ClientActors,
                   PartnerEntityTypes:   null,
                   DocumentSubRevision:  Profile.DocumentSubRevision)

        {

            this.Profile  = Profile;

            this.Phases   = [.. Phases ?? [ ElectricalConnectionPhaseNameType.A,
                                            ElectricalConnectionPhaseNameType.B,
                                            ElectricalConnectionPhaseNameType.C ]];

            Measurement   = Entity.Feature(FeatureTypeType.Measurement, RoleType.Server)
                                ?? Entity.AddFeature(FeatureTypeType.Measurement, RoleType.Server);

            Measurement.AddFunction(MonitoringFunctions.MeasurementDescriptionListData);
            Measurement.AddFunction(MonitoringFunctions.MeasurementListData,
                                    Read:         true,
                                    PartialRead:  true);

            if (Profile.ElectricalParameters)
            {

                Electrical = Entity.Feature(FeatureTypeType.ElectricalConnection, RoleType.Server)
                                 ?? Entity.AddFeature(FeatureTypeType.ElectricalConnection, RoleType.Server);

                Electrical.AddFunction(MonitoringFunctions.ElectricalDescriptionListData);
                Electrical.AddFunction(MonitoringFunctions.ParameterDescriptionListData);

            }

            // One entity may be measured by more than one monitoring use case -
            // an electric vehicle is measured by the electricity measurement and
            // by the state of charge at once, the meter at a grid connection
            // point is regularly the monitored unit of the monitoring of power
            // consumption as well - and SPINE allows only one measurement feature
            // per entity. So they share it, and the identifiers have to be picked
            // around each other rather than started from one.
            var quantities = Quantities.Distinct().ToList();

            foreach (var (quantity, measurementId) in quantities.Zip(FreeMeasurementIds((UInt32) quantities.Count)))
                measurementIdOf[quantity] = measurementId;

            foreach (var (quantity, parameterId)   in quantities.Zip(FreeParameterIds  ((UInt32) quantities.Count)))
                parameterIdOf  [quantity] = parameterId;

            // A use case which names no mandatory scenario still has to be
            // implemented as something: the measurement of electricity during EV
            // charging asks for at least one of its three, because they measure
            // the same electricity and convert into each other.
            if (Profile.AtLeastOneScenario &&
                this.Scenarios.Count == 0)
                throw new ArgumentException($"A {Profile.ServerActor} of the {Profile.UseCaseName} has to support at least one of its scenarios.",
                                            nameof(Quantities));

            Publish();

        }


        /// <summary>
        /// Measurement identifiers nobody else on this measurement feature has
        /// taken.
        /// </summary>
        private IEnumerable<UInt32> FreeMeasurementIds(UInt32 Count)

            => UseCaseIds.NextFree(Measurement.
                                       DataCopy<MeasurementDescriptionListDataType>(MonitoringFunctions.MeasurementDescriptionListData)?.
                                       MeasurementDescriptionData?.
                                       Select(description => description.MeasurementId) ?? [],
                                   Count,
                                   StartingAt: 0);


        /// <summary>
        /// Parameter identifiers nobody else on this electrical connection
        /// feature has taken.
        ///
        /// Not the same numbers as the measurement identifiers, even though a
        /// use case on its own could get away with using one for both: the EV
        /// commissioning use case puts a parameter on this feature which
        /// describes no measurement at all (its scenario 6, the charging power
        /// limits), so the two sequences are not interchangeable.
        /// </summary>
        private IEnumerable<UInt32> FreeParameterIds(UInt32 Count)

            => Electrical is null
                   ? []
                   : UseCaseIds.NextFree(Electrical.
                                             DataCopy<ElectricalConnectionParameterDescriptionListDataType>(MonitoringFunctions.ParameterDescriptionListData)?.
                                             ElectricalConnectionParameterDescriptionData?.
                                             Select(parameter => parameter.ParameterId) ?? [],
                                         Count,
                                         StartingAt: 0);

        #endregion


        #region Set(Quantity, Value, CancellationToken = default)

        /// <summary>
        /// Publish a measured value.
        ///
        /// Whoever subscribed to the measurement feature is told at once - which
        /// is how these use cases are meant to be used: the general
        /// implementation guideline § 3.2.2 makes subscriptions the primary way
        /// of getting data and polling the exception.
        /// </summary>
        /// <param name="Quantity">Which quantity was measured.</param>
        /// <param name="Value">Its value, in the unit of the quantity.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="ArgumentException">When this device does not publish that quantity.</exception>
        public Task Set(MonitoringQuantity          Quantity,
                        Decimal                     Value,
                        MeasurementValueStateType?  ValueState          = null,
                        CancellationToken           CancellationToken   = default)

            => Set([ (Quantity, Value) ], ValueState, CancellationToken);


        /// <summary>
        /// Publish several measured values at once, which is one notify rather
        /// than several.
        /// </summary>
        /// <param name="Values">The quantities and their values.</param>
        /// <param name="ValueState">Whether the values are normal, out of range or erroneous. Normal by omission.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task Set(IEnumerable<(MonitoringQuantity Quantity, Decimal Value)>  Values,
                              MeasurementValueStateType?                                 ValueState          = null,
                              CancellationToken                                          CancellationToken   = default)
        {

            var data       = Measurement.DataCopy<MeasurementListDataType>(MonitoringFunctions.MeasurementListData)
                                 ?? new MeasurementListDataType { MeasurementData = [] };

            data.MeasurementData ??= [];

            var timestamp  = AbsoluteOrRelativeTimeType.Parse(Device.TimeProvider.GetUtcNow());

            foreach (var (quantity, value) in Values)
            {

                if (!measurementIdOf.TryGetValue(quantity, out var measurementId))
                    throw new ArgumentException($"This {Profile.ServerActor} does not publish {quantity}.",
                                                nameof(Values));

                var entry = data.MeasurementData.FirstOrDefault(measurement => measurement.MeasurementId == measurementId);

                if (entry is null)
                {
                    entry = new MeasurementDataType { MeasurementId = measurementId };
                    data.MeasurementData.Add(entry);
                }

                entry.ValueType   = MeasurementValueTypeType.Value;
                entry.Value       = ScaledNumberType.FromValue(value);
                entry.Timestamp   = timestamp;

                // A measurement is normal unless the device says otherwise, and
                // saying otherwise is not decoration: a value marked "outOfRange"
                // or "error" is one the reader has to throw away rather than act
                // on (MPC 1.0.0, section 2.5.2). A sensor which has failed and
                // keeps publishing its last plausible number is worse than one
                // which admits it.
                entry.ValueState  = ValueState;

            }

            await Measurement.SetData(MonitoringFunctions.MeasurementListData,
                                      data,
                                      CancellationToken: CancellationToken);

        }

        #endregion

        #region Get(Quantity)

        /// <summary>
        /// The value this device last published for a quantity, or null when it
        /// has published none.
        /// </summary>
        /// <param name="Quantity">A quantity.</param>
        public Decimal? Get(MonitoringQuantity Quantity)

            => measurementIdOf.TryGetValue(Quantity, out var measurementId)
                   ? Measurement.DataCopy<MeasurementListDataType>(MonitoringFunctions.MeasurementListData)?.
                         MeasurementData?.FirstOrDefault(measurement => measurement.MeasurementId == measurementId)?.
                         Value?.Value
                   : null;

        #endregion


        #region (private) Publish()

        /// <summary>
        /// Write the descriptions of everything this device measures.
        ///
        /// Two of them per quantity which is on a wire: the measurement
        /// description says what it is, and the electrical connection parameter
        /// description says where. A monitoring appliance which reads only one
        /// of the two learns a number without knowing which wire it came from.
        /// </summary>
        private void Publish()
        {

            // Whatever another use case on this entity has already described
            // stays; only our own entries are rewritten. Matched on the
            // identifier we were given rather than on "the identifier or zero" -
            // an entry with no measurement identifier at all is somebody else's
            // by definition, and the lowest free identifier really can be zero.
            var mine          = measurementIdOf.Values.ToHashSet();
            var minesToo      = parameterIdOf.  Values.ToHashSet();

            var descriptions  = Measurement.DataCopy<MeasurementDescriptionListDataType>(MonitoringFunctions.MeasurementDescriptionListData)?.
                                    MeasurementDescriptionData?.
                                    Where(description => description.MeasurementId is not UInt32 id || !mine.Contains(id)).
                                    ToList() ?? [];

            var parameters    = Electrical?.DataCopy<ElectricalConnectionParameterDescriptionListDataType>(MonitoringFunctions.ParameterDescriptionListData)?.
                                    ElectricalConnectionParameterDescriptionData?.
                                    Where(parameter => parameter.ParameterId is not UInt32 id || !minesToo.Contains(id)).
                                    ToList() ?? [];

            foreach (var (quantity, measurementId) in measurementIdOf.OrderBy(entry => entry.Value))
            {

                descriptions.Add(new MeasurementDescriptionDataType {
                                     MeasurementId    = measurementId,
                                     MeasurementType  = quantity.Type,
                                     CommodityType    = quantity.Commodity,
                                     Unit             = quantity.Unit,
                                     ScopeType        = quantity.Scope
                                 });

                if (Electrical is null)
                    continue;

                parameters.Add(new ElectricalConnectionParameterDescriptionDataType {
                                   ElectricalConnectionId   = electricalConnectionId,
                                   ParameterId              = parameterIdOf[quantity],
                                   MeasurementId            = measurementId,
                                   VoltageType              = ElectricalConnectionVoltageTypeType.Ac,

                                   // A total is measured across all the phases
                                   // this device has; a per-phase quantity names
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

            Measurement.FunctionData(MonitoringFunctions.MeasurementDescriptionListData)!.SetData(
                new MeasurementDescriptionListDataType { MeasurementDescriptionData = descriptions }
            );

            if (Electrical is null)
                return;

            Electrical.FunctionData(MonitoringFunctions.ParameterDescriptionListData)!.SetData(
                new ElectricalConnectionParameterDescriptionListDataType { ElectricalConnectionParameterDescriptionData = parameters }
            );

            // Which connection these parameters belong to, and which way round
            // its numbers are meant. Written once and left alone when another
            // use case has already described the same connection.
            var connections = Electrical.DataCopy<ElectricalConnectionDescriptionListDataType>(MonitoringFunctions.ElectricalDescriptionListData)?.
                                  ElectricalConnectionDescriptionData?.ToList() ?? [];

            if (!connections.Any(connection => connection.ElectricalConnectionId == electricalConnectionId))
            {

                connections.Add(new ElectricalConnectionDescriptionDataType {
                                    ElectricalConnectionId   = electricalConnectionId,
                                    PowerSupplyType          = ElectricalConnectionVoltageTypeType.Ac,
                                    AcConnectedPhases        = (UInt32) Phases.Count,
                                    PositiveEnergyDirection  = Profile.PositiveEnergyDirection
                                });

                Electrical.FunctionData(MonitoringFunctions.ElectricalDescriptionListData)!.SetData(
                    new ElectricalConnectionDescriptionListDataType { ElectricalConnectionDescriptionData = connections }
                );

            }

        }


        /// <summary>
        /// How SPINE names "all the phases of this device at once".
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
