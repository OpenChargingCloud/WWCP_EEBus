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

namespace cloud.charging.open.protocols.EEBUS.UseCases.LimitationOfPower
{

    /// <summary>
    /// The energy guard of "Limitation of Power Consumption" - the device which
    /// does the limiting.
    ///
    /// It is the client actor, and the one exception to that is its heartbeat:
    /// it hosts a **server** feature for it, because the controllable system has
    /// to watch it. Per the general implementation guideline § 2.1.3 that does
    /// not make the energy guard a server actor - a secondary function whose
    /// direction is reversed never changes the classification.
    ///
    /// The heartbeat is not a nicety. A controllable system which stops hearing
    /// it limits itself to its failsafe value after 120 seconds, so an energy
    /// guard which forgets to send it has limited every device it manages.
    /// </summary>
    public abstract class APowerLimitationEnergyGuard : AUseCase
    {

        #region Data

        /// <summary>
        /// The last limit written to each controllable system, so that rule 913
        /// has something to say when communication comes back.
        /// </summary>
        private readonly Dictionary<String, (Decimal Value, Boolean IsActive, TimeSpan? Duration)> lastWritten
            = new (StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>
        /// Which of the two use cases this is.
        /// </summary>
        public PowerLimitationProfile  Profile  { get; }

        /// <summary>
        /// The device diagnosis server feature, which sends the heartbeat the
        /// controllable systems watch.
        /// </summary>
        public SPINELocalFeature  Diagnosis    { get; }

        /// <summary>
        /// The heartbeat itself.
        /// </summary>
        public SPINEHeartbeat     Heartbeat    { get; }

        /// <summary>
        /// Whether this energy guard introduces itself to a controllable system
        /// as soon as it discovers one, as rule 913 requires.
        /// </summary>
        public Boolean            AnnounceOnDiscovery   { get; set; } = true;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the energy guard of LPC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which does the limiting.</param>
        /// <param name="Profile">Which of the two use cases this is.</param>
        protected APowerLimitationEnergyGuard(SPINELocalEntity        Entity,
                                              PowerLimitationProfile  Profile)

            : base(Entity,
                   UseCaseActors.EnergyGuard,
                   Profile.UseCaseName,
                   Profile.Version,
                   PowerLimitation.Scenarios(ForEnergyGuard: true),
                   [ UseCaseActors.ControllableSystem ],
                   PartnerEntityTypes:   null,
                   DocumentSubRevision:  Profile.DocumentSubRevision)

        {

            this.Profile = Profile;

            // The client features: everything it reads from and writes to the
            // controllable system.
            foreach (var featureType in new[] {
                         FeatureTypeType.LoadControl,
                         FeatureTypeType.DeviceConfiguration,
                         FeatureTypeType.ElectricalConnection,
                         FeatureTypeType.DeviceDiagnosis
                     })
                if (Entity.Feature(featureType, RoleType.Client) is null)
                    Entity.AddFeature(featureType, RoleType.Client);

            // The one server feature: its own heartbeat.
            Diagnosis = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Server)
                            ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Server);

            Diagnosis.AddFunction(PowerLimitation.HeartbeatData);

            Heartbeat = new SPINEHeartbeat(Diagnosis);

            // Rule 913: "After initial connection or restoration of
            // communication, the EG SHALL send a heartbeat and a following APCL
            // within 60 seconds to the CS after having determined that the
            // communication is possible again."
            //
            // Determining that is exactly what this event says. Without it a
            // controllable system which has just rebooted sits in "init" waiting
            // for someone to take charge of it, and after 120 seconds decides
            // that nobody will - which is a house limited to its failsafe value
            // because two devices were each waiting for the other to speak first.
            //
            // The announcement is queued rather than awaited here: this runs
            // inside the handling of a datagram, and sending one from there would
            // put the sender back into itself.
            // The trigger is the arrival of the partner's use case data, not a
            // *change* in it. After a reconnection nothing about the partner has
            // changed - it is the same device saying the same things - so
            // anything watching for a change would stay silent exactly when the
            // rule needs it to speak. What has changed is that the data arrived
            // at all, which is precisely "having determined that the
            // communication is possible again".
            //
            // The base class subscribes first and at the same level, so by the
            // time this runs it has already worked out who the partners are.
            //
            // Sent here and now rather than handed to the thread pool. A test
            // bench and a simulation both have to be able to run the same
            // scenario twice and get the same log, and an announcement which
            // races the next test step gives neither - the whole point of the
            // FakeTimeProvider is that nothing happens except when something
            // makes it happen.
            Device.Events.Subscribe<SPINEDataChanged>(
                @event => {

                    if (!AnnounceOnDiscovery ||
                        @event.Change.Function != SPINENodeManagement.UseCaseData)
                        return;

                    var device   = @event.Change.RemoteFeature.Device.DeviceAddress;

                    var partners = Partners.
                                       Where (partner => partner.Entity.Address.Device == device).
                                       Select(partner => partner.Entity).
                                       ToList();

                    foreach (var partner in partners)
                        try
                        {
                            AnnounceTo(partner).GetAwaiter().GetResult();
                        }
                        catch
                        {
                            // A partner which vanished again between the event
                            // and the write is not an error worth propagating
                            // into somebody else's event loop.
                        }

                },
                SPINEEventLevel.Core
            );

        }

        #endregion

        #region AnnounceTo(Partner, CancellationToken = default)

        /// <summary>
        /// Tell a controllable system that this energy guard is here and what it
        /// wants: one heartbeat, and then the limit (rule 913).
        ///
        /// The order matters and is the whole rule. In "init", the failsafe state
        /// and "unlimited/autonomous" a controllable system evaluates a limit
        /// only when a heartbeat came first, so an energy guard which writes the
        /// limit before saying hello has written nothing at all.
        ///
        /// What is announced is the last limit this energy guard wrote to that
        /// partner, or a deactivated one where it has written none yet - which
        /// is the correct opening statement: "I am here, and I am not limiting
        /// you".
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task AnnounceTo(SPINERemoteEntity  Partner,
                                     CancellationToken  CancellationToken   = default)
        {

            await Heartbeat.SendOnce(PowerLimitation.HeartbeatInterval, CancellationToken);

            var (value, isActive, duration) = lastWritten.TryGetValue(KeyOf(Partner), out var remembered)
                                                  ? remembered
                                                  : (0m, false, (TimeSpan?) null);

            await WriteConsumptionLimit(Partner,
                                        value,
                                        isActive,
                                        duration,
                                        CancellationToken);

        }


        private static String KeyOf(SPINERemoteEntity Entity)

            => $"{Entity.Address.Device?.ToLowerInvariant()}:[{String.Join(',', Entity.EntityId)}]";

        #endregion


        #region StartHeartbeat(...) / StopHeartbeat()

        /// <summary>
        /// Start telling the controllable systems that this energy guard is
        /// still there.
        /// </summary>
        /// <param name="Interval">How often. Every 60 seconds by default; the specification gives the controllable system 120 seconds of patience.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task StartHeartbeat(TimeSpan?          Interval            = null,
                                   CancellationToken  CancellationToken   = default)

            => Heartbeat.Start(Interval ?? PowerLimitation.HeartbeatInterval,
                               CancellationToken);


        /// <summary>
        /// Stop. Every controllable system watching will fall into its failsafe
        /// state 120 seconds later, which is the point of the mechanism.
        /// </summary>
        public void StopHeartbeat()
        {
            Heartbeat.Stop();
        }

        #endregion


        #region LoadControlOf(Partner) / ConfigurationOf(Partner) / ElectricalOf(Partner)

        /// <summary>
        /// The load control of a controllable system, paired with our client
        /// feature.
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        public UseCaseFeature LoadControlOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.LoadControl, Entity, Partner);


        /// <summary>
        /// Its device configuration, which holds the failsafe values.
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        public UseCaseFeature ConfigurationOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.DeviceConfiguration, Entity, Partner);


        /// <summary>
        /// Its electrical connection, which holds the nominal maximum.
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        public UseCaseFeature ElectricalOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.ElectricalConnection, Entity, Partner);

        #endregion

        #region WriteConsumptionLimit(Partner, Value, IsActive, Duration = null, ...)

        /// <summary>
        /// Set the active power consumption limit of a controllable system
        /// (scenario 1).
        ///
        /// The write is partial, because it has to be: only the value, the
        /// activation and the duration are writable, and a full write would be
        /// refused (general implementation guideline § 3.1). The identifier
        /// comes along whatever happens - "all Primary- and (available)
        /// Sub-Identifiers SHALL be included in the message, regardless of being
        /// writeable or not".
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        /// <param name="Value">The limit in watts. Never below zero (section 2.2).</param>
        /// <param name="IsActive">Whether the limit is activated (rule 008).</param>
        /// <param name="Duration">How long it is valid for (rule 004).</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> WriteConsumptionLimit(SPINERemoteEntity  Partner,
                                                               Decimal            Value,
                                                               Boolean            IsActive,
                                                               TimeSpan?          Duration            = null,
                                                               CancellationToken  CancellationToken   = default)
        {

            if (Value < 0)
                throw new ArgumentOutOfRangeException(nameof(Value),
                                                      "An active power consumption limit is never below zero (section 2.2).");

            var loadControl  = LoadControlOf(Partner);

            var limitId      = await LimitIdOf(loadControl, CancellationToken)
                                   ?? throw new InvalidOperationException(
                                          $"{Partner.Address} has no active power consumption limit of this use case.");

            var response = await loadControl.WriteData(
                                     PowerLimitation.LimitListData,
                                     new LoadControlLimitListDataType {
                                         LoadControlLimitData = [
                                             new LoadControlLimitDataType {
                                                 LimitId        = limitId,
                                                 IsLimitActive  = IsActive,
                                                 Value          = ScaledNumberType.FromValue(Value),
                                                 TimePeriod     = Duration is not null
                                                                      ? TimePeriodType.FromDuration(Duration.Value)
                                                                      : null
                                             }
                                         ]
                                     },
                                     Partial: true,
                                     CancellationToken: CancellationToken
                                 );

            // What was said last is what gets said again after a reconnection
            // (rule 913). Remembered whether or not it was accepted: a limit the
            // controllable system refused is still this energy guard's intent,
            // and refusing it again is its answer to give.
            lastWritten[KeyOf(Partner)] = (Value, IsActive, Duration);

            return response;

        }

        #endregion

        #region ReadConsumptionLimit(Partner, ...) / ReadFailsafeValues(Partner, ...) / ReadNominalMax(Partner, ...)

        /// <summary>
        /// Read the active power consumption limit of a controllable system, as
        /// it currently stands there.
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<(Decimal? Value, Boolean IsActive)> ReadConsumptionLimit(SPINERemoteEntity  Partner,
                                                                                    CancellationToken  CancellationToken   = default)
        {

            var loadControl = LoadControlOf(Partner);

            await loadControl.RequestData(PowerLimitation.LimitListData, CancellationToken: CancellationToken);

            var limitId = await LimitIdOf(loadControl, CancellationToken);

            var entry   = loadControl.Data<LoadControlLimitListDataType>(PowerLimitation.LimitListData)?.
                              LoadControlLimitData?.
                              FirstOrDefault(limit => limit.LimitId == limitId);

            return (entry?.Value?.Value,
                    entry?.IsLimitActive == true);

        }


        /// <summary>
        /// Read the failsafe values of a controllable system (scenario 2).
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<(Decimal? Limit, TimeSpan? DurationMinimum)> ReadFailsafeValues(SPINERemoteEntity  Partner,
                                                                                           CancellationToken  CancellationToken   = default)
        {

            var configuration = ConfigurationOf(Partner);

            await configuration.RequestData(PowerLimitation.KeyValueDescriptionListData, CancellationToken: CancellationToken);
            await configuration.RequestData(PowerLimitation.KeyValueListData,            CancellationToken: CancellationToken);

            var descriptions  = configuration.Data<DeviceConfigurationKeyValueDescriptionListDataType>(PowerLimitation.KeyValueDescriptionListData)?.
                                    DeviceConfigurationKeyValueDescriptionData ?? [];

            var values        = configuration.Data<DeviceConfigurationKeyValueListDataType>(PowerLimitation.KeyValueListData)?.
                                    DeviceConfigurationKeyValueData ?? [];

            DeviceConfigurationKeyValueValueType? Of(DeviceConfigurationKeyNameType KeyName)
            {

                var keyId = descriptions.FirstOrDefault(description => description.KeyName == KeyName)?.KeyId;

                return keyId is null
                           ? null
                           : values.FirstOrDefault(value => value.KeyId == keyId)?.Value;

            }

            return (Of(Profile.FailsafeLimitKey)?.   ScaledNumber?.Value,
                    Of(PowerLimitation.FailsafeDurationKey)?.Duration?.    AsTimeSpan);

        }


        /// <summary>
        /// Read the nominal maximum consumption of a controllable system
        /// (scenario 4).
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<Decimal?> ReadConsumptionNominalMax(SPINERemoteEntity  Partner,
                                                               CancellationToken  CancellationToken   = default)
        {

            var electrical = ElectricalOf(Partner);

            await electrical.RequestData(PowerLimitation.CharacteristicListData, CancellationToken: CancellationToken);

            return electrical.Data<ElectricalConnectionCharacteristicListDataType>(PowerLimitation.CharacteristicListData)?.
                       ElectricalConnectionCharacteristicData?.
                       FirstOrDefault(characteristic => characteristic.CharacteristicType == ElectricalConnectionCharacteristicTypeType.PowerConsumptionNominalMax ||
                                                        characteristic.CharacteristicType == ElectricalConnectionCharacteristicTypeType.ContractualConsumptionNominalMax)?.
                       Value?.Value;

        }

        #endregion

        #region WriteFailsafeValues(Partner, Limit = null, DurationMinimum = null, ...)

        /// <summary>
        /// Change the failsafe values of a controllable system
        /// (rules 021/2 and 022/2).
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        /// <param name="Limit">The failsafe consumption active power limit in watts.</param>
        /// <param name="DurationMinimum">The failsafe duration minimum, between two and 24 hours (rule 022/3).</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> WriteFailsafeValues(SPINERemoteEntity  Partner,
                                                             Decimal?           Limit               = null,
                                                             TimeSpan?          DurationMinimum     = null,
                                                             CancellationToken  CancellationToken   = default)
        {

            if (DurationMinimum is not null &&
                (DurationMinimum < PowerLimitation.FailsafeDurationMinimumLowerBound ||
                 DurationMinimum > PowerLimitation.FailsafeDurationMinimumUpperBound))
                throw new ArgumentOutOfRangeException(nameof(DurationMinimum),
                                                      $"The energy guard SHALL choose a value between " +
                                                      $"{PowerLimitation.FailsafeDurationMinimumLowerBound} and {PowerLimitation.FailsafeDurationMinimumUpperBound} " +
                                                      $"(rule 022/3).");

            var configuration = ConfigurationOf(Partner);

            await configuration.RequestData(PowerLimitation.KeyValueDescriptionListData, CancellationToken: CancellationToken);

            var descriptions  = configuration.Data<DeviceConfigurationKeyValueDescriptionListDataType>(PowerLimitation.KeyValueDescriptionListData)?.
                                    DeviceConfigurationKeyValueDescriptionData ?? [];

            var entries       = new List<DeviceConfigurationKeyValueDataType>();

            if (Limit is not null &&
                descriptions.FirstOrDefault(description => description.KeyName == Profile.FailsafeLimitKey)?.KeyId is UInt32 limitKey)
                entries.Add(new DeviceConfigurationKeyValueDataType {
                                KeyId  = limitKey,
                                Value  = new DeviceConfigurationKeyValueValueType {
                                             ScaledNumber = ScaledNumberType.FromValue(Limit.Value)
                                         }
                            });

            if (DurationMinimum is not null &&
                descriptions.FirstOrDefault(description => description.KeyName == PowerLimitation.FailsafeDurationKey)?.KeyId is UInt32 durationKey)
                entries.Add(new DeviceConfigurationKeyValueDataType {
                                KeyId  = durationKey,
                                Value  = new DeviceConfigurationKeyValueValueType {
                                             Duration = DurationType.Parse(DurationMinimum.Value)
                                         }
                            });

            if (entries.Count == 0)
                throw new ArgumentException("Neither a failsafe limit nor a failsafe duration minimum was given, " +
                                            "or the controllable system does not offer them.");

            return await configuration.WriteData(
                             PowerLimitation.KeyValueListData,
                             new DeviceConfigurationKeyValueListDataType {
                                 DeviceConfigurationKeyValueData = entries
                             },
                             Partial: true,
                             CancellationToken: CancellationToken
                         );

        }

        #endregion


        #region (private) LimitIdOf(LoadControl, CancellationToken)

        /// <summary>
        /// Which of the load control limits of the partner is the active power
        /// consumption limit of this use case.
        ///
        /// A device may have several limits on one feature, and only the
        /// description tells them apart: this one is a sign dependent absolute
        /// value limit, an obligation, about consumption, scoped to the active
        /// power (Table 14). Writing to the wrong one would be writing a number
        /// nobody asked for into somebody else's limit.
        /// </summary>
        private async Task<UInt32?> LimitIdOf(UseCaseFeature     LoadControl,
                                              CancellationToken  CancellationToken)
        {

            if (LoadControl.Data<LoadControlLimitDescriptionListDataType>(PowerLimitation.LimitDescriptionListData) is null)
                await LoadControl.RequestData(PowerLimitation.LimitDescriptionListData, CancellationToken: CancellationToken);

            return LoadControl.Data<LoadControlLimitDescriptionListDataType>(PowerLimitation.LimitDescriptionListData)?.
                       LoadControlLimitDescriptionData?.
                       FirstOrDefault(Profile.IsTheLimit)?.
                       LimitId;

        }

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the load control client feature, which
        /// is the one it is about.
        /// </summary>
        protected override SPINEFeature Feature()

            => Entity.Feature(FeatureTypeType.LoadControl, RoleType.Client) ?? Diagnosis;

        #endregion

    }

}
