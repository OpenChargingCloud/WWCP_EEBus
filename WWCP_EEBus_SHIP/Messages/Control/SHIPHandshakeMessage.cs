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

using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// SHIP Handshake Message
    /// </summary>
    /// <param name="MessageProtocolHandshake">A message protocol handshake.</param>
    /// <param name="CustomData">An optional custom data object to allow to store any kind of customer specific data.</param>
    public class SHIPHandshakeMessage(MessageProtocolHandshake  MessageProtocolHandshake,
                                      CustomData?               CustomData   = null)

        : ASHIPMessage(CustomData)

    {

        #region Properties

        /// <summary>
        /// The message protocol handshake.
        /// </summary>
        [Mandatory]
        public MessageProtocolHandshake  MessageProtocolHandshake    { get; } = MessageProtocolHandshake;

        #endregion


        #region (static) Parse   (JSON, CustomSHIPHandshakeMessageParser = null)

        /// <summary>
        /// Parse the given JSON representation of a SHIP-Handshake message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomSHIPHandshakeMessageParser">A delegate to parse custom SHIP-Handshake messages.</param>
        public static SHIPHandshakeMessage Parse(JObject                                             JSON,
                                                 CustomJObjectParserDelegate<SHIPHandshakeMessage>?  CustomSHIPHandshakeMessageParser   = null)
        {

            if (TryParse(JSON,
                         out var shipHandshakeMessage,
                         out var errorResponse,
                         CustomSHIPHandshakeMessageParser))
            {
                return shipHandshakeMessage;
            }

            throw new ArgumentException("The given JSON representation of a SHIP-Handshake message is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out SHIPHandshakeMessage, CustomSHIPHandshakeMessageParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Handshake message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPHandshakeMessage">The parsed shipHandshakeMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                         JSON,
                                       [NotNullWhen(true)]  out SHIPHandshakeMessage?  SHIPHandshakeMessage,
                                       [NotNullWhen(false)] out String?                ErrorResponse)

            => TryParse(JSON,
                        out SHIPHandshakeMessage,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Handshake message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPHandshakeMessage">The parsed shipHandshakeMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomSHIPHandshakeMessageParser">A delegate to parse custom SHIP-Handshake messages.</param>
        public static Boolean TryParse(JObject                                             JSON,
                                       [NotNullWhen(true)]  out SHIPHandshakeMessage?      SHIPHandshakeMessage,
                                       [NotNullWhen(false)] out String?                    ErrorResponse,
                                       CustomJObjectParserDelegate<SHIPHandshakeMessage>?  CustomSHIPHandshakeMessageParser)
        {

            try
            {

                SHIPHandshakeMessage = default;

                #region MessageProtocolHandshake    [mandatory]

                if (!JSON.ParseMandatoryJSON("messageProtocolHandshake",
                                             "message protocol handshake",
                                             SHIP.MessageProtocolHandshake.TryParse,
                                             out MessageProtocolHandshake? MessageProtocolHandshake,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region CustomData                  [optional]

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


                SHIPHandshakeMessage = new SHIPHandshakeMessage(
                                           MessageProtocolHandshake,
                                           CustomData
                                       );

                if (CustomSHIPHandshakeMessageParser is not null)
                    SHIPHandshakeMessage = CustomSHIPHandshakeMessageParser(JSON,
                                                                            SHIPHandshakeMessage);

                return true;

            }
            catch (Exception e)
            {
                SHIPHandshakeMessage  = default;
                ErrorResponse         = "The given JSON representation of a SHIP-Handshake message is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomSHIPHandshakeMessageSerializer = null, CustomMessageProtocolHandshakeTypeSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomSHIPHandshakeMessageSerializer">A delegate to serialize custom event data objects.</param>
        /// <param name="CustomMessageProtocolHandshakeTypeSerializer">A delegate to serialize custom MessageProtocolHandshakeType objects.</param>
        /// <param name="CustomMessageProtocolHandshakeTypeVersionSerializer">A delegate to serialize custom MessageProtocolHandshakeTypeVersion objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<SHIPHandshakeMessage>?             CustomSHIPHandshakeMessageSerializer                  = null,
                              CustomJObjectSerializerDelegate<MessageProtocolHandshake>?         CustomMessageProtocolHandshakeTypeSerializer          = null,
                              CustomJObjectSerializerDelegate<MessageProtocolHandshakeVersion>?  CustomMessageProtocolHandshakeTypeVersionSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?                       CustomCustomDataSerializer                            = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("messageProtocolHandshake",   MessageProtocolHandshake.ToJSON(CustomMessageProtocolHandshakeTypeSerializer,
                                                                                                             CustomMessageProtocolHandshakeTypeVersionSerializer,
                                                                                                             CustomCustomDataSerializer)),

                           CustomData is not null
                               ? new JProperty("customData",                 CustomData.              ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomSHIPHandshakeMessageSerializer is not null
                       ? CustomSHIPHandshakeMessageSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
