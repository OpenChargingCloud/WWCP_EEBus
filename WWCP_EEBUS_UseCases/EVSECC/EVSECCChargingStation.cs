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
    /// The charging station of "EVSE Commissioning and Configuration" - the side
    /// which says what it is.
    ///
    /// What it does is the shared work of every commissioning use case, see
    /// <see cref="ACommissionedDevice"/>; all this adds is the failure, which is
    /// what this use case is for.
    /// </summary>
    public class EVSECCChargingStation : ACommissionedDevice
    {

        #region Constructor(s)

        /// <summary>
        /// Add the EVSE of EVSECC to an entity.
        /// </summary>
        /// <param name="Entity">The entity of the charging station.</param>
        /// <param name="Manufacturer">Who made it. Null means it does not support scenario 1.</param>
        public EVSECCChargingStation(SPINELocalEntity   Entity,
                                     ManufacturerData?  Manufacturer   = null)

            : base(Entity,
                   EVSECommissioningAndConfiguration.Profile,
                   Manufacturer is not null
                       ? [ EVSECommissioningAndConfiguration.ScenarioManufacturerData ]
                       : null,
                   Manufacturer)

        { }

        #endregion


        #region HasFailed / Fail(LastErrorCode, ...) / Recover(...)

        /// <summary>
        /// Whether this charging station is currently reporting a failure
        /// (scenario 2).
        /// </summary>
        public Boolean HasFailed

            => OperatingState == DeviceDiagnosisOperatingStateType.Failure;


        /// <summary>
        /// Report a failure, and tell whoever subscribed.
        ///
        /// Which matters to more than a user interface: [EVSECC-020] says that
        /// while a charging station has failed, "the EV may no longer be able to
        /// follow the charging plan correctly and updates from the EV may no
        /// longer contain valid data" - so an energy manager which sees this
        /// should stop believing the numbers, not just show a red dot.
        /// </summary>
        /// <param name="LastErrorCode">What went wrong.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task Fail(String?            LastErrorCode       = null,
                         CancellationToken  CancellationToken   = default)

            => SetOperatingState(DeviceDiagnosisOperatingStateType.Failure,
                                 LastErrorCode,
                                 CancellationToken);


        /// <summary>
        /// Report that whatever went wrong is over.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task Recover(CancellationToken CancellationToken = default)

            => SetOperatingState(DeviceDiagnosisOperatingStateType.NormalOperation,
                                 CancellationToken: CancellationToken);

        #endregion

    }

}
