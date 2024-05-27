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
    /// SHIP-Access-Methods Message
    /// </summary>
    /// <param name="AccessMethodsRequest">An access methods request.</param>
    /// <param name="CustomData">An optional custom data object to allow to store any kind of customer specific data.</param>
    public class SHIPAccessMethodsMessage(AccessMethodsType  AccessMethodsRequest,
                                          CustomData?        CustomData   = null)

        : ASHIPMessage(CustomData)

    {

        #region Properties

        /// <summary>
        /// The access methods request.
        /// </summary>
        [Mandatory]
        public AccessMethodsType  AccessMethodsRequest    { get; } = AccessMethodsRequest;

        #endregion


        #region (static) Parse   (JSON, CustomSHIPAccessMethodsMessageParser = null)

        /// <summary>
        /// Parse the given JSON representation of a SHIP-Access-Methods message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomSHIPAccessMethodsMessageParser">A delegate to parse custom SHIP-Access-Methods messages.</param>
        public static SHIPAccessMethodsMessage Parse(JObject                                                 JSON,
                                                     CustomJObjectParserDelegate<SHIPAccessMethodsMessage>?  CustomSHIPAccessMethodsMessageParser   = null)
        {

            if (TryParse(JSON,
                         out var shipAccessMethodsMessage,
                         out var errorResponse,
                         CustomSHIPAccessMethodsMessageParser))
            {
                return shipAccessMethodsMessage;
            }

            throw new ArgumentException("The given JSON representation of a SHIP-Access-Methods message is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out SHIPAccessMethodsMessage, CustomSHIPAccessMethodsMessageParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Access-Methods message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPAccessMethodsMessage">The parsed shipAccessMethodsMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                             JSON,
                                       [NotNullWhen(true)]  out SHIPAccessMethodsMessage?  SHIPAccessMethodsMessage,
                                       [NotNullWhen(false)] out String?                    ErrorResponse)

            => TryParse(JSON,
                        out SHIPAccessMethodsMessage,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Access-Methods message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPAccessMethodsMessage">The parsed shipAccessMethodsMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomSHIPAccessMethodsMessageParser">A delegate to parse custom SHIP-Access-Methods messages.</param>
        public static Boolean TryParse(JObject                                                 JSON,
                                       [NotNullWhen(true)]  out SHIPAccessMethodsMessage?      SHIPAccessMethodsMessage,
                                       [NotNullWhen(false)] out String?                        ErrorResponse,
                                       CustomJObjectParserDelegate<SHIPAccessMethodsMessage>?  CustomSHIPAccessMethodsMessageParser)
        {

            try
            {

                SHIPAccessMethodsMessage = default;

                #region AccessMethodsRequest    [mandatory]

                if (!JSON.ParseMandatoryJSON("accessMethodsRequest",
                                             "access methods request",
                                             AccessMethodsType.TryParse,
                                             out AccessMethodsType? AccessMethodsRequest,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region CustomData              [optional]

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


                SHIPAccessMethodsMessage = new SHIPAccessMethodsMessage(
                                                AccessMethodsRequest,
                                                CustomData
                                            );

                if (CustomSHIPAccessMethodsMessageParser is not null)
                    SHIPAccessMethodsMessage = CustomSHIPAccessMethodsMessageParser(JSON,
                                                                                    SHIPAccessMethodsMessage);

                return true;

            }
            catch (Exception e)
            {
                SHIPAccessMethodsMessage  = default;
                ErrorResponse             = "The given JSON representation of a SHIP-Access-Methods message is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomSHIPAccessMethodsMessageSerializer = null, CustomAccessMethodsTypeSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomSHIPAccessMethodsMessageSerializer">A delegate to serialize custom event data objects.</param>
        /// <param name="CustomAccessMethodsTypeSerializer">A delegate to serialize custom AccessMethodsType objects.</param>
        /// <param name="CustomAccessMethodsTypeDNSSerializer">A delegate to serialize custom AccessMethodsTypeDNS objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<SHIPAccessMethodsMessage>?  CustomSHIPAccessMethodsMessageSerializer   = null,
                              CustomJObjectSerializerDelegate<AccessMethodsType>?         CustomAccessMethodsTypeSerializer          = null,
                              CustomJObjectSerializerDelegate<AccessMethodsTypeDNS>?      CustomAccessMethodsTypeDNSSerializer       = null,                              
                              CustomJObjectSerializerDelegate<CustomData>?                CustomCustomDataSerializer                 = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("accessMethodsRequest",   AccessMethodsRequest.ToJSON(CustomAccessMethodsTypeSerializer,
                                                                                                     CustomAccessMethodsTypeDNSSerializer,
                                                                                                     CustomCustomDataSerializer)),

                           CustomData is not null
                               ? new JProperty("customData",             CustomData.          ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomSHIPAccessMethodsMessageSerializer is not null
                       ? CustomSHIPAccessMethodsMessageSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
