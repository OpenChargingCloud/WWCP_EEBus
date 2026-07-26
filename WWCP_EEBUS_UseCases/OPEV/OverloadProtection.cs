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

using cloud.charging.open.protocols.EEBUS.SPINE.Model;
using cloud.charging.open.protocols.EEBUS.UseCases.ChargingCurrent;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.OPEV
{

    /// <summary>
    /// What "Overload Protection by EV Charging Current Curtailment" is made of
    /// (EEBus_UC_TS_OverloadProtectionByEVChargingCurrentCurtailment_V1.0.1b).
    ///
    /// An energy guard watches the current of a circuit and tells an electric
    /// vehicle to charge with less, so that the fuse does not trip. Everything
    /// about it is faster and smaller than the limitation of power consumption:
    /// the limit is a **current per phase** rather than a power, the heartbeat
    /// timeout is four seconds rather than 120, and there is no failsafe value -
    /// an EV which stops hearing from its energy guard falls back to a current
    /// it chose itself.
    ///
    /// The reason for the four seconds is in section 2.1: a sensitive circuit
    /// breaker can trip within six seconds at twice its nominal current, so the
    /// whole chain - submeter, energy guard, EV - has to react inside that. The
    /// specification budgets two seconds at the submeter and the energy guard,
    /// one second between energy guard and EV, and two seconds at the EV, which
    /// leaves one second of slack.
    ///
    /// Structurally this is the same use case as the optimisation of self
    /// consumption during EV charging - see <see cref="ChargingCurrentProfile"/>
    /// - and the one thing which really differs is that these limits are an
    /// **obligation**. What is behind them is a fuse, so an EV which loses its
    /// energy guard does not simply stop obeying.
    /// </summary>
    public static class OverloadProtection
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.OverloadProtectionByEVChargingCurrentCurtailment;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 1);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        /// <summary>What scenario 1 is called.</summary>
        public const  String          ScenarioName          = "Energy Guard curtails charging current of EV";

        #endregion

        #region The scenarios (section 2.3)

        /// <summary>Scenario 1: the energy guard curtails the charging current of the EV.</summary>
        public const UInt32 ScenarioCurtailment    = ChargingCurrentProfile.ScenarioCurrents;

        /// <summary>Scenario 2: the EV checks whether the energy guard is still there.</summary>
        public const UInt32 ScenarioAvailability   = ChargingCurrentProfile.ScenarioAvailability;

        /// <summary>Scenario 3: the energy guard says that it has a problem.</summary>
        public const UInt32 ScenarioErrorState     = ChargingCurrentProfile.ScenarioErrorState;

        #endregion

        #region The timings (sections 2.1 and 3.2.1.3.1)

        /// <summary>
        /// After this long without a heartbeat, the EV stops trusting the energy
        /// guard and charges with a current it is sure about ([OPEV-005]).
        ///
        /// Four seconds, not 120 as in the limitation of power consumption: this
        /// use case is protecting a fuse, not managing a tariff.
        /// </summary>
        public static readonly TimeSpan  HeartbeatTimeout      = TimeSpan.FromSeconds(4);

        /// <summary>
        /// How often the energy guard sends its heartbeat.
        /// "deviceDiagnosisHeartbeatData SHALL be sent at least each
        /// heartbeatTimeout period" (Table 10), so it is sent a little more
        /// often than that.
        /// </summary>
        public static readonly TimeSpan  HeartbeatInterval     = TimeSpan.FromSeconds(2);

        /// <summary>
        /// How long the whole chain has, from the overload happening to the EV
        /// charging with less (section 2.1).
        /// </summary>
        public static readonly TimeSpan  ReactionBudget        = TimeSpan.FromSeconds(6);

        /// <summary>
        /// Of which the energy guard may use this much to decide and to send
        /// (section 2.1).
        /// </summary>
        public static readonly TimeSpan  EnergyGuardBudget     = TimeSpan.FromSeconds(2);

        /// <summary>
        /// ... this much for the message to reach the EV ...
        /// </summary>
        public static readonly TimeSpan  MessageBudget         = TimeSpan.FromSeconds(1);

        /// <summary>
        /// ... and this much for the EV to act on it.
        /// </summary>
        public static readonly TimeSpan  ElectricVehicleBudget = TimeSpan.FromSeconds(2);

        #endregion

        #region The functions

        /// <summary>The function carrying the limits.</summary>
        public const String LimitListData                = ChargingCurrentFunctions.LimitListData;

        /// <summary>The function describing them.</summary>
        public const String LimitDescriptionListData     = ChargingCurrentFunctions.LimitDescriptionListData;

        /// <summary>The function describing which measurement belongs to which phase.</summary>
        public const String ParameterDescriptionListData = ChargingCurrentFunctions.ParameterDescriptionListData;

        /// <summary>The function carrying the currents the EV can charge with.</summary>
        public const String PermittedValueSetListData    = ChargingCurrentFunctions.PermittedValueSetListData;

        /// <summary>The function carrying the heartbeat.</summary>
        public const String HeartbeatData                = ChargingCurrentFunctions.HeartbeatData;

        /// <summary>The function carrying the state of the energy guard.</summary>
        public const String StateData                    = ChargingCurrentFunctions.StateData;

        #endregion

        #region The profile

        /// <summary>
        /// What tells this use case from the optimisation of self consumption.
        ///
        /// An obligation with the scope "overloadProtection", and an EV which
        /// loses its energy guard falls back to a safe current rather than
        /// charging freely.
        ///
        /// The actor nuance is handled rather than chosen: the specification
        /// calls the client actor **EnergyGuard**, while the certified Go
        /// implementation announces it as **CEM**. An EV which only accepts one
        /// of the two would not work with half the field, so this implementation
        /// accepts both and can announce either.
        /// </summary>
        public static ChargingCurrentProfile Profile { get; }

            = new (UseCaseName:          Name,
                   Version:              Version,
                   DocumentSubRevision:  DocumentSubRevision,
                   RulePrefix:           "OPEV",
                   ClientActor:          UseCaseActors.EnergyGuard,
                   LimitCategory:        LoadControlCategoryType.Obligation,
                   Scope:                ScopeTypeType.OverloadProtection) {

                       AlsoKnownAsClientActor  = UseCaseActors.CEM,
                       FallsBackToSafeCurrent  = true,
                       HeartbeatTimeout        = HeartbeatTimeout,
                       HeartbeatInterval       = HeartbeatInterval

                   };


        /// <summary>
        /// The description which makes a load control limit an overload
        /// protection limit ("LoadControlLimit_OverloadProtection", Table 15).
        /// </summary>
        public static LoadControlLimitDescriptionDataType LimitDescription(UInt32  LimitId,
                                                                            UInt32  MeasurementId)

            => Profile.LimitDescription(LimitId, MeasurementId);


        /// <summary>
        /// Whether the given limit description is one of this use case.
        /// </summary>
        /// <param name="Description">A load control limit description.</param>
        public static Boolean IsALimit(LoadControlLimitDescriptionDataType? Description)

            => Profile.IsALimit(Description);

        #endregion

    }

}
