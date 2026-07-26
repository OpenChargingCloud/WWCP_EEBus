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
using cloud.charging.open.protocols.EEBUS.UseCases.ChargingCurrent;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.OSCEV
{

    /// <summary>
    /// The energy manager of "Optimization of Self-Consumption During EV
    /// Charging" - the side which knows how much the house is producing.
    ///
    /// What it does is the shared work of both charging current use cases - see
    /// <see cref="AChargingCurrentAdvisor"/> - so all this adds is the words
    /// this specification uses for it.
    /// </summary>
    public class OSCEVEnergyManager : AChargingCurrentAdvisor
    {

        #region Constructor(s)

        /// <summary>
        /// Add the CEM of OSCEV to an entity.
        /// </summary>
        /// <param name="Entity">The entity which manages the energy of the premises.</param>
        public OSCEVEnergyManager(SPINELocalEntity Entity)

            : base(Entity,
                   SelfConsumptionOptimization.Profile,
                   SelfConsumptionOptimization.ScenarioName)

        { }

        #endregion


        #region WriteSelfProducedCurrents(...) / WriteSelfProducedCurrent(...)

        /// <summary>
        /// Tell an EV how much self-produced current there is, phase by phase
        /// ([OSCEV-001], [OSCEV-002]).
        ///
        /// Per phase where the EV charges asymmetrically, which is what lets a
        /// manager put a high current on the phase with spare production and a
        /// low one on the phase which is already loaded. Where it does not,
        /// every phase gets the same - or a **consolidated** current over all
        /// three, which section 2.3.1.1 recommends for the common case where
        /// nothing is measured per phase anyway: "it can be advantageous for the
        /// CEM to provide consolidated current that matches the self-produced
        /// power and therefore allows an EV to consume all self-produced power."
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Currents">The current per phase in ampere, in the order the phases were read.</param>
        /// <param name="IsActive">Whether the recommendation applies. False says "no self-produced current to speak of right now".</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SPINEResponse> WriteSelfProducedCurrents(SPINERemoteEntity     Partner,
                                                             IEnumerable<Decimal>  Currents,
                                                             Boolean               IsActive            = true,
                                                             CancellationToken     CancellationToken   = default)

            => WriteCurrents(Partner, Currents, IsActive, CancellationToken);


        /// <summary>
        /// Tell an EV the same self-produced current on every phase.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Current">The current in ampere.</param>
        /// <param name="IsActive">Whether the recommendation applies.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SPINEResponse> WriteSelfProducedCurrent(SPINERemoteEntity  Partner,
                                                            Decimal            Current,
                                                            Boolean            IsActive            = true,
                                                            CancellationToken  CancellationToken   = default)

            => WriteCurrent(Partner, Current, IsActive, CancellationToken);

        #endregion

    }

}
