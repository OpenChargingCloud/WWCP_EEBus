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

    public class ConnectionHello(ConnectionHelloPhase  Phase,
                                 UInt32?               Waiting               = null,
                                 Boolean?              ProlongationRequest   = null,
                                 CustomData?           CustomData            = null)

        : ACustomData(CustomData)

    {

        #region Properties

        [Mandatory]
        public ConnectionHelloPhase  Phase                  { get; } = Phase;


        [Optional]
        public UInt32?               Waiting                { get; } = Waiting;


        [Optional]
        public Boolean?              ProlongationRequest    { get; } = ProlongationRequest;

        #endregion


        #region (static) Parse   (JSON, CustomConnectionHelloParser = null)

        /// <summary>
        /// Parse the given JSON representation of a ConnectionHello.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomConnectionHelloParser">A delegate to parse custom ConnectionHellos.</param>
        public static ConnectionHello Parse(JObject                                        JSON,
                                            CustomJObjectParserDelegate<ConnectionHello>?  CustomConnectionHelloParser   = null)
        {

            if (TryParse(JSON,
                         out var connectionHello,
                         out var errorResponse,
                         CustomConnectionHelloParser))
            {
                return connectionHello;
            }

            throw new ArgumentException("The given JSON representation of a ConnectionHello is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out ConnectionHello, CustomConnectionHelloParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a ConnectionHello.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionHello">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                    JSON,
                                       [NotNullWhen(true)]  out ConnectionHello?  ConnectionHello,
                                       [NotNullWhen(false)] out String?           ErrorResponse)

            => TryParse(JSON,
                        out ConnectionHello,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a ConnectionHello.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionHello">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomConnectionHelloParser">A delegate to parse custom ConnectionHellos.</param>
        public static Boolean TryParse(JObject                                        JSON,
                                       [NotNullWhen(true)]  out ConnectionHello?      ConnectionHello,
                                       [NotNullWhen(false)] out String?               ErrorResponse,
                                       CustomJObjectParserDelegate<ConnectionHello>?  CustomConnectionHelloParser)
        {

            try
            {

                ConnectionHello = default;

                #region Phase                  [mandatory]

                if (!JSON.ParseMandatory("phase",
                                         "phase",
                                         ConnectionHelloPhase.TryParse,
                                         out ConnectionHelloPhase Phase,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region Waiting                [optional]

                if (JSON.ParseOptional("waiting",
                                       "waiting",
                                       out UInt32? Waiting,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region ProlongationRequest    [optional]

                if (JSON.ParseOptional("prolongationRequest",
                                       "prolongation request",
                                       out Boolean? ProlongationRequest,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region CustomData             [optional]

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


                ConnectionHello = new ConnectionHello(
                                          Phase,
                                          Waiting,
                                          ProlongationRequest,
                                          CustomData
                                      );

                if (CustomConnectionHelloParser is not null)
                    ConnectionHello = CustomConnectionHelloParser(JSON,
                                                                  ConnectionHello);

                return true;

            }
            catch (Exception e)
            {
                ConnectionHello  = default;
                ErrorResponse    = "The given JSON representation of a ConnectionHello is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomConnectionHelloSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomConnectionHelloSerializer">A delegate to serialize custom ConnectionHello objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<ConnectionHello>?  CustomConnectionHelloSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?       CustomCustomDataSerializer        = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("phase",                 Phase.              ToString()),

                           Waiting.HasValue
                               ? new JProperty("waiting",               Waiting.            Value)
                               : null,

                           ProlongationRequest.HasValue
                               ? new JProperty("prolongationRequest",   ProlongationRequest.Value)
                               : null,

                           CustomData is not null
                               ? new JProperty("customData",            CustomData.         ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomConnectionHelloSerializer is not null
                       ? CustomConnectionHelloSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
