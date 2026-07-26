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

namespace cloud.charging.open.protocols.EEBUS.UseCases.EVCEM
{

    /// <summary>
    /// The car of "Measurement of Electricity during EV Charging" - the side
    /// which is being measured.
    ///
    /// What it does is the shared work of every monitoring use case, see
    /// <see cref="AMonitoredDevice"/>; all this adds is which quantities a
    /// charging car publishes and the rule that it has to publish some.
    /// </summary>
    public class EVCEMElectricVehicle : AMonitoredDevice
    {

        #region Constructor(s)

        /// <summary>
        /// Add the EV of EVCEM to an entity.
        /// </summary>
        /// <param name="Entity">The entity which is being charged.</param>
        /// <param name="Phases">Which phases are measured. All three by default; an empty list means only totals.</param>
        /// <param name="Current">Whether the charging current of each phase is measured (scenario 1).</param>
        /// <param name="Power">Whether the charging power is measured (scenario 2).</param>
        /// <param name="PowerPerPhase">Whether the charging power of each phase is measured as well as the total (scenario 2).</param>
        /// <param name="Energy">Whether the charged energy is measured (scenario 3).</param>
        /// <exception cref="ArgumentException">When none of the three scenarios is supported.</exception>
        public EVCEMElectricVehicle(SPINELocalEntity                                 Entity,
                                    IEnumerable<ElectricalConnectionPhaseNameType>?  Phases          = null,
                                    Boolean                                          Current         = true,
                                    Boolean                                          Power           = false,
                                    Boolean                                          PowerPerPhase   = false,
                                    Boolean                                          Energy          = false)

            : base(Entity,
                   MeasurementOfElectricityDuringEVCharging.Profile,
                   Measures(Phases, Current, Power, PowerPerPhase, Energy),
                   Phases)

        { }


        /// <summary>
        /// Which quantities a car with these measurements publishes.
        ///
        /// Current is on by default because section 2.3 recommends it: it is the
        /// one measurement the other two can be derived from, and the only one
        /// which survives asymmetric charging.
        /// </summary>
        private static IEnumerable<MonitoringQuantity> Measures(IEnumerable<ElectricalConnectionPhaseNameType>?  Phases,
                                                                Boolean                                          Current,
                                                                Boolean                                          Power,
                                                                Boolean                                          PowerPerPhase,
                                                                Boolean                                          Energy)
        {

            var phases      = Phases ?? [ ElectricalConnectionPhaseNameType.A,
                                          ElectricalConnectionPhaseNameType.B,
                                          ElectricalConnectionPhaseNameType.C ];

            var quantities  = new List<MonitoringQuantity>();

            if (Current)
                quantities.AddRange(phases.Select(MeasurementOfElectricityDuringEVCharging.Current));

            if (Power || PowerPerPhase)
                quantities.Add(MeasurementOfElectricityDuringEVCharging.PowerTotal);

            if (PowerPerPhase)
                quantities.AddRange(phases.Select(MeasurementOfElectricityDuringEVCharging.Power));

            if (Energy)
                quantities.Add(MeasurementOfElectricityDuringEVCharging.EnergyCharged);

            return quantities;

        }

        #endregion

    }

}
