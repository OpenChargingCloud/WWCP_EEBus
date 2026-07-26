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
    /// The side which writes the charging currents of an electric vehicle - the
    /// energy guard of the overload protection, the energy manager of the
    /// optimisation of self consumption.
    ///
    /// It is the client actor and, as in the limitation of power consumption,
    /// the one exception is its own **diagnosis**, which it hosts as a server
    /// feature: the heartbeat which proves it is still there, and the state
    /// which says whether it is working. Announcing one's own failure is a rare
    /// thing for a protocol to ask for, and it is what lets an EV stop relying
    /// on a partner which is present but not to be trusted.
    /// </summary>
    public abstract class AChargingCurrentAdvisor : AUseCase
    {

        #region Properties

        /// <summary>
        /// Which of the charging current use cases this is.
        /// </summary>
        public ChargingCurrentProfile  Profile      { get; }

        /// <summary>
        /// The device diagnosis server feature, which carries the heartbeat and
        /// the state of this device.
        /// </summary>
        public SPINELocalFeature       Diagnosis    { get; }

        /// <summary>
        /// The heartbeat, which both use cases want every two seconds.
        /// </summary>
        public SPINEHeartbeat          Heartbeat    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the writing side of a charging current use case to an entity.
        /// </summary>
        /// <param name="Entity">The entity which writes the currents.</param>
        /// <param name="Profile">Which of the charging current use cases this is.</param>
        /// <param name="ScenarioName">What scenario 1 is called in this use case.</param>
        /// <param name="AnnounceAsAlternateActor">Whether to announce the second actor name of the profile rather than the one its document gives.</param>
        protected AChargingCurrentAdvisor(SPINELocalEntity        Entity,
                                          ChargingCurrentProfile  Profile,
                                          String                  ScenarioName,
                                          Boolean                 AnnounceAsAlternateActor   = false)

            : base(Entity,
                   AnnounceAsAlternateActor
                       ? Profile.AlsoKnownAsClientActor ?? Profile.ClientActor
                       : Profile.ClientActor,
                   Profile.UseCaseName,
                   Profile.Version,
                   ChargingCurrentProfile.ScenariosOf(ForClient: true, What: ScenarioName),
                   [ UseCaseActors.EV ],
                   [ EntityTypeType.EV ],
                   Profile.DocumentSubRevision)

        {

            this.Profile = Profile;

            foreach (var featureType in new[] {
                         FeatureTypeType.LoadControl,
                         FeatureTypeType.ElectricalConnection
                     })
                if (Entity.Feature(featureType, RoleType.Client) is null)
                    Entity.AddFeature(featureType, RoleType.Client);

            // Shared with whatever else on this entity hosts a diagnosis: a
            // customer energy manager running both charging current use cases
            // has one heartbeat, not two.
            Diagnosis = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Server)
                            ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Server);

            Diagnosis.AddFunction(ChargingCurrentFunctions.HeartbeatData);
            Diagnosis.AddFunction(ChargingCurrentFunctions.StateData);

            if (Diagnosis.DataCopy<DeviceDiagnosisStateDataType>(ChargingCurrentFunctions.StateData) is null)
                Diagnosis.FunctionData(ChargingCurrentFunctions.StateData)!.SetData(
                    new DeviceDiagnosisStateDataType {
                        OperatingState = DeviceDiagnosisOperatingStateType.NormalOperation
                    }
                );

            Heartbeat = new SPINEHeartbeat(Diagnosis);

        }

        #endregion


        #region StartHeartbeat(...) / StopHeartbeat() / SetOperatingState(...)

        /// <summary>
        /// Start proving that this device is there.
        /// </summary>
        /// <param name="Interval">How often. Every two seconds by default, because the EV gives up after four.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task StartHeartbeat(TimeSpan?          Interval            = null,
                                   CancellationToken  CancellationToken   = default)

            => Heartbeat.Start(Interval ?? Profile.HeartbeatInterval,
                               CancellationToken);


        /// <summary>
        /// Stop. Every EV watching stops following four seconds later.
        /// </summary>
        public void StopHeartbeat()
        {
            Heartbeat.Stop();
        }


        /// <summary>
        /// Say how this device is doing ([OPEV-007], [OSCEV-007]).
        ///
        /// Announcing a failure is the honest thing to do and the useful one: an
        /// EV which is told stops relying on what was written at once, rather
        /// than four seconds later when the heartbeat has also stopped.
        /// </summary>
        /// <param name="OperatingState">The state, i.e. "normalOperation" or "failure".</param>
        /// <param name="LastErrorCode">What went wrong, at most 128 characters.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task SetOperatingState(DeviceDiagnosisOperatingStateType  OperatingState,
                                      String?                            LastErrorCode       = null,
                                      CancellationToken                  CancellationToken   = default)

            => Diagnosis.SetData(
                   ChargingCurrentFunctions.StateData,
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
        /// Find out which phases an EV charges on, which limit of **this** use
        /// case belongs to which of them, and which currents it can charge with.
        ///
        /// This is the whole of the "before anything is written" part of
        /// scenario 1, and it needs three functions across two features: the
        /// limit descriptions say which limit measures what, the parameter
        /// descriptions say which measurement is on which phase, and the
        /// permitted value sets say what the EV can do. Nothing joins them but
        /// the identifiers.
        ///
        /// An EV running both charging current use cases has both sets of limits
        /// in one list, which is why the filter is on the profile rather than on
        /// the limit numbers.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<IReadOnlyList<ChargingCurrentPhase>> ReadPhases(SPINERemoteEntity  Partner,
                                                                          CancellationToken  CancellationToken   = default)
        {

            var loadControl  = LoadControlOf(Partner);
            var electrical   = ElectricalOf (Partner);

            await loadControl.RequestData(ChargingCurrentFunctions.LimitDescriptionListData,     CancellationToken: CancellationToken);
            await electrical. RequestData(ChargingCurrentFunctions.ParameterDescriptionListData, CancellationToken: CancellationToken);
            await electrical. RequestData(ChargingCurrentFunctions.PermittedValueSetListData,    CancellationToken: CancellationToken);

            var descriptions = loadControl.Data<LoadControlLimitDescriptionListDataType>(ChargingCurrentFunctions.LimitDescriptionListData)?.
                                   LoadControlLimitDescriptionData ?? [];

            var parameters   = electrical. Data<ElectricalConnectionParameterDescriptionListDataType>(ChargingCurrentFunctions.ParameterDescriptionListData)?.
                                   ElectricalConnectionParameterDescriptionData ?? [];

            var permitted    = electrical. Data<ElectricalConnectionPermittedValueSetListDataType>(ChargingCurrentFunctions.PermittedValueSetListData)?.
                                   ElectricalConnectionPermittedValueSetData ?? [];

            var phases       = new List<ChargingCurrentPhase>();

            foreach (var description in descriptions.Where(Profile.IsALimit))
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

                phases.Add(new ChargingCurrentPhase(phase,
                                                     limitId,
                                                     range?.Min?.Value,
                                                     range?.Max?.Value));

            }

            return [.. phases.OrderBy(entry => entry.Phase.ToString(), StringComparer.Ordinal)];

        }

        #endregion

        #region WriteCurrents(Partner, Currents, IsActive = true, ...) / WriteCurrent(...)

        /// <summary>
        /// Write a charging current per phase ([OPEV-001], [OSCEV-001]).
        ///
        /// Where the EV supports asymmetric charging the phases may differ
        /// ([OPEV-002], [OSCEV-002]); where it does not, they have to be equal,
        /// and the writing side has to use the lowest of the three.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Currents">The current per phase in ampere, in the order the phases were read.</param>
        /// <param name="IsActive">Whether it applies. False says "nothing to say right now".</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> WriteCurrents(SPINERemoteEntity     Partner,
                                                       IEnumerable<Decimal>  Currents,
                                                       Boolean               IsActive            = true,
                                                       CancellationToken     CancellationToken   = default)
        {

            var phases   = await ReadPhases(Partner, CancellationToken);
            var currents = Currents.ToList();

            if (phases.Count == 0)
                throw new InvalidOperationException($"{Partner.Address} has no {Profile.Scope} limits.");

            if (currents.Count != phases.Count)
                throw new ArgumentException($"The EV charges on {phases.Count} phase(s), but {currents.Count} current(s) were given.",
                                            nameof(Currents));

            if (currents.Any(current => current < 0))
                throw new ArgumentOutOfRangeException(nameof(Currents),
                                                      "A charging current is never below zero.");

            return await LoadControlOf(Partner).WriteData(
                             ChargingCurrentFunctions.LimitListData,
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
        /// Write the same current to every phase, which is what an EV without
        /// asymmetric charging needs.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Current">The current in ampere.</param>
        /// <param name="IsActive">Whether it applies.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> WriteCurrent(SPINERemoteEntity  Partner,
                                                      Decimal            Current,
                                                      Boolean            IsActive            = true,
                                                      CancellationToken  CancellationToken   = default)
        {

            var phases = await ReadPhases(Partner, CancellationToken);

            return await WriteCurrents(Partner,
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
