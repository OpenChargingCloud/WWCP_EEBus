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

namespace cloud.charging.open.protocols.EEBUS.UseCases.OPEV
{

    /// <summary>
    /// The energy guard of "Overload Protection by EV Charging Current
    /// Curtailment" - the device which keeps the fuse from tripping.
    ///
    /// What it does is the shared work of both charging current use cases - see
    /// <see cref="AChargingCurrentAdvisor"/> - so all this adds is the words
    /// this specification uses for it.
    /// </summary>
    public class OPEVEnergyGuard : AChargingCurrentAdvisor
    {

        #region Constructor(s)

        /// <summary>
        /// Add the energy guard of OPEV to an entity.
        /// </summary>
        /// <param name="Entity">The entity which protects against overload.</param>
        /// <param name="AnnounceAsCEM">
        /// Whether to announce the actor as "CEM" rather than "EnergyGuard".
        /// The specification says EnergyGuard; the certified Go implementation
        /// says CEM, and devices in the field were built against it.
        /// </param>
        public OPEVEnergyGuard(SPINELocalEntity  Entity,
                               Boolean           AnnounceAsCEM   = false)

            : base(Entity,
                   OverloadProtection.Profile,
                   OverloadProtection.ScenarioName,
                   AnnounceAsAlternateActor: AnnounceAsCEM)

        { }

        #endregion


        #region WriteCurrentLimits(...) / WriteCurrentLimit(...)

        /// <summary>
        /// Curtail the charging current of an EV, phase by phase ([OPEV-001]).
        ///
        /// Where the EV supports asymmetric charging the phases may differ
        /// ([OPEV-002]); where it does not, they have to be equal, and the
        /// energy guard has to use the lowest of the three - which is the
        /// difference between 690 W and 460 W in the specification's own example.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Currents">The current per phase in ampere, in the order the phases were read.</param>
        /// <param name="IsActive">Whether the curtailment applies. False says "no curtailment is needed" ([OPEV-004]).</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SPINEResponse> WriteCurrentLimits(SPINERemoteEntity     Partner,
                                                      IEnumerable<Decimal>  Currents,
                                                      Boolean               IsActive            = true,
                                                      CancellationToken     CancellationToken   = default)

            => WriteCurrents(Partner, Currents, IsActive, CancellationToken);


        /// <summary>
        /// Curtail every phase to the same current, which is what an EV without
        /// asymmetric charging needs.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Current">The current in ampere.</param>
        /// <param name="IsActive">Whether the curtailment applies.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SPINEResponse> WriteCurrentLimit(SPINERemoteEntity  Partner,
                                                     Decimal            Current,
                                                     Boolean            IsActive            = true,
                                                     CancellationToken  CancellationToken   = default)

            => WriteCurrent(Partner, Current, IsActive, CancellationToken);

        #endregion

    }

}
