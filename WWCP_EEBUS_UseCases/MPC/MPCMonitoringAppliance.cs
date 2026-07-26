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
    /// One measured value of a monitored unit, with everything needed to know
    /// what it is.
    /// </summary>
    /// <param name="Quantity">What was measured.</param>
    /// <param name="Value">Its value, in the unit of the quantity.</param>
    /// <param name="Timestamp">When it was measured, where the unit said so.</param>
    public sealed record MPCReading(MPCQuantity      Quantity,
                                    Decimal          Value,
                                    DateTimeOffset?  Timestamp)
    {

        /// <summary>Return a text representation of this reading.</summary>
        public override String ToString()

            => $"{Quantity}: {Value}";

    }


    /// <summary>
    /// The monitoring appliance of "Monitoring of Power Consumption" - the
    /// device which watches.
    ///
    /// It is the client actor, and it has nothing to offer: no server feature,
    /// no heartbeat, nothing writable. All it does is read the descriptions
    /// once, join them, and then let the subscription bring the values.
    ///
    /// That join is the whole of the client side. A measured value arrives as a
    /// number under an identifier; what it means comes from two descriptions in
    /// two features, and an appliance which skips either of them is reading
    /// numbers it cannot name.
    /// </summary>
    public class MPCMonitoringAppliance : AUseCase
    {

        #region Constructor(s)

        /// <summary>
        /// Add the monitoring appliance of MPC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which watches.</param>
        /// <param name="Scenarios">Which scenarios it is interested in. Scenario 1 is always included.</param>
        public MPCMonitoringAppliance(SPINELocalEntity      Entity,
                                      IEnumerable<UInt32>?  Scenarios   = null)

            : base(Entity,
                   UseCaseActors.MonitoringAppliance,
                   MonitoringOfPowerConsumption.Name,
                   MonitoringOfPowerConsumption.Version,
                   MonitoringOfPowerConsumption.Scenarios(ForMonitoringAppliance: true,
                                                          Scenarios: Scenarios ?? [ 2, 3, 4, 5 ]),
                   [ UseCaseActors.MonitoredUnit ],
                   PartnerEntityTypes:   null,
                   DocumentSubRevision:  MonitoringOfPowerConsumption.DocumentSubRevision)

        {

            foreach (var featureType in new[] {
                         FeatureTypeType.Measurement,
                         FeatureTypeType.ElectricalConnection
                     })
                if (Entity.Feature(featureType, RoleType.Client) is null)
                    Entity.AddFeature(featureType, RoleType.Client);

        }

        #endregion


        #region MeasurementOf(Partner) / ElectricalOf(Partner)

        /// <summary>
        /// The measurements of a monitored unit, paired with our client feature.
        /// </summary>
        /// <param name="Partner">An entity of a monitored unit.</param>
        public UseCaseFeature MeasurementOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.Measurement, Entity, Partner);


        /// <summary>
        /// Its electrical connection, which says which measurement is on which
        /// phase.
        /// </summary>
        /// <param name="Partner">An entity of a monitored unit.</param>
        public UseCaseFeature ElectricalOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.ElectricalConnection, Entity, Partner);

        #endregion

        #region Subscribe(Partner, CancellationToken = default)

        /// <summary>
        /// Read what the monitored unit measures, and ask to be told when it
        /// changes.
        ///
        /// This is the "initial scenario communication" of every one of the five
        /// scenarios: the descriptions first, then a subscription, and from then
        /// on the values arrive by themselves. The general implementation
        /// guideline § 3.2.2 makes that the primary way and polling the
        /// exception.
        /// </summary>
        /// <param name="Partner">An entity of a monitored unit.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task Subscribe(SPINERemoteEntity  Partner,
                                    CancellationToken  CancellationToken   = default)
        {

            var measurement = MeasurementOf(Partner);
            var electrical  = ElectricalOf (Partner);

            await measurement.RequestData(MonitoringOfPowerConsumption.MeasurementDescriptionListData, CancellationToken: CancellationToken);
            await electrical. RequestData(MonitoringOfPowerConsumption.ParameterDescriptionListData,   CancellationToken: CancellationToken);

            await measurement.Subscribe(CancellationToken);

            // The values themselves, once, so that an appliance which starts
            // after the unit does not wait for the first change.
            await measurement.RequestData(MonitoringOfPowerConsumption.MeasurementListData, CancellationToken: CancellationToken);

        }

        #endregion


        #region Quantities(Partner) / Readings(Partner) / Read(Partner, Quantity)

        /// <summary>
        /// Which quantities a monitored unit publishes, and under which
        /// measurement identifier.
        ///
        /// This is the join: the measurement description says what a
        /// measurement is, the electrical connection parameter description says
        /// which phase it is on, and the identifier is what connects them. A
        /// measurement whose description is missing is left out - a number
        /// nobody can name is worse than no number.
        /// </summary>
        /// <param name="Partner">An entity of a monitored unit.</param>
        public IReadOnlyDictionary<UInt32, MPCQuantity> Quantities(SPINERemoteEntity Partner)
        {

            var descriptions = MeasurementOf(Partner).
                                   Data<MeasurementDescriptionListDataType>(MonitoringOfPowerConsumption.MeasurementDescriptionListData)?.
                                   MeasurementDescriptionData ?? [];

            var parameters   = ElectricalOf(Partner).
                                   Data<ElectricalConnectionParameterDescriptionListDataType>(MonitoringOfPowerConsumption.ParameterDescriptionListData)?.
                                   ElectricalConnectionParameterDescriptionData ?? [];

            var quantities   = new Dictionary<UInt32, MPCQuantity>();

            foreach (var description in descriptions)
            {

                if (description.MeasurementId    is not UInt32                 measurementId ||
                    description.MeasurementType  is not MeasurementTypeType    type          ||
                    description.Unit             is not UnitOfMeasurementType  unit          ||
                    description.ScopeType        is not ScopeTypeType           scope)
                    continue;

                var phase = parameters.FirstOrDefault(parameter => parameter.MeasurementId == measurementId)?.
                                AcMeasuredPhases;

                // A "phase" which names several of them is a total rather than a
                // phase: "abc" is the sum, not a wire.
                if (phase is not null &&
                    phase != ElectricalConnectionPhaseNameType.A &&
                    phase != ElectricalConnectionPhaseNameType.B &&
                    phase != ElectricalConnectionPhaseNameType.C)
                    phase = null;

                quantities[measurementId] = new MPCQuantity(ScenarioOf(scope),
                                                            type,
                                                            unit,
                                                            scope,
                                                            phase);

            }

            return quantities;

        }


        /// <summary>
        /// Everything a monitored unit last said it measured.
        /// </summary>
        /// <param name="Partner">An entity of a monitored unit.</param>
        public IReadOnlyList<MPCReading> Readings(SPINERemoteEntity Partner)
        {

            var quantities   = Quantities(Partner);

            var measurements = MeasurementOf(Partner).
                                   Data<MeasurementListDataType>(MonitoringOfPowerConsumption.MeasurementListData)?.
                                   MeasurementData ?? [];

            var readings     = new List<MPCReading>();

            foreach (var measurement in measurements)
            {

                if (measurement.MeasurementId is not UInt32 measurementId ||
                    !quantities.TryGetValue(measurementId, out var quantity) ||
                    measurement.Value?.Value is not Decimal value)
                    continue;

                // A minimum or an average is a different statement about the
                // same quantity; this use case is about the value itself.
                if (measurement.ValueType is not null &&
                    measurement.ValueType != MeasurementValueTypeType.Value)
                    continue;

                readings.Add(new MPCReading(quantity,
                                            value,
                                            measurement.Timestamp?.AsDateTimeOffset));

            }

            return readings;

        }


        /// <summary>
        /// What a monitored unit last measured for one quantity, or null when it
        /// does not measure it.
        /// </summary>
        /// <param name="Partner">An entity of a monitored unit.</param>
        /// <param name="Quantity">A quantity.</param>
        public MPCReading? Read(SPINERemoteEntity  Partner,
                                MPCQuantity        Quantity)

            => Readings(Partner).FirstOrDefault(reading => reading.Quantity.Scope == Quantity.Scope &&
                                                           reading.Quantity.Phase == Quantity.Phase);

        #endregion


        #region (private static) ScenarioOf(Scope)

        /// <summary>
        /// Which scenario of the use case a scope belongs to.
        /// </summary>
        private static UInt32 ScenarioOf(ScopeTypeType Scope)
        {

            if (Scope == ScopeTypeType.AcPowerTotal ||
                Scope == ScopeTypeType.AcPower)
                return MonitoringOfPowerConsumption.ScenarioPower;

            if (Scope == ScopeTypeType.AcEnergyConsumed ||
                Scope == ScopeTypeType.AcEnergyProduced)
                return MonitoringOfPowerConsumption.ScenarioEnergy;

            if (Scope == ScopeTypeType.AcCurrent)
                return MonitoringOfPowerConsumption.ScenarioCurrent;

            if (Scope == ScopeTypeType.AcVoltage)
                return MonitoringOfPowerConsumption.ScenarioVoltage;

            if (Scope == ScopeTypeType.AcFrequency)
                return MonitoringOfPowerConsumption.ScenarioFrequency;

            return 0;

        }

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the measurement client feature.
        /// </summary>
        protected override SPINEFeature Feature()

            => Entity.Feature(FeatureTypeType.Measurement, RoleType.Client)
                   ?? Entity.Features.First();

        #endregion

    }

}
