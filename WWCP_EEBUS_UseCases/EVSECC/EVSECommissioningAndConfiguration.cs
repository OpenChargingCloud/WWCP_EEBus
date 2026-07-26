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

namespace cloud.charging.open.protocols.EEBUS.UseCases.EVSECC
{

    /// <summary>
    /// What "EVSE Commissioning and Configuration" is made of
    /// (EEBus_UC_TS_EVSECommissioningAndConfiguration_V1.0.1).
    ///
    /// The smallest use case in the family, and the one everything else in
    /// e-mobility stands on: a charging station is plugged into an energy
    /// manager and says who made it and whether it is working. Section 2.1 calls
    /// itself "the basis for other Use Cases related to the support of EV
    /// charging", and it means it - for most of those the charging station is
    /// the middle box which carries the car's data.
    ///
    /// The scenario numbering is worth reading twice, because it is the other
    /// way round from what one would guess: the manufacturer data is
    /// **recommended** and the error state is **mandatory**. A charging station
    /// which has no name is a nuisance; a charging station which cannot say that
    /// it has failed is a car which quietly stops following the charging plan
    /// with nobody the wiser (section 2.3.2.1).
    /// </summary>
    public static class EVSECommissioningAndConfiguration
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.EVSECommissioningAndConfiguration;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 1);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        #endregion

        #region The scenarios (section 2.3)

        /// <summary>Scenario 1: the EVSE sends manufacturer information. Recommended.</summary>
        public const UInt32 ScenarioManufacturerData  = 1;

        /// <summary>Scenario 2: the EVSE sends its error state. The mandatory one.</summary>
        public const UInt32 ScenarioErrorState        = 2;

        #endregion

        #region The functions

        /// <summary>The function carrying who made the charging station.</summary>
        public const String ManufacturerData   = CommissioningFunctions.ManufacturerData;

        /// <summary>The function carrying whether it is working.</summary>
        public const String DiagnosisStateData = CommissioningFunctions.DiagnosisStateData;

        #endregion

        #region The profile

        /// <summary>
        /// What tells this use case from the other commissioning use cases.
        /// </summary>
        public static CommissioningProfile Profile { get; }

            = new (UseCaseName:           Name,
                   Version:               Version,
                   DocumentSubRevision:   DocumentSubRevision,
                   ServerActor:           UseCaseActors.EVSE,
                   ClientActor:           UseCaseActors.CEM,
                   ServerEntityTypes:     [ EntityTypeType.EVSE ],

                   Scenarios: [
                       new (ScenarioManufacturerData,  [ FeatureTypeType.DeviceClassification ], "EVSE sends manufacturer information"),
                       new (ScenarioErrorState,        [ FeatureTypeType.DeviceDiagnosis ],      "EVSE sends error state") { Mandatory = true }
                   ],

                   ManufacturerScenario:  ScenarioManufacturerData,
                   StateScenario:         ScenarioErrorState,
                   ReportedState:         DeviceDiagnosisOperatingStateType.Failure);

        #endregion

    }

}
