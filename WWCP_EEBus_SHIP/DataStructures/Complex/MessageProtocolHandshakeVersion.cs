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

    public class MessageProtocolHandshakeVersion(UInt16       Major,
                                                 UInt16       Minor,
                                                 CustomData?  CustomData   = null)

        : ACustomData(CustomData)

    {

        #region Properties

        [Mandatory]
        public UInt16  Major    { get; } = Major;


        [Mandatory]
        public UInt16  Minor    { get; } = Minor;

        #endregion


        #region (static) Parse   (JSON, CustomMessageProtocolHandshakeTypeVersionParser = null)

        /// <summary>
        /// Parse the given JSON representation of a MessageProtocolHandshakeTypeVersion.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomMessageProtocolHandshakeTypeVersionParser">A delegate to parse custom MessageProtocolHandshakeTypeVersions.</param>
        public static MessageProtocolHandshakeVersion Parse(JObject                                                        JSON,
                                                            CustomJObjectParserDelegate<MessageProtocolHandshakeVersion>?  CustomMessageProtocolHandshakeTypeVersionParser   = null)
        {

            if (TryParse(JSON,
                         out var messageProtocolHandshakeVersion,
                         out var errorResponse,
                         CustomMessageProtocolHandshakeTypeVersionParser))
            {
                return messageProtocolHandshakeVersion;
            }

            throw new ArgumentException("The given JSON representation of a MessageProtocolHandshakeTypeVersion is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out MessageProtocolHandshakeTypeVersion, CustomMessageProtocolHandshakeTypeVersionParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a MessageProtocolHandshakeTypeVersion.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="MessageProtocolHandshakeTypeVersion">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                                    JSON,
                                       [NotNullWhen(true)]  out MessageProtocolHandshakeVersion?  MessageProtocolHandshakeTypeVersion,
                                       [NotNullWhen(false)] out String?                           ErrorResponse)

            => TryParse(JSON,
                        out MessageProtocolHandshakeTypeVersion,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a MessageProtocolHandshakeTypeVersion.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="MessageProtocolHandshakeTypeVersion">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomMessageProtocolHandshakeTypeVersionParser">A delegate to parse custom MessageProtocolHandshakeTypeVersions.</param>
        public static Boolean TryParse(JObject                                                        JSON,
                                       [NotNullWhen(true)]  out MessageProtocolHandshakeVersion?      MessageProtocolHandshakeTypeVersion,
                                       [NotNullWhen(false)] out String?                               ErrorResponse,
                                       CustomJObjectParserDelegate<MessageProtocolHandshakeVersion>?  CustomMessageProtocolHandshakeTypeVersionParser)
        {

            try
            {

                MessageProtocolHandshakeTypeVersion = default;

                #region Major         [mandatory]

                if (!JSON.ParseMandatory("major",
                                         "major version number",
                                         out UInt16 Major,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Minor         [mandatory]

                if (!JSON.ParseMandatory("minor",
                                         "minor version number",
                                         out UInt16 Minor,
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


                MessageProtocolHandshakeTypeVersion = new MessageProtocolHandshakeVersion(
                                                          Major,
                                                          Minor,
                                                          CustomData
                                                      );

                if (CustomMessageProtocolHandshakeTypeVersionParser is not null)
                    MessageProtocolHandshakeTypeVersion = CustomMessageProtocolHandshakeTypeVersionParser(JSON,
                                                                                                          MessageProtocolHandshakeTypeVersion);

                return true;

            }
            catch (Exception e)
            {
                MessageProtocolHandshakeTypeVersion  = default;
                ErrorResponse                        = "The given JSON representation of a MessageProtocolHandshakeTypeVersion is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomMessageProtocolHandshakeTypeVersionSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomMessageProtocolHandshakeTypeVersionSerializer">A delegate to serialize custom MessageProtocolHandshakeTypeVersion objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<MessageProtocolHandshakeVersion>?  CustomMessageProtocolHandshakeTypeVersionSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?                       CustomCustomDataSerializer                            = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("major",        Major),
                                 new JProperty("minor",        Minor),

                           CustomData is not null
                               ? new JProperty("customData",   CustomData.ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomMessageProtocolHandshakeTypeVersionSerializer is not null
                       ? CustomMessageProtocolHandshakeTypeVersionSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
