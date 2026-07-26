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
using cloud.charging.open.protocols.EEBUS.UseCases.Commissioning;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.EVSECC
{

    /// <summary>
    /// The energy manager of "EVSE Commissioning and Configuration" - the side
    /// which writes down what was plugged in.
    ///
    /// The commissioning side is the same in every commissioning use case - read
    /// once, subscribe, listen - so this only says which use case it is doing,
    /// plus which actors it will accept at the other end.
    ///
    /// That last part is not a detail. The Porsche PMCC announces this use case
    /// with the actor **EV** rather than EVSE, which the specification does not
    /// allow and the field contains anyway; the Go reference implementation
    /// accepts it with a comment saying so. An energy manager which insisted on
    /// the letter would refuse to name a charging station somebody actually owns.
    /// </summary>
    public class EVSECCEnergyManager : ACommissioningAppliance
    {

        #region Constructor(s)

        /// <summary>
        /// Add the CEM of EVSECC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which commissions.</param>
        /// <param name="Scenarios">Which scenarios it is interested in. Scenario 2 is always included.</param>
        /// <param name="StrictActor">
        /// Whether to accept only the actor the specification names. False by
        /// default, because devices in the field announce the wrong one.
        /// </param>
        public EVSECCEnergyManager(SPINELocalEntity      Entity,
                                   IEnumerable<UInt32>?  Scenarios     = null,
                                   Boolean               StrictActor   = false)

            : base(Entity,
                   EVSECommissioningAndConfiguration.Profile,
                   Scenarios ?? [ EVSECommissioningAndConfiguration.ScenarioManufacturerData ],
                   PartnerActors: StrictActor
                                      ? [ UseCaseActors.EVSE ]
                                      : [ UseCaseActors.EVSE, UseCaseActors.EV ])

        { }

        #endregion


        #region HasFailed(Partner)

        /// <summary>
        /// Whether a charging station is currently reporting a failure
        /// (scenario 2).
        ///
        /// False when it has not said anything yet, which is the same answer a
        /// person would give and not the same fact - see
        /// <see cref="ACommissioningAppliance.OperatingState"/> for the
        /// difference.
        /// </summary>
        /// <param name="Partner">An entity of a charging station.</param>
        public Boolean HasFailed(SPINERemoteEntity Partner)

            => IsReporting(Partner);

        #endregion

    }

}
