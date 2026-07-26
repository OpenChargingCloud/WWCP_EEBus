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

namespace cloud.charging.open.protocols.EEBUS.UseCases.OPEV
{

    /// <summary>
    /// Why an electric vehicle is not doing what the energy guard last told it.
    /// </summary>
    public enum OPEVTrust
    {

        /// <summary>
        /// The energy guard is there and healthy, so its curtailment applies.
        /// </summary>
        Curtailed,

        /// <summary>
        /// No heartbeat for longer than four seconds ([OPEV-005]).
        /// </summary>
        HeartbeatMissing,

        /// <summary>
        /// The energy guard announced a failure ([OPEV-007]).
        /// </summary>
        EnergyGuardFailed

    }


    /// <summary>
    /// The trust of an electric vehicle in its energy guard changed.
    /// </summary>
    /// <param name="Timestamp">When it happened.</param>
    /// <param name="From">What it was.</param>
    /// <param name="To">What it is now.</param>
    /// <param name="Reason">Which rule of the specification caused it.</param>
    public sealed record OPEVTrustChanged(DateTimeOffset  Timestamp,
                                          OPEVTrust       From,
                                          OPEVTrust       To,
                                          String          Reason)

        : SPINEEvent(Timestamp)

    {

        /// <summary>Return a text representation of this event.</summary>
        public override String ToString()

            => $"{From} -> {To} ({Reason})";

    }


    /// <summary>
    /// The EV of "Overload Protection by EV Charging Current Curtailment".
    ///
    /// It is the server actor: it holds one current limit per phase and the
    /// currents it is able to charge with, and the energy guard writes the
    /// limits. What it does when the energy guard goes quiet or announces a
    /// failure is chapter 3 of the specification, which the Go reference
    /// implementation does not have at all - eebus-go implements the energy
    /// guard side only, so the behaviour a certification actually tests an EV
    /// for has no reference implementation to copy.
    ///
    /// That behaviour is two rules, and they are the reason the use case exists:
    ///
    /// * no heartbeat for more than four seconds and the EV "should switch to a
    ///   safe current setting that guarantees that no overload occurs during
    ///   absence of the Energy Guard" ([OPEV-005]);
    /// * the same when the energy guard announces a failure, because then it is
    ///   present but not to be trusted ([OPEV-007]).
    /// </summary>
    public class OPEVElectricVehicle : AUseCase
    {

        #region Data

        private readonly UInt32                       electricalConnectionId  = 0;

        private readonly Dictionary<UInt32, UInt32>   limitIdOfPhase          = [];

        private          DateTimeOffset?              lastHeartbeat;

        private          Boolean                      energyGuardFailed;

        private          OPEVTrust                    trust                   = OPEVTrust.HeartbeatMissing;

        private readonly Lock                         trustLock               = new ();

        #endregion

        #region Properties

        /// <summary>
        /// The load control server feature, which holds the limits.
        /// </summary>
        public SPINELocalFeature   LoadControl    { get; }

        /// <summary>
        /// The electrical connection server feature, which says which phases
        /// this EV charges on and which currents it can charge with.
        /// </summary>
        public SPINELocalFeature   Electrical     { get; }

        /// <summary>
        /// The device diagnosis client feature, with which it watches the energy
        /// guard.
        /// </summary>
        public SPINELocalFeature   Diagnosis      { get; }

        /// <summary>
        /// How many phases this EV charges on.
        /// </summary>
        public UInt32              PhaseCount     { get; }

        /// <summary>
        /// Whether the energy guard is to be trusted right now, and why not.
        /// </summary>
        public OPEVTrust           Trust
        {
            get { lock (trustLock) { return trust; } }
        }

        /// <summary>
        /// The current this EV charges with while it does not trust the energy
        /// guard: a value it is sure cannot cause an overload ([OPEV-005]).
        ///
        /// The specification does not say what it is - it is a property of the
        /// installation, and only the vehicle and its owner know it.
        /// </summary>
        public Decimal             SafeCurrent    { get; set; } = 6;

        #endregion

        #region Events

        /// <summary>
        /// The EV started or stopped trusting its energy guard.
        /// </summary>
        public event Action<OPEVElectricVehicle, OPEVTrustChanged>? OnTrustChanged;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the EV of OPEV to an entity.
        /// </summary>
        /// <param name="Entity">The entity of the electric vehicle.</param>
        /// <param name="PhaseCount">How many phases it charges on. Three by default.</param>
        public OPEVElectricVehicle(SPINELocalEntity  Entity,
                                   UInt32            PhaseCount   = 3)

            : base(Entity,
                   UseCaseActors.EV,
                   OverloadProtection.Name,
                   OverloadProtection.Version,
                   OverloadProtection.Scenarios(ForEnergyGuard: false),
                   [ UseCaseActors.EnergyGuard, UseCaseActors.CEM ],
                   [ EntityTypeType.CEM ],
                   OverloadProtection.DocumentSubRevision)

        {

            if (PhaseCount is < 1 or > 3)
                throw new ArgumentOutOfRangeException(nameof(PhaseCount),
                                                      "An EV charges on one, two or three phases.");

            this.PhaseCount = PhaseCount;

            #region The electrical connection: which phase is which (scenario 1)

            Electrical = Entity.Feature(FeatureTypeType.ElectricalConnection, RoleType.Server)
                             ?? Entity.AddFeature(FeatureTypeType.ElectricalConnection, RoleType.Server);

            Electrical.AddFunction(OverloadProtection.ParameterDescriptionListData);
            Electrical.AddFunction(OverloadProtection.PermittedValueSetListData);

            var phases = new[] { ElectricalConnectionPhaseNameType.A,
                                 ElectricalConnectionPhaseNameType.B,
                                 ElectricalConnectionPhaseNameType.C };

            Electrical.FunctionData(OverloadProtection.ParameterDescriptionListData)!.SetData(
                new ElectricalConnectionParameterDescriptionListDataType {
                    ElectricalConnectionParameterDescriptionData = [.. Enumerable.Range(0, (Int32) PhaseCount).
                        Select(phase => new ElectricalConnectionParameterDescriptionDataType {
                                            ElectricalConnectionId  = electricalConnectionId,
                                            ParameterId             = (UInt32) phase,
                                            MeasurementId           = (UInt32) phase,
                                            VoltageType             = ElectricalConnectionVoltageTypeType.Ac,
                                            AcMeasuredPhases        = phases[phase],
                                            AcMeasuredInReferenceTo = ElectricalConnectionPhaseNameType.Neutral,
                                            AcMeasurementType       = ElectricalConnectionAcMeasurementTypeType.Real,
                                            AcMeasurementVariant    = ElectricalConnectionMeasurandVariantType.Rms,
                                            ScopeType               = ScopeTypeType.AcCurrent
                                        })]
                }
            );

            #endregion

            #region The load control: one limit per phase (scenario 1)

            LoadControl = Entity.Feature(FeatureTypeType.LoadControl, RoleType.Server)
                              ?? Entity.AddFeature(FeatureTypeType.LoadControl, RoleType.Server);

            LoadControl.AddFunction(OverloadProtection.LimitDescriptionListData,
                                    Read:         true,
                                    PartialRead:  true);

            LoadControl.AddFunction(OverloadProtection.LimitListData,
                                    Read:          true,
                                    Write:         true,
                                    PartialRead:   true,
                                    PartialWrite:  true);

            var descriptions = new List<LoadControlLimitDescriptionDataType>();
            var limits       = new List<LoadControlLimitDataType>();

            for (var phase = 0u; phase < PhaseCount; phase++)
            {

                var limitId = phase + 1;

                limitIdOfPhase[phase] = limitId;

                descriptions.Add(OverloadProtection.LimitDescription(limitId, phase));

                limits.Add(new LoadControlLimitDataType {
                               LimitId            = limitId,
                               IsLimitChangeable  = true,
                               IsLimitActive      = false,
                               Value              = ScaledNumberType.FromValue(0)
                           });

            }

            LoadControl.FunctionData(OverloadProtection.LimitDescriptionListData)!.SetData(
                new LoadControlLimitDescriptionListDataType { LoadControlLimitDescriptionData = descriptions }
            );

            LoadControl.FunctionData(OverloadProtection.LimitListData)!.SetData(
                new LoadControlLimitListDataType { LoadControlLimitData = limits }
            );

            LoadControl.WriteApproval = ApproveLimits;

            #endregion

            #region The device diagnosis client: watching the energy guard (scenarios 2 and 3)

            Diagnosis = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Client)
                            ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Client);

            Device.Events.Subscribe<SPINEDataChanged>(Watch, SPINEEventLevel.Core);

            #endregion

        }

        #endregion


        #region SetPermittedCurrents(Minimum, Maximum)

        /// <summary>
        /// Say which currents this EV can charge with, per phase (Table 9).
        ///
        /// The energy guard has to know them before it curtails anything: a
        /// limit below the minimum would stop the charging rather than slow it
        /// down, which is not what overload protection is for.
        /// </summary>
        /// <param name="Minimum">The lowest current it can charge with, in ampere.</param>
        /// <param name="Maximum">The highest, in ampere.</param>
        public void SetPermittedCurrents(Decimal  Minimum,
                                         Decimal  Maximum)
        {

            Electrical.FunctionData(OverloadProtection.PermittedValueSetListData)!.SetData(
                new ElectricalConnectionPermittedValueSetListDataType {
                    ElectricalConnectionPermittedValueSetData = [.. Enumerable.Range(0, (Int32) PhaseCount).
                        Select(phase => new ElectricalConnectionPermittedValueSetDataType {
                                            ElectricalConnectionId  = electricalConnectionId,
                                            ParameterId             = (UInt32) phase,
                                            PermittedValueSet       = [
                                                new ScaledNumberSetType {
                                                    Range = [
                                                        new ScaledNumberRangeType {
                                                            Min = ScaledNumberType.FromValue(Minimum),
                                                            Max = ScaledNumberType.FromValue(Maximum)
                                                        }
                                                    ]
                                                }
                                            ]
                                        })]
                }
            );

        }

        #endregion

        #region CurrentLimits / ChargingCurrents

        /// <summary>
        /// The limits the energy guard set, per phase, in ampere - whether or
        /// not the EV is currently following them.
        /// </summary>
        public IReadOnlyList<(Decimal? Value, Boolean IsActive)> CurrentLimits
        {
            get
            {

                var limits = LoadControl.DataCopy<LoadControlLimitListDataType>(OverloadProtection.LimitListData)?.
                                 LoadControlLimitData ?? [];

                return [.. Enumerable.Range(0, (Int32) PhaseCount).
                             Select(phase => {

                                 var entry = limits.FirstOrDefault(limit => limit.LimitId == limitIdOfPhase[(UInt32) phase]);

                                 return (entry?.Value?.Value, entry?.IsLimitActive == true);

                             })];

            }
        }


        /// <summary>
        /// What this EV is actually charging with, per phase, in ampere.
        ///
        /// This is the answer the whole use case is about: the curtailment where
        /// the energy guard is there and healthy, and the safe current where it
        /// is not ([OPEV-005], [OPEV-007]).
        /// </summary>
        public IReadOnlyList<Decimal> ChargingCurrents
        {
            get
            {

                if (Trust != OPEVTrust.Curtailed)
                    return [.. Enumerable.Repeat(SafeCurrent, (Int32) PhaseCount)];

                return [.. CurrentLimits.Select(limit => limit.IsActive && limit.Value is Decimal value
                                                             ? value
                                                             : SafeCurrent)];

            }
        }

        #endregion

        #region Check()

        /// <summary>
        /// Look at the clock: has the energy guard gone quiet?
        ///
        /// Called by whoever drives the time. A device which stops sending gives
        /// nothing to react to, so somebody has to look - and in this use case
        /// the looking has to happen often, because the whole budget is six
        /// seconds.
        /// </summary>
        public OPEVTrustChanged? Check()
        {

            var now = Device.TimeProvider.GetUtcNow();

            lock (trustLock)
            {

                if (energyGuardFailed)
                    return To(OPEVTrust.EnergyGuardFailed,
                              "[OPEV-007] the energy guard announced a failure",
                              now);

                if (lastHeartbeat is not DateTimeOffset heartbeat ||
                    now - heartbeat > OverloadProtection.HeartbeatTimeout)
                    return To(OPEVTrust.HeartbeatMissing,
                              $"[OPEV-005] no heartbeat for more than {OverloadProtection.HeartbeatTimeout}",
                              now);

                return To(OPEVTrust.Curtailed,
                          "the energy guard is there and healthy",
                          now);

            }

        }

        #endregion


        #region (private) Watch(Event)

        /// <summary>
        /// The energy guard said something about itself: a heartbeat, or its
        /// state.
        /// </summary>
        private void Watch(SPINEDataChanged Event)
        {

            if (Event.Change.RemoteFeature.Role != RoleType.Server)
                return;

            if (Event.Change.Function == OverloadProtection.HeartbeatData)
            {

                lock (trustLock)
                {
                    lastHeartbeat = Device.TimeProvider.GetUtcNow();
                }

                Check();

            }

            if (Event.Change.Function == OverloadProtection.StateData)
            {

                var state = (Event.Change.Data as DeviceDiagnosisStateDataType)?.OperatingState;

                lock (trustLock)
                {
                    // Anything other than normal operation is a reason not to
                    // rely on the curtailment. The specification names "failure"
                    // (Table 11); a guard which says it is in an alarm or not
                    // ready is not more trustworthy than one which says failure.
                    energyGuardFailed = state is not null &&
                                        state != DeviceDiagnosisOperatingStateType.NormalOperation;
                }

                Check();

            }

        }

        #endregion

        #region (private) ApproveLimits(Message, CancellationToken)

        /// <summary>
        /// The energy guard wrote the limits.
        ///
        /// There is much less to decide than in the limitation of power
        /// consumption: a current below zero makes no sense, and a limit for a
        /// phase this EV does not charge on is about nothing. Everything else is
        /// accepted - the EV may then charge with less than it was told, but
        /// that is what the permitted value set is for, and it told the energy
        /// guard about it.
        /// </summary>
        private Task<ResultDataType?> ApproveLimits(SPINEMessage       Message,
                                                    CancellationToken  CancellationToken)
        {

            if (Message.Data is not LoadControlLimitListDataType data)
                return Task.FromResult<ResultDataType?>(null);

            foreach (var entry in data.LoadControlLimitData ?? [])
            {

                if (entry.LimitId is not UInt32 limitId)
                    continue;

                if (!limitIdOfPhase.ContainsValue(limitId))
                    return Task.FromResult<ResultDataType?>(
                               ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                    $"This EV has no limit {limitId}; it charges on {PhaseCount} phase(s).")
                           );

                if (entry.Value?.Value is Decimal value && value < 0)
                    return Task.FromResult<ResultDataType?>(
                               ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                    "A charging current below zero is refused.")
                           );

            }

            return Task.FromResult<ResultDataType?>(null);

        }

        #endregion

        #region (private) To(Trust, Reason, Timestamp)

        private OPEVTrustChanged? To(OPEVTrust       Next,
                                     String          Reason,
                                     DateTimeOffset  Timestamp)
        {

            if (trust == Next)
                return null;

            var change = new OPEVTrustChanged(Timestamp, trust, Next, Reason);

            trust = Next;

            OnTrustChanged?.Invoke(this, change);

            Device.Events.Publish(change);

            return change;

        }

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the load control feature.
        /// </summary>
        protected override SPINEFeature Feature()

            => LoadControl;

        #endregion

    }

}
