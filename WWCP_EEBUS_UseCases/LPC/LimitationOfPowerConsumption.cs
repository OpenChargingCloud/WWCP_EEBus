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

namespace cloud.charging.open.protocols.EEBUS.UseCases.LPC
{

    /// <summary>
    /// What "Limitation of Power Consumption" is made of.
    ///
    /// The use case (EEBus_UC_TS_LimitationOfPowerConsumption_V1.0.0) has an
    /// energy guard limiting the active power consumption of a controllable
    /// system. In Germany this is the use case behind §14a EnWG, which is why
    /// its numbers are law rather than convention: 120 seconds without a
    /// heartbeat and the controllable system limits itself to its failsafe
    /// value, whatever the energy guard meant to say.
    ///
    /// Everything here is a value the specification names, in one place, so that
    /// the two actors cannot disagree about it.
    /// </summary>
    public static class LimitationOfPowerConsumption
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.LimitationOfPowerConsumption;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 0);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        #endregion

        #region The scenarios (section 2.6)

        /// <summary>Scenario 1: control the active power consumption limit.</summary>
        public const UInt32 ScenarioControlLimit   = 1;

        /// <summary>Scenario 2: the failsafe values.</summary>
        public const UInt32 ScenarioFailsafe       = 2;

        /// <summary>Scenario 3: the heartbeat of the energy guard.</summary>
        public const UInt32 ScenarioHeartbeat      = 3;

        /// <summary>Scenario 4: the nominal maximum values.</summary>
        public const UInt32 ScenarioConstraints    = 4;

        #endregion

        #region The timings (sections 2.2 and 2.3)

        /// <summary>
        /// After this long without a heartbeat from the energy guard, the
        /// controllable system goes into its failsafe state
        /// ([LPC-911], [LPC-912]).
        /// </summary>
        public static readonly TimeSpan  HeartbeatTimeout           = TimeSpan.FromSeconds(120);

        /// <summary>
        /// How often the energy guard sends its heartbeat. The specification
        /// fixes the timeout rather than the interval, and names 60 seconds as
        /// the time within which the energy guard has to send a heartbeat and a
        /// limit after communication is restored ([LPC-913]).
        /// </summary>
        public static readonly TimeSpan  HeartbeatInterval          = TimeSpan.FromSeconds(60);

        /// <summary>
        /// In the states "init", "failsafe" and "unlimited/autonomous", a write
        /// of the limit is only evaluated when it follows a heartbeat within
        /// this time (section 2.2).
        /// </summary>
        public static readonly TimeSpan  LimitAfterHeartbeat        = TimeSpan.FromSeconds(60);

        /// <summary>
        /// How long the controllable system waits for a heartbeat and a limit
        /// before it decides that nobody is controlling it
        /// ([LPC-906], [LPC-921]).
        /// </summary>
        public static readonly TimeSpan  WaitForControl             = TimeSpan.FromSeconds(120);

        /// <summary>
        /// The lowest failsafe duration minimum a controllable system has to
        /// accept ([LPC-022/1], [LPC-022/3]).
        /// </summary>
        public static readonly TimeSpan  FailsafeDurationMinimumLowerBound  = TimeSpan.FromHours(2);

        /// <summary>
        /// The highest one ([LPC-022/1], [LPC-022/3]).
        /// </summary>
        public static readonly TimeSpan  FailsafeDurationMinimumUpperBound  = TimeSpan.FromHours(24);

        #endregion

        #region The specialisations (section 3.2)

        /// <summary>
        /// The function carrying the limit.
        /// </summary>
        public const String LimitListData             = "loadControlLimitListData";

        /// <summary>
        /// The function describing it.
        /// </summary>
        public const String LimitDescriptionListData  = "loadControlLimitDescriptionListData";

        /// <summary>
        /// The function carrying the failsafe values.
        /// </summary>
        public const String KeyValueListData          = "deviceConfigurationKeyValueListData";

        /// <summary>
        /// The function describing them.
        /// </summary>
        public const String KeyValueDescriptionListData = "deviceConfigurationKeyValueDescriptionListData";

        /// <summary>
        /// The function carrying the nominal maximum values.
        /// </summary>
        public const String CharacteristicListData    = "electricalConnectionCharacteristicListData";

        /// <summary>
        /// The function carrying the heartbeat.
        /// </summary>
        public const String HeartbeatData             = "deviceDiagnosisHeartbeatData";


        /// <summary>
        /// The description which makes a load control limit **the** active power
        /// consumption limit of this use case, rather than one of the other
        /// limits a device may have (Table 14,
        /// "LoadControlLimit_ActivePowerConsumptionLimit").
        ///
        /// All four of these have to match: a limit which is a recommendation
        /// rather than an obligation, or which is about production rather than
        /// consumption, is a different limit.
        /// </summary>
        public static LoadControlLimitDescriptionDataType LimitDescription(UInt32   LimitId,
                                                                           UInt32?  MeasurementId   = null)

            => new () {
                   LimitId         = LimitId,
                   LimitType       = LoadControlLimitTypeType.SignDependentAbsValueLimit,
                   LimitCategory   = LoadControlCategoryType.Obligation,
                   LimitDirection  = EnergyDirectionType.Consume,
                   MeasurementId   = MeasurementId,
                   Unit            = UnitOfMeasurementType.W,
                   ScopeType       = ScopeTypeType.ActivePowerLimit
               };


        /// <summary>
        /// Whether the given limit description is the one of this use case.
        /// </summary>
        /// <param name="Description">A load control limit description.</param>
        public static Boolean IsTheLimit(LoadControlLimitDescriptionDataType? Description)

            => Description is not null                                                        &&
               Description.LimitType      == LoadControlLimitTypeType.SignDependentAbsValueLimit &&
               Description.LimitCategory  == LoadControlCategoryType.Obligation               &&
               Description.LimitDirection == EnergyDirectionType.Consume                      &&
               Description.ScopeType      == ScopeTypeType.ActivePowerLimit;


        /// <summary>
        /// The configuration key holding the failsafe consumption active power
        /// limit ([LPC-021], Table 15).
        /// </summary>
        public static readonly DeviceConfigurationKeyNameType FailsafeLimitKey
            = DeviceConfigurationKeyNameType.FailsafeConsumptionActivePowerLimit;

        /// <summary>
        /// The configuration key holding the failsafe duration minimum
        /// ([LPC-022], Table 16).
        /// </summary>
        public static readonly DeviceConfigurationKeyNameType FailsafeDurationKey
            = DeviceConfigurationKeyNameType.FailsafeDurationMinimum;

        #endregion

        #region The scenarios as the framework needs them

        /// <summary>
        /// The four scenarios, with the server features a partner needs for
        /// each of them.
        ///
        /// The features are the ones of the **partner**, so the two actors need
        /// different lists: the energy guard looks for the load control and
        /// configuration servers of the controllable system, the controllable
        /// system looks for the heartbeat server of the energy guard.
        /// </summary>
        /// <param name="ForEnergyGuard">Whether the list is for the energy guard.</param>
        public static IEnumerable<UseCaseScenario> Scenarios(Boolean ForEnergyGuard)

            => ForEnergyGuard

                   ? [
                         new (ScenarioControlLimit, [ FeatureTypeType.LoadControl          ], "Control active power consumption limit"),
                         new (ScenarioFailsafe,     [ FeatureTypeType.DeviceConfiguration  ], "Failsafe values"),
                         new (ScenarioHeartbeat,    [ FeatureTypeType.DeviceDiagnosis      ], "Heartbeat"),
                         new (ScenarioConstraints,  [ FeatureTypeType.ElectricalConnection ], "Constraints")
                     ]

                   : [
                         new (ScenarioControlLimit, [ ],                                     "Control active power consumption limit"),
                         new (ScenarioFailsafe,     [ ],                                     "Failsafe values"),

                         // The one thing the controllable system needs from the
                         // energy guard: something which sends heartbeats.
                         new (ScenarioHeartbeat,    [ FeatureTypeType.DeviceDiagnosis      ], "Heartbeat"),
                         new (ScenarioConstraints,  [ ],                                     "Constraints")
                     ];

        #endregion

    }

}
