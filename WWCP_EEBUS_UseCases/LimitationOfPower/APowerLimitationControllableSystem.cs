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
    /// The controllable system of "Limitation of Power Consumption" - the device
    /// which is being limited.
    ///
    /// It is the server actor: it holds the limit, the failsafe values and its
    /// nominal maximum, and the energy guard writes to them. Its own state
    /// machine (section 2.3) decides which limit it is actually holding itself
    /// to, and that is the part which matters: a charging station which accepts
    /// every limit and never falls back is not implementing this use case, it is
    /// implementing a remote control.
    /// </summary>
    public abstract class APowerLimitationControllableSystem : AUseCase
    {

        #region Data

        // Not constants: one entity may play the controllable system of both the
        // consumption and the production use case - a battery does, an inverter
        // with a house connection does - and SPINE allows only one feature per
        // feature type and role per entity. So the two use cases share the load
        // control, device configuration and electrical connection features, and
        // each of them has to find identifiers which the other is not using.
        private readonly UInt32  limitId;
        private readonly UInt32  failsafeLimitKeyId;
        private readonly UInt32  failsafeDurationKeyId;

        #endregion

        #region Properties

        /// <summary>
        /// Which of the two use cases this is: the limitation of power
        /// consumption, or of power production.
        /// </summary>
        public PowerLimitationProfile          Profile         { get; }

        /// <summary>
        /// The state machine of section 2.3, which decides which limit applies.
        /// </summary>
        public PowerLimitationStateMachine     StateMachine    { get; }

        /// <summary>
        /// The load control server feature, which holds the limit.
        /// </summary>
        public SPINELocalFeature   LoadControl     { get; }

        /// <summary>
        /// The device configuration server feature, which holds the failsafe
        /// values.
        /// </summary>
        public SPINELocalFeature   Configuration   { get; }

        /// <summary>
        /// The device diagnosis client feature, which watches the heartbeat of
        /// the energy guard.
        /// </summary>
        public SPINELocalFeature   Diagnosis       { get; }

        /// <summary>
        /// The device diagnosis server feature, which offers a heartbeat of this
        /// controllable system (Table 21).
        /// </summary>
        public SPINELocalFeature   HeartbeatServer { get; }

        /// <summary>
        /// The electrical connection server feature, which holds the nominal
        /// maximum.
        /// </summary>
        public SPINELocalFeature   Electrical      { get; }

        /// <summary>
        /// Whether this controllable system runs on an energy manager, which
        /// changes which nominal maximum it reports (rule 041 vs rule 042) and
        /// which reasons let it break a limit (section 2.2).
        /// </summary>
        public Boolean             IsEnergyManager    { get; }

        /// <summary>
        /// Asked before a limit is accepted: whether this device can actually
        /// apply it.
        ///
        /// The specification is strict about the answer (section 2.2): a limit
        /// SHALL be applied unless self-protection, safety, law - or, on an
        /// energy manager, loads it does not control - require otherwise. It is
        /// a question about the device, so the device answers it; the default is
        /// yes.
        /// </summary>
        public Func<Decimal, Boolean>?  CanApplyLimit    { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// The state of the controllable system changed.
        /// </summary>
        public event Action<APowerLimitationControllableSystem, PowerLimitationTransition>? OnTransition;

        /// <summary>
        /// The energy guard wrote a limit, and it was accepted or refused.
        /// </summary>
        public event Action<APowerLimitationControllableSystem, Decimal, Boolean, Boolean>? OnLimitWritten;

        #endregion

        #region (private static) NextFree(Used, StartingAt = 1)

        /// <summary>
        /// The lowest identifier which nobody on this feature is using yet.
        /// </summary>
        /// <param name="Used">The identifiers already taken, if any.</param>
        /// <param name="StartingAt">The lowest identifier worth having.</param>
        private static UInt32 NextFree(IEnumerable<UInt32?>  Used,
                                       UInt32                StartingAt   = 1)

            => UseCaseIds.NextFree(Used, StartingAt);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the controllable system of LPC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which is being limited.</param>
        /// <param name="Profile">Which of the two use cases this is.</param>
        /// <param name="IsEnergyManager">Whether it runs on an energy manager.</param>
        protected APowerLimitationControllableSystem(SPINELocalEntity        Entity,
                                                     PowerLimitationProfile  Profile,
                                                     Boolean                 IsEnergyManager   = false)

            : base(Entity,
                   UseCaseActors.ControllableSystem,
                   Profile.UseCaseName,
                   Profile.Version,
                   PowerLimitation.Scenarios(ForEnergyGuard: false),
                   [ UseCaseActors.EnergyGuard ],
                   [ EntityTypeType.GridGuard, EntityTypeType.CEM ],
                   Profile.DocumentSubRevision)

        {

            this.Profile          = Profile;
            this.IsEnergyManager  = IsEnergyManager;

            this.StateMachine     = new PowerLimitationStateMachine(Profile, Entity.Device.TimeProvider);
            this.StateMachine.OnTransition += (_, transition) => OnTransition?.Invoke(this, transition);

            #region The load control server: the limit itself (scenario 1)

            LoadControl = Entity.Feature(FeatureTypeType.LoadControl, RoleType.Server)
                              ?? Entity.AddFeature(FeatureTypeType.LoadControl, RoleType.Server);

            LoadControl.AddFunction(PowerLimitation.LimitDescriptionListData);
            LoadControl.AddFunction(PowerLimitation.LimitListData,
                                    Read:          true,
                                    Write:         true,
                                    PartialRead:   true,
                                    PartialWrite:  true);

            var descriptions = LoadControl.DataCopy<LoadControlLimitDescriptionListDataType>(PowerLimitation.LimitDescriptionListData)?.
                                   LoadControlLimitDescriptionData?.ToList() ?? [];

            var limits       = LoadControl.DataCopy<LoadControlLimitListDataType>(PowerLimitation.LimitListData)?.
                                   LoadControlLimitData?.ToList() ?? [];

            this.limitId     = NextFree(descriptions.Select(description => description.LimitId));

            descriptions.Add(Profile.LimitDescription(limitId));

            limits.      Add(new LoadControlLimitDataType {
                                 LimitId            = limitId,
                                 IsLimitChangeable  = true,
                                 IsLimitActive      = false,
                                 Value              = ScaledNumberType.FromValue(0)
                             });

            LoadControl.FunctionData(PowerLimitation.LimitDescriptionListData)!.SetData(
                new LoadControlLimitDescriptionListDataType {
                    LoadControlLimitDescriptionData = descriptions
                }
            );

            LoadControl.FunctionData(PowerLimitation.LimitListData)!.SetData(
                new LoadControlLimitListDataType {
                    LoadControlLimitData = limits
                }
            );

            // The other use case may already be watching this feature. Both have
            // to be asked, and a refusal from either one stands.
            var limitApprovedBySomeoneElse    = LoadControl.WriteApproval;

            LoadControl.WriteApproval         = async (message, cancellationToken) =>
                await ApproveLimit(message, cancellationToken)
                    ?? (limitApprovedBySomeoneElse is not null
                            ? await limitApprovedBySomeoneElse(message, cancellationToken)
                            : null);

            #endregion

            #region The device configuration server: the failsafe values (scenario 2)

            Configuration = Entity.Feature(FeatureTypeType.DeviceConfiguration, RoleType.Server)
                                ?? Entity.AddFeature(FeatureTypeType.DeviceConfiguration, RoleType.Server);

            Configuration.AddFunction(PowerLimitation.KeyValueDescriptionListData);
            Configuration.AddFunction(PowerLimitation.KeyValueListData,
                                      Read:          true,
                                      Write:         true,
                                      PartialRead:   true,
                                      PartialWrite:  true);

            var keyDescriptions        = Configuration.DataCopy<DeviceConfigurationKeyValueDescriptionListDataType>(PowerLimitation.KeyValueDescriptionListData)?.
                                             DeviceConfigurationKeyValueDescriptionData?.ToList() ?? [];

            var keyValues             = Configuration.DataCopy<DeviceConfigurationKeyValueListDataType>(PowerLimitation.KeyValueListData)?.
                                            DeviceConfigurationKeyValueData?.ToList() ?? [];

            // The failsafe limit is per direction, so it is always a new key ...
            this.failsafeLimitKeyId   = NextFree(keyDescriptions.Select(description => description.KeyId));

            keyDescriptions.Add(new DeviceConfigurationKeyValueDescriptionDataType {
                                    KeyId      = failsafeLimitKeyId,
                                    KeyName    = Profile.FailsafeLimitKey,
                                    ValueType  = DeviceConfigurationKeyValueTypeType.ScaledNumber,
                                    Unit       = UnitOfMeasurementType.W
                                });

            keyValues.      Add(new DeviceConfigurationKeyValueDataType {
                                    KeyId              = failsafeLimitKeyId,
                                    IsValueChangeable  = true,
                                    Value              = new DeviceConfigurationKeyValueValueType {
                                                             ScaledNumber = ScaledNumberType.FromValue(0)
                                                         }
                                });

            // ... but how long the device can ride through without an energy
            // guard is a property of the device and not of a direction, so there
            // is one such key even when both use cases are running. Whichever of
            // the two is constructed first declares it.
            var duration              = keyDescriptions.FirstOrDefault(description => description.KeyName == PowerLimitation.FailsafeDurationKey &&
                                                                                      description.KeyId   != failsafeLimitKeyId);

            this.failsafeDurationKeyId = duration?.KeyId
                                             ?? NextFree(keyDescriptions.Select(description => description.KeyId));

            if (duration is null)
            {

                keyDescriptions.Add(new DeviceConfigurationKeyValueDescriptionDataType {
                                        KeyId      = failsafeDurationKeyId,
                                        KeyName    = PowerLimitation.FailsafeDurationKey,
                                        ValueType  = DeviceConfigurationKeyValueTypeType.Duration
                                    });

                keyValues.      Add(new DeviceConfigurationKeyValueDataType {
                                        KeyId              = failsafeDurationKeyId,
                                        IsValueChangeable  = true,
                                        Value              = new DeviceConfigurationKeyValueValueType {
                                                                 Duration = DurationType.Parse(PowerLimitation.FailsafeDurationMinimumLowerBound)
                                                             }
                                    });

            }

            Configuration.FunctionData(PowerLimitation.KeyValueDescriptionListData)!.SetData(
                new DeviceConfigurationKeyValueDescriptionListDataType {
                    DeviceConfigurationKeyValueDescriptionData = keyDescriptions
                }
            );

            Configuration.FunctionData(PowerLimitation.KeyValueListData)!.SetData(
                new DeviceConfigurationKeyValueListDataType {
                    DeviceConfigurationKeyValueData = keyValues
                }
            );

            var configurationApprovedBySomeoneElse  = Configuration.WriteApproval;

            Configuration.WriteApproval             = async (message, cancellationToken) =>
                await ApproveConfiguration(message, cancellationToken)
                    ?? (configurationApprovedBySomeoneElse is not null
                            ? await configurationApprovedBySomeoneElse(message, cancellationToken)
                            : null);

            #endregion

            #region The electrical connection server: the nominal maximum (scenario 4)

            Electrical = Entity.Feature(FeatureTypeType.ElectricalConnection, RoleType.Server)
                             ?? Entity.AddFeature(FeatureTypeType.ElectricalConnection, RoleType.Server);

            Electrical.AddFunction(PowerLimitation.CharacteristicListData);

            var characteristics = Electrical.DataCopy<ElectricalConnectionCharacteristicListDataType>(PowerLimitation.CharacteristicListData)?.
                                      ElectricalConnectionCharacteristicData?.ToList() ?? [];

            characteristics.Add(new ElectricalConnectionCharacteristicDataType {
                                    ElectricalConnectionId  = 0,
                                    ParameterId             = 0,
                                    CharacteristicId        = NextFree(characteristics.Select(characteristic => characteristic.CharacteristicId),
                                                                       StartingAt: 0),
                                    CharacteristicContext   = ElectricalConnectionCharacteristicContextType.Entity,
                                    CharacteristicType      = IsEnergyManager
                                                                  ? Profile.ContractualNominalMax
                                                                  : Profile.NominalMax,
                                    Unit                    = UnitOfMeasurementType.W
                                });

            Electrical.FunctionData(PowerLimitation.CharacteristicListData)!.SetData(
                new ElectricalConnectionCharacteristicListDataType {
                    ElectricalConnectionCharacteristicData = characteristics
                }
            );

            #endregion

            #region The device diagnosis: watching one heartbeat and offering one (scenario 3)

            // Both roles, on the same entity. Table 21 lists
            // "deviceDiagnosisHeartbeatData" among the server data of the
            // controllable system as well - so it offers a heartbeat of its own
            // - while the client feature is the one which watches the energy
            // guard's. The note under the table ("at maximum one Feature with
            // the Feature Type in the Entity") sits in the server section and is
            // read per role; the certified Go implementation does the same.
            HeartbeatServer = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Server)
                                  ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Server);

            HeartbeatServer.AddFunction(PowerLimitation.HeartbeatData);

            Diagnosis = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Client)
                            ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Client);

            // Every heartbeat of the energy guard keeps us out of the failsafe
            // state, and the state machine is the one which counts them.
            Device.Events.Subscribe<SPINEDataChanged>(
                @event => {

                    if (@event.Change.Function            == PowerLimitation.HeartbeatData &&
                        @event.Change.RemoteFeature.Role  == RoleType.Server)
                        StateMachine.HeartbeatReceived();

                },
                SPINEEventLevel.Core
            );

            #endregion

        }

        #endregion


        #region ConsumptionLimit / FailsafeLimit / FailsafeDurationMinimum / ConsumptionNominalMax

        /// <summary>
        /// The active power consumption limit as it currently stands, in watts,
        /// together with whether it is activated.
        /// </summary>
        public (Decimal? Value, Boolean IsActive, TimeSpan? Duration) ConsumptionLimit
        {
            get
            {

                var entry = Limit();

                return (entry?.Value?.Value,
                        entry?.IsLimitActive == true,
                        entry?.TimePeriod?.Duration(Device.TimeProvider));

            }
        }


        /// <summary>
        /// The failsafe consumption active power limit, in watts (rule 021).
        /// </summary>
        public Decimal? FailsafeLimit
        {

            get => KeyValue(failsafeLimitKeyId)?.Value?.ScaledNumber?.Value;

            set => SetKeyValue(failsafeLimitKeyId,
                               new DeviceConfigurationKeyValueValueType {
                                   ScaledNumber = value is not null ? ScaledNumberType.FromValue(value.Value) : null
                               });

        }


        /// <summary>
        /// The failsafe duration minimum (rule 022).
        /// </summary>
        public TimeSpan? FailsafeDurationMinimum
        {

            get => KeyValue(failsafeDurationKeyId)?.Value?.Duration?.AsTimeSpan;

            set {

                SetKeyValue(failsafeDurationKeyId,
                            new DeviceConfigurationKeyValueValueType {
                                Duration = value is not null ? DurationType.Parse(value.Value) : null
                            });

                if (value is not null)
                    StateMachine.FailsafeDurationMinimum = value.Value;

            }

        }


        /// <summary>
        /// The nominal maximum active power this system can consume (rule 041),
        /// or is allowed to consume (rule 042) where it is an energy manager.
        /// </summary>
        public Decimal? ConsumptionNominalMax
        {

            get => Electrical.DataCopy<ElectricalConnectionCharacteristicListDataType>(PowerLimitation.CharacteristicListData)?.
                       ElectricalConnectionCharacteristicData?.FirstOrDefault()?.Value?.Value;

            set {

                var data = Electrical.DataCopy<ElectricalConnectionCharacteristicListDataType>(PowerLimitation.CharacteristicListData);

                if (data?.ElectricalConnectionCharacteristicData?.FirstOrDefault() is ElectricalConnectionCharacteristicDataType characteristic)
                {
                    characteristic.Value = value is not null ? ScaledNumberType.FromValue(value.Value) : null;
                    Electrical.SetData(PowerLimitation.CharacteristicListData, data).GetAwaiter().GetResult();
                }

            }

        }

        #endregion

        #region Check(CancellationToken = default)

        /// <summary>
        /// Look at the clock, take whichever transition it made due, and tell
        /// the energy guard when that changed whether the limit is active.
        ///
        /// A device with a heartbeat which stopped has nothing to react to, so
        /// somebody has to look; whoever drives the time calls this.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<PowerLimitationTransition?> Check(CancellationToken CancellationToken = default)
        {

            var transition = StateMachine.Check();

            if (transition is not null)
                await PublishLimitState(CancellationToken);

            return transition;

        }

        #endregion

        #region LimitExpired(...) / LimitInterrupted(...)

        /// <summary>
        /// The duration of the limit ran out (rule 908).
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<PowerLimitationTransition?> LimitExpired(CancellationToken CancellationToken = default)
        {

            var transition = StateMachine.LimitExpired();

            if (transition is not null)
                await PublishLimitState(CancellationToken);

            return transition;

        }


        /// <summary>
        /// This device had to stop keeping the limit, for one of the reasons the
        /// specification allows (rule 923).
        /// </summary>
        /// <param name="Reason">Which of them.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<PowerLimitationTransition?> LimitInterrupted(String             Reason,
                                                           CancellationToken  CancellationToken   = default)
        {

            var transition = StateMachine.LimitInterrupted(Reason);

            if (transition is not null)
                await PublishLimitState(CancellationToken);

            return transition;

        }

        #endregion


        #region (private) ApproveLimit(Message, CancellationToken)

        /// <summary>
        /// The energy guard wrote the limit: may it be applied?
        ///
        /// Three answers are possible, and only one of them is "no" in the sense
        /// of the protocol:
        ///
        /// * a limit below zero SHALL be rejected (section 2.2) - that is a
        ///   NACK;
        /// * a write which does not follow a heartbeat within 60 seconds is not
        ///   evaluated at all in the states "init", "failsafe" and
        ///   "unlimited/autonomous" - also a NACK, because the energy guard has
        ///   to know that its limit did not take effect;
        /// * a limit which this device cannot apply is accepted as data and
        ///   refused as an instruction (rule 003/1) - the write is NACKed and
        ///   the state machine goes to "unlimited/controlled" (rule 918).
        /// </summary>
        private Task<ResultDataType?> ApproveLimit(SPINEMessage       Message,
                                                   CancellationToken  CancellationToken)
        {

            if (Message.Data is not LoadControlLimitListDataType data)
                return Task.FromResult<ResultDataType?>(null);

            var written = data.LoadControlLimitData?.FirstOrDefault(entry => entry.LimitId == limitId);

            // Somebody else's limit on the same feature: none of our business.
            if (written is null)
                return Task.FromResult<ResultDataType?>(null);

            var value = written.Value?.Value ?? ConsumptionLimit.Value ?? 0;

            #region A limit below zero is refused (section 2.2)

            if (value < 0)
                return Task.FromResult<ResultDataType?>(
                           ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                $"An active power {Profile.Direction.ToString().ToLower()} limit below zero is refused (section 2.2).")
                       );

            #endregion

            #region Only a write which follows a heartbeat is evaluated (section 2.2)

            if (!StateMachine.MayEvaluateLimitWrite())
                return Task.FromResult<ResultDataType?>(
                           ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                $"In state '{StateMachine.State}' a limit is only evaluated when it follows " +
                                                $"a heartbeat within {PowerLimitation.LimitAfterHeartbeat} (section 2.2).")
                       );

            #endregion

            var activated  = written.IsLimitActive ?? ConsumptionLimit.IsActive;
            var applicable = CanApplyLimit?.Invoke(value) ?? true;

            // The state follows either way: a limit which cannot be applied
            // still takes the controllable system out of its failsafe state
            // (rules 916 and 918).
            var transition = StateMachine.LimitWritten(activated, applicable);

            OnLimitWritten?.Invoke(this, value, activated, applicable);

            if (!applicable)
                return Task.FromResult<ResultDataType?>(
                           ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                "This controllable system cannot apply the limit (rule 003/1).")
                       );

            return Task.FromResult<ResultDataType?>(null);

        }

        #endregion

        #region (private) ApproveConfiguration(Message, CancellationToken)

        /// <summary>
        /// The energy guard wrote a failsafe value.
        ///
        /// The failsafe duration minimum has to be between two and 24 hours
        /// ([LPC-022/3]); a value outside that is refused ([LPC-022/4]). A
        /// failsafe limit below zero is refused for the same reason as the
        /// active one (section 2.2).
        /// </summary>
        private Task<ResultDataType?> ApproveConfiguration(SPINEMessage       Message,
                                                           CancellationToken  CancellationToken)
        {

            if (Message.Data is not DeviceConfigurationKeyValueListDataType data)
                return Task.FromResult<ResultDataType?>(null);

            foreach (var entry in data.DeviceConfigurationKeyValueData ?? [])
            {

                if (entry.KeyId == failsafeLimitKeyId &&
                    entry.Value?.ScaledNumber?.Value is Decimal limit &&
                    limit < 0)
                    return Task.FromResult<ResultDataType?>(
                               ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                    $"A '{Profile.FailsafeLimitKey}' below zero is refused (section 2.2).")
                           );

                if (entry.KeyId == failsafeDurationKeyId &&
                    entry.Value?.Duration?.AsTimeSpan is TimeSpan duration &&
                    (duration < PowerLimitation.FailsafeDurationMinimumLowerBound ||
                     duration > PowerLimitation.FailsafeDurationMinimumUpperBound))
                    return Task.FromResult<ResultDataType?>(
                               ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                    $"The failsafe duration minimum has to be between " +
                                                    $"{PowerLimitation.FailsafeDurationMinimumLowerBound} and {PowerLimitation.FailsafeDurationMinimumUpperBound} " +
                                                    $"({Profile.Rule("022/3")}).")
                           );

            }

            return Task.FromResult<ResultDataType?>(null);

        }

        #endregion

        #region (private) PublishLimitState(CancellationToken)

        /// <summary>
        /// Write the state machine's answer into the limit and tell whoever
        /// subscribed.
        ///
        /// Rule 009: the controllable system sets the limit to activated or
        /// deactivated according to its state - so the energy guard reading the
        /// limit sees what is actually happening, not what it asked for.
        /// </summary>
        private async Task PublishLimitState(CancellationToken CancellationToken)
        {

            var data = LoadControl.DataCopy<LoadControlLimitListDataType>(PowerLimitation.LimitListData);

            if (data?.LoadControlLimitData?.FirstOrDefault(entry => entry.LimitId == limitId) is not LoadControlLimitDataType entry)
                return;

            if (entry.IsLimitActive == StateMachine.IsLimitActive)
                return;

            entry.IsLimitActive = StateMachine.IsLimitActive;

            await LoadControl.SetData(PowerLimitation.LimitListData, data, CancellationToken: CancellationToken);

        }

        #endregion

        #region (private) Limit() / KeyValue(KeyId) / SetKeyValue(KeyId, Value)

        private LoadControlLimitDataType? Limit()

            => LoadControl.DataCopy<LoadControlLimitListDataType>(PowerLimitation.LimitListData)?.
                   LoadControlLimitData?.FirstOrDefault(entry => entry.LimitId == limitId);


        private DeviceConfigurationKeyValueDataType? KeyValue(UInt32 KeyId)

            => Configuration.DataCopy<DeviceConfigurationKeyValueListDataType>(PowerLimitation.KeyValueListData)?.
                   DeviceConfigurationKeyValueData?.FirstOrDefault(entry => entry.KeyId == KeyId);


        private void SetKeyValue(UInt32                                KeyId,
                                 DeviceConfigurationKeyValueValueType  Value)
        {

            var data = Configuration.DataCopy<DeviceConfigurationKeyValueListDataType>(PowerLimitation.KeyValueListData);

            if (data?.DeviceConfigurationKeyValueData?.FirstOrDefault(entry => entry.KeyId == KeyId) is DeviceConfigurationKeyValueDataType entry)
            {
                entry.Value = Value;
                Configuration.SetData(PowerLimitation.KeyValueListData, data).GetAwaiter().GetResult();
            }

        }

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the load control feature, which is the
        /// one it is about.
        /// </summary>
        protected override SPINEFeature Feature()

            => LoadControl;

        #endregion

    }

}
