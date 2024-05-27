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

    public class ExtensionType(String?      ExtensionId     = null,
                               Byte[]?      BinaryPayload   = null,
                               String?      TextPayload     = null,
                               CustomData?  CustomData      = null)

        : ACustomData(CustomData)

    {

        #region Properties

        /// <summary>
        /// Extension identification.
        /// </summary>
        [Optional]
        public String?      ExtensionId      { get; } = ExtensionId;

        /// <summary>
        /// Binary payload (HEX encoded).
        /// </summary>
        [Optional]
        public Byte[]?      BinaryPayload    { get; } = BinaryPayload;

        /// <summary>
        /// Text payload.
        /// </summary>
        [Optional]
        public String?      TextPayload      { get; } = TextPayload;

        #endregion


        #region (static) Parse   (JSON, CustomExtensionTypeParser = null)

        /// <summary>
        /// Parse the given JSON representation of a ExtensionType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomExtensionTypeParser">A delegate to parse custom ExtensionTypes.</param>
        public static ExtensionType Parse(JObject                                      JSON,
                                          CustomJObjectParserDelegate<ExtensionType>?  CustomExtensionTypeParser   = null)
        {

            if (TryParse(JSON,
                         out var extensionType,
                         out var errorResponse,
                         CustomExtensionTypeParser))
            {
                return extensionType;
            }

            throw new ArgumentException("The given JSON representation of a ExtensionType is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out ExtensionType, CustomExtensionTypeParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a ExtensionType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ExtensionType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                  JSON,
                                       [NotNullWhen(true)]  out ExtensionType?  ExtensionType,
                                       [NotNullWhen(false)] out String?         ErrorResponse)

            => TryParse(JSON,
                        out ExtensionType,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a ExtensionType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ExtensionType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomExtensionTypeParser">A delegate to parse custom ExtensionTypes.</param>
        public static Boolean TryParse(JObject                                      JSON,
                                       [NotNullWhen(true)]  out ExtensionType?      ExtensionType,
                                       [NotNullWhen(false)] out String?             ErrorResponse,
                                       CustomJObjectParserDelegate<ExtensionType>?  CustomExtensionTypeParser)
        {

            try
            {

                ExtensionType = default;

                #region ExtensionId      [optional]

                var ExtensionId = JSON["extensionId"]?.Value<String>();

                #endregion

                #region BinaryPayload    [optional]

                Byte[]? BinaryPayload     = null;
                var     binaryPayloadHEX  = JSON["binary"]?.Value<String>();

                if (binaryPayloadHEX is not null)
                {
                    if (!ByteExtensions.TryParseHexBytes(binaryPayloadHEX,
                                                         out BinaryPayload,
                                                         out ErrorResponse))
                    {
                        return false;
                    }
                }

                #endregion

                #region TextPayload      [optional]

                var TextPayload = JSON["string"]?.Value<String>();

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


                ExtensionType = new ExtensionType(
                                    ExtensionId,
                                    BinaryPayload,
                                    TextPayload,
                                    CustomData
                                );

                if (CustomExtensionTypeParser is not null)
                    ExtensionType = CustomExtensionTypeParser(JSON,
                                                              ExtensionType);

                return true;

            }
            catch (Exception e)
            {
                ExtensionType  = default;
                ErrorResponse  = "The given JSON representation of a ExtensionType is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomExtensionTypeSerializer = null, CustomCustomDataSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomExtensionTypeSerializer">A delegate to serialize custom ExtensionType objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<ExtensionType>?  CustomExtensionTypeSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?     CustomCustomDataSerializer      = null)
        {

            var json = JSONObject.Create(

                           ExtensionId   is not null
                               ? new JProperty("extensionId",   ExtensionId)
                               : null,

                           BinaryPayload is not null
                               ? new JProperty("binary",        BinaryPayload.ToHexString())
                               : null,

                           TextPayload   is not null
                               ? new JProperty("string",        TextPayload)
                               : null,

                           CustomData    is not null
                               ? new JProperty("customData",    CustomData.ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomExtensionTypeSerializer is not null
                       ? CustomExtensionTypeSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
