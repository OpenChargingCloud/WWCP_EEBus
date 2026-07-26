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
using cloud.charging.open.protocols.EEBUS.UseCases.Monitoring;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.EVSOC
{

    /// <summary>
    /// What "EV State of Charge" is made of
    /// (EEBus_UC_TS_EVStateOfCharge_V1.0.0_RC1).
    ///
    /// A car says how full its battery is and a monitoring appliance shows it.
    /// The shape is the monitoring one again - descriptions, a join, a
    /// subscription - but it is the use case which shows where that shape ends:
    ///
    /// * **None of these measurements is on a wire.** A state of charge in per
    ///   cent, a state of health, a travel range in metres: no phase, no rms
    ///   variant, no voltage type. Table 6 lists no electrical connection
    ///   parameter description at all, which is why this profile says
    ///   <see cref="MonitoringProfile.ElectricalParameters"/> is false. Every
    ///   other monitoring use case we have publishes one per measurement, and
    ///   doing it here would put a claim on the wire the document does not make.
    /// * **Two of the three carry no commodity type**, for the same reason: the
    ///   state of health of a battery is not a measurement *of electricity*.
    ///   Only the state of charge names one (Table 7).
    /// * **Scenario 2 is not a measurement either.** The nominal capacity of the
    ///   battery is a fixed characteristic of the electrical connection, not
    ///   something which is measured over and over - so it is declared through
    ///   the "also supports" door, the same one the curtailment factor of the
    ///   grid connection point uses.
    ///
    /// A note on the actor: this document names the watching side
    /// **MonitoringAppliance** (section 3.2.2), while the Go reference
    /// implementation announces a CEM. Both are accepted, and ours can announce
    /// either - an appliance which insists on one would miss half the field. The
    /// same thing happened in OPEV, from the other direction.
    /// </summary>
    public static class EVStateOfCharge
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.EVStateOfCharge;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 0);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "RC1";

        #endregion

        #region The scenarios (section 2.3)

        /// <summary>Scenario 1: monitor the state of charge. The only mandatory one.</summary>
        public const UInt32 ScenarioStateOfCharge   = 1;

        /// <summary>Scenario 2: monitor the nominal capacity of the battery.</summary>
        public const UInt32 ScenarioNominalCapacity = 2;

        /// <summary>Scenario 3: monitor the state of health.</summary>
        public const UInt32 ScenarioStateOfHealth   = 3;

        /// <summary>Scenario 4: monitor the actual travel range.</summary>
        public const UInt32 ScenarioTravelRange     = 4;

        #endregion

        #region The functions

        /// <summary>The function carrying the fixed characteristics of the battery.</summary>
        public const String CharacteristicListData = MonitoringFunctions.CharacteristicListData;

        #endregion

        #region The quantities

        /// <summary>
        /// How much of the usable capacity has been charged, in per cent
        /// (scenario 1).
        /// </summary>
        public static MonitoringQuantity StateOfCharge { get; }
            = new (ScenarioStateOfCharge, MeasurementTypeType.Percentage, UnitOfMeasurementType.Pct, ScopeTypeType.StateOfCharge);

        /// <summary>
        /// How healthy the battery still is, in per cent (scenario 3). Not a
        /// measurement of electricity, and the document does not call it one.
        /// </summary>
        public static MonitoringQuantity StateOfHealth { get; }
            = new (ScenarioStateOfHealth, MeasurementTypeType.Percentage, UnitOfMeasurementType.Pct, ScopeTypeType.StateOfHealth)
              { Commodity = null };

        /// <summary>
        /// How far the car can still travel, in metres (scenario 4).
        /// </summary>
        public static MonitoringQuantity TravelRange { get; }
            = new (ScenarioTravelRange, MeasurementTypeType.Distance, UnitOfMeasurementType.M, ScopeTypeType.TravelRange)
              { Commodity = null };

        #endregion

        #region The profile

        /// <summary>
        /// What tells this use case from the other monitoring use cases.
        /// </summary>
        public static MonitoringProfile Profile { get; }

            = new (UseCaseName:          Name,
                   Version:              Version,
                   DocumentSubRevision:  DocumentSubRevision,
                   ServerActor:          UseCaseActors.EV,
                   ClientActor:          UseCaseActors.MonitoringAppliance,
                   ClientEntityTypes:    [ EntityTypeType.EV ],

                   Scenarios: [
                       new (ScenarioStateOfCharge,    [ FeatureTypeType.Measurement ],           "Monitor EV state of charge") { Mandatory = true },
                       new (ScenarioNominalCapacity,  [ FeatureTypeType.ElectricalConnection ], "Monitor EV nominal capacity"),
                       new (ScenarioStateOfHealth,    [ FeatureTypeType.Measurement ],           "Monitor EV state of health"),
                       new (ScenarioTravelRange,      [ FeatureTypeType.Measurement ],           "Monitor EV actual travel range")
                   ],

                   ScenarioOfScope: new Dictionary<ScopeTypeType, UInt32> {
                       [ScopeTypeType.StateOfCharge]  = ScenarioStateOfCharge,
                       [ScopeTypeType.StateOfHealth]  = ScenarioStateOfHealth,
                       [ScopeTypeType.TravelRange]    = ScenarioTravelRange
                   }) {

                       ElectricalParameters    = false,
                       AlsoKnownAsClientActor  = UseCaseActors.CEM

                   };

        #endregion

    }

}
