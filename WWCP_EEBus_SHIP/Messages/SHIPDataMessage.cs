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

    /// <summary>
    /// SHIP Data Message
    /// </summary>
    /// <param name="Data">A transported data.</param>
    /// <param name="CustomData">An optional custom data object allowing to store any kind of customer specific data.</param>
    public class SHIPDataMessage(DataType     Data,
                                 CustomData?  CustomData   = null)

        : ASHIPMessage(CustomData)

    {

        #region Properties

        /// <summary>
        /// The transported data.
        /// </summary>
        [Mandatory]
        public DataType  Data    { get; } = Data;

        #endregion


        #region (static) Parse   (JSON, CustomSHIPDataMessageParser = null)

        /// <summary>
        /// Parse the given JSON representation of a SHIP-Data message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomSHIPDataMessageParser">A delegate to parse custom SHIP Data Messages.</param>
        public static SHIPDataMessage Parse(JObject                                        JSON,
                                            CustomJObjectParserDelegate<SHIPDataMessage>?  CustomSHIPDataMessageParser   = null)
        {

            if (TryParse(JSON,
                         out var shipDataMessage,
                         out var errorResponse,
                         CustomSHIPDataMessageParser))
            {
                return shipDataMessage;
            }

            throw new ArgumentException("The given JSON representation of a SHIP-Data-Message is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out SHIPDataMessage, CustomSHIPDataMessageParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Data-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPDataMessage">The parsed shipDataMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                    JSON,
                                       [NotNullWhen(true)]  out SHIPDataMessage?  SHIPDataMessage,
                                       [NotNullWhen(false)] out String?           ErrorResponse)

            => TryParse(JSON,
                        out SHIPDataMessage,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Data-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPDataMessage">The parsed shipDataMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomSHIPDataMessageParser">A delegate to parse custom SHIP Data Messages.</param>
        public static Boolean TryParse(JObject                                        JSON,
                                       [NotNullWhen(true)]  out SHIPDataMessage?      SHIPDataMessage,
                                       [NotNullWhen(false)] out String?               ErrorResponse,
                                       CustomJObjectParserDelegate<SHIPDataMessage>?  CustomSHIPDataMessageParser)
        {

            try
            {

                SHIPDataMessage = default;

                #region Data          [mandatory]

                if (!JSON.ParseMandatoryJSON("data",
                                             "data",
                                             DataType.TryParse,
                                             out DataType? Data,
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


                SHIPDataMessage = new SHIPDataMessage(
                                      Data,
                                      CustomData
                                  );

                if (CustomSHIPDataMessageParser is not null)
                    SHIPDataMessage = CustomSHIPDataMessageParser(JSON,
                                                                  SHIPDataMessage);

                return true;

            }
            catch (Exception e)
            {
                SHIPDataMessage  = default;
                ErrorResponse    = "The given JSON representation of a SHIP-Data-Message is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomSHIPDataMessageSerializer = null, CustomHeaderTypeSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomSHIPDataMessageSerializer">A delegate to serialize custom event data objects.</param>
        /// <param name="CustomHeaderTypeSerializer">A delegate to serialize custom HeaderType objects.</param>
        /// <param name="CustomDataTypeSerializer">A delegate to serialize custom DataType objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<SHIPDataMessage>?  CustomSHIPDataMessageSerializer   = null,
                              CustomJObjectSerializerDelegate<HeaderType>?       CustomHeaderTypeSerializer        = null,
                              CustomJObjectSerializerDelegate<DataType>?         CustomDataTypeSerializer          = null,
                              CustomJObjectSerializerDelegate<CustomData>?       CustomCustomDataSerializer        = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("data",         Data.      ToJSON(CustomDataTypeSerializer,
                                                                                 CustomHeaderTypeSerializer,
                                                                                 CustomCustomDataSerializer)),

                           CustomData is not null
                               ? new JProperty("customData",   CustomData.ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomSHIPDataMessageSerializer is not null
                       ? CustomSHIPDataMessageSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
