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


    public class ConnectionClose(ConnectionClosePhases    Phase,
                                 UInt32?                  MaxTime      = null,
                                 ConnectionCloseReasons?  Reason       = null,
                                 CustomData?              CustomData   = null)

        : ACustomData(CustomData)

    {

        #region Properties

        [Mandatory]
        public ConnectionClosePhases    Phase     { get; } = Phase;

        [Optional]
        public UInt32?                  MaxTime   { get; } = MaxTime;

        [Optional]
        public ConnectionCloseReasons?  Reason    { get; } = Reason;

        #endregion


        #region (static) Parse   (JSON, CustomConnectionCloseTypeParser = null)

        /// <summary>
        /// Parse the given JSON representation of a ConnectionClose.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomConnectionCloseTypeParser">A delegate to parse custom ConnectionCloseTypes.</param>
        public static ConnectionClose Parse(JObject                                        JSON,
                                            CustomJObjectParserDelegate<ConnectionClose>?  CustomConnectionCloseTypeParser   = null)
        {

            if (TryParse(JSON,
                         out var connectionClose,
                         out var errorResponse,
                         CustomConnectionCloseTypeParser))
            {
                return connectionClose;
            }

            throw new ArgumentException("The given JSON representation of a ConnectionClose is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out ConnectionCloseType, CustomConnectionCloseTypeParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a ConnectionClose.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionCloseType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                    JSON,
                                       [NotNullWhen(true)]  out ConnectionClose?  ConnectionCloseType,
                                       [NotNullWhen(false)] out String?           ErrorResponse)

            => TryParse(JSON,
                        out ConnectionCloseType,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a ConnectionClose.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionCloseType">The parsed shipHelloMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomConnectionCloseTypeParser">A delegate to parse custom ConnectionCloseTypes.</param>
        public static Boolean TryParse(JObject                                        JSON,
                                       [NotNullWhen(true)]  out ConnectionClose?      ConnectionCloseType,
                                       [NotNullWhen(false)] out String?               ErrorResponse,
                                       CustomJObjectParserDelegate<ConnectionClose>?  CustomConnectionCloseTypeParser)
        {

            try
            {

                ConnectionCloseType = default;

                #region Phase          [mandatory]

                if (!JSON.ParseMandatory("phase",
                                         "phase",
                                         ConnectionClosePhasesExtensions.TryParse,
                                         out ConnectionClosePhases Phase,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region MaxTime        [optional]

                if (JSON.ParseOptional("maxTime",
                                       "max time",
                                       out UInt32? MaxTime,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion

                #region Reason         [optional]

                if (JSON.ParseOptional("dns",
                                       "dns",
                                       ConnectionCloseReasonsExtensions.TryParse,
                                       out ConnectionCloseReasons? Reason,
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


                ConnectionCloseType = new ConnectionClose(
                                          Phase,
                                          MaxTime,
                                          Reason,
                                          CustomData
                                      );

                if (CustomConnectionCloseTypeParser is not null)
                    ConnectionCloseType = CustomConnectionCloseTypeParser(JSON,
                                                                          ConnectionCloseType);

                return true;

            }
            catch (Exception e)
            {
                ConnectionCloseType  = default;
                ErrorResponse        = "The given JSON representation of a ConnectionClose is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomConnectionCloseTypeSerializer = null, CustomCustomDataSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomConnectionCloseTypeSerializer">A delegate to serialize custom ConnectionCloseType objects.</param>
        /// <param name="CustomCustomDataSerializer">A delegate to serialize CustomData objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<ConnectionClose>?  CustomConnectionCloseTypeSerializer   = null,
                              CustomJObjectSerializerDelegate<CustomData>?       CustomCustomDataSerializer            = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("phase",        Phase.        AsText()),

                           MaxTime.HasValue
                               ? new JProperty("maxTime",      MaxTime.Value)
                               : null,

                           Reason.HasValue
                               ? new JProperty("reason",       Reason. Value.AsText())
                               : null,

                           CustomData is not null
                               ? new JProperty("customData",   CustomData.   ToJSON(CustomCustomDataSerializer))
                               : null

                       );

            return CustomConnectionCloseTypeSerializer is not null
                       ? CustomConnectionCloseTypeSerializer(this, json)
                       : json;

        }

        #endregion


    }

}
