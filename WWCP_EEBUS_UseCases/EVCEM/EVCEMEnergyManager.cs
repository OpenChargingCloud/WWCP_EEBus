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
    /// The energy manager of "Measurement of Electricity during EV Charging" -
    /// the side which watches a car being charged.
    ///
    /// The watching side is the same in every monitoring use case - read the
    /// descriptions, join them, subscribe - so this only says which use case it
    /// is watching for, plus the three questions worth asking by name. See
    /// <see cref="AMonitoringAppliance"/>.
    ///
    /// A note on the actor: chapter 2 of the specification calls this side the
    /// **Energy Guard**, but section 3.2.2 says it "SHALL be denoted as 'CEM'"
    /// in the use case discovery. The name on the wire is the one which decides,
    /// so this is a CEM.
    /// </summary>
    public class EVCEMEnergyManager : AMonitoringAppliance
    {

        #region Constructor(s)

        /// <summary>
        /// Add the energy manager of EVCEM to an entity.
        /// </summary>
        /// <param name="Entity">The entity which watches.</param>
        /// <param name="Scenarios">Which scenarios it is interested in. All three by default - a car supports whichever it likes, and the manager takes what it gets.</param>
        public EVCEMEnergyManager(SPINELocalEntity      Entity,
                                  IEnumerable<UInt32>?  Scenarios   = null)

            : base(Entity,
                   MeasurementOfElectricityDuringEVCharging.Profile,
                   Scenarios ?? [ MeasurementOfElectricityDuringEVCharging.ScenarioCurrent,
                                  MeasurementOfElectricityDuringEVCharging.ScenarioPower,
                                  MeasurementOfElectricityDuringEVCharging.ScenarioEnergy ])

        { }

        #endregion


        #region Current(Partner, Phase) / Power(Partner) / EnergyCharged(Partner)

        /// <summary>
        /// The charging current on one phase, in ampere (scenario 1).
        /// </summary>
        /// <param name="Partner">An entity of a car being charged.</param>
        /// <param name="Phase">Which phase.</param>
        public Decimal? Current(SPINERemoteEntity                  Partner,
                                ElectricalConnectionPhaseNameType  Phase)

            => Read(Partner, MeasurementOfElectricityDuringEVCharging.Current(Phase))?.Value;


        /// <summary>
        /// The total charging power, in watts (scenario 2).
        /// </summary>
        /// <param name="Partner">An entity of a car being charged.</param>
        public Decimal? Power(SPINERemoteEntity Partner)

            => Read(Partner, MeasurementOfElectricityDuringEVCharging.PowerTotal)?.Value;


        /// <summary>
        /// The energy charged into the car, in watt hours (scenario 3).
        /// </summary>
        /// <param name="Partner">An entity of a car being charged.</param>
        public Decimal? EnergyCharged(SPINERemoteEntity Partner)

            => Read(Partner, MeasurementOfElectricityDuringEVCharging.EnergyCharged)?.Value;

        #endregion

    }

}
