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

using cloud.charging.open.protocols.EEBUS.SPINE;
using cloud.charging.open.protocols.EEBUS.SPINE.Model;
using cloud.charging.open.protocols.EEBUS.UseCases.Monitoring;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.MPC
{

    /// <summary>
    /// The monitored unit of "Monitoring of Power Consumption" - the device
    /// which is being watched.
    ///
    /// What it does is the shared work of every monitoring use case - see
    /// <see cref="AMonitoredDevice"/> - so all this adds is which quantities it
    /// is that a monitored unit publishes.
    /// </summary>
    public class MPCMonitoredUnit : AMonitoredDevice
    {

        #region Constructor(s)

        /// <summary>
        /// Add the monitored unit of MPC to an entity.
        ///
        /// Scenario 1 is mandatory and always there; the other four are added
        /// when this device measures them. A meter which knows nothing but its
        /// total active power implements the use case completely.
        /// </summary>
        /// <param name="Entity">The entity which is being watched.</param>
        /// <param name="Phases">Which phases it measures. All three by default; an empty list means it measures only totals.</param>
        /// <param name="PowerPerPhase">Whether it measures the active power of each phase as well as the total (scenario 1).</param>
        /// <param name="Energy">Whether it measures energy (scenario 2).</param>
        /// <param name="Current">Whether it measures current (scenario 3).</param>
        /// <param name="Voltage">Whether it measures voltage (scenario 4).</param>
        /// <param name="Frequency">Whether it measures the grid frequency (scenario 5).</param>
        public MPCMonitoredUnit(SPINELocalEntity                                 Entity,
                                IEnumerable<ElectricalConnectionPhaseNameType>?  Phases          = null,
                                Boolean                                          PowerPerPhase   = false,
                                Boolean                                          Energy          = false,
                                Boolean                                          Current         = false,
                                Boolean                                          Voltage         = false,
                                Boolean                                          Frequency       = false)

            : base(Entity,
                   MonitoringOfPowerConsumption.Profile,
                   Measures(Phases, PowerPerPhase, Energy, Current, Voltage, Frequency),
                   Phases)

        { }


        /// <summary>
        /// Which quantities a monitored unit with these measurements publishes.
        /// </summary>
        private static IEnumerable<MonitoringQuantity> Measures(IEnumerable<ElectricalConnectionPhaseNameType>?  Phases,
                                                                Boolean                                          PowerPerPhase,
                                                                Boolean                                          Energy,
                                                                Boolean                                          Current,
                                                                Boolean                                          Voltage,
                                                                Boolean                                          Frequency)
        {

            var phases      = Phases ?? [ ElectricalConnectionPhaseNameType.A,
                                          ElectricalConnectionPhaseNameType.B,
                                          ElectricalConnectionPhaseNameType.C ];

            var quantities  = new List<MonitoringQuantity> {
                                  MonitoringOfPowerConsumption.PowerTotal
                              };

            if (PowerPerPhase)
                quantities.AddRange(phases.Select(MonitoringOfPowerConsumption.Power));

            if (Energy)
            {
                quantities.Add(MonitoringOfPowerConsumption.EnergyConsumed);
                quantities.Add(MonitoringOfPowerConsumption.EnergyProduced);
            }

            if (Current)
                quantities.AddRange(phases.Select(MonitoringOfPowerConsumption.Current));

            if (Voltage)
                quantities.AddRange(phases.Select(MonitoringOfPowerConsumption.Voltage));

            if (Frequency)
                quantities.Add(MonitoringOfPowerConsumption.Frequency);

            return quantities;

        }

        #endregion

    }

}
