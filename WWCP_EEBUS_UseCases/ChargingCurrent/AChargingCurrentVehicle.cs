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

namespace cloud.charging.open.protocols.EEBUS.UseCases.ChargingCurrent
{

    /// <summary>
    /// The electric vehicle of a charging current use case - the side which is
    /// told what to charge with.
    ///
    /// It is the server actor: it holds one current limit per phase and the
    /// currents it is able to charge with, and the other side writes the limits.
    /// What it does when that side goes quiet or announces a failure is chapter
    /// 3 of both specifications, which the Go reference implementation does not
    /// have at all - eebus-go implements the writing side only, so the behaviour
    /// a certification actually tests an EV for has no reference to copy.
    ///
    /// That behaviour is two rules and they are the reason the use cases exist:
    ///
    /// * no heartbeat for more than four seconds ([OPEV-005], [OSCEV-005]);
    /// * the other side announced a failure ([OPEV-007], [OSCEV-007]), which is
    ///   the faster of the two, because the heartbeat is still arriving.
    ///
    /// What *follows* from either of them is where the two use cases differ, and
    /// it is the difference between an obligation and a recommendation - see
    /// <see cref="ChargingCurrentProfile.FallsBackToSafeCurrent"/>.
    /// </summary>
    public abstract class AChargingCurrentVehicle : AUseCase
    {

        #region Data

        private readonly UInt32                       electricalConnectionId  = 0;

        private readonly Dictionary<UInt32, UInt32>   limitIdOfPhase          = [];

        private readonly Dictionary<UInt32, UInt32>   parameterIdOfPhase      = [];

        private readonly Dictionary<UInt32, UInt32>   measurementIdOfPhase    = [];

        private          DateTimeOffset?              lastHeartbeat;

        private          Boolean                      partnerFailed;

        private          ChargingCurrentTrust         trust                   = ChargingCurrentTrust.HeartbeatMissing;

        private readonly Lock                         trustLock               = new ();

        #endregion

        #region Properties

        /// <summary>
        /// Which of the charging current use cases this is.
        /// </summary>
        public ChargingCurrentProfile  Profile        { get; }

        /// <summary>
        /// The load control server feature, which holds the limits.
        /// </summary>
        public SPINELocalFeature       LoadControl    { get; }

        /// <summary>
        /// The electrical connection server feature, which says which phases
        /// this EV charges on and which currents it can charge with.
        /// </summary>
        public SPINELocalFeature       Electrical     { get; }

        /// <summary>
        /// The device diagnosis client feature, with which it watches the other
        /// side.
        /// </summary>
        public SPINELocalFeature       Diagnosis      { get; }

        /// <summary>
        /// How many phases this EV charges on.
        /// </summary>
        public UInt32                  PhaseCount     { get; }

        /// <summary>
        /// Whether the other side is to be trusted right now, and why not.
        /// </summary>
        public ChargingCurrentTrust    Trust
        {
            get { lock (trustLock) { return trust; } }
        }

        /// <summary>
        /// The current this EV charges with while it does not trust the other
        /// side, for a use case whose limits are an obligation ([OPEV-005]).
        ///
        /// The specification does not say what it is - it is a property of the
        /// installation, and only the vehicle and its owner know it. Ignored by
        /// a use case whose limits are a recommendation, where losing the other
        /// side means charging as if it had never been there.
        /// </summary>
        public Decimal                 SafeCurrent    { get; set; } = 6;

        #endregion

        #region Events

        /// <summary>
        /// The EV started or stopped trusting the other side.
        /// </summary>
        public event Action<AChargingCurrentVehicle, ChargingCurrentTrustChanged>? OnTrustChanged;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the EV of a charging current use case to an entity.
        /// </summary>
        /// <param name="Entity">The entity of the electric vehicle.</param>
        /// <param name="Profile">Which of the charging current use cases this is.</param>
        /// <param name="ScenarioName">What scenario 1 is called in this use case.</param>
        /// <param name="PhaseCount">How many phases it charges on. Three by default.</param>
        protected AChargingCurrentVehicle(SPINELocalEntity        Entity,
                                          ChargingCurrentProfile  Profile,
                                          String                  ScenarioName,
                                          UInt32                  PhaseCount   = 3)

            : base(Entity,
                   UseCaseActors.EV,
                   Profile.UseCaseName,
                   Profile.Version,
                   ChargingCurrentProfile.ScenariosOf(ForClient: false, What: ScenarioName),
                   Profile.ClientActors,
                   [ EntityTypeType.CEM ],
                   Profile.DocumentSubRevision)

        {

            if (PhaseCount is < 1 or > 3)
                throw new ArgumentOutOfRangeException(nameof(PhaseCount),
                                                      "An EV charges on one, two or three phases.");

            this.Profile     = Profile;
            this.PhaseCount  = PhaseCount;

            #region The electrical connection: which phase is which (scenario 1)

            // An EV entity is regularly the server of several use cases at once
            // - the commissioning, the electricity measurement, the state of
            // charge and the *other* charging current use case all write to
            // these same features - and SPINE allows only one of each per
            // entity. So the identifiers are picked around whatever is already
            // there and the entries are appended rather than replacing the list
            // (see docs/adr/0006-one-feature-many-use-cases.md).
            Electrical = Entity.Feature(FeatureTypeType.ElectricalConnection, RoleType.Server)
                             ?? Entity.AddFeature(FeatureTypeType.ElectricalConnection, RoleType.Server);

            Electrical.AddFunction(ChargingCurrentFunctions.ParameterDescriptionListData);
            Electrical.AddFunction(ChargingCurrentFunctions.PermittedValueSetListData);

            var phases     = new[] { ElectricalConnectionPhaseNameType.A,
                                     ElectricalConnectionPhaseNameType.B,
                                     ElectricalConnectionPhaseNameType.C };

            var parameters = Electrical.DataCopy<ElectricalConnectionParameterDescriptionListDataType>(ChargingCurrentFunctions.ParameterDescriptionListData)?.
                                 ElectricalConnectionParameterDescriptionData?.ToList() ?? [];

            // A phase this EV already described for another use case is the same
            // phase: one parameter, and both use cases point their limits at it.
            // Inventing a second parameter for phase A would tell the other side
            // that this car has two of them.
            //
            // Note that the parameter identifier and the measurement identifier
            // it carries are two different numbers. They happen to be equal in
            // the parameters we write ourselves, and they are not equal in one
            // written by the electricity measurement use case - so a limit
            // description has to quote the *measurement* identifier, which is
            // what the other side joins on.
            foreach (var phase in Enumerable.Range(0, (Int32) PhaseCount))
            {

                var existing = parameters.FirstOrDefault(parameter => parameter.AcMeasuredPhases == phases[phase]           &&
                                                                       parameter.ScopeType        == ScopeTypeType.AcCurrent &&
                                                                       parameter.ParameterId      is not null                &&
                                                                       parameter.MeasurementId    is not null);

                if (existing is not null)
                {
                    parameterIdOfPhase  [(UInt32) phase] = existing.ParameterId!.Value;
                    measurementIdOfPhase[(UInt32) phase] = existing.MeasurementId!.Value;
                    continue;
                }

                var parameterId   = UseCaseIds.NextFree(parameters.Select(parameter => parameter.ParameterId),
                                                        StartingAt: 0);

                var measurementId = UseCaseIds.NextFree(parameters.Select(parameter => parameter.MeasurementId),
                                                        StartingAt: 0);

                parameterIdOfPhase  [(UInt32) phase] = parameterId;
                measurementIdOfPhase[(UInt32) phase] = measurementId;

                parameters.Add(new ElectricalConnectionParameterDescriptionDataType {
                                   ElectricalConnectionId  = electricalConnectionId,
                                   ParameterId             = parameterId,
                                   MeasurementId           = measurementId,
                                   VoltageType             = ElectricalConnectionVoltageTypeType.Ac,
                                   AcMeasuredPhases        = phases[phase],
                                   AcMeasuredInReferenceTo = ElectricalConnectionPhaseNameType.Neutral,
                                   AcMeasurementType       = ElectricalConnectionAcMeasurementTypeType.Real,
                                   AcMeasurementVariant    = ElectricalConnectionMeasurandVariantType.Rms,
                                   ScopeType               = ScopeTypeType.AcCurrent
                               });

            }

            Electrical.FunctionData(ChargingCurrentFunctions.ParameterDescriptionListData)!.SetData(
                new ElectricalConnectionParameterDescriptionListDataType {
                    ElectricalConnectionParameterDescriptionData = parameters
                }
            );

            #endregion

            #region The load control: one limit per phase (scenario 1)

            LoadControl = Entity.Feature(FeatureTypeType.LoadControl, RoleType.Server)
                              ?? Entity.AddFeature(FeatureTypeType.LoadControl, RoleType.Server);

            LoadControl.AddFunction(ChargingCurrentFunctions.LimitDescriptionListData,
                                    Read:         true,
                                    PartialRead:  true);

            LoadControl.AddFunction(ChargingCurrentFunctions.LimitListData,
                                    Read:          true,
                                    Write:         true,
                                    PartialRead:   true,
                                    PartialWrite:  true);

            var descriptions = LoadControl.DataCopy<LoadControlLimitDescriptionListDataType>(ChargingCurrentFunctions.LimitDescriptionListData)?.
                                   LoadControlLimitDescriptionData?.ToList() ?? [];

            var limits       = LoadControl.DataCopy<LoadControlLimitListDataType>(ChargingCurrentFunctions.LimitListData)?.
                                   LoadControlLimitData?.ToList() ?? [];

            foreach (var (phase, limitId) in Enumerable.Range(0, (Int32) PhaseCount).
                                                 Zip(UseCaseIds.NextFree(descriptions.Select(description => description.LimitId),
                                                                         Count: PhaseCount)))
            {

                limitIdOfPhase[(UInt32) phase] = limitId;

                descriptions.Add(Profile.LimitDescription(limitId, measurementIdOfPhase[(UInt32) phase]));

                limits.Add(new LoadControlLimitDataType {
                               LimitId            = limitId,
                               IsLimitChangeable  = true,
                               IsLimitActive      = false,
                               Value              = ScaledNumberType.FromValue(0)
                           });

            }

            LoadControl.FunctionData(ChargingCurrentFunctions.LimitDescriptionListData)!.SetData(
                new LoadControlLimitDescriptionListDataType { LoadControlLimitDescriptionData = descriptions }
            );

            LoadControl.FunctionData(ChargingCurrentFunctions.LimitListData)!.SetData(
                new LoadControlLimitListDataType { LoadControlLimitData = limits }
            );

            // Chained rather than assigned: the other charging current use case
            // on this feature keeps its veto.
            var approvedBySomeoneElse  = LoadControl.WriteApproval;
            LoadControl.WriteApproval  = async (message, cancellationToken) =>
                await ApproveLimits(message, cancellationToken)
                    ?? (approvedBySomeoneElse is not null
                            ? await approvedBySomeoneElse(message, cancellationToken)
                            : null);

            #endregion

            #region The device diagnosis client: watching the other side (scenarios 2 and 3)

            Diagnosis = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Client)
                            ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Client);

            Device.Events.Subscribe<SPINEDataChanged>(Watch, SPINEEventLevel.Core);

            #endregion

        }

        #endregion


        #region SetPermittedCurrents(Minimum, Maximum)

        /// <summary>
        /// Say which currents this EV can charge with, per phase.
        ///
        /// The other side has to know them before it writes anything: a current
        /// below the minimum would stop the charging rather than slow it down,
        /// which is not what either use case is for.
        /// </summary>
        /// <param name="Minimum">The lowest current it can charge with, in ampere.</param>
        /// <param name="Maximum">The highest, in ampere.</param>
        public void SetPermittedCurrents(Decimal  Minimum,
                                         Decimal  Maximum)
        {

            // Only our own parameters are rewritten: the charging power limits
            // of the EV commissioning use case live in this same list.
            var mine      = parameterIdOfPhase.Values.ToHashSet();

            var permitted = Electrical.DataCopy<ElectricalConnectionPermittedValueSetListDataType>(ChargingCurrentFunctions.PermittedValueSetListData)?.
                                ElectricalConnectionPermittedValueSetData?.
                                Where(entry => entry.ParameterId is not UInt32 id || !mine.Contains(id)).
                                ToList() ?? [];

            foreach (var parameterId in parameterIdOfPhase.OrderBy(entry => entry.Key).Select(entry => entry.Value))
                permitted.Add(new ElectricalConnectionPermittedValueSetDataType {
                                  ElectricalConnectionId  = electricalConnectionId,
                                  ParameterId             = parameterId,
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
                              });

            Electrical.FunctionData(ChargingCurrentFunctions.PermittedValueSetListData)!.SetData(
                new ElectricalConnectionPermittedValueSetListDataType {
                    ElectricalConnectionPermittedValueSetData = permitted
                }
            );

        }

        #endregion

        #region CurrentLimits / ChargingCurrents

        /// <summary>
        /// What the other side wrote, per phase, in ampere - whether or not the
        /// EV is currently following it.
        /// </summary>
        public IReadOnlyList<(Decimal? Value, Boolean IsActive)> CurrentLimits
        {
            get
            {

                var limits = LoadControl.DataCopy<LoadControlLimitListDataType>(ChargingCurrentFunctions.LimitListData)?.
                                 LoadControlLimitData ?? [];

                return [.. Enumerable.Range(0, (Int32) PhaseCount).
                             Select(phase => {

                                 var entry = limits.FirstOrDefault(limit => limit.LimitId == limitIdOfPhase[(UInt32) phase]);

                                 return (entry?.Value?.Value, entry?.IsLimitActive == true);

                             })];

            }
        }


        /// <summary>
        /// What this EV charges with as far as this use case is concerned, per
        /// phase, in ampere - or null for a phase this use case currently has
        /// nothing to say about.
        ///
        /// This is the answer the whole use case is about. While the other side
        /// is there and healthy it is what was written; while it is not, it
        /// depends on whether that was an obligation or a recommendation:
        ///
        /// * an obligation falls back to a current the EV is sure about
        ///   ([OPEV-005]);
        /// * a recommendation simply stops applying - null, meaning "this use
        ///   case is not currently constraining anything", not zero.
        /// </summary>
        public IReadOnlyList<Decimal?> Currents
        {
            get
            {

                if (Trust != ChargingCurrentTrust.Following)
                    return [.. Enumerable.Repeat(Profile.FallsBackToSafeCurrent
                                                     ? SafeCurrent
                                                     : (Decimal?) null,
                                                 (Int32) PhaseCount)];

                return [.. CurrentLimits.Select<(Decimal? Value, Boolean IsActive), Decimal?>(
                               limit => limit.IsActive && limit.Value is Decimal value
                                            ? value
                                            : Profile.FallsBackToSafeCurrent
                                                  ? SafeCurrent
                                                  : null)];

            }
        }

        #endregion

        #region Check()

        /// <summary>
        /// Look at the clock: has the other side gone quiet?
        ///
        /// Called by whoever drives the time. A device which stops sending gives
        /// nothing to react to, so somebody has to look - and here the looking
        /// has to happen often, because the whole budget is a few seconds.
        /// </summary>
        public ChargingCurrentTrustChanged? Check()
        {

            var now = Device.TimeProvider.GetUtcNow();

            lock (trustLock)
            {

                if (partnerFailed)
                    return To(ChargingCurrentTrust.PartnerFailed,
                              $"[{Profile.RulePrefix}-007] the partner announced a failure",
                              now);

                if (lastHeartbeat is not DateTimeOffset heartbeat ||
                    now - heartbeat > Profile.HeartbeatTimeout)
                    return To(ChargingCurrentTrust.HeartbeatMissing,
                              $"[{Profile.RulePrefix}-005] no heartbeat for more than {Profile.HeartbeatTimeout}",
                              now);

                return To(ChargingCurrentTrust.Following,
                          "the partner is there and healthy",
                          now);

            }

        }

        #endregion


        #region (private) Watch(Event)

        /// <summary>
        /// The other side said something about itself: a heartbeat, or its
        /// state.
        /// </summary>
        private void Watch(SPINEDataChanged Event)
        {

            if (Event.Change.RemoteFeature.Role != RoleType.Server)
                return;

            if (Event.Change.Function == ChargingCurrentFunctions.HeartbeatData)
            {

                lock (trustLock)
                {
                    lastHeartbeat = Device.TimeProvider.GetUtcNow();
                }

                Check();

            }

            if (Event.Change.Function == ChargingCurrentFunctions.StateData)
            {

                var state = (Event.Change.Data as DeviceDiagnosisStateDataType)?.OperatingState;

                lock (trustLock)
                {
                    // Anything other than normal operation is a reason not to
                    // rely on what was written. Both specifications name
                    // "failure"; a partner which says it is in an alarm or not
                    // ready is not more trustworthy than one which says failure.
                    partnerFailed = state is not null &&
                                    state != DeviceDiagnosisOperatingStateType.NormalOperation;
                }

                Check();

            }

        }

        #endregion

        #region (private) ApproveLimits(Message, CancellationToken)

        /// <summary>
        /// The other side wrote the limits.
        ///
        /// There is much less to decide than in the limitation of power
        /// consumption: a current below zero makes no sense, and a limit for a
        /// phase this EV does not charge on is about nothing. Everything else is
        /// accepted - the EV may then charge with less than it was told, but
        /// that is what the permitted value set is for, and it said so.
        ///
        /// Limits which are not ours are passed on rather than refused: the
        /// other charging current use case on this feature gets to judge its
        /// own.
        /// </summary>
        private Task<ResultDataType?> ApproveLimits(SPINEMessage       Message,
                                                    CancellationToken  CancellationToken)
        {

            if (Message.Data is not LoadControlLimitListDataType data)
                return Task.FromResult<ResultDataType?>(null);

            var onTheFeature = LoadControl.DataCopy<LoadControlLimitListDataType>(ChargingCurrentFunctions.LimitListData)?.
                                   LoadControlLimitData?.
                                   Select(limit => limit.LimitId).
                                   ToHashSet() ?? [];

            foreach (var entry in data.LoadControlLimitData ?? [])
            {

                if (entry.LimitId is not UInt32 limitId)
                    continue;

                // Not one of ours. If somebody else on this feature owns it, it
                // is their judgement to make; if nobody does, it is about
                // nothing and no chained approval will catch it either.
                if (!limitIdOfPhase.ContainsValue(limitId))
                {

                    if (!onTheFeature.Contains(limitId))
                        return Task.FromResult<ResultDataType?>(
                                   ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                        $"This EV has no limit {limitId}; it charges on {PhaseCount} phase(s).")
                               );

                    continue;

                }

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

        private ChargingCurrentTrustChanged? To(ChargingCurrentTrust  Next,
                                                String                Reason,
                                                DateTimeOffset        Timestamp)
        {

            if (trust == Next)
                return null;

            var change = new ChargingCurrentTrustChanged(Timestamp, Profile.UseCaseName, trust, Next, Reason);

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
