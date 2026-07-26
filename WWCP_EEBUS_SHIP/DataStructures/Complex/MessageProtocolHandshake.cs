/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;


#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// The SHIP message protocol handshake (SHIP TS 1.0.1, chapter 13.4.4.2).
    /// </summary>
    /// <param name="HandshakeType">Whether the maximum supported version is announced, or one is selected.</param>
    /// <param name="Version">The announced or selected protocol version.</param>
    /// <param name="Formats">The announced or selected message protocol formats.</param>
    public class MessageProtocolHandshake(ProtocolHandshakeTypeTypes           HandshakeType,
                                          MessageProtocolHandshakeVersion      Version,
                                          IEnumerable<MessageProtocolFormat>   Formats)

    {

        #region Properties

        /// <summary>
        /// Whether the maximum supported version is announced, or one is selected.
        /// </summary>
        [Mandatory]
        public ProtocolHandshakeTypeTypes          HandshakeType    { get; } = HandshakeType;

        /// <summary>
        /// The announced or selected protocol version.
        /// </summary>
        [Mandatory]
        public MessageProtocolHandshakeVersion     Version          { get; } = Version;

        /// <summary>
        /// The announced or selected message protocol formats.
        /// </summary>
        [Mandatory]
        public IEnumerable<MessageProtocolFormat>  Formats          { get; } = Formats.Distinct();

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

                // The XSD wraps the repeated "format" elements within a "formats"
                // complex type: { "formats": { "format": [ "JSON-UTF8" ] } }
                if (JSON["formats"] is not JObject formatsJSON)
                {
                    ErrorResponse = "The given message protocol formats are missing or invalid!";
                    return false;
                }

                if (formatsJSON["format"] is not JArray formatArray ||
                    formatArray.Count == 0)
                {
                    ErrorResponse = "The given message protocol formats must contain at least one format!";
                    return false;
                }

                var Formats = new List<MessageProtocolFormat>();

                foreach (var format in formatArray)
                {

                    if (format.Type != JTokenType.String ||
                        !MessageProtocolFormat.TryParse(format.Value<String>() ?? "", out var messageProtocolFormat))
                    {
                        ErrorResponse = $"The message protocol format '{format}' is invalid!";
                        return false;
                    }

                    Formats.Add(messageProtocolFormat);

                }

                #endregion



                MessageProtocolHandshake = new MessageProtocolHandshake(
                                               HandshakeType,
                                               Version,
                                               Formats
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
        public JObject ToJSON(CustomJObjectSerializerDelegate<MessageProtocolHandshake>?         CustomMessageProtocolHandshakeSerializer          = null,
                              CustomJObjectSerializerDelegate<MessageProtocolHandshakeVersion>?  CustomMessageProtocolHandshakeVersionSerializer   = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("handshakeType",   HandshakeType.ToString()),

                                 new JProperty("version",         Version.      ToJSON(CustomMessageProtocolHandshakeVersionSerializer)),

                                 new JProperty("formats",         new JObject(
                                                                      new JProperty("format",
                                                                          new JArray(Formats.Select(format => format.ToString()))
                                                                      )
                                                                  ))

                       );

            return CustomMessageProtocolHandshakeSerializer is not null
                       ? CustomMessageProtocolHandshakeSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
