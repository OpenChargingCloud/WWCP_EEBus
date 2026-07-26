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
    /// The EV of "Overload Protection by EV Charging Current Curtailment".
    ///
    /// The shared work is in <see cref="AChargingCurrentVehicle"/>; what this
    /// adds is the one thing which makes this use case an obligation rather than
    /// advice. When the energy guard goes quiet or announces a failure, the EV
    /// "should switch to a safe current setting that guarantees that no overload
    /// occurs during absence of the Energy Guard" ([OPEV-005], [OPEV-007]) -
    /// there is always a current, never "no opinion", because what is behind the
    /// limit is a fuse and the fuse does not go away with the energy guard.
    /// </summary>
    public class OPEVElectricVehicle : AChargingCurrentVehicle
    {

        #region Constructor(s)

        /// <summary>
        /// Add the EV of OPEV to an entity.
        /// </summary>
        /// <param name="Entity">The entity of the electric vehicle.</param>
        /// <param name="PhaseCount">How many phases it charges on. Three by default.</param>
        public OPEVElectricVehicle(SPINELocalEntity  Entity,
                                   UInt32            PhaseCount   = 3)

            : base(Entity,
                   OverloadProtection.Profile,
                   OverloadProtection.ScenarioName,
                   PhaseCount)

        { }

        #endregion


        #region ChargingCurrents

        /// <summary>
        /// What this EV is actually charging with, per phase, in ampere.
        ///
        /// This is the answer the whole use case is about: the curtailment where
        /// the energy guard is there and healthy, and the safe current where it
        /// is not ([OPEV-005], [OPEV-007]). Never null - an obligation which
        /// nobody is currently stating is still an obligation.
        /// </summary>
        public IReadOnlyList<Decimal> ChargingCurrents

            => [.. Currents.Select(current => current ?? SafeCurrent)];

        #endregion

    }

}
