/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// SHIP Close Message
    /// </summary>
    /// <param name="ConnectionClose">A connection close.</param>
    /// <param name="CustomData">An optional custom data object allowing to store any kind of customer specific data.</param>
    public class SHIPCloseMessage(ConnectionClose  ConnectionClose,
                                  CustomData?      CustomData   = null)

        : ASHIPMessage(CustomData)

    {

        #region Properties

        /// <summary>
        /// The connection close.
        /// </summary>
        [Mandatory]
        public ConnectionClose  ConnectionClose    { get; } = ConnectionClose;

        #endregion


        #region (static) Parse   (JSON, CustomSHIPCloseMessageParser = null)

        /// <summary>
        /// Parse the given JSON representation of a SHIP-Close message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomSHIPCloseMessageParser">A delegate to parse custom SHIP Close Messages.</param>
        public static SHIPCloseMessage Parse(JObject                                         JSON,
                                             CustomJObjectParserDelegate<SHIPCloseMessage>?  CustomSHIPCloseMessageParser   = null)
        {

            if (TryParse(JSON,
                         out var shipCloseMessage,
                         out var errorResponse,
                         CustomSHIPCloseMessageParser))
            {
                return shipCloseMessage;
            }

            throw new ArgumentException("The given JSON representation of a SHIP-Close-Message is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out SHIPCloseMessage, CustomSHIPCloseMessageParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Close-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPCloseMessage">The parsed shipCloseMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                     JSON,
                                       [NotNullWhen(true)]  out SHIPCloseMessage?  SHIPCloseMessage,
                                       [NotNullWhen(false)] out String?            ErrorResponse)

            => TryParse(JSON,
                        out SHIPCloseMessage,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Close-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPCloseMessage">The parsed shipCloseMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomSHIPCloseMessageParser">A delegate to parse custom SHIP Close Messages.</param>
        public static Boolean TryParse(JObject                                         JSON,
                                       [NotNullWhen(true)]  out SHIPCloseMessage?      SHIPCloseMessage,
                                       [NotNullWhen(false)] out String?                ErrorResponse,
                                       CustomJObjectParserDelegate<SHIPCloseMessage>?  CustomSHIPCloseMessageParser)
        {

            try
            {

                SHIPCloseMessage = default;

                #region ConnectionClose    [mandatory]

                if (!JSON.ParseMandatoryJSON("connectionClose",
                                             "connection close",
                                             SHIP.ConnectionClose.TryParse,
                                             out ConnectionClose? ConnectionClose,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region CustomData         [optional]

                if (JSON.ParseOptionalJSON("customData",
                                           "custom data",
                                           WWCP.OverlayNetworking.CustomData.TryParse,
                                           out CustomData? CustomData,
                                           out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion


                SHIPCloseMessage = new SHIPCloseMessage(
                                       ConnectionClose,
                                       CustomData
                                   );

                if (CustomSHIPCloseMessageParser is not null)
                    SHIPCloseMessage = CustomSHIPCloseMessageParser(JSON,
                                                                    SHIPCloseMessage);

                return true;

            }
            catch (Exception e)
            {
                SHIPCloseMessage  = default;
                ErrorResponse     = "The given JSON representation of a SHIP-Close-Message is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomSHIPCloseMessageSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomSHIPCloseMessageSerializer">A delegate to serialize custom event data objects.</param>
        /// <param name="CustomConnectionCloseTypeSerializer">A delegate to serialize custom ConnectionCloseType objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<SHIPCloseMessage>?  CustomSHIPCloseMessageSerializer      = null,
                              CustomJObjectSerializerDelegate<ConnectionClose>?   CustomConnectionCloseTypeSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?        CustomCustomDataSerializer            = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("connectionClose",    ConnectionClose.ToJSON(CustomConnectionCloseTypeSerializer,
                                                                                            CustomCustomDataSerializer)),

                           CustomData is not null
                               ? new JProperty("customData",         CustomData.     ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomSHIPCloseMessageSerializer is not null
                       ? CustomSHIPCloseMessageSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
