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

    public class MessageProtocolHandshakeError(Byte         Error,
                                               CustomData?  CustomData   = null)

        : ACustomData(CustomData)

    {

        #region Properties

        [Mandatory]
        public Byte  Error    { get; } = Error;

        #endregion


        #region (static) Parse   (JSON, CustomMessageProtocolHandshakeErrorTypeParser = null)

        /// <summary>
        /// Parse the given JSON representation of a MessageProtocolHandshakeError.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomMessageProtocolHandshakeErrorTypeParser">A delegate to parse custom MessageProtocolHandshakeErrors.</param>
        public static MessageProtocolHandshakeError Parse(JObject                                                      JSON,
                                                          CustomJObjectParserDelegate<MessageProtocolHandshakeError>?  CustomMessageProtocolHandshakeErrorTypeParser   = null)
        {

            if (TryParse(JSON,
                         out var messageProtocolHandshakeError,
                         out var errorResponse,
                         CustomMessageProtocolHandshakeErrorTypeParser))
            {
                return messageProtocolHandshakeError;
            }

            throw new ArgumentException("The given JSON representation of a MessageProtocolHandshakeError is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out MessageProtocolHandshakeErrorType, CustomMessageProtocolHandshakeErrorTypeParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a MessageProtocolHandshakeError.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="MessageProtocolHandshakeErrorType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                                  JSON,
                                       [NotNullWhen(true)]  out MessageProtocolHandshakeError?  MessageProtocolHandshakeErrorType,
                                       [NotNullWhen(false)] out String?                         ErrorResponse)

            => TryParse(JSON,
                        out MessageProtocolHandshakeErrorType,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a MessageProtocolHandshakeError.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="MessageProtocolHandshakeError">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomMessageProtocolHandshakeErrorTypeParser">A delegate to parse custom MessageProtocolHandshakeErrors.</param>
        public static Boolean TryParse(JObject                                                      JSON,
                                       [NotNullWhen(true)]  out MessageProtocolHandshakeError?      MessageProtocolHandshakeError,
                                       [NotNullWhen(false)] out String?                             ErrorResponse,
                                       CustomJObjectParserDelegate<MessageProtocolHandshakeError>?  CustomMessageProtocolHandshakeErrorTypeParser)
        {

            try
            {

                MessageProtocolHandshakeError = default;

                #region Error         [mandatory]

                if (!JSON.ParseMandatory("error",
                                         "error",
                                         out Byte Error,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region CustomData    [optional]

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


                MessageProtocolHandshakeError = new MessageProtocolHandshakeError(
                                                    Error,
                                                    CustomData
                                                );

                if (CustomMessageProtocolHandshakeErrorTypeParser is not null)
                    MessageProtocolHandshakeError = CustomMessageProtocolHandshakeErrorTypeParser(JSON,
                                                                                                  MessageProtocolHandshakeError);

                return true;

            }
            catch (Exception e)
            {
                MessageProtocolHandshakeError  = default;
                ErrorResponse                  = "The given JSON representation of a MessageProtocolHandshakeError is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomMessageProtocolHandshakeErrorTypeSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomMessageProtocolHandshakeErrorTypeSerializer">A delegate to serialize custom MessageProtocolHandshakeErrorType objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<MessageProtocolHandshakeError>?  CustomMessageProtocolHandshakeErrorTypeSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?                     CustomCustomDataSerializer                          = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("error",        Error),

                           CustomData is not null
                               ? new JProperty("customData",   CustomData.ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomMessageProtocolHandshakeErrorTypeSerializer is not null
                       ? CustomMessageProtocolHandshakeErrorTypeSerializer(this, json)
                       : json;

        }

        #endregion



    }

}
