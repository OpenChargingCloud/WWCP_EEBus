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

namespace cloud.charging.open.protocols.EEBUS.UseCases
{

    /// <summary>
    /// The names of the EEBUS use cases.
    ///
    /// These are not in SPINE, and not by accident: the XSD declares
    /// "UseCaseNameEnumType" as an empty restriction of a string, unioned with
    /// the extension type, which is the specification saying "the names come
    /// from the use case documents, not from me". Every use case specification
    /// then states its own name in its section 3.1.2 as a SHALL, together with
    /// the rule that the string "SHALL only be defined by this Use Case
    /// (regardless of the Use Case version)".
    ///
    /// The spelling matters and is not derivable: "coordinatedEvCharging" has a
    /// small "v", "monitoringOfPvString" a small "v" and a capital "S". They are
    /// taken from the Go reference implementation, which is proven in
    /// certification, and cross-checked against Annex A of the general
    /// implementation guideline.
    /// </summary>
    public static class UseCaseNames
    {

        #region Grid

        /// <summary>Limitation of Power Consumption (LPC).</summary>
        public const String LimitationOfPowerConsumption                          = "limitationOfPowerConsumption";

        /// <summary>Limitation of Power Production (LPP).</summary>
        public const String LimitationOfPowerProduction                           = "limitationOfPowerProduction";

        /// <summary>Monitoring of Grid Connection Point (MGCP).</summary>
        public const String MonitoringOfGridConnectionPoint                       = "monitoringOfGridConnectionPoint";

        /// <summary>Monitoring of Power Consumption (MPC).</summary>
        public const String MonitoringOfPowerConsumption                          = "monitoringOfPowerConsumption";

        /// <summary>Incentive-Table based power consumption management (ITPCM).</summary>
        public const String IncentiveTableBasedPowerConsumptionManagement         = "incentiveTableBasedPowerConsumptionManagement";

        #endregion

        #region E-Mobility

        /// <summary>Coordinated EV Charging (CEVC).</summary>
        public const String CoordinatedEVCharging                                 = "coordinatedEvCharging";

        /// <summary>EV Charging Summary (EVCS).</summary>
        public const String EVChargingSummary                                     = "evChargingSummary";

        /// <summary>EV Commissioning and Configuration (EVCC).</summary>
        public const String EVCommissioningAndConfiguration                       = "evCommissioningAndConfiguration";

        /// <summary>EVSE Commissioning and Configuration (EVSECC).</summary>
        public const String EVSECommissioningAndConfiguration                     = "evseCommissioningAndConfiguration";

        /// <summary>EV State of Charge (EVSOC).</summary>
        public const String EVStateOfCharge                                       = "evStateOfCharge";

        /// <summary>Measurement of Electricity during EV Charging (EVCEM).</summary>
        public const String MeasurementOfElectricityDuringEVCharging              = "measurementOfElectricityDuringEvCharging";

        /// <summary>Optimization of Self-Consumption During EV Charging (OSCEV).</summary>
        public const String OptimizationOfSelfConsumptionDuringEVCharging         = "optimizationOfSelfConsumptionDuringEvCharging";

        /// <summary>Overload Protection by EV Charging Current Curtailment (OPEV).</summary>
        public const String OverloadProtectionByEVChargingCurrentCurtailment      = "overloadProtectionByEvChargingCurrentCurtailment";

        #endregion

        #region Inverter and battery

        /// <summary>Control of Battery (COB).</summary>
        public const String ControlOfBattery                                      = "controlOfBattery";

        /// <summary>Monitoring of Battery (MOB).</summary>
        public const String MonitoringOfBattery                                   = "monitoringOfBattery";

        /// <summary>Monitoring of Inverter (MOI).</summary>
        public const String MonitoringOfInverter                                  = "monitoringOfInverter";

        /// <summary>Monitoring of PV String (MPS).</summary>
        public const String MonitoringOfPVString                                  = "monitoringOfPvString";

        /// <summary>Visualization of Aggregated Battery Data (VABD).</summary>
        public const String VisualizationOfAggregatedBatteryData                  = "visualizationOfAggregatedBatteryData";

        /// <summary>Visualization of Aggregated Photovoltaic Data (VAPD).</summary>
        public const String VisualizationOfAggregatedPhotovoltaicData             = "visualizationOfAggregatedPhotovoltaicData";

        #endregion

        #region HVAC and white goods

        /// <summary>Configuration of DHW System Function (CDSF).</summary>
        public const String ConfigurationOfDHWSystemFunction                      = "configurationOfDhwSystemFunction";

        /// <summary>Configuration of DHW Temperature (CDT).</summary>
        public const String ConfigurationOfDHWTemperature                         = "configurationOfDhwTemperature";

        /// <summary>Configuration of Room Cooling System Function (CRCSF).</summary>
        public const String ConfigurationOfRoomCoolingSystemFunction              = "configurationOfRoomCoolingSystemFunction";

        /// <summary>Configuration of Room Cooling Temperature (CRCT).</summary>
        public const String ConfigurationOfRoomCoolingTemperature                 = "configurationOfRoomCoolingTemperature";

        /// <summary>Configuration of Room Heating System Function (CRHSF).</summary>
        public const String ConfigurationOfRoomHeatingSystemFunction              = "configurationOfRoomHeatingSystemFunction";

        /// <summary>Configuration of Room Heating Temperature (CRHT).</summary>
        public const String ConfigurationOfRoomHeatingTemperature                 = "configurationOfRoomHeatingTemperature";

        /// <summary>Monitoring of DHW System Function (MDSF).</summary>
        public const String MonitoringOfDHWSystemFunction                         = "monitoringOfDhwSystemFunction";

        /// <summary>Monitoring of DHW Temperature (MDT).</summary>
        public const String MonitoringOfDHWTemperature                            = "monitoringOfDhwTemperature";

        /// <summary>Monitoring of Outdoor Temperature (MOT).</summary>
        public const String MonitoringOfOutdoorTemperature                        = "monitoringOfOutdoorTemperature";

        /// <summary>Monitoring of Room Cooling System Function (MRCSF).</summary>
        public const String MonitoringOfRoomCoolingSystemFunction                 = "monitoringOfRoomCoolingSystemFunction";

        /// <summary>Monitoring of Room Heating System Function (MRHSF).</summary>
        public const String MonitoringOfRoomHeatingSystemFunction                 = "monitoringOfRoomHeatingSystemFunction";

        /// <summary>Monitoring of Room Temperature (MRT).</summary>
        public const String MonitoringOfRoomTemperature                           = "monitoringOfRoomTemperature";

        /// <summary>Monitoring and Control of Smart Grid Ready Conditions (MCSGRC).</summary>
        public const String MonitoringAndControlOfSmartGridReadyConditions        = "monitoringAndControlOfSmartGridReadyConditions";

        /// <summary>Optimization of Self Consumption by Heat Pump Compressor Flexibility (OHPCF).</summary>
        public const String OptimizationOfSelfConsumptionByHeatPumpCompressorFlexibility
                                                                                  = "optimizationOfSelfConsumptionByHeatPumpCompressorFlexibility";

        /// <summary>Visualization of Heating Area Name (VHAN).</summary>
        public const String VisualizationOfHeatingAreaName                        = "visualizationOfHeatingAreaName";

        /// <summary>Flexible Load (FLOA).</summary>
        public const String FlexibleLoad                                          = "flexibleLoad";

        /// <summary>Flexible Start for White Goods (FSWG).</summary>
        public const String FlexibleStartForWhiteGoods                            = "flexibleStartForWhiteGoods";

        #endregion


        #region All

        /// <summary>
        /// Every use case name this framework knows. A device may announce one
        /// which is not here - the names are extensible, and a name we do not
        /// know is a use case we do not implement rather than an error.
        /// </summary>
        public static readonly IReadOnlySet<String> All = new HashSet<String>(StringComparer.Ordinal) {
            LimitationOfPowerConsumption,
            LimitationOfPowerProduction,
            MonitoringOfGridConnectionPoint,
            MonitoringOfPowerConsumption,
            IncentiveTableBasedPowerConsumptionManagement,
            CoordinatedEVCharging,
            EVChargingSummary,
            EVCommissioningAndConfiguration,
            EVSECommissioningAndConfiguration,
            EVStateOfCharge,
            MeasurementOfElectricityDuringEVCharging,
            OptimizationOfSelfConsumptionDuringEVCharging,
            OverloadProtectionByEVChargingCurrentCurtailment,
            ControlOfBattery,
            MonitoringOfBattery,
            MonitoringOfInverter,
            MonitoringOfPVString,
            VisualizationOfAggregatedBatteryData,
            VisualizationOfAggregatedPhotovoltaicData,
            ConfigurationOfDHWSystemFunction,
            ConfigurationOfDHWTemperature,
            ConfigurationOfRoomCoolingSystemFunction,
            ConfigurationOfRoomCoolingTemperature,
            ConfigurationOfRoomHeatingSystemFunction,
            ConfigurationOfRoomHeatingTemperature,
            MonitoringOfDHWSystemFunction,
            MonitoringOfDHWTemperature,
            MonitoringOfOutdoorTemperature,
            MonitoringOfRoomCoolingSystemFunction,
            MonitoringOfRoomHeatingSystemFunction,
            MonitoringOfRoomTemperature,
            MonitoringAndControlOfSmartGridReadyConditions,
            OptimizationOfSelfConsumptionByHeatPumpCompressorFlexibility,
            VisualizationOfHeatingAreaName,
            FlexibleLoad,
            FlexibleStartForWhiteGoods
        };

        #endregion

    }


    /// <summary>
    /// The actors of the EEBUS use cases.
    ///
    /// An actor is a role within one use case, and a SPINE entity may play
    /// different actors in different use cases at the same time. Which actor is
    /// the client and which the server is decided by the **primary** purpose of
    /// the use case - the act of limiting power in LPC, of collecting
    /// measurements in MPC - and, per the general implementation guideline
    /// § 2.1.3, a secondary function whose direction is reversed does not change
    /// that. The energy guard of LPC hosts a server feature for its own
    /// heartbeat and remains the client actor.
    /// </summary>
    public static class UseCaseActors
    {

        /// <summary>A battery.</summary>
        public const String Battery                   = "Battery";

        /// <summary>A battery system.</summary>
        public const String BatterySystem             = "BatterySystem";

        /// <summary>A customer energy manager.</summary>
        public const String CEM                       = "CEM";

        /// <summary>A compressor.</summary>
        public const String Compressor                = "Compressor";

        /// <summary>A device which configures another one.</summary>
        public const String ConfigurationAppliance    = "ConfigurationAppliance";

        /// <summary>The system being limited, i.e. in LPC and LPP.</summary>
        public const String ControllableSystem        = "ControllableSystem";

        /// <summary>A domestic hot water circuit.</summary>
        public const String DHWCircuit                = "DHWCircuit";

        /// <summary>A broker of energy.</summary>
        public const String EnergyBroker              = "EnergyBroker";

        /// <summary>A consumer of energy.</summary>
        public const String EnergyConsumer            = "EnergyConsumer";

        /// <summary>The one which limits, i.e. in LPC and LPP.</summary>
        public const String EnergyGuard               = "EnergyGuard";

        /// <summary>An electric vehicle.</summary>
        public const String EV                        = "EV";

        /// <summary>Electric vehicle supply equipment: a charging station.</summary>
        public const String EVSE                      = "EVSE";

        /// <summary>The point where the premises meet the grid.</summary>
        public const String GridConnectionPoint       = "GridConnectionPoint";

        /// <summary>A heating circuit.</summary>
        public const String HeatingCircuit            = "HeatingCircuit";

        /// <summary>A heating zone.</summary>
        public const String HeatingZone               = "HeatingZone";

        /// <summary>A heat pump.</summary>
        public const String HeatPump                  = "HeatPump";

        /// <summary>A room with heating or cooling.</summary>
        public const String HVACRoom                  = "HVACRoom";

        /// <summary>An inverter.</summary>
        public const String Inverter                  = "Inverter";

        /// <summary>The unit being monitored, i.e. in MPC.</summary>
        public const String MonitoredUnit             = "MonitoredUnit";

        /// <summary>The one which monitors, i.e. in MPC.</summary>
        public const String MonitoringAppliance       = "MonitoringAppliance";

        /// <summary>A sensor for the outdoor temperature.</summary>
        public const String OutdoorTemperatureSensor  = "OutdoorTemperatureSensor";

        /// <summary>A string of photovoltaic modules.</summary>
        public const String PVString                  = "PVString";

        /// <summary>A photovoltaic system.</summary>
        public const String PVSystem                  = "PVSystem";

        /// <summary>An appliance which can be shifted in time.</summary>
        public const String SmartAppliance            = "SmartAppliance";

        /// <summary>A broker of transmission capacity.</summary>
        public const String TransmissionBroker        = "TransmissionBroker";

        /// <summary>A device which shows things to people.</summary>
        public const String VisualizationAppliance    = "VisualizationAppliance";

    }

}
