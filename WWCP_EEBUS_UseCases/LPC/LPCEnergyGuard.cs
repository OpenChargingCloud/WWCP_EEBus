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

namespace cloud.charging.open.protocols.EEBUS.UseCases.LPC
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
    public class LPCEnergyGuard : AUseCase
    {

        #region Properties

        /// <summary>
        /// The device diagnosis server feature, which sends the heartbeat the
        /// controllable systems watch.
        /// </summary>
        public SPINELocalFeature  Diagnosis    { get; }

        /// <summary>
        /// The heartbeat itself.
        /// </summary>
        public SPINEHeartbeat     Heartbeat    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the energy guard of LPC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which does the limiting.</param>
        public LPCEnergyGuard(SPINELocalEntity Entity)

            : base(Entity,
                   UseCaseActors.EnergyGuard,
                   LimitationOfPowerConsumption.Name,
                   LimitationOfPowerConsumption.Version,
                   LimitationOfPowerConsumption.Scenarios(ForEnergyGuard: true),
                   [ UseCaseActors.ControllableSystem ],
                   PartnerEntityTypes:   null,
                   DocumentSubRevision:  LimitationOfPowerConsumption.DocumentSubRevision)

        {

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

            Diagnosis.AddFunction(LimitationOfPowerConsumption.HeartbeatData);

            Heartbeat = new SPINEHeartbeat(Diagnosis);

        }

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

            => Heartbeat.Start(Interval ?? LimitationOfPowerConsumption.HeartbeatInterval,
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
        /// <param name="IsActive">Whether the limit is activated ([LPC-008]).</param>
        /// <param name="Duration">How long it is valid for ([LPC-004]).</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> WriteConsumptionLimit(SPINERemoteEntity  Partner,
                                                               Decimal            Value,
                                                               Boolean            IsActive,
                                                               TimeSpan?          Duration            = null,
                                                               CancellationToken  CancellationToken   = default)
        {

            if (Value < 0)
                throw new ArgumentOutOfRangeException(nameof(Value),
                                                      "An active power consumption limit is never below zero (LPC 1.0.0, 2.2).");

            var loadControl  = LoadControlOf(Partner);

            var limitId      = await LimitIdOf(loadControl, CancellationToken)
                                   ?? throw new InvalidOperationException(
                                          $"{Partner.Address} has no active power consumption limit of this use case.");

            return await loadControl.WriteData(
                             LimitationOfPowerConsumption.LimitListData,
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

            await loadControl.RequestData(LimitationOfPowerConsumption.LimitListData, CancellationToken: CancellationToken);

            var limitId = await LimitIdOf(loadControl, CancellationToken);

            var entry   = loadControl.Data<LoadControlLimitListDataType>(LimitationOfPowerConsumption.LimitListData)?.
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

            await configuration.RequestData(LimitationOfPowerConsumption.KeyValueDescriptionListData, CancellationToken: CancellationToken);
            await configuration.RequestData(LimitationOfPowerConsumption.KeyValueListData,            CancellationToken: CancellationToken);

            var descriptions  = configuration.Data<DeviceConfigurationKeyValueDescriptionListDataType>(LimitationOfPowerConsumption.KeyValueDescriptionListData)?.
                                    DeviceConfigurationKeyValueDescriptionData ?? [];

            var values        = configuration.Data<DeviceConfigurationKeyValueListDataType>(LimitationOfPowerConsumption.KeyValueListData)?.
                                    DeviceConfigurationKeyValueData ?? [];

            DeviceConfigurationKeyValueValueType? Of(DeviceConfigurationKeyNameType KeyName)
            {

                var keyId = descriptions.FirstOrDefault(description => description.KeyName == KeyName)?.KeyId;

                return keyId is null
                           ? null
                           : values.FirstOrDefault(value => value.KeyId == keyId)?.Value;

            }

            return (Of(LimitationOfPowerConsumption.FailsafeLimitKey)?.   ScaledNumber?.Value,
                    Of(LimitationOfPowerConsumption.FailsafeDurationKey)?.Duration?.    AsTimeSpan);

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

            await electrical.RequestData(LimitationOfPowerConsumption.CharacteristicListData, CancellationToken: CancellationToken);

            return electrical.Data<ElectricalConnectionCharacteristicListDataType>(LimitationOfPowerConsumption.CharacteristicListData)?.
                       ElectricalConnectionCharacteristicData?.
                       FirstOrDefault(characteristic => characteristic.CharacteristicType == ElectricalConnectionCharacteristicTypeType.PowerConsumptionNominalMax ||
                                                        characteristic.CharacteristicType == ElectricalConnectionCharacteristicTypeType.ContractualConsumptionNominalMax)?.
                       Value?.Value;

        }

        #endregion

        #region WriteFailsafeValues(Partner, Limit = null, DurationMinimum = null, ...)

        /// <summary>
        /// Change the failsafe values of a controllable system
        /// ([LPC-021/2], [LPC-022/2]).
        /// </summary>
        /// <param name="Partner">An entity of a controllable system.</param>
        /// <param name="Limit">The failsafe consumption active power limit in watts.</param>
        /// <param name="DurationMinimum">The failsafe duration minimum, between two and 24 hours ([LPC-022/3]).</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> WriteFailsafeValues(SPINERemoteEntity  Partner,
                                                             Decimal?           Limit               = null,
                                                             TimeSpan?          DurationMinimum     = null,
                                                             CancellationToken  CancellationToken   = default)
        {

            if (DurationMinimum is not null &&
                (DurationMinimum < LimitationOfPowerConsumption.FailsafeDurationMinimumLowerBound ||
                 DurationMinimum > LimitationOfPowerConsumption.FailsafeDurationMinimumUpperBound))
                throw new ArgumentOutOfRangeException(nameof(DurationMinimum),
                                                      $"The energy guard SHALL choose a value between " +
                                                      $"{LimitationOfPowerConsumption.FailsafeDurationMinimumLowerBound} and {LimitationOfPowerConsumption.FailsafeDurationMinimumUpperBound} " +
                                                      $"(LPC 1.0.0, [LPC-022/3]).");

            var configuration = ConfigurationOf(Partner);

            await configuration.RequestData(LimitationOfPowerConsumption.KeyValueDescriptionListData, CancellationToken: CancellationToken);

            var descriptions  = configuration.Data<DeviceConfigurationKeyValueDescriptionListDataType>(LimitationOfPowerConsumption.KeyValueDescriptionListData)?.
                                    DeviceConfigurationKeyValueDescriptionData ?? [];

            var entries       = new List<DeviceConfigurationKeyValueDataType>();

            if (Limit is not null &&
                descriptions.FirstOrDefault(description => description.KeyName == LimitationOfPowerConsumption.FailsafeLimitKey)?.KeyId is UInt32 limitKey)
                entries.Add(new DeviceConfigurationKeyValueDataType {
                                KeyId  = limitKey,
                                Value  = new DeviceConfigurationKeyValueValueType {
                                             ScaledNumber = ScaledNumberType.FromValue(Limit.Value)
                                         }
                            });

            if (DurationMinimum is not null &&
                descriptions.FirstOrDefault(description => description.KeyName == LimitationOfPowerConsumption.FailsafeDurationKey)?.KeyId is UInt32 durationKey)
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
                             LimitationOfPowerConsumption.KeyValueListData,
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

            if (LoadControl.Data<LoadControlLimitDescriptionListDataType>(LimitationOfPowerConsumption.LimitDescriptionListData) is null)
                await LoadControl.RequestData(LimitationOfPowerConsumption.LimitDescriptionListData, CancellationToken: CancellationToken);

            return LoadControl.Data<LoadControlLimitDescriptionListDataType>(LimitationOfPowerConsumption.LimitDescriptionListData)?.
                       LoadControlLimitDescriptionData?.
                       FirstOrDefault(LimitationOfPowerConsumption.IsTheLimit)?.
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
