/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of WWCP EEBus <https://github.com/OpenChargingCloud/WWCP_EEBus>
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

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// The request for the access methods of the communication partner
    /// (SHIP TS 1.0.1, chapter 13.4.6).
    ///
    /// The request has no content; on the wire it is an empty element, which
    /// EEBus JSON represents as an empty array: { "accessMethodsRequest": [] }.
    /// </summary>
    public class SHIPAccessMethodsRequestMessage() : ASHIPMessage(SHIPMessageTypes.CONTROL)
    {

        #region ToMessageJSON()

        /// <summary>
        /// Return the JSON representation of this message.
        /// </summary>
        public override JObject ToMessageJSON()

            => new (
                   new JProperty("accessMethodsRequest", new JObject())
               );

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => "accessMethodsRequest";

        #endregion

    }

}
