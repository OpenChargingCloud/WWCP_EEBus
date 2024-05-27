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

    public class AccessMethodsTypeDNS(String       URI,
                                      CustomData?  CustomData   = null)

        : ACustomData(CustomData)

    {

        #region Properties

        [Mandatory]
        public String  URI    { get; } = URI;

        #endregion


        #region (static) Parse   (JSON, CustomAccessMethodsTypeDNSParser = null)

        /// <summary>
        /// Parse the given JSON representation of a AccessMethodsTypeDNS.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomAccessMethodsTypeDNSParser">A delegate to parse custom AccessMethodsTypeDNSs.</param>
        public static AccessMethodsTypeDNS Parse(JObject                                             JSON,
                                                 CustomJObjectParserDelegate<AccessMethodsTypeDNS>?  CustomAccessMethodsTypeDNSParser   = null)
        {

            if (TryParse(JSON,
                         out var accessMethodsTypeDNS,
                         out var errorResponse,
                         CustomAccessMethodsTypeDNSParser))
            {
                return accessMethodsTypeDNS;
            }

            throw new ArgumentException("The given JSON representation of a AccessMethodsTypeDNS is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out AccessMethodsTypeDNS, CustomAccessMethodsTypeDNSParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a AccessMethodsTypeDNS.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="AccessMethodsTypeDNS">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                         JSON,
                                       [NotNullWhen(true)]  out AccessMethodsTypeDNS?  AccessMethodsTypeDNS,
                                       [NotNullWhen(false)] out String?                ErrorResponse)

            => TryParse(JSON,
                        out AccessMethodsTypeDNS,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a AccessMethodsTypeDNS.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="AccessMethodsTypeDNS">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomAccessMethodsTypeDNSParser">A delegate to parse custom AccessMethodsTypeDNSs.</param>
        public static Boolean TryParse(JObject                                             JSON,
                                       [NotNullWhen(true)]  out AccessMethodsTypeDNS?      AccessMethodsTypeDNS,
                                       [NotNullWhen(false)] out String?                    ErrorResponse,
                                       CustomJObjectParserDelegate<AccessMethodsTypeDNS>?  CustomAccessMethodsTypeDNSParser)
        {

            try
            {

                AccessMethodsTypeDNS = default;

                #region URI           [mandatory]

                if (!JSON.ParseMandatoryText("uri",
                                             "URI",
                                             out String? URI,
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


                AccessMethodsTypeDNS = new AccessMethodsTypeDNS(
                                           URI,
                                           CustomData
                                       );

                if (CustomAccessMethodsTypeDNSParser is not null)
                    AccessMethodsTypeDNS = CustomAccessMethodsTypeDNSParser(JSON,
                                                                            AccessMethodsTypeDNS);

                return true;

            }
            catch (Exception e)
            {
                AccessMethodsTypeDNS  = default;
                ErrorResponse         = "The given JSON representation of a AccessMethodsTypeDNS is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomAccessMethodsTypeDNSSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomAccessMethodsTypeDNSSerializer">A delegate to serialize custom AccessMethodsTypeDNS objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<AccessMethodsTypeDNS>?  CustomAccessMethodsTypeDNSSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?            CustomCustomDataSerializer             = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("uri",          URI),

                           CustomData is not null
                               ? new JProperty("customData",   CustomData.ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomAccessMethodsTypeDNSSerializer is not null
                       ? CustomAccessMethodsTypeDNSSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
