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
    /// The watching side of a monitoring use case.
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
    public abstract class AMonitoringAppliance : AUseCase
    {

        #region Properties

        /// <summary>
        /// Which of the monitoring use cases this is.
        /// </summary>
        public MonitoringProfile  Profile    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the watching side of a monitoring use case to an entity.
        /// </summary>
        /// <param name="Entity">The entity which watches.</param>
        /// <param name="Profile">Which of the monitoring use cases this is.</param>
        /// <param name="Scenarios">Which optional scenarios it is interested in. The mandatory ones are always included.</param>
        /// <param name="AnnounceAsAlternateActor">Whether to announce the second actor name of the profile rather than the one its document gives.</param>
        protected AMonitoringAppliance(SPINELocalEntity      Entity,
                                       MonitoringProfile     Profile,
                                       IEnumerable<UInt32>?  Scenarios                  = null,
                                       Boolean               AnnounceAsAlternateActor   = false)

            : base(Entity,
                   AnnounceAsAlternateActor
                       ? Profile.AlsoKnownAsClientActor ?? Profile.ClientActor
                       : Profile.ClientActor,
                   Profile.UseCaseName,
                   Profile.Version,
                   Profile.SupportedScenarios(ForClient: true,
                                              Scenarios: Scenarios ?? Profile.Scenarios.Select(scenario => scenario.Number)),
                   [ Profile.ServerActor ],
                   PartnerEntityTypes:   Profile.ClientEntityTypes,
                   DocumentSubRevision:  Profile.DocumentSubRevision)

        {

            this.Profile = Profile;

            // Whatever any of the supported scenarios needs at the other side,
            // this side needs a client feature for.
            foreach (var featureType in this.Scenarios.SelectMany(scenario => scenario.ServerFeatures).Distinct())
                if (Entity.Feature(featureType, RoleType.Client) is null)
                    Entity.AddFeature(featureType, RoleType.Client);

        }

        #endregion


        #region MeasurementOf(Partner) / ElectricalOf(Partner)

        /// <summary>
        /// The measurements of a monitored device, paired with our client
        /// feature.
        /// </summary>
        /// <param name="Partner">An entity of a monitored device.</param>
        public UseCaseFeature MeasurementOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.Measurement, Entity, Partner);


        /// <summary>
        /// Its electrical connection, which says which measurement is on which
        /// phase - or null when there is nothing to join.
        ///
        /// Null for two different reasons, and both of them are ordinary rather
        /// than errors: this use case may not measure an electrical connection at
        /// all (a state of charge has no phase), or the monitored device may not
        /// offer the feature because it supports none of the scenarios which need
        /// it.
        /// </summary>
        /// <param name="Partner">An entity of a monitored device.</param>
        public UseCaseFeature? ElectricalOf(SPINERemoteEntity Partner)

            => Profile.ElectricalParameters &&
               Partner.Feature(FeatureTypeType.ElectricalConnection, RoleType.Server) is not null &&
               Entity. Feature(FeatureTypeType.ElectricalConnection, RoleType.Client) is not null

                   ? new (FeatureTypeType.ElectricalConnection, Entity, Partner)
                   : null;

        #endregion

        #region Subscribe(Partner, CancellationToken = default)

        /// <summary>
        /// Read what the monitored device measures, and ask to be told when it
        /// changes.
        ///
        /// This is the "initial scenario communication" of every measuring
        /// scenario: the descriptions first, then a subscription, and from then
        /// on the values arrive by themselves. The general implementation
        /// guideline § 3.2.2 makes that the primary way and polling the
        /// exception.
        /// </summary>
        /// <param name="Partner">An entity of a monitored device.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public virtual async Task Subscribe(SPINERemoteEntity  Partner,
                                            CancellationToken  CancellationToken   = default)
        {

            var measurement = MeasurementOf(Partner);
            var electrical  = ElectricalOf (Partner);

            await measurement.RequestData(MonitoringFunctions.MeasurementDescriptionListData, CancellationToken: CancellationToken);

            if (electrical is not null)
                await electrical.RequestData(MonitoringFunctions.ParameterDescriptionListData, CancellationToken: CancellationToken);

            await measurement.Subscribe(CancellationToken);

            // The values themselves, once, so that an appliance which starts
            // after the monitored device does not wait for the first change.
            await measurement.RequestData(MonitoringFunctions.MeasurementListData, CancellationToken: CancellationToken);

        }

        #endregion


        #region Quantities(Partner) / Readings(Partner) / Read(Partner, Quantity)

        /// <summary>
        /// Which quantities a monitored device publishes, and under which
        /// measurement identifier.
        ///
        /// This is the join: the measurement description says what a
        /// measurement is, the electrical connection parameter description says
        /// which phase it is on, and the identifier is what connects them. A
        /// measurement whose description is missing is left out - a number
        /// nobody can name is worse than no number.
        /// </summary>
        /// <param name="Partner">An entity of a monitored device.</param>
        public IReadOnlyDictionary<UInt32, MonitoringQuantity> Quantities(SPINERemoteEntity Partner)
        {

            var descriptions = MeasurementOf(Partner).
                                   Data<MeasurementDescriptionListDataType>(MonitoringFunctions.MeasurementDescriptionListData)?.
                                   MeasurementDescriptionData ?? [];

            var parameters   = ElectricalOf(Partner)?.
                                   Data<ElectricalConnectionParameterDescriptionListDataType>(MonitoringFunctions.ParameterDescriptionListData)?.
                                   ElectricalConnectionParameterDescriptionData ?? [];

            var quantities   = new Dictionary<UInt32, MonitoringQuantity>();

            foreach (var description in descriptions)
            {

                if (description.MeasurementId    is not UInt32                 measurementId ||
                    description.MeasurementType  is not MeasurementTypeType    type          ||
                    description.Unit             is not UnitOfMeasurementType  unit          ||
                    description.ScopeType        is not ScopeTypeType          scope)
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

                quantities[measurementId] = new MonitoringQuantity(Profile.ScenarioOf(scope),
                                                                   type,
                                                                   unit,
                                                                   scope,
                                                                   phase);

            }

            return quantities;

        }


        /// <summary>
        /// Everything a monitored device last said it measured.
        /// </summary>
        /// <param name="Partner">An entity of a monitored device.</param>
        public IReadOnlyList<MonitoringReading> Readings(SPINERemoteEntity Partner)
        {

            var quantities   = Quantities(Partner);

            var measurements = MeasurementOf(Partner).
                                   Data<MeasurementListDataType>(MonitoringFunctions.MeasurementListData)?.
                                   MeasurementData ?? [];

            var readings     = new List<MonitoringReading>();

            foreach (var measurement in measurements)
            {

                if (measurement.MeasurementId is not UInt32 measurementId ||
                    !quantities.TryGetValue(measurementId, out var quantity) ||
                    measurement.Value?.Value is not Decimal value)
                    continue;

                // A minimum or an average is a different statement about the
                // same quantity; these use cases are about the value itself.
                if (measurement.ValueType is not null &&
                    measurement.ValueType != MeasurementValueTypeType.Value)
                    continue;

                readings.Add(new MonitoringReading(quantity,
                                                   value,
                                                   measurement.Timestamp?.AsDateTimeOffset));

            }

            return readings;

        }


        /// <summary>
        /// What a monitored device last measured for one quantity, or null when
        /// it does not measure it.
        /// </summary>
        /// <param name="Partner">An entity of a monitored device.</param>
        /// <param name="Quantity">A quantity.</param>
        public MonitoringReading? Read(SPINERemoteEntity   Partner,
                                       MonitoringQuantity  Quantity)

            => Readings(Partner).FirstOrDefault(reading => reading.Quantity.Scope == Quantity.Scope &&
                                                           reading.Quantity.Phase == Quantity.Phase);

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
