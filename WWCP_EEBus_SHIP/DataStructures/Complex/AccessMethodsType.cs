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

    public class AccessMethodsType(SHIP_Id                Id,
                                   MDNS_Id?               DNS_SD_MDNS,
                                   AccessMethodsTypeDNS?  DNS,
                                   CustomData?            CustomData   = null)

        : ACustomData(CustomData)

    {

        #region Properties

        [Mandatory]
        public SHIP_Id                Id             { get; } = Id;

        [Optional]
        public MDNS_Id?               DNS_SD_MDNS    { get; } = DNS_SD_MDNS;

        [Optional]
        public AccessMethodsTypeDNS?  DNS            { get; } = DNS;

        #endregion


        #region (static) Parse   (JSON, CustomAccessMethodsTypeParser = null)

        /// <summary>
        /// Parse the given JSON representation of a AccessMethodsType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomAccessMethodsTypeParser">A delegate to parse custom AccessMethodsTypes.</param>
        public static AccessMethodsType Parse(JObject                                          JSON,
                                              CustomJObjectParserDelegate<AccessMethodsType>?  CustomAccessMethodsTypeParser   = null)
        {

            if (TryParse(JSON,
                         out var accessMethodsType,
                         out var errorResponse,
                         CustomAccessMethodsTypeParser))
            {
                return accessMethodsType;
            }

            throw new ArgumentException("The given JSON representation of a AccessMethodsType is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out AccessMethodsType, CustomAccessMethodsTypeParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a AccessMethodsType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="AccessMethodsType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                      JSON,
                                       [NotNullWhen(true)]  out AccessMethodsType?  AccessMethodsType,
                                       [NotNullWhen(false)] out String?             ErrorResponse)

            => TryParse(JSON,
                        out AccessMethodsType,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a AccessMethodsType.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="AccessMethodsType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomAccessMethodsTypeParser">A delegate to parse custom AccessMethodsTypes.</param>
        public static Boolean TryParse(JObject                                          JSON,
                                       [NotNullWhen(true)]  out AccessMethodsType?      AccessMethodsType,
                                       [NotNullWhen(false)] out String?                 ErrorResponse,
                                       CustomJObjectParserDelegate<AccessMethodsType>?  CustomAccessMethodsTypeParser)
        {

            try
            {

                AccessMethodsType = default;

                #region Id             [mandatory]

                if (!JSON.ParseMandatory("id",
                                         "identification",
                                         SHIP_Id.TryParse,
                                         out SHIP_Id Id,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region DNS_SD_MDNS    [optional]

                if (JSON.ParseOptional("dnsSd_mDns",
                                       "waiting",
                                       MDNS_Id.TryParse,
                                       out MDNS_Id? DNS_SD_MDNS,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region DNS            [optional]

                if (JSON.ParseOptionalJSON("dns",
                                           "dns",
                                           AccessMethodsTypeDNS.TryParse,
                                           out AccessMethodsTypeDNS? DNS,
                                           out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region CustomData     [optional]

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


                AccessMethodsType = new AccessMethodsType(
                                        Id,
                                        DNS_SD_MDNS,
                                        DNS,
                                        CustomData
                                    );

                if (CustomAccessMethodsTypeParser is not null)
                    AccessMethodsType = CustomAccessMethodsTypeParser(JSON,
                                                                      AccessMethodsType);

                return true;

            }
            catch (Exception e)
            {
                AccessMethodsType  = default;
                ErrorResponse      = "The given JSON representation of a AccessMethodsType is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomAccessMethodsTypeSerializer = null, CustomAccessMethodsTypeDNSSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomAccessMethodsTypeSerializer">A delegate to serialize custom AccessMethodsType objects.</param>
        /// <param name="CustomAccessMethodsTypeDNSSerializer">A delegate to serialize custom AccessMethodsTypeDNS objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<AccessMethodsType>?     CustomAccessMethodsTypeSerializer      = null,
                              CustomJObjectSerializerDelegate<AccessMethodsTypeDNS>?  CustomAccessMethodsTypeDNSSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?            CustomCustomDataSerializer             = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("id",           Id.         ToString()),

                           DNS_SD_MDNS.HasValue
                               ? new JProperty("dnsSd_mDns",   DNS_SD_MDNS.ToString())
                               : null,

                           DNS is not null
                               ? new JProperty("dns",          DNS.        ToJSON(CustomAccessMethodsTypeDNSSerializer,
                                                                                  CustomCustomDataSerializer))
                               : null,

                           CustomData is not null
                               ? new JProperty("customData",   CustomData. ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomAccessMethodsTypeSerializer is not null
                       ? CustomAccessMethodsTypeSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
