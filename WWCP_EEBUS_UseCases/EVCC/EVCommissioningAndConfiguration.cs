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
using cloud.charging.open.protocols.EEBUS.UseCases.Commissioning;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.EVCC
{

    /// <summary>
    /// What "EV Commissioning and Configuration" is made of
    /// (EEBus_UC_TS_EVCommissioningAndConfiguration_V1.0.1).
    ///
    /// A car is plugged in and says what it is. Every other e-mobility use case
    /// leans on this one - the coordinated charging, the charging summary, the
    /// overload protection all reference it - because it is where "there is a
    /// car here" comes from in the first place.
    ///
    /// Two of its eight scenarios have no features at all. "EV connected"
    /// (scenario 1) and "EV disconnected" (scenario 8) are the EV entity
    /// appearing in and disappearing from the detailed discovery, and that is
    /// the whole of them. Both are mandatory on both sides, which is the
    /// specification saying: a car which never announces itself and an energy
    /// manager which does not notice are each a failure of this use case, with
    /// nothing to read either way.
    ///
    /// The three which decide what a manager may then do with the car are
    /// scenarios 2 and 3 - the communication standard and whether asymmetric
    /// charging is supported - and scenario 6, the charging power limits. Under
    /// IEC 61851 there is no identification and no energy demand to be had: the
    /// charging station controls the car with a PWM signal and nothing else, so
    /// a manager which learns "iec61851" has learned that half the family of use
    /// cases is unavailable here (section 2.3.2.1).
    /// </summary>
    public static class EVCommissioningAndConfiguration
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.EVCommissioningAndConfiguration;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 1);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        #endregion

        #region The scenarios (section 2.3)

        /// <summary>Scenario 1: the EV is connected. Mandatory, and has no data of its own.</summary>
        public const UInt32 ScenarioConnected            = 1;

        /// <summary>Scenario 2: the EV sends the communication standard it uses. Mandatory.</summary>
        public const UInt32 ScenarioCommunicationStandard = 2;

        /// <summary>Scenario 3: the EV says whether it supports asymmetric charging. Mandatory.</summary>
        public const UInt32 ScenarioAsymmetricCharging   = 3;

        /// <summary>Scenario 4: the EV sends an identification.</summary>
        public const UInt32 ScenarioIdentification       = 4;

        /// <summary>Scenario 5: the EV sends manufacturer information.</summary>
        public const UInt32 ScenarioManufacturerData     = 5;

        /// <summary>Scenario 6: the EV sends its charging power limits.</summary>
        public const UInt32 ScenarioChargingPowerLimits  = 6;

        /// <summary>Scenario 7: the EV says it is in sleep mode.</summary>
        public const UInt32 ScenarioSleepMode            = 7;

        /// <summary>Scenario 8: the EV is disconnected. Mandatory, and has no data of its own.</summary>
        public const UInt32 ScenarioDisconnected         = 8;

        #endregion

        #region The functions

        /// <summary>The function describing the configuration keys of the car.</summary>
        public const String KeyValueDescriptionListData  = "deviceConfigurationKeyValueDescriptionListData";

        /// <summary>The function carrying their values.</summary>
        public const String KeyValueListData             = "deviceConfigurationKeyValueListData";

        /// <summary>The function carrying how the car identifies itself.</summary>
        public const String IdentificationListData       = "identificationListData";

        /// <summary>The function carrying who made the car.</summary>
        public const String ManufacturerData             = CommissioningFunctions.ManufacturerData;

        /// <summary>The function saying which parameter the power limits belong to.</summary>
        public const String ParameterDescriptionListData = "electricalConnectionParameterDescriptionListData";

        /// <summary>The function carrying the charging power limits themselves.</summary>
        public const String PermittedValueSetListData    = "electricalConnectionPermittedValueSetListData";

        /// <summary>The function carrying whether the car is asleep.</summary>
        public const String DiagnosisStateData           = CommissioningFunctions.DiagnosisStateData;

        #endregion

        #region The configuration keys

        /// <summary>
        /// The key under which a car publishes the standard it speaks to the
        /// charging station (scenario 2).
        ///
        /// **The specification contradicts itself about this name.** Its two
        /// content tables say "communicationStandard" (Table 6 and Table 13),
        /// while the sequence diagram section which tells a client what selector
        /// to read with says "communicationsStandard" (section 3.4.2.2). The
        /// SPINE resource specification, the certified Go implementation and
        /// therefore the field all use the second. So the second is what we
        /// send - and what a client of ours accepts is both, see
        /// <see cref="CommunicationStandardKeys"/> and finding S9 in
        /// docs/spec-deviations.md.
        /// </summary>
        public static DeviceConfigurationKeyNameType CommunicationStandardKey { get; }
            = DeviceConfigurationKeyNameType.CommunicationsStandard;

        /// <summary>
        /// Every spelling of that key name a car in the field may use.
        /// </summary>
        public static IReadOnlyList<DeviceConfigurationKeyNameType> CommunicationStandardKeys { get; }
            = [ DeviceConfigurationKeyNameType.CommunicationsStandard,
                DeviceConfigurationKeyNameType.Parse("communicationStandard") ];

        /// <summary>
        /// The key under which a car says whether the phases may carry different
        /// currents (scenario 3).
        /// </summary>
        public static DeviceConfigurationKeyNameType AsymmetricChargingKey { get; }
            = DeviceConfigurationKeyNameType.AsymmetricChargingSupported;

        #endregion

        #region The communication standards (section 2.3.2)

        /// <summary>ISO 15118-2 edition 1 [EVCC-003].</summary>
        public const String ISO15118_2_ed1  = "iso15118-2ed1";

        /// <summary>ISO 15118-2 edition 2 [EVCC-004].</summary>
        public const String ISO15118_2_ed2  = "iso15118-2ed2";

        /// <summary>IEC 61851 - a PWM signal and nothing more [EVCC-005].</summary>
        public const String IEC61851        = "iec61851";

        /// <summary>
        /// The three standards this version of the use case knows about.
        /// </summary>
        public static IReadOnlyList<String> CommunicationStandards { get; }
            = [ ISO15118_2_ed1, ISO15118_2_ed2, IEC61851 ];

        /// <summary>
        /// Whether a car speaking this standard can be asked who it is and what
        /// it wants.
        ///
        /// Under IEC 61851 it cannot: the charging station has a PWM duty cycle
        /// and no channel to the car at all, so an identification and an energy
        /// demand have to come from a person at the charging station instead
        /// (section 2.3.2.1).
        /// </summary>
        /// <param name="Standard">A communication standard.</param>
        public static Boolean IsDigital(String? Standard)

            => Standard == ISO15118_2_ed1 ||
               Standard == ISO15118_2_ed2;

        #endregion

        #region The profile

        /// <summary>
        /// What tells this use case from the other commissioning use cases.
        ///
        /// The manufacturer data and the sleep mode are the two facts every
        /// commissioning use case carries; the rest is this one's own and lives
        /// in <see cref="EVCCElectricVehicle"/>.
        /// </summary>
        public static CommissioningProfile Profile { get; }

            = new (UseCaseName:           Name,
                   Version:               Version,
                   DocumentSubRevision:   DocumentSubRevision,
                   ServerActor:           UseCaseActors.EV,
                   ClientActor:           UseCaseActors.CEM,
                   ServerEntityTypes:     [ EntityTypeType.EV ],

                   Scenarios: [
                       new (ScenarioConnected,             [],                                          "EV connected")                          { Mandatory = true },
                       new (ScenarioCommunicationStandard, [ FeatureTypeType.DeviceConfiguration ],     "EV sends communication standard")       { Mandatory = true },
                       new (ScenarioAsymmetricCharging,    [ FeatureTypeType.DeviceConfiguration ],     "EV sends support of asymmetric charging") { Mandatory = true },
                       new (ScenarioIdentification,        [ FeatureTypeType.Identification ],          "EV sends identification"),
                       new (ScenarioManufacturerData,      [ FeatureTypeType.DeviceClassification ],    "EV sends manufacturer information"),
                       new (ScenarioChargingPowerLimits,   [ FeatureTypeType.ElectricalConnection ],    "EV sends charging power limits"),
                       new (ScenarioSleepMode,             [ FeatureTypeType.DeviceDiagnosis ],         "EV sleep mode"),
                       new (ScenarioDisconnected,          [],                                          "EV disconnected")                       { Mandatory = true }
                   ],

                   ManufacturerScenario:  ScenarioManufacturerData,
                   StateScenario:         ScenarioSleepMode,
                   ReportedState:         DeviceDiagnosisOperatingStateType.Standby);

        #endregion

    }

}
