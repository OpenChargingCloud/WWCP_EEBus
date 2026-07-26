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
    /// One phase of an EV, as the energy guard sees it.
    /// </summary>
    /// <param name="Phase">Which phase it is.</param>
    /// <param name="LimitId">The load control limit which curtails it.</param>
    /// <param name="MinimumCurrent">The lowest current the EV can charge that phase with, in ampere.</param>
    /// <param name="MaximumCurrent">The highest, in ampere.</param>
    public sealed record OPEVPhase(ElectricalConnectionPhaseNameType  Phase,
                                   UInt32                             LimitId,
                                   Decimal?                           MinimumCurrent,
                                   Decimal?                           MaximumCurrent)
    {

        /// <summary>Return a text representation of this phase.</summary>
        public override String ToString()

            => $"{Phase}: limit {LimitId}, {MinimumCurrent}..{MaximumCurrent} A";

    }


    /// <summary>
    /// The energy guard of "Overload Protection by EV Charging Current
    /// Curtailment" - the device which keeps the fuse from tripping.
    ///
    /// It is the client actor and, as in the limitation of power consumption,
    /// the one exception is its own heartbeat, which it hosts as a server
    /// feature. Here it also hosts its **state**: an energy guard which knows it
    /// is not working properly says so, and the EV then stops relying on it
    /// ([OPEV-007]). Announcing one's own failure is a rare thing for a protocol
    /// to ask for, and it is the difference between a safe fallback and a fuse.
    ///
    /// One nuance worth knowing, and the reason this stack announces two actor
    /// names: the specification calls the client actor **EnergyGuard**, while
    /// the certified Go implementation announces it as **CEM**. An EV which only
    /// accepts one of the two would not work with half the field, so this
    /// implementation accepts both and can announce either.
    /// </summary>
    public class OPEVEnergyGuard : AUseCase
    {

        #region Properties

        /// <summary>
        /// The device diagnosis server feature, which carries the heartbeat and
        /// the state of this energy guard.
        /// </summary>
        public SPINELocalFeature  Diagnosis    { get; }

        /// <summary>
        /// The heartbeat, which this use case wants every two seconds.
        /// </summary>
        public SPINEHeartbeat     Heartbeat    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the energy guard of OPEV to an entity.
        /// </summary>
        /// <param name="Entity">The entity which protects against overload.</param>
        /// <param name="AnnounceAsCEM">
        /// Whether to announce the actor as "CEM" rather than "EnergyGuard".
        /// The specification says EnergyGuard; the certified Go implementation
        /// says CEM, and devices in the field were built against it.
        /// </param>
        public OPEVEnergyGuard(SPINELocalEntity  Entity,
                               Boolean           AnnounceAsCEM   = false)

            : base(Entity,
                   AnnounceAsCEM ? UseCaseActors.CEM : UseCaseActors.EnergyGuard,
                   OverloadProtection.Name,
                   OverloadProtection.Version,
                   OverloadProtection.Scenarios(ForEnergyGuard: true),
                   [ UseCaseActors.EV ],
                   [ EntityTypeType.EV ],
                   OverloadProtection.DocumentSubRevision)

        {

            foreach (var featureType in new[] {
                         FeatureTypeType.LoadControl,
                         FeatureTypeType.ElectricalConnection
                     })
                if (Entity.Feature(featureType, RoleType.Client) is null)
                    Entity.AddFeature(featureType, RoleType.Client);

            Diagnosis = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Server)
                            ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Server);

            Diagnosis.AddFunction(OverloadProtection.HeartbeatData);
            Diagnosis.AddFunction(OverloadProtection.StateData);

            Diagnosis.FunctionData(OverloadProtection.StateData)!.SetData(
                new DeviceDiagnosisStateDataType {
                    OperatingState = DeviceDiagnosisOperatingStateType.NormalOperation
                }
            );

            Heartbeat = new SPINEHeartbeat(Diagnosis);

        }

        #endregion


        #region StartHeartbeat(...) / StopHeartbeat() / SetOperatingState(...)

        /// <summary>
        /// Start proving that this energy guard is there.
        /// </summary>
        /// <param name="Interval">How often. Every two seconds by default, because the EV gives up after four.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task StartHeartbeat(TimeSpan?          Interval            = null,
                                   CancellationToken  CancellationToken   = default)

            => Heartbeat.Start(Interval ?? OverloadProtection.HeartbeatInterval,
                               CancellationToken);


        /// <summary>
        /// Stop. Every EV watching charges with its safe current four seconds
        /// later.
        /// </summary>
        public void StopHeartbeat()
        {
            Heartbeat.Stop();
        }


        /// <summary>
        /// Say how this energy guard is doing ([OPEV-007], Table 11).
        ///
        /// Announcing a failure is the honest thing to do and the useful one:
        /// an EV which is told stops relying on the curtailment at once, rather
        /// than four seconds later when the heartbeat has also stopped.
        /// </summary>
        /// <param name="OperatingState">The state, i.e. "normalOperation" or "failure".</param>
        /// <param name="LastErrorCode">What went wrong, at most 128 characters (Table 11).</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task SetOperatingState(DeviceDiagnosisOperatingStateType  OperatingState,
                                      String?                            LastErrorCode       = null,
                                      CancellationToken                  CancellationToken   = default)

            => Diagnosis.SetData(
                   OverloadProtection.StateData,
                   new DeviceDiagnosisStateDataType {
                       Timestamp       = AbsoluteOrRelativeTimeType.Parse(Device.TimeProvider.GetUtcNow()),
                       OperatingState  = OperatingState,
                       LastErrorCode   = LastErrorCode
                   },
                   CancellationToken: CancellationToken
               );

        #endregion


        #region LoadControlOf(Partner) / ElectricalOf(Partner)

        /// <summary>
        /// The load control of an EV, paired with our client feature.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        public UseCaseFeature LoadControlOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.LoadControl, Entity, Partner);


        /// <summary>
        /// Its electrical connection, which says which phase is which.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        public UseCaseFeature ElectricalOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.ElectricalConnection, Entity, Partner);

        #endregion

        #region ReadPhases(Partner, CancellationToken = default)

        /// <summary>
        /// Find out which phases an EV charges on, which limit belongs to which
        /// of them, and which currents it can charge with.
        ///
        /// This is the whole of the "before the energy guard curtails" part of
        /// scenario 1, and it needs three functions across two features: the
        /// limit descriptions say which limit measures what, the parameter
        /// descriptions say which measurement is on which phase, and the
        /// permitted value sets say what the EV can do. Nothing joins them but
        /// the identifiers.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<IReadOnlyList<OPEVPhase>> ReadPhases(SPINERemoteEntity  Partner,
                                                                CancellationToken  CancellationToken   = default)
        {

            var loadControl  = LoadControlOf(Partner);
            var electrical   = ElectricalOf (Partner);

            await loadControl.RequestData(OverloadProtection.LimitDescriptionListData,     CancellationToken: CancellationToken);
            await electrical. RequestData(OverloadProtection.ParameterDescriptionListData, CancellationToken: CancellationToken);
            await electrical. RequestData(OverloadProtection.PermittedValueSetListData,    CancellationToken: CancellationToken);

            var descriptions = loadControl.Data<LoadControlLimitDescriptionListDataType>(OverloadProtection.LimitDescriptionListData)?.
                                   LoadControlLimitDescriptionData ?? [];

            var parameters   = electrical. Data<ElectricalConnectionParameterDescriptionListDataType>(OverloadProtection.ParameterDescriptionListData)?.
                                   ElectricalConnectionParameterDescriptionData ?? [];

            var permitted    = electrical. Data<ElectricalConnectionPermittedValueSetListDataType>(OverloadProtection.PermittedValueSetListData)?.
                                   ElectricalConnectionPermittedValueSetData ?? [];

            var phases       = new List<OPEVPhase>();

            foreach (var description in descriptions.Where(OverloadProtection.IsALimit))
            {

                if (description.LimitId       is not UInt32 limitId ||
                    description.MeasurementId is not UInt32 measurementId)
                    continue;

                var parameter = parameters.FirstOrDefault(entry => entry.MeasurementId == measurementId);

                if (parameter?.AcMeasuredPhases is not ElectricalConnectionPhaseNameType phase)
                    continue;

                var range = permitted.FirstOrDefault(entry => entry.ElectricalConnectionId == parameter.ElectricalConnectionId &&
                                                              entry.ParameterId            == parameter.ParameterId)?.
                                PermittedValueSet?.FirstOrDefault()?.
                                Range?.FirstOrDefault();

                phases.Add(new OPEVPhase(phase,
                                         limitId,
                                         range?.Min?.Value,
                                         range?.Max?.Value));

            }

            return [.. phases.OrderBy(entry => entry.Phase.ToString(), StringComparer.Ordinal)];

        }

        #endregion

        #region WriteCurrentLimits(Partner, Currents, IsActive = true, ...)

        /// <summary>
        /// Curtail the charging current of an EV, phase by phase ([OPEV-001]).
        ///
        /// Where the EV supports asymmetric charging the phases may differ
        /// ([OPEV-002]); where it does not, they have to be equal, and the
        /// energy guard has to use the lowest of the three - which is the
        /// difference between 690 W and 460 W in the specification's own example.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Currents">The current per phase in ampere, in the order the phases were read.</param>
        /// <param name="IsActive">Whether the curtailment applies. False says "no curtailment is needed" ([OPEV-004]).</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> WriteCurrentLimits(SPINERemoteEntity      Partner,
                                                            IEnumerable<Decimal>   Currents,
                                                            Boolean                IsActive            = true,
                                                            CancellationToken      CancellationToken   = default)
        {

            var phases   = await ReadPhases(Partner, CancellationToken);
            var currents = Currents.ToList();

            if (phases.Count == 0)
                throw new InvalidOperationException($"{Partner.Address} has no overload protection limits.");

            if (currents.Count != phases.Count)
                throw new ArgumentException($"The EV charges on {phases.Count} phase(s), but {currents.Count} current(s) were given.",
                                            nameof(Currents));

            if (currents.Any(current => current < 0))
                throw new ArgumentOutOfRangeException(nameof(Currents),
                                                      "A charging current is never below zero.");

            return await LoadControlOf(Partner).WriteData(
                             OverloadProtection.LimitListData,
                             new LoadControlLimitListDataType {
                                 LoadControlLimitData = [.. phases.Select((phase, index) =>
                                     new LoadControlLimitDataType {
                                         LimitId        = phase.LimitId,
                                         IsLimitActive  = IsActive,
                                         Value          = ScaledNumberType.FromValue(currents[index])
                                     })]
                             },
                             Partial: true,
                             CancellationToken: CancellationToken
                         );

        }


        /// <summary>
        /// Curtail every phase to the same current, which is what an EV without
        /// asymmetric charging needs.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Current">The current in ampere.</param>
        /// <param name="IsActive">Whether the curtailment applies.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> WriteCurrentLimit(SPINERemoteEntity  Partner,
                                                           Decimal            Current,
                                                           Boolean            IsActive            = true,
                                                           CancellationToken  CancellationToken   = default)
        {

            var phases = await ReadPhases(Partner, CancellationToken);

            return await WriteCurrentLimits(Partner,
                                            Enumerable.Repeat(Current, phases.Count),
                                            IsActive,
                                            CancellationToken);

        }

        #endregion


        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the load control client feature.
        /// </summary>
        protected override SPINEFeature Feature()

            => Entity.Feature(FeatureTypeType.LoadControl, RoleType.Client) ?? Diagnosis;

        #endregion

    }

}
