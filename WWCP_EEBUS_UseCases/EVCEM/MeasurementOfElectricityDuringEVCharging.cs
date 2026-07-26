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

namespace cloud.charging.open.protocols.EEBUS.UseCases.EVCEM
{

    /// <summary>
    /// What "Measurement of Electricity during EV Charging" is made of
    /// (EEBus_UC_TS_EVChargingElectricityMeasurement_V1.0.1).
    ///
    /// A car being charged says how much electricity is going into it, and an
    /// energy manager watches. Structurally it is the monitoring of power
    /// consumption pointed at the charging cable - the same descriptions, the
    /// same join by measurement identifier, the same subscription - so it is
    /// another <see cref="MonitoringProfile"/> rather than another copy.
    ///
    /// Two things about it are its own:
    ///
    /// * **No scenario is mandatory, but silence is not an option.** Section 2.3
    ///   asks for at least one of the three, "as all 3 scenarios measure
    ///   electricity and can be converted into each other". Current is the one to
    ///   support, because it is the one the other two are derived from and the
    ///   only one which cannot be derived; if the car charges asymmetrically,
    ///   energy alone is not enough, because a total in watt hours cannot be
    ///   taken apart into phases again.
    /// * **The energy is charged energy, not consumed energy.** Its scope is
    ///   "charge" rather than the "acEnergyConsumed" of a meter: this counts what
    ///   went into this car during this session, not what a connection has passed
    ///   since it was installed.
    /// </summary>
    public static class MeasurementOfElectricityDuringEVCharging
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.MeasurementOfElectricityDuringEVCharging;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 1);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        #endregion

        #region The scenarios (section 2.3)

        /// <summary>Scenario 1: measure EV charging current, per phase.</summary>
        public const UInt32 ScenarioCurrent  = 1;

        /// <summary>Scenario 2: measure EV charging power.</summary>
        public const UInt32 ScenarioPower    = 2;

        /// <summary>Scenario 3: measure EV charged energy.</summary>
        public const UInt32 ScenarioEnergy   = 3;

        #endregion

        #region The quantities

        /// <summary>The charging current on one phase, in ampere (scenario 1).</summary>
        public static MonitoringQuantity Current(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioCurrent, MeasurementTypeType.Current, UnitOfMeasurementType.A, ScopeTypeType.AcCurrent, Phase);

        /// <summary>The total charging power, in watts (scenario 2).</summary>
        public static MonitoringQuantity PowerTotal { get; }
            = new (ScenarioPower, MeasurementTypeType.Power, UnitOfMeasurementType.W, ScopeTypeType.AcPower);

        /// <summary>The charging power on one phase, in watts (scenario 2).</summary>
        public static MonitoringQuantity Power(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioPower, MeasurementTypeType.Power, UnitOfMeasurementType.W, ScopeTypeType.AcPower, Phase);

        /// <summary>The energy charged into the car, in watt hours (scenario 3).</summary>
        public static MonitoringQuantity EnergyCharged { get; }
            = new (ScenarioEnergy, MeasurementTypeType.Energy, UnitOfMeasurementType.Wh, ScopeTypeType.Charge);

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
                   ClientActor:          UseCaseActors.CEM,
                   ClientEntityTypes:    [ EntityTypeType.EV ],

                   // None of the three is mandatory on its own; see
                   // AtLeastOneScenario below.
                   Scenarios: [
                       new (ScenarioCurrent,  Measured, "Measure EV charging current"),
                       new (ScenarioPower,    Measured, "Measure EV charging power"),
                       new (ScenarioEnergy,   Measured, "Measure EV charged energy")
                   ],

                   ScenarioOfScope: new Dictionary<ScopeTypeType, UInt32> {
                       [ScopeTypeType.AcCurrent]  = ScenarioCurrent,
                       [ScopeTypeType.AcPower]    = ScenarioPower,
                       [ScopeTypeType.Charge]     = ScenarioEnergy
                   }) {

                       AtLeastOneScenario = true

                   };


        /// <summary>
        /// What the energy manager needs at the car in order to read a
        /// measurement and know which phase it came from.
        /// </summary>
        private static FeatureTypeType[] Measured
            => [ FeatureTypeType.Measurement, FeatureTypeType.ElectricalConnection ];

        #endregion

    }

}
