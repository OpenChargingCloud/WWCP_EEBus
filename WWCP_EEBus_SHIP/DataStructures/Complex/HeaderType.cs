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

    public class HeaderType(String       ProtocolId,
                            CustomData?  CustomData   = null)

        : ACustomData(CustomData)

    {

        #region Properties

        [Mandatory]
        public String  ProtocolId    { get; } = ProtocolId;

        #endregion


        #region (static) Parse   (JSON, CustomHeaderTypeParser = null)

        /// <summary>
        /// Parse the given JSON representation of a HeaderType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomHeaderTypeParser">A delegate to parse custom HeaderTypes.</param>
        public static HeaderType Parse(JObject                                   JSON,
                                       CustomJObjectParserDelegate<HeaderType>?  CustomHeaderTypeParser   = null)
        {

            if (TryParse(JSON,
                         out var headerType,
                         out var errorResponse,
                         CustomHeaderTypeParser))
            {
                return headerType;
            }

            throw new ArgumentException("The given JSON representation of a HeaderType is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out HeaderType, CustomHeaderTypeParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a HeaderType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="HeaderType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                               JSON,
                                       [NotNullWhen(true)]  out HeaderType?  HeaderType,
                                       [NotNullWhen(false)] out String?      ErrorResponse)

            => TryParse(JSON,
                        out HeaderType,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a HeaderType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="HeaderType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomHeaderTypeParser">A delegate to parse custom HeaderTypes.</param>
        public static Boolean TryParse(JObject                                   JSON,
                                       [NotNullWhen(true)]  out HeaderType?      HeaderType,
                                       [NotNullWhen(false)] out String?          ErrorResponse,
                                       CustomJObjectParserDelegate<HeaderType>?  CustomHeaderTypeParser)
        {

            try
            {

                HeaderType = default;

                #region ProtocolId    [mandatory]

                if (!JSON.ParseMandatoryText("protocolId",
                                             "protocol identification",
                                             out String? ProtocolId,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region CustomData    [optional]

                if (JSON.ParseOptionalJSON("customHeader",
                                           "custom data",
                                           WWCP.OverlayNetworking.CustomData.TryParse,
                                           out CustomData? CustomData,
                                           out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion


                HeaderType = new HeaderType(
                                 ProtocolId,
                                 CustomData
                             );

                if (CustomHeaderTypeParser is not null)
                    HeaderType = CustomHeaderTypeParser(JSON,
                                                      HeaderType);

                return true;

            }
            catch (Exception e)
            {
                HeaderType     = default;
                ErrorResponse  = "The given JSON representation of a HeaderType is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomHeaderTypeSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomHeaderTypeSerializer">A delegate to serialize custom HeaderType objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<HeaderType>?  CustomHeaderTypeSerializer     = null,
                              CustomJObjectSerializerDelegate<CustomData>?  CustomCustomDataSerializer   = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("protocolId",     ProtocolId),

                           CustomData is not null
                               ? new JProperty("customHeader",   CustomData.ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomHeaderTypeSerializer is not null
                       ? CustomHeaderTypeSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
