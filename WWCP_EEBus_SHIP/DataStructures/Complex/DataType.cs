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

    public class DataType(HeaderType      Header,
                          JToken          Payload,
                          ExtensionType?  Extension    = null,
                          CustomData?     CustomData   = null)

        : ACustomData(CustomData)

    {

        #region Properties

        [Mandatory]
        public HeaderType      Header       { get; } = Header;


        [Mandatory]
        public JToken          Payload      { get; } = Payload;


        [Optional]
        public ExtensionType?  Extension    { get; } = Extension;

        #endregion


        #region (static) Parse   (JSON, CustomDataTypeParser = null)

        /// <summary>
        /// Parse the given JSON representation of a DataType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomDataTypeParser">A delegate to parse custom DataTypes.</param>
        public static DataType Parse(JObject                                 JSON,
                                     CustomJObjectParserDelegate<DataType>?  CustomDataTypeParser   = null)
        {

            if (TryParse(JSON,
                         out var dataType,
                         out var errorResponse,
                         CustomDataTypeParser))
            {
                return dataType;
            }

            throw new ArgumentException("The given JSON representation of a DataType is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out DataType, CustomDataTypeParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a DataType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="DataType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                             JSON,
                                       [NotNullWhen(true)]  out DataType?  DataType,
                                       [NotNullWhen(false)] out String?    ErrorResponse)

            => TryParse(JSON,
                        out DataType,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a DataType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="DataType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomDataTypeParser">A delegate to parse custom DataTypes.</param>
        public static Boolean TryParse(JObject                                 JSON,
                                       [NotNullWhen(true)]  out DataType?      DataType,
                                       [NotNullWhen(false)] out String?        ErrorResponse,
                                       CustomJObjectParserDelegate<DataType>?  CustomDataTypeParser)
        {

            try
            {

                DataType = default;

                #region Header        [mandatory]

                if (!JSON.ParseMandatoryJSON("header",
                                             "data header",
                                             HeaderType.TryParse,
                                             out HeaderType? Header,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Payload       [mandatory]

                var Payload = JSON["payload"];

                if (Payload is null)
                {
                    ErrorResponse = "The 'payload' must not be null!";
                    return false;
                }

                #endregion

                #region Extension     [optional]

                if (JSON.ParseOptionalJSON("extension",
                                           "extension",
                                           ExtensionType.TryParse,
                                           out ExtensionType? Extension,
                                           out ErrorResponse))
                {
                    if (ErrorResponse is not null)
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


                DataType = new DataType(
                               Header,
                               Payload,
                               Extension,
                               CustomData
                           );

                if (CustomDataTypeParser is not null)
                    DataType = CustomDataTypeParser(JSON,
                                                    DataType);

                return true;

            }
            catch (Exception e)
            {
                DataType       = default;
                ErrorResponse  = "The given JSON representation of a DataType is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomDataTypeSerializer = null, CustomHeaderTypeSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomDataTypeSerializer">A delegate to serialize custom DataType objects.</param>
        /// <param name="CustomHeaderTypeSerializer">A delegate to serialize custom HeaderType objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<DataType>?    CustomDataTypeSerializer     = null,
                              CustomJObjectSerializerDelegate<HeaderType>?  CustomHeaderTypeSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?  CustomCustomDataSerializer   = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("header",       Header.    ToJSON(CustomHeaderTypeSerializer,
                                                                                 CustomCustomDataSerializer)),

                                 new JProperty("payload",      Payload),

                           Extension is not null
                               ? new JProperty("extension",    Extension. ToJSON())
                               : null,

                           CustomData is not null
                               ? new JProperty("customData",   CustomData.ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomDataTypeSerializer is not null
                       ? CustomDataTypeSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
