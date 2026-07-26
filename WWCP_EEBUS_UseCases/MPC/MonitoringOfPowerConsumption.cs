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

namespace cloud.charging.open.protocols.EEBUS.UseCases.MPC
{

    /// <summary>
    /// What "Monitoring of Power Consumption" is made of
    /// (EEBus_UC_TS_MonitoringOfPowerConsumption_V1.0.0).
    ///
    /// A monitoring appliance reads what a monitored unit consumes: power,
    /// energy, current, voltage, frequency. It is the plainest of the use cases
    /// - nothing is written, nothing has a state, nothing falls back - and it is
    /// the one which everything else leans on: the limitation of power
    /// consumption tells an energy guard to use it in order to see whether its
    /// limits are being kept (LPC 1.0.0, section 2.2).
    ///
    /// Only scenario 1 is mandatory. A device which measures nothing but its
    /// total active power implements this use case completely.
    ///
    /// Everything about it other than which quantities they are and what they
    /// are called is shared with the monitoring of a grid connection point - see
    /// <see cref="MonitoringProfile"/>.
    /// </summary>
    public static class MonitoringOfPowerConsumption
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.MonitoringOfPowerConsumption;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 0);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        #endregion

        #region The scenarios (section 2.3)

        /// <summary>Scenario 1: monitor power. The only mandatory one.</summary>
        public const UInt32 ScenarioPower      = 1;

        /// <summary>Scenario 2: monitor energy.</summary>
        public const UInt32 ScenarioEnergy     = 2;

        /// <summary>Scenario 3: monitor current.</summary>
        public const UInt32 ScenarioCurrent    = 3;

        /// <summary>Scenario 4: monitor voltage.</summary>
        public const UInt32 ScenarioVoltage    = 4;

        /// <summary>Scenario 5: monitor frequency.</summary>
        public const UInt32 ScenarioFrequency  = 5;

        #endregion

        #region The functions

        /// <summary>The function carrying the measured values.</summary>
        public const String MeasurementListData             = MonitoringFunctions.MeasurementListData;

        /// <summary>The function describing what they are.</summary>
        public const String MeasurementDescriptionListData  = MonitoringFunctions.MeasurementDescriptionListData;

        /// <summary>The function saying which measurement is on which phase.</summary>
        public const String ParameterDescriptionListData    = MonitoringFunctions.ParameterDescriptionListData;

        /// <summary>The function describing the electrical connection itself.</summary>
        public const String ElectricalDescriptionListData   = MonitoringFunctions.ElectricalDescriptionListData;

        #endregion

        #region The quantities

        /// <summary>The total active power consumed, in watts (scenario 1).</summary>
        public static MonitoringQuantity PowerTotal { get; }
            = new (ScenarioPower, MeasurementTypeType.Power, UnitOfMeasurementType.W, ScopeTypeType.AcPowerTotal);

        /// <summary>The active power on one phase, in watts (scenario 1).</summary>
        public static MonitoringQuantity Power(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioPower, MeasurementTypeType.Power, UnitOfMeasurementType.W, ScopeTypeType.AcPower, Phase);

        /// <summary>The energy consumed since the meter was installed, in watt hours (scenario 2).</summary>
        public static MonitoringQuantity EnergyConsumed { get; }
            = new (ScenarioEnergy, MeasurementTypeType.Energy, UnitOfMeasurementType.Wh, ScopeTypeType.AcEnergyConsumed);

        /// <summary>The energy fed back, in watt hours (scenario 2).</summary>
        public static MonitoringQuantity EnergyProduced { get; }
            = new (ScenarioEnergy, MeasurementTypeType.Energy, UnitOfMeasurementType.Wh, ScopeTypeType.AcEnergyProduced);

        /// <summary>The current on one phase, in ampere (scenario 3).</summary>
        public static MonitoringQuantity Current(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioCurrent, MeasurementTypeType.Current, UnitOfMeasurementType.A, ScopeTypeType.AcCurrent, Phase);

        /// <summary>The voltage of one phase, in volts (scenario 4).</summary>
        public static MonitoringQuantity Voltage(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioVoltage, MeasurementTypeType.Voltage, UnitOfMeasurementType.V, ScopeTypeType.AcVoltage, Phase);

        /// <summary>The frequency of the grid, in hertz (scenario 5).</summary>
        public static MonitoringQuantity Frequency { get; }
            = new (ScenarioFrequency, MeasurementTypeType.Frequency, UnitOfMeasurementType.Hz, ScopeTypeType.AcFrequency);

        #endregion

        #region The profile

        /// <summary>
        /// What tells this use case from the other monitoring use cases.
        ///
        /// Every scenario needs the same two server features - the measurements,
        /// and the electrical connection which says which phase they are on.
        /// </summary>
        public static MonitoringProfile Profile { get; }

            = new (UseCaseName:          Name,
                   Version:              Version,
                   DocumentSubRevision:  DocumentSubRevision,
                   ServerActor:          UseCaseActors.MonitoredUnit,
                   ClientActor:          UseCaseActors.MonitoringAppliance,
                   ClientEntityTypes:    null,

                   Scenarios: [
                       new (ScenarioPower,      Measured, "Monitor power")     { Mandatory = true },
                       new (ScenarioEnergy,     Measured, "Monitor energy"),
                       new (ScenarioCurrent,    Measured, "Monitor current"),
                       new (ScenarioVoltage,    Measured, "Monitor voltage"),
                       new (ScenarioFrequency,  Measured, "Monitor frequency")
                   ],

                   ScenarioOfScope: new Dictionary<ScopeTypeType, UInt32> {
                       [ScopeTypeType.AcPowerTotal]      = ScenarioPower,
                       [ScopeTypeType.AcPower]           = ScenarioPower,
                       [ScopeTypeType.AcEnergyConsumed]  = ScenarioEnergy,
                       [ScopeTypeType.AcEnergyProduced]  = ScenarioEnergy,
                       [ScopeTypeType.AcCurrent]         = ScenarioCurrent,
                       [ScopeTypeType.AcVoltage]         = ScenarioVoltage,
                       [ScopeTypeType.AcFrequency]       = ScenarioFrequency
                   });


        /// <summary>
        /// What a monitoring appliance needs at the monitored unit in order to
        /// read a measurement and know which phase it came from.
        /// </summary>
        private static FeatureTypeType[] Measured
            => [ FeatureTypeType.Measurement, FeatureTypeType.ElectricalConnection ];

        #endregion

    }

}
