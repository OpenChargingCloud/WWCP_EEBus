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

    public class MessageProtocolHandshake(ProtocolHandshakeTypeTypes       HandshakeType,
                                          MessageProtocolHandshakeVersion  Version,
                                          IEnumerable<String>              Formats,
                                          CustomData?                      CustomData   = null)

        : ACustomData(CustomData)

    {

        #region Properties

        [Mandatory]
        public ProtocolHandshakeTypeTypes           HandshakeType    { get; } = HandshakeType;


        [Mandatory]
        public MessageProtocolHandshakeVersion  Version          { get; } = Version;


        [Mandatory]
        public IEnumerable<String>                  Formats          { get; } = Formats;

        #endregion


        #region (static) Parse   (JSON, CustomMessageProtocolHandshakeParser = null)

        /// <summary>
        /// Parse the given JSON representation of a MessageProtocolHandshake.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomMessageProtocolHandshakeParser">A delegate to parse custom MessageProtocolHandshakes.</param>
        public static MessageProtocolHandshake Parse(JObject                                                 JSON,
                                                     CustomJObjectParserDelegate<MessageProtocolHandshake>?  CustomMessageProtocolHandshakeParser   = null)
        {

            if (TryParse(JSON,
                         out var messageProtocolHandshake,
                         out var errorResponse,
                         CustomMessageProtocolHandshakeParser))
            {
                return messageProtocolHandshake;
            }

            throw new ArgumentException("The given JSON representation of a MessageProtocolHandshake is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out MessageProtocolHandshake, CustomMessageProtocolHandshakeParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a MessageProtocolHandshake.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="MessageProtocolHandshake">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                             JSON,
                                       [NotNullWhen(true)]  out MessageProtocolHandshake?  MessageProtocolHandshake,
                                       [NotNullWhen(false)] out String?                    ErrorResponse)

            => TryParse(JSON,
                        out MessageProtocolHandshake,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a MessageProtocolHandshake.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="MessageProtocolHandshake">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomMessageProtocolHandshakeParser">A delegate to parse custom MessageProtocolHandshakes.</param>
        public static Boolean TryParse(JObject                                                 JSON,
                                       [NotNullWhen(true)]  out MessageProtocolHandshake?      MessageProtocolHandshake,
                                       [NotNullWhen(false)] out String?                        ErrorResponse,
                                       CustomJObjectParserDelegate<MessageProtocolHandshake>?  CustomMessageProtocolHandshakeParser)
        {

            try
            {

                MessageProtocolHandshake = default;

                #region HandshakeType    [mandatory]

                if (!JSON.ParseMandatory("handshakeType",
                                         "handshake type",
                                         ProtocolHandshakeTypeTypes.TryParse,
                                         out ProtocolHandshakeTypeTypes HandshakeType,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Version          [mandatory]

                if (!JSON.ParseMandatoryJSON("version",
                                             "handshake type version",
                                             MessageProtocolHandshakeVersion.TryParse,
                                             out MessageProtocolHandshakeVersion? Version,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Formats          [mandatory]

                if (!JSON.ParseMandatory("formats",
                                         "formats",
                                         out IEnumerable<String> Formats,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region CustomData       [optional]

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


                MessageProtocolHandshake = new MessageProtocolHandshake(
                                               HandshakeType,
                                               Version,
                                               Formats,
                                               CustomData
                                           );

                if (CustomMessageProtocolHandshakeParser is not null)
                    MessageProtocolHandshake = CustomMessageProtocolHandshakeParser(JSON,
                                                                                    MessageProtocolHandshake);

                return true;

            }
            catch (Exception e)
            {
                MessageProtocolHandshake  = default;
                ErrorResponse             = "The given JSON representation of a MessageProtocolHandshake is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomMessageProtocolHandshakeSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomMessageProtocolHandshakeSerializer">A delegate to serialize custom MessageProtocolHandshake objects.</param>
        /// <param name="CustomMessageProtocolHandshakeVersionSerializer">A delegate to serialize custom MessageProtocolHandshakeVersion objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<MessageProtocolHandshake>?         CustomMessageProtocolHandshakeSerializer          = null,
                              CustomJObjectSerializerDelegate<MessageProtocolHandshakeVersion>?  CustomMessageProtocolHandshakeVersionSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?                       CustomCustomDataSerializer                        = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("handshakeType",   HandshakeType.ToString()),

                                 new JProperty("version",         Version.      ToJSON(CustomMessageProtocolHandshakeVersionSerializer,
                                                                                       CustomCustomDataSerializer)),

                                 new JProperty("formats",         new JArray(Formats)),

                           CustomData is not null
                               ? new JProperty("customData",      CustomData.ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomMessageProtocolHandshakeSerializer is not null
                       ? CustomMessageProtocolHandshakeSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
