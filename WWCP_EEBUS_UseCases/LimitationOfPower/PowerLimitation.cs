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

namespace cloud.charging.open.protocols.EEBUS.UseCases.LimitationOfPower
{

    /// <summary>
    /// What tells "Limitation of Power Consumption" from "Limitation of Power
    /// Production".
    ///
    /// The two specifications are the same document with three words changed.
    /// Their scenarios are the same four, their heartbeat is the same 120
    /// seconds, their state machines are the same five states with the same
    /// twelve transitions - the rule numbers even match, [LPC-919] and
    /// [LPP-919] being the same sentence. What differs is the direction of the
    /// energy, the name of the failsafe configuration key, and which nominal
    /// maximum is reported.
    ///
    /// Writing that twice would mean writing a normative state machine twice,
    /// which is how two implementations of one rule drift apart. So it is
    /// written once and this record says which of the two is meant.
    /// </summary>
    /// <param name="UseCaseName">The name of the use case.</param>
    /// <param name="Version">The version this implementation follows.</param>
    /// <param name="DocumentSubRevision">The sub revision of the use case document.</param>
    /// <param name="RulePrefix">How the rules of this specification are numbered, i.e. "LPC" or "LPP".</param>
    /// <param name="Direction">Whether the limit is about consuming or producing.</param>
    /// <param name="FailsafeLimitKey">The configuration key holding the failsafe limit.</param>
    /// <param name="NominalMax">Which nominal maximum an ordinary device reports.</param>
    /// <param name="ContractualNominalMax">Which one an energy manager reports instead.</param>
    public sealed record PowerLimitationProfile(String                                      UseCaseName,
                                                UseCaseVersion                              Version,
                                                String                                      DocumentSubRevision,
                                                String                                      RulePrefix,
                                                EnergyDirectionType                         Direction,
                                                DeviceConfigurationKeyNameType              FailsafeLimitKey,
                                                ElectricalConnectionCharacteristicTypeType  NominalMax,
                                                ElectricalConnectionCharacteristicTypeType  ContractualNominalMax)
    {

        /// <summary>
        /// The description which makes a load control limit **the** limit of
        /// this use case, rather than one of the other limits a device may have.
        ///
        /// All four fields have to match: a limit which is a recommendation
        /// rather than an obligation, or which is about the other direction, is
        /// a different limit on the same feature.
        /// </summary>
        /// <param name="LimitId">The identifier to give it.</param>
        /// <param name="MeasurementId">Which measurement it limits, where the device says so.</param>
        public LoadControlLimitDescriptionDataType LimitDescription(UInt32   LimitId,
                                                                    UInt32?  MeasurementId   = null)

            => new () {
                   LimitId         = LimitId,
                   LimitType       = LoadControlLimitTypeType.SignDependentAbsValueLimit,
                   LimitCategory   = LoadControlCategoryType.Obligation,
                   LimitDirection  = Direction,
                   MeasurementId   = MeasurementId,
                   Unit            = UnitOfMeasurementType.W,
                   ScopeType       = ScopeTypeType.ActivePowerLimit
               };


        /// <summary>
        /// Whether the given limit description is the one of this use case.
        /// </summary>
        /// <param name="Description">A load control limit description.</param>
        public Boolean IsTheLimit(LoadControlLimitDescriptionDataType? Description)

            => Description is not null                                                           &&
               Description.LimitType      == LoadControlLimitTypeType.SignDependentAbsValueLimit &&
               Description.LimitCategory  == LoadControlCategoryType.Obligation                  &&
               Description.LimitDirection == Direction                                           &&
               Description.ScopeType      == ScopeTypeType.ActivePowerLimit;


        /// <summary>
        /// A rule of this specification, i.e. "[LPC-919]".
        /// </summary>
        /// <param name="Number">The number of the rule.</param>
        public String Rule(String Number)

            => $"[{RulePrefix}-{Number}]";


        /// <summary>Return a text representation of this profile.</summary>
        public override String ToString()

            => $"{UseCaseName} {Version}";

    }


    /// <summary>
    /// What the limitation of power - consumption or production - is made of.
    ///
    /// The values here are the ones both specifications share, and in Germany
    /// they are law rather than convention: 120 seconds without a heartbeat and
    /// the controllable system holds itself to its failsafe value, whatever the
    /// energy guard meant to say.
    /// </summary>
    public static class PowerLimitation
    {

        #region The two use cases

        /// <summary>
        /// Limitation of Power Consumption
        /// (EEBus_UC_TS_LimitationOfPowerConsumption_V1.0.0).
        /// </summary>
        public static PowerLimitationProfile Consumption { get; }
            = new (UseCaseNames.LimitationOfPowerConsumption,
                   new UseCaseVersion(1, 0, 0),
                   "release",
                   "LPC",
                   EnergyDirectionType.Consume,
                   DeviceConfigurationKeyNameType.FailsafeConsumptionActivePowerLimit,
                   ElectricalConnectionCharacteristicTypeType.PowerConsumptionNominalMax,
                   ElectricalConnectionCharacteristicTypeType.ContractualConsumptionNominalMax);

        /// <summary>
        /// Limitation of Power Production
        /// (EEBus_UC_TS_LimitationOfPowerProduction_V1.0.0).
        /// </summary>
        public static PowerLimitationProfile Production { get; }
            = new (UseCaseNames.LimitationOfPowerProduction,
                   new UseCaseVersion(1, 0, 0),
                   "release",
                   "LPP",
                   EnergyDirectionType.Produce,
                   DeviceConfigurationKeyNameType.FailsafeProductionActivePowerLimit,
                   ElectricalConnectionCharacteristicTypeType.PowerProductionNominalMax,
                   ElectricalConnectionCharacteristicTypeType.ContractualProductionNominalMax);

        #endregion

        #region The scenarios (section 2.6)

        /// <summary>Scenario 1: control the active power limit.</summary>
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
        /// controllable system goes into its failsafe state (rule 911, 912).
        /// </summary>
        public static readonly TimeSpan  HeartbeatTimeout                   = TimeSpan.FromSeconds(120);

        /// <summary>
        /// How often the energy guard sends its heartbeat. The specification
        /// fixes the timeout rather than the interval, and names 60 seconds as
        /// the time within which a heartbeat and a limit have to follow the
        /// restoration of communication (rule 913).
        /// </summary>
        public static readonly TimeSpan  HeartbeatInterval                  = TimeSpan.FromSeconds(60);

        /// <summary>
        /// In the states "init", "failsafe" and "unlimited/autonomous", a write
        /// of the limit is only evaluated when it follows a heartbeat within
        /// this time (section 2.2).
        /// </summary>
        public static readonly TimeSpan  LimitAfterHeartbeat                = TimeSpan.FromSeconds(60);

        /// <summary>
        /// How long the controllable system waits for a heartbeat and a limit
        /// before it decides that nobody is controlling it (rules 906, 921).
        /// </summary>
        public static readonly TimeSpan  WaitForControl                     = TimeSpan.FromSeconds(120);

        /// <summary>
        /// The lowest failsafe duration minimum a controllable system has to
        /// accept (rule 022).
        /// </summary>
        public static readonly TimeSpan  FailsafeDurationMinimumLowerBound  = TimeSpan.FromHours(2);

        /// <summary>
        /// The highest one (rule 022).
        /// </summary>
        public static readonly TimeSpan  FailsafeDurationMinimumUpperBound  = TimeSpan.FromHours(24);

        #endregion

        #region The functions

        /// <summary>The function carrying the limit.</summary>
        public const String LimitListData               = "loadControlLimitListData";

        /// <summary>The function describing it.</summary>
        public const String LimitDescriptionListData    = "loadControlLimitDescriptionListData";

        /// <summary>The function carrying the failsafe values.</summary>
        public const String KeyValueListData            = "deviceConfigurationKeyValueListData";

        /// <summary>The function describing them.</summary>
        public const String KeyValueDescriptionListData = "deviceConfigurationKeyValueDescriptionListData";

        /// <summary>The function carrying the nominal maximum values.</summary>
        public const String CharacteristicListData      = "electricalConnectionCharacteristicListData";

        /// <summary>The function carrying the heartbeat.</summary>
        public const String HeartbeatData               = "deviceDiagnosisHeartbeatData";

        #endregion

        #region The configuration keys

        /// <summary>
        /// The configuration key holding the failsafe duration minimum, which is
        /// the same for both use cases (rule 022).
        /// </summary>
        public static readonly DeviceConfigurationKeyNameType FailsafeDurationKey
            = DeviceConfigurationKeyNameType.FailsafeDurationMinimum;

        #endregion

        #region The scenarios as the framework needs them

        /// <summary>
        /// The four scenarios, with the server features a partner needs for each
        /// of them.
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
                         new (ScenarioControlLimit, [ FeatureTypeType.LoadControl          ], "Control active power limit"),
                         new (ScenarioFailsafe,     [ FeatureTypeType.DeviceConfiguration  ], "Failsafe values"),
                         new (ScenarioHeartbeat,    [ FeatureTypeType.DeviceDiagnosis      ], "Heartbeat"),
                         new (ScenarioConstraints,  [ FeatureTypeType.ElectricalConnection ], "Constraints")
                     ]

                   : [
                         new (ScenarioControlLimit, [ ],                                     "Control active power limit"),
                         new (ScenarioFailsafe,     [ ],                                     "Failsafe values"),

                         // The one thing the controllable system needs from the
                         // energy guard: something which sends heartbeats.
                         new (ScenarioHeartbeat,    [ FeatureTypeType.DeviceDiagnosis      ], "Heartbeat"),
                         new (ScenarioConstraints,  [ ],                                     "Constraints")
                     ];

        #endregion

    }

}
