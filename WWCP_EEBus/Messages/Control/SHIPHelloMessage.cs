/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    public class SHIPHelloMessage(ConnectionHelloType  ConnectionHello,
                                  CustomData?          CustomData = null)

        : ASHIPMessage(CustomData)

    {

        #region Properties

        [Mandatory]
        public ConnectionHelloType  ConnectionHello    { get; } = ConnectionHello;

        #endregion


        #region (static) Parse   (JSON, CustomSHIPHelloMessageParser = null)

        /// <summary>
        /// Parse the given JSON representation of a SHIP hello message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomSHIPHelloMessageParser">A delegate to parse custom SHIP hello messages.</param>
        public static SHIPHelloMessage Parse(JObject                                         JSON,
                                             CustomJObjectParserDelegate<SHIPHelloMessage>?  CustomSHIPHelloMessageParser   = null)
        {

            if (TryParse(JSON,
                         out var shipHelloMessage,
                         out var errorResponse,
                         CustomSHIPHelloMessageParser) &&
                shipHelloMessage is not null)
            {
                return shipHelloMessage;
            }

            throw new ArgumentException("The given JSON representation of a SHIP hello message is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out SHIPHelloMessage, CustomSHIPHelloMessageParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a SHIP hello message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPHelloMessage">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                     JSON,
                                       [NotNullWhen(true)]  out SHIPHelloMessage?  SHIPHelloMessage,
                                       [NotNullWhen(false)] out String?            ErrorResponse)

            => TryParse(JSON,
                        out SHIPHelloMessage,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a SHIP hello message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPHelloMessage">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomSHIPHelloMessageParser">A delegate to parse custom SHIP hello messages.</param>
        public static Boolean TryParse(JObject                                         JSON,
                                       [NotNullWhen(true)]  out SHIPHelloMessage?      SHIPHelloMessage,
                                       [NotNullWhen(false)] out String?                ErrorResponse,
                                       CustomJObjectParserDelegate<SHIPHelloMessage>?  CustomSHIPHelloMessageParser)
        {

            try
            {

                SHIPHelloMessage = default;

                #region ConnectionHello    [mandatory]

                if (!JSON.ParseMandatoryJSON("connectionHello",
                                             "connection hello",
                                             ConnectionHelloType.TryParse,
                                             out ConnectionHelloType? ConnectionHello,
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


                SHIPHelloMessage = new SHIPHelloMessage(
                                       ConnectionHello,
                                       CustomData
                                   );

                if (CustomSHIPHelloMessageParser is not null)
                    SHIPHelloMessage = CustomSHIPHelloMessageParser(JSON,
                                                                    SHIPHelloMessage);

                return true;

            }
            catch (Exception e)
            {
                SHIPHelloMessage  = default;
                ErrorResponse     = "The given JSON representation of a SHIP hello message is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomSHIPHelloMessageSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomSHIPHelloMessageSerializer">A delegate to serialize custom event data objects.</param>
        /// <param name="CustomConnectionHelloTypeSerializer">A delegate to serialize custom ConnectionHelloType objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<SHIPHelloMessage>?     CustomSHIPHelloMessageSerializer      = null,
                              CustomJObjectSerializerDelegate<ConnectionHelloType>?  CustomConnectionHelloTypeSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?           CustomCustomDataSerializer            = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("connectionHello",    ConnectionHello.ToJSON(CustomConnectionHelloTypeSerializer,
                                                                                            CustomCustomDataSerializer)),

                           CustomData is not null
                               ? new JProperty("customData",         CustomData.     ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomSHIPHelloMessageSerializer is not null
                       ? CustomSHIPHelloMessageSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
