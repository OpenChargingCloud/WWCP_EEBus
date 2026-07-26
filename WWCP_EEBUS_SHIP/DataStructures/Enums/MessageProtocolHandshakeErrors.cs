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

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// The reasons for aborting a SHIP message protocol handshake
    /// (SHIP TS 1.0.1, chapter 13.4.4.2).
    /// </summary>
    public enum MessageProtocolHandshakeErrors : Byte
    {

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        RFU                = 0,

        /// <summary>
        /// The communication partner did not answer in time.
        /// </summary>
        Timeout            = 1,

        /// <summary>
        /// A message arrived which is not expected in the current state.
        /// </summary>
        UnexpectedMessage  = 2,

        /// <summary>
        /// The announced or selected protocol version or format cannot be used.
        /// </summary>
        SelectionMismatch  = 3

    }

}
