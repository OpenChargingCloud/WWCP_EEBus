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

namespace cloud.charging.open.protocols.EEBUS.UseCases.MGCP
{

    /// <summary>
    /// What "Monitoring of Grid Connection Point" is made of
    /// (EEBus_UC_TS_MonitoringOfGridConnectionPoint_V1.0.0).
    ///
    /// A monitoring appliance reads what crosses the boundary between a
    /// building and the grid: how much is flowing right now and in which
    /// direction, how much has been fed in and drawn over the life of the
    /// meter, and the current, voltage and frequency behind those numbers.
    ///
    /// It is the monitoring of power consumption pointed at a different place,
    /// with three differences worth naming:
    ///
    /// * the scopes are those of a **grid connection point** -
    ///   "gridFeedIn" and "gridConsumption" rather than "acEnergyProduced" and
    ///   "acEnergyConsumed" - because a grid connection point counts what
    ///   crosses it, not what a device does;
    /// * the momentary power is signed: positive means the building is drawing
    ///   from the grid, negative that it is feeding in. One measurement answers
    ///   both questions;
    /// * scenario 1 is not a measurement at all. It is a configuration value -
    ///   how far a photovoltaic system is allowed to feed in, as a factor
    ///   between zero and one - and it lives in the device configuration
    ///   feature. It is what a grid operator's curtailment order looks like when
    ///   it reaches the building.
    ///
    /// Scenarios 2, 3 and 4 are mandatory: a grid connection point which cannot
    /// say what is flowing and what has flowed is not one.
    /// </summary>
    public static class MonitoringOfGridConnectionPoint
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.MonitoringOfGridConnectionPoint;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 0);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        #endregion

        #region The scenarios (section 2.3)

        /// <summary>Scenario 1: monitor the PV curtailment limit factor.</summary>
        public const UInt32 ScenarioCurtailment      = 1;

        /// <summary>Scenario 2: monitor the momentary power at the grid connection point. Mandatory.</summary>
        public const UInt32 ScenarioPower            = 2;

        /// <summary>Scenario 3: monitor the total energy fed into the grid. Mandatory.</summary>
        public const UInt32 ScenarioEnergyFeedIn     = 3;

        /// <summary>Scenario 4: monitor the total energy drawn from the grid. Mandatory.</summary>
        public const UInt32 ScenarioEnergyConsumed   = 4;

        /// <summary>Scenario 5: monitor the current per phase.</summary>
        public const UInt32 ScenarioCurrent          = 5;

        /// <summary>Scenario 6: monitor the voltage per phase.</summary>
        public const UInt32 ScenarioVoltage          = 6;

        /// <summary>Scenario 7: monitor the grid frequency.</summary>
        public const UInt32 ScenarioFrequency        = 7;

        #endregion

        #region The functions

        /// <summary>The function carrying the measured values.</summary>
        public const String MeasurementListData             = MonitoringFunctions.MeasurementListData;

        /// <summary>The function describing what they are.</summary>
        public const String MeasurementDescriptionListData  = MonitoringFunctions.MeasurementDescriptionListData;

        /// <summary>The function saying which measurement is on which phase.</summary>
        public const String ParameterDescriptionListData    = MonitoringFunctions.ParameterDescriptionListData;

        /// <summary>The function carrying the curtailment limit factor (scenario 1).</summary>
        public const String KeyValueListData                = "deviceConfigurationKeyValueListData";

        /// <summary>The function describing it (scenario 1).</summary>
        public const String KeyValueDescriptionListData     = "deviceConfigurationKeyValueDescriptionListData";

        #endregion

        #region The curtailment limit factor (scenario 1)

        /// <summary>
        /// The configuration key which says how much of what a photovoltaic
        /// system could produce it is allowed to feed in, as a factor between
        /// zero (nothing) and one (everything).
        /// </summary>
        public static DeviceConfigurationKeyNameType CurtailmentLimitFactorKey { get; }
            = DeviceConfigurationKeyNameType.PvCurtailmentLimitFactor;

        #endregion

        #region The quantities

        /// <summary>
        /// The momentary power at the grid connection point, in watts, signed:
        /// positive while the building draws from the grid, negative while it
        /// feeds in (scenario 2).
        /// </summary>
        public static MonitoringQuantity Power { get; }
            = new (ScenarioPower, MeasurementTypeType.Power, UnitOfMeasurementType.W, ScopeTypeType.AcPowerTotal);

        /// <summary>The total energy fed into the grid, in watt hours (scenario 3).</summary>
        public static MonitoringQuantity EnergyFeedIn { get; }
            = new (ScenarioEnergyFeedIn, MeasurementTypeType.Energy, UnitOfMeasurementType.Wh, ScopeTypeType.GridFeedIn);

        /// <summary>The total energy drawn from the grid, in watt hours (scenario 4).</summary>
        public static MonitoringQuantity EnergyConsumed { get; }
            = new (ScenarioEnergyConsumed, MeasurementTypeType.Energy, UnitOfMeasurementType.Wh, ScopeTypeType.GridConsumption);

        /// <summary>The current on one phase, in ampere (scenario 5).</summary>
        public static MonitoringQuantity Current(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioCurrent, MeasurementTypeType.Current, UnitOfMeasurementType.A, ScopeTypeType.AcCurrent, Phase);

        /// <summary>The voltage of one phase, in volts (scenario 6).</summary>
        public static MonitoringQuantity Voltage(ElectricalConnectionPhaseNameType Phase)
            => new (ScenarioVoltage, MeasurementTypeType.Voltage, UnitOfMeasurementType.V, ScopeTypeType.AcVoltage, Phase);

        /// <summary>The frequency of the grid, in hertz (scenario 7).</summary>
        public static MonitoringQuantity Frequency { get; }
            = new (ScenarioFrequency, MeasurementTypeType.Frequency, UnitOfMeasurementType.Hz, ScopeTypeType.AcFrequency);

        #endregion

        #region The profile

        /// <summary>
        /// What tells this use case from the other monitoring use cases.
        ///
        /// Scenario 1 is the odd one: it needs the device configuration feature
        /// rather than the measurement one, because a curtailment factor is not
        /// something the grid connection point measures - it is something it was
        /// told.
        /// </summary>
        public static MonitoringProfile Profile { get; }

            = new (UseCaseName:          Name,
                   Version:              Version,
                   DocumentSubRevision:  DocumentSubRevision,
                   ServerActor:          UseCaseActors.GridConnectionPoint,
                   ClientActor:          UseCaseActors.MonitoringAppliance,

                   // The document names who may watch a grid connection point,
                   // which the monitoring of power consumption leaves open.
                   ClientEntityTypes:    [ EntityTypeType.CEM,
                                           EntityTypeType.GridConnectionPointOfPremises ],

                   Scenarios: [
                       new (ScenarioCurtailment,     [ FeatureTypeType.DeviceConfiguration ], "Monitor the PV curtailment limit factor"),
                       new (ScenarioPower,           Measured, "Monitor the momentary power")        { Mandatory = true },
                       new (ScenarioEnergyFeedIn,    Measured, "Monitor the total feed-in energy")   { Mandatory = true },
                       new (ScenarioEnergyConsumed,  Measured, "Monitor the total consumed energy")  { Mandatory = true },
                       new (ScenarioCurrent,         Measured, "Monitor the current"),
                       new (ScenarioVoltage,         Measured, "Monitor the voltage"),
                       new (ScenarioFrequency,       Measured, "Monitor the frequency")
                   ],

                   ScenarioOfScope: new Dictionary<ScopeTypeType, UInt32> {
                       [ScopeTypeType.AcPowerTotal]     = ScenarioPower,
                       [ScopeTypeType.AcPower]          = ScenarioPower,
                       [ScopeTypeType.GridFeedIn]       = ScenarioEnergyFeedIn,
                       [ScopeTypeType.GridConsumption]  = ScenarioEnergyConsumed,
                       [ScopeTypeType.AcCurrent]        = ScenarioCurrent,
                       [ScopeTypeType.AcVoltage]        = ScenarioVoltage,
                       [ScopeTypeType.AcFrequency]      = ScenarioFrequency
                   });


        /// <summary>
        /// What a monitoring appliance needs at the grid connection point in
        /// order to read a measurement and know which phase it came from.
        /// </summary>
        private static FeatureTypeType[] Measured
            => [ FeatureTypeType.Measurement, FeatureTypeType.ElectricalConnection ];

        #endregion

    }

}
