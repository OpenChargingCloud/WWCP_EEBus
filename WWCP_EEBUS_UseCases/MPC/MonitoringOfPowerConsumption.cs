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

namespace cloud.charging.open.protocols.EEBUS.UseCases.MPC
{

    /// <summary>
    /// One thing a monitored unit measures.
    ///
    /// Every measurement of this use case is the same four facts - what kind of
    /// quantity, in which unit, at which scope, and on which phase - and the
    /// scope is what tells "the total active power" from "the active power of
    /// phase B". The phase is not in the measurement description at all: it
    /// comes from the electrical connection parameter description, joined by the
    /// measurement identifier.
    /// </summary>
    /// <param name="Scenario">Which scenario of the use case it belongs to.</param>
    /// <param name="Type">Which kind of quantity it is.</param>
    /// <param name="Unit">In which unit it is measured.</param>
    /// <param name="Scope">What exactly it is a measurement of.</param>
    /// <param name="Phase">Which phase it is on, where it is on one.</param>
    public sealed record MPCQuantity(UInt32                              Scenario,
                                     MeasurementTypeType                 Type,
                                     UnitOfMeasurementType               Unit,
                                     ScopeTypeType                       Scope,
                                     ElectricalConnectionPhaseNameType?  Phase   = null)
    {

        /// <summary>Return a text representation of this quantity.</summary>
        public override String ToString()

            => $"{Scope}{(Phase is not null ? $" ({Phase})" : "")} in {Unit}";

    }


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
        public const String MeasurementListData             = "measurementListData";

        /// <summary>The function describing what they are.</summary>
        public const String MeasurementDescriptionListData  = "measurementDescriptionListData";

        /// <summary>The function saying which measurement is on which phase.</summary>
        public const String ParameterDescriptionListData    = "electricalConnectionParameterDescriptionListData";

        /// <summary>The function describing the electrical connection itself.</summary>
        public const String ElectricalDescriptionListData   = "electricalConnectionDescriptionListData";

        #endregion

        #region The quantities

        /// <summary>The total active power consumed, in watts (scenario 1).</summary>
        public static MPCQuantity PowerTotal { get; }
            = new (ScenarioPower, MeasurementTypeType.Power, UnitOfMeasurementType.W, ScopeTypeType.AcPowerTotal);

        /// <summary>The active power on one phase, in watts (scenario 1).</summary>
        public static MPCQuantity Power(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioPower, MeasurementTypeType.Power, UnitOfMeasurementType.W, ScopeTypeType.AcPower, Phase);

        /// <summary>The energy consumed since the meter was installed, in watt hours (scenario 2).</summary>
        public static MPCQuantity EnergyConsumed { get; }
            = new (ScenarioEnergy, MeasurementTypeType.Energy, UnitOfMeasurementType.Wh, ScopeTypeType.AcEnergyConsumed);

        /// <summary>The energy fed back, in watt hours (scenario 2).</summary>
        public static MPCQuantity EnergyProduced { get; }
            = new (ScenarioEnergy, MeasurementTypeType.Energy, UnitOfMeasurementType.Wh, ScopeTypeType.AcEnergyProduced);

        /// <summary>The current on one phase, in ampere (scenario 3).</summary>
        public static MPCQuantity Current(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioCurrent, MeasurementTypeType.Current, UnitOfMeasurementType.A, ScopeTypeType.AcCurrent, Phase);

        /// <summary>The voltage of one phase, in volts (scenario 4).</summary>
        public static MPCQuantity Voltage(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioVoltage, MeasurementTypeType.Voltage, UnitOfMeasurementType.V, ScopeTypeType.AcVoltage, Phase);

        /// <summary>The frequency of the grid, in hertz (scenario 5).</summary>
        public static MPCQuantity Frequency { get; }
            = new (ScenarioFrequency, MeasurementTypeType.Frequency, UnitOfMeasurementType.Hz, ScopeTypeType.AcFrequency);

        #endregion

        #region The scenarios as the framework needs them

        /// <summary>
        /// The scenarios of this use case which the given side supports.
        ///
        /// Both actors need the same two server features for every scenario -
        /// the measurements and the electrical connection which says which phase
        /// they are on - so the two lists differ only in direction: the
        /// monitoring appliance looks for them at the monitored unit, and the
        /// monitored unit needs nothing at all from the appliance.
        /// </summary>
        /// <param name="ForMonitoringAppliance">Whether the list is for the monitoring appliance.</param>
        /// <param name="Scenarios">Which scenarios are supported. Scenario 1 is mandatory and is always included.</param>
        public static IEnumerable<UseCaseScenario> Scenarios(Boolean               ForMonitoringAppliance,
                                                             IEnumerable<UInt32>?  Scenarios   = null)
        {

            var supported = new SortedSet<UInt32>(Scenarios ?? []) { ScenarioPower };

            var needed    = ForMonitoringAppliance
                                ? new[] { FeatureTypeType.Measurement, FeatureTypeType.ElectricalConnection }
                                : [];

            var names     = new Dictionary<UInt32, String> {
                                [ScenarioPower]      = "Monitor power",
                                [ScenarioEnergy]     = "Monitor energy",
                                [ScenarioCurrent]    = "Monitor current",
                                [ScenarioVoltage]    = "Monitor voltage",
                                [ScenarioFrequency]  = "Monitor frequency"
                            };

            return [.. supported.Select(scenario => new UseCaseScenario(scenario,
                                                                        needed,
                                                                        names.GetValueOrDefault(scenario)))];

        }

        #endregion

    }

}
