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

namespace cloud.charging.open.protocols.EEBUS.UseCases.OSCEV
{

    /// <summary>
    /// What "Optimization of Self-Consumption During EV Charging" is made of
    /// (EEBus_UC_TS_OptimizationOfSelfConsumptionDuringEVCharging_V1.0.1b).
    ///
    /// A customer energy manager watches how much electricity the house is
    /// producing itself - from a photovoltaic system, usually - and tells the
    /// car how much of it there is. The car may then charge with it. That is the
    /// whole use case, and it is deliberately small: "How the CEM monitors the
    /// self-produced current is not in the scope of this Use Case."
    ///
    /// On the wire it is the overload protection with two words changed - a
    /// **recommendation** instead of an obligation, with the scope
    /// "selfConsumption" instead of "overloadProtection" - so the two share a
    /// <see cref="ChargingCurrentProfile"/> rather than a copy. The rule numbers
    /// even match: [OSCEV-005] and [OPEV-005] are both the four second
    /// heartbeat, [OSCEV-007] and [OPEV-007] both the announced failure.
    ///
    /// Two things about it really are its own, and both follow from being a
    /// recommendation rather than an obligation:
    ///
    /// * **losing the energy manager does not mean charging slowly.** The car
    ///   was following advice about the sun; advice which has stopped arriving
    ///   is not advice. It stops applying the recommendation and charges as it
    ///   otherwise would, rather than falling back to a low safe current.
    /// * **a car with nothing left to optimise withdraws the scenario.** "If the
    ///   EV has no more flexibility to consume self-produced energy (e.g. the EV
    ///   has reached the maximum energy capacity), the EV SHALL stop to support
    ///   this scenario" [OSCEV-009]. It still implements the use case; it just
    ///   cannot do anything with it until the battery has room again.
    ///
    /// And one number worth keeping: the manager "SHOULD deliver new
    /// self-produced current values in near real-time to the EV and the EV
    /// SHOULD react within 3 seconds" [OSCEV-004].
    /// </summary>
    public static class SelfConsumptionOptimization
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.OptimizationOfSelfConsumptionDuringEVCharging;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 1);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        /// <summary>What scenario 1 is called.</summary>
        public const  String          ScenarioName          = "CEM informs EV about self-produced current";

        #endregion

        #region The scenarios (section 2.3)

        /// <summary>Scenario 1: the energy manager tells the EV about self-produced current.</summary>
        public const UInt32 ScenarioSelfProducedCurrent = ChargingCurrentProfile.ScenarioCurrents;

        /// <summary>Scenario 2: the EV checks whether the energy manager is still there.</summary>
        public const UInt32 ScenarioAvailability        = ChargingCurrentProfile.ScenarioAvailability;

        /// <summary>Scenario 3: the energy manager says that it has a problem.</summary>
        public const UInt32 ScenarioErrorState          = ChargingCurrentProfile.ScenarioErrorState;

        #endregion

        #region The timings

        /// <summary>
        /// After this long without a heartbeat, the EV stops relying on the
        /// self-consumption information ([OSCEV-005], Table 10).
        /// </summary>
        public static readonly TimeSpan  HeartbeatTimeout   = TimeSpan.FromSeconds(4);

        /// <summary>
        /// How often the energy manager sends its heartbeat.
        /// </summary>
        public static readonly TimeSpan  HeartbeatInterval  = TimeSpan.FromSeconds(2);

        /// <summary>
        /// How quickly the EV should act on a new value ([OSCEV-004]).
        ///
        /// Three seconds rather than the six of the overload protection, and for
        /// the opposite reason: nothing trips if the car is late, but a
        /// photovoltaic system's output moves with the clouds, so advice which
        /// arrives late is advice about weather which has passed.
        /// </summary>
        public static readonly TimeSpan  ReactionBudget     = TimeSpan.FromSeconds(3);

        #endregion

        #region The functions

        /// <summary>The function carrying the recommended currents.</summary>
        public const String LimitListData                = ChargingCurrentFunctions.LimitListData;

        /// <summary>The function describing them.</summary>
        public const String LimitDescriptionListData     = ChargingCurrentFunctions.LimitDescriptionListData;

        /// <summary>The function describing which measurement belongs to which phase.</summary>
        public const String ParameterDescriptionListData = ChargingCurrentFunctions.ParameterDescriptionListData;

        /// <summary>The function carrying the currents the EV can charge with.</summary>
        public const String PermittedValueSetListData    = ChargingCurrentFunctions.PermittedValueSetListData;

        /// <summary>The function carrying the heartbeat.</summary>
        public const String HeartbeatData                = ChargingCurrentFunctions.HeartbeatData;

        /// <summary>The function carrying the state of the energy manager.</summary>
        public const String StateData                    = ChargingCurrentFunctions.StateData;

        #endregion

        #region The profile

        /// <summary>
        /// What tells this use case from the overload protection: a
        /// recommendation with the scope "selfConsumption", and an EV which
        /// loses its energy manager charges as if it had never been there.
        /// </summary>
        public static ChargingCurrentProfile Profile { get; }

            = new (UseCaseName:          Name,
                   Version:              Version,
                   DocumentSubRevision:  DocumentSubRevision,
                   RulePrefix:           "OSCEV",
                   ClientActor:          UseCaseActors.CEM,
                   LimitCategory:        LoadControlCategoryType.Recommendation,
                   Scope:                ScopeTypeType.SelfConsumption) {

                       FallsBackToSafeCurrent  = false,
                       HeartbeatTimeout        = HeartbeatTimeout,
                       HeartbeatInterval       = HeartbeatInterval

                   };


        /// <summary>
        /// The description which makes a load control limit a self-consumption
        /// recommendation ("LoadControlLimit_SelfConsumptionOptimization",
        /// Table 15).
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
