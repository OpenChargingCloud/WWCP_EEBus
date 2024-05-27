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
    /// SHIP Handshake Error Message
    /// </summary>
    /// <param name="MessageProtocolHandshakeError">A message protocol handshake error.</param>
    /// <param name="CustomData">An optional custom data object to allow to store any kind of customer specific data.</param>
    public class SHIPHandshakeErrorMessage(MessageProtocolHandshakeError  MessageProtocolHandshakeError,
                                           CustomData?                    CustomData   = null)

        : ASHIPMessage(CustomData)

    {

        #region Properties

        /// <summary>
        /// The message protocol handshake error.
        /// </summary>
        [Mandatory]
        public MessageProtocolHandshakeError  MessageProtocolHandshakeError    { get; } = MessageProtocolHandshakeError;

        #endregion


        #region (static) Parse   (JSON, CustomSHIPHandshakeErrorMessageParser = null)

        /// <summary>
        /// Parse the given JSON representation of a SHIP-Handshake-Error message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomSHIPHandshakeErrorMessageParser">A delegate to parse custom SHIP-Handshake-Error messages.</param>
        public static SHIPHandshakeErrorMessage Parse(JObject                                                  JSON,
                                                      CustomJObjectParserDelegate<SHIPHandshakeErrorMessage>?  CustomSHIPHandshakeErrorMessageParser   = null)
        {

            if (TryParse(JSON,
                         out var shipHandshakeErrorMessage,
                         out var errorResponse,
                         CustomSHIPHandshakeErrorMessageParser))
            {
                return shipHandshakeErrorMessage;
            }

            throw new ArgumentException("The given JSON representation of a SHIP-Handshake-Error message is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out SHIPHandshakeErrorMessage, CustomSHIPHandshakeErrorMessageParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Handshake-Error message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPHandshakeErrorMessage">The parsed shipHandshakeErrorMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                              JSON,
                                       [NotNullWhen(true)]  out SHIPHandshakeErrorMessage?  SHIPHandshakeErrorMessage,
                                       [NotNullWhen(false)] out String?                     ErrorResponse)

            => TryParse(JSON,
                        out SHIPHandshakeErrorMessage,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Handshake-Error message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPHandshakeErrorMessage">The parsed shipHandshakeErrorMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomSHIPHandshakeErrorMessageParser">A delegate to parse custom SHIP-Handshake-Error messages.</param>
        public static Boolean TryParse(JObject                                                  JSON,
                                       [NotNullWhen(true)]  out SHIPHandshakeErrorMessage?      SHIPHandshakeErrorMessage,
                                       [NotNullWhen(false)] out String?                         ErrorResponse,
                                       CustomJObjectParserDelegate<SHIPHandshakeErrorMessage>?  CustomSHIPHandshakeErrorMessageParser)
        {

            try
            {

                SHIPHandshakeErrorMessage = default;

                #region messageProtocolHandshakeError    [mandatory]

                if (!JSON.ParseMandatoryJSON("messageProtocolHandshakeError",
                                             "message protocol handshake error",
                                             SHIP.MessageProtocolHandshakeError.TryParse,
                                             out MessageProtocolHandshakeError? MessageProtocolHandshakeError,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region CustomData                       [optional]

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


                SHIPHandshakeErrorMessage = new SHIPHandshakeErrorMessage(
                                                MessageProtocolHandshakeError,
                                                CustomData
                                            );

                if (CustomSHIPHandshakeErrorMessageParser is not null)
                    SHIPHandshakeErrorMessage = CustomSHIPHandshakeErrorMessageParser(JSON,
                                                                                      SHIPHandshakeErrorMessage);

                return true;

            }
            catch (Exception e)
            {
                SHIPHandshakeErrorMessage  = default;
                ErrorResponse              = "The given JSON representation of a SHIP-Handshake-Error message is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomSHIPHandshakeErrorMessageSerializer = null, CustomMessageProtocolHandshakeErrorTypeSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomSHIPHandshakeErrorMessageSerializer">A delegate to serialize custom event data objects.</param>
        /// <param name="CustomMessageProtocolHandshakeErrorTypeSerializer">A delegate to serialize custom MessageProtocolHandshakeErrorType objects.</param>
        /// <param name="CustomMessageProtocolHandshakeErrorTypeVersionSerializer">A delegate to serialize custom MessageProtocolHandshakeErrorTypeVersion objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<SHIPHandshakeErrorMessage>?      CustomSHIPHandshakeErrorMessageSerializer           = null,
                              CustomJObjectSerializerDelegate<MessageProtocolHandshakeError>?  CustomMessageProtocolHandshakeErrorTypeSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?                     CustomCustomDataSerializer                          = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("messageProtocolHandshakeError",   MessageProtocolHandshakeError.ToJSON(CustomMessageProtocolHandshakeErrorTypeSerializer,
                                                                                                                       CustomCustomDataSerializer)),

                           CustomData is not null
                               ? new JProperty("customData",                      CustomData.                   ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomSHIPHandshakeErrorMessageSerializer is not null
                       ? CustomSHIPHandshakeErrorMessageSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
