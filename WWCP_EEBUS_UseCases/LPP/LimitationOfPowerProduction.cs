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
using cloud.charging.open.protocols.EEBUS.UseCases.LimitationOfPower;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.LPP
{

    /// <summary>
    /// "Limitation of Power Production"
    /// (EEBus_UC_TS_LimitationOfPowerProduction_V1.0.0).
    ///
    /// An energy guard limits how much a device may feed into the grid - a
    /// photovoltaic inverter, a battery, a combined heat and power unit. It is
    /// the mirror of the limitation of power **consumption** and it is the same
    /// document with three words changed: the direction of the energy, the name
    /// of the failsafe configuration key, and which nominal maximum is reported.
    ///
    /// Everything else is shared, down to the rule numbers: [LPP-919] and
    /// [LPC-919] are the same sentence. So the state machine, the controllable
    /// system and the energy guard are the ones of
    /// <see cref="PowerLimitation"/>, and this file says which of the two use
    /// cases they are being asked to be.
    /// </summary>
    public static class LimitationOfPowerProduction
    {

        /// <summary>
        /// What tells this use case from the limitation of power consumption.
        /// </summary>
        public static PowerLimitationProfile Profile
            => PowerLimitation.Production;

    }


    /// <summary>
    /// The controllable system of "Limitation of Power Production" - the device
    /// whose feed-in is being limited.
    /// </summary>
    public class LPPControllableSystem : APowerLimitationControllableSystem
    {

        /// <summary>
        /// Add the controllable system of LPP to an entity.
        /// </summary>
        /// <param name="Entity">The entity which is being limited.</param>
        /// <param name="IsEnergyManager">Whether it runs on an energy manager, which changes which nominal maximum it reports.</param>
        public LPPControllableSystem(SPINELocalEntity  Entity,
                                     Boolean           IsEnergyManager   = false)

            : base(Entity,
                   PowerLimitation.Production,
                   IsEnergyManager)

        { }

    }


    /// <summary>
    /// The energy guard of "Limitation of Power Production" - the device which
    /// does the limiting.
    /// </summary>
    public class LPPEnergyGuard : APowerLimitationEnergyGuard
    {

        /// <summary>
        /// Add the energy guard of LPP to an entity.
        /// </summary>
        /// <param name="Entity">The entity which does the limiting.</param>
        public LPPEnergyGuard(SPINELocalEntity Entity)

            : base(Entity,
                   PowerLimitation.Production)

        { }

    }

}
