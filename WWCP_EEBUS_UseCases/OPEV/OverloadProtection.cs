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

        #endregion

        #region The scenarios (section 2.3)

        /// <summary>Scenario 1: the energy guard curtails the charging current of the EV.</summary>
        public const UInt32 ScenarioCurtailment    = 1;

        /// <summary>Scenario 2: the EV checks whether the energy guard is still there.</summary>
        public const UInt32 ScenarioAvailability   = 2;

        /// <summary>Scenario 3: the energy guard says that it has a problem.</summary>
        public const UInt32 ScenarioErrorState     = 3;

        #endregion

        #region The timings (sections 2.1 and 3.2.1.3.1)

        /// <summary>
        /// After this long without a heartbeat, the EV stops trusting the energy
        /// guard and charges with a current it is sure about ([OPEV-005]).
        ///
        /// Four seconds, not 120 as in the limitation of power consumption: this
        /// use case is protecting a fuse, not managing a tariff.
        /// </summary>
        public static readonly TimeSpan  HeartbeatTimeout    = TimeSpan.FromSeconds(4);

        /// <summary>
        /// How often the energy guard sends its heartbeat.
        /// "deviceDiagnosisHeartbeatData SHALL be sent at least each
        /// heartbeatTimeout period" (Table 10), so it is sent a little more
        /// often than that.
        /// </summary>
        public static readonly TimeSpan  HeartbeatInterval   = TimeSpan.FromSeconds(2);

        /// <summary>
        /// How long the whole chain has, from the overload happening to the EV
        /// charging with less (section 2.1).
        /// </summary>
        public static readonly TimeSpan  ReactionBudget      = TimeSpan.FromSeconds(6);

        /// <summary>
        /// Of which the energy guard may use this much to decide and to send
        /// (section 2.1).
        /// </summary>
        public static readonly TimeSpan  EnergyGuardBudget   = TimeSpan.FromSeconds(2);

        /// <summary>
        /// ... this much for the message to reach the EV ...
        /// </summary>
        public static readonly TimeSpan  MessageBudget       = TimeSpan.FromSeconds(1);

        /// <summary>
        /// ... and this much for the EV to act on it.
        /// </summary>
        public static readonly TimeSpan  ElectricVehicleBudget = TimeSpan.FromSeconds(2);

        #endregion

        #region The functions

        /// <summary>The function carrying the limits.</summary>
        public const String LimitListData              = "loadControlLimitListData";

        /// <summary>The function describing them.</summary>
        public const String LimitDescriptionListData   = "loadControlLimitDescriptionListData";

        /// <summary>The function describing which measurement belongs to which phase.</summary>
        public const String ParameterDescriptionListData = "electricalConnectionParameterDescriptionListData";

        /// <summary>The function carrying the currents the EV can charge with.</summary>
        public const String PermittedValueSetListData  = "electricalConnectionPermittedValueSetListData";

        /// <summary>The function carrying the heartbeat.</summary>
        public const String HeartbeatData              = "deviceDiagnosisHeartbeatData";

        /// <summary>The function carrying the state of the energy guard.</summary>
        public const String StateData                  = "deviceDiagnosisStateData";

        #endregion

        #region The specialisation (Table 15)

        /// <summary>
        /// The description which makes a load control limit **an** overload
        /// protection limit of this use case
        /// ("LoadControlLimit_OverloadProtection").
        ///
        /// There is one per phase, and which phase it is about is not in the
        /// description: it links to a measurement, and the electrical connection
        /// parameter description says which phase that measurement is on. That
        /// indirection is the whole reason scenario 1 needs two features.
        /// </summary>
        public static LoadControlLimitDescriptionDataType LimitDescription(UInt32  LimitId,
                                                                            UInt32  MeasurementId)

            => new () {
                   LimitId         = LimitId,
                   LimitType       = LoadControlLimitTypeType.MaxValueLimit,
                   LimitCategory   = LoadControlCategoryType.Obligation,
                   LimitDirection  = EnergyDirectionType.Consume,
                   MeasurementId   = MeasurementId,
                   Unit            = UnitOfMeasurementType.A,
                   ScopeType       = ScopeTypeType.OverloadProtection
               };


        /// <summary>
        /// Whether the given limit description is one of this use case.
        ///
        /// Three of the four fields, not four: the certified Go implementation
        /// matches on the limit type, the category and the scope, and a device
        /// which leaves the direction out is a device we still want to talk to.
        /// </summary>
        /// <param name="Description">A load control limit description.</param>
        public static Boolean IsALimit(LoadControlLimitDescriptionDataType? Description)

            => Description is not null                                              &&
               Description.LimitType     == LoadControlLimitTypeType.MaxValueLimit  &&
               Description.LimitCategory == LoadControlCategoryType.Obligation      &&
               Description.ScopeType     == ScopeTypeType.OverloadProtection;

        #endregion

        #region The scenarios as the framework needs them

        /// <summary>
        /// The three scenarios, with the server features the partner needs.
        ///
        /// The two actors need different lists, and the split is unusually
        /// clean here: scenario 1 lives entirely at the EV, scenarios 2 and 3
        /// entirely at the energy guard.
        /// </summary>
        /// <param name="ForEnergyGuard">Whether the list is for the energy guard.</param>
        public static IEnumerable<UseCaseScenario> Scenarios(Boolean ForEnergyGuard)

            => ForEnergyGuard

                   ? [
                         new (ScenarioCurtailment,  [ FeatureTypeType.LoadControl,
                                                      FeatureTypeType.ElectricalConnection ], "Energy Guard curtails charging current of EV"),
                         new (ScenarioAvailability, [ ],                                      "EV checks Energy Guard availability"),
                         new (ScenarioErrorState,   [ ],                                      "Energy Guard sends error state")
                     ]

                   : [
                         new (ScenarioCurtailment,  [ ],                                      "Energy Guard curtails charging current of EV"),
                         new (ScenarioAvailability, [ FeatureTypeType.DeviceDiagnosis      ], "EV checks Energy Guard availability"),
                         new (ScenarioErrorState,   [ FeatureTypeType.DeviceDiagnosis      ], "Energy Guard sends error state")
                     ];

        #endregion

    }

}
