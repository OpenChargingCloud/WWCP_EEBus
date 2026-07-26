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
using cloud.charging.open.protocols.EEBUS.UseCases.Monitoring;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.MPC
{

    /// <summary>
    /// The monitoring appliance of "Monitoring of Power Consumption" - the
    /// device which watches.
    ///
    /// The watching side is the same in every monitoring use case - read the
    /// descriptions, join them, subscribe - so this only says which use case it
    /// is watching for. See <see cref="AMonitoringAppliance"/>.
    /// </summary>
    public class MPCMonitoringAppliance : AMonitoringAppliance
    {

        #region Constructor(s)

        /// <summary>
        /// Add the monitoring appliance of MPC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which watches.</param>
        /// <param name="Scenarios">Which scenarios it is interested in. Scenario 1 is always included.</param>
        public MPCMonitoringAppliance(SPINELocalEntity      Entity,
                                      IEnumerable<UInt32>?  Scenarios   = null)

            : base(Entity,
                   MonitoringOfPowerConsumption.Profile,
                   Scenarios ?? [ MonitoringOfPowerConsumption.ScenarioEnergy,
                                  MonitoringOfPowerConsumption.ScenarioCurrent,
                                  MonitoringOfPowerConsumption.ScenarioVoltage,
                                  MonitoringOfPowerConsumption.ScenarioFrequency ])

        { }

        #endregion

    }

}
