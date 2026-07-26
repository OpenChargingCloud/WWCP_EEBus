/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// The PIN state of a SHIP connection (SHIP TS 1.0.1, chapter 13.4.5).
    /// </summary>
    /// <param name="PinState">The PIN state.</param>
    /// <param name="InputPermission">Whether the sender currently accepts a PIN input.</param>
    public class ConnectionPinState(PinState             PinState,
                                    PinInputPermission?  InputPermission   = null)
    {

        #region Properties

        /// <summary>
        /// The PIN state.
        /// </summary>
        [Mandatory]
        public PinState             PinState           { get; } = PinState;

        /// <summary>
        /// Whether the sender currently accepts a PIN input.
        /// </summary>
        [Optional]
        public PinInputPermission?  InputPermission    { get; } = InputPermission;

        #endregion


        #region (static) Parse   (JSON, CustomConnectionPinStateParser = null)

        /// <summary>
        /// Parse the given JSON representation of a connection PIN state.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomConnectionPinStateParser">A delegate to parse custom connection PIN states.</param>
        public static ConnectionPinState Parse(JObject                                           JSON,
                                               CustomJObjectParserDelegate<ConnectionPinState>?  CustomConnectionPinStateParser   = null)
        {

            if (TryParse(JSON,
                         out var connectionPinState,
                         out var errorResponse,
                         CustomConnectionPinStateParser))
            {
                return connectionPinState;
            }

            throw new ArgumentException("The given JSON representation of a connection PIN state is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out ConnectionPinState, out ErrorResponse, CustomConnectionPinStateParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a connection PIN state.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionPinState">The parsed connection PIN state.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                       JSON,
                                       [NotNullWhen(true)]  out ConnectionPinState?  ConnectionPinState,
                                       [NotNullWhen(false)] out String?              ErrorResponse)

            => TryParse(JSON,
                        out ConnectionPinState,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a connection PIN state.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionPinState">The parsed connection PIN state.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomConnectionPinStateParser">A delegate to parse custom connection PIN states.</param>
        public static Boolean TryParse(JObject                                           JSON,
                                       [NotNullWhen(true)]  out ConnectionPinState?      ConnectionPinState,
                                       [NotNullWhen(false)] out String?                  ErrorResponse,
                                       CustomJObjectParserDelegate<ConnectionPinState>?  CustomConnectionPinStateParser)
        {

            try
            {

                ConnectionPinState = default;

                #region PinState           [mandatory]

                if (!JSON.ParseMandatory("pinState",
                                         "PIN state",
                                         SHIP.PinState.TryParse,
                                         out PinState PinState,
                                         out ErrorResponse))
                {
                    return false;
                }

                #endregion

                #region InputPermission    [optional]

                if (JSON.ParseOptional("inputPermission",
                                       "input permission",
                                       PinInputPermission.TryParse,
                                       out PinInputPermission? InputPermission,
                                       out ErrorResponse))
                {
                    if (ErrorResponse is not null)
                        return false;
                }

                #endregion


                ConnectionPinState = new ConnectionPinState(
                                         PinState,
                                         InputPermission
                                     );

                if (CustomConnectionPinStateParser is not null)
                    ConnectionPinState = CustomConnectionPinStateParser(JSON,
                                                                        ConnectionPinState);

                return true;

            }
            catch (Exception e)
            {
                ConnectionPinState  = default;
                ErrorResponse       = "The given JSON representation of a connection PIN state is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomConnectionPinStateSerializer = null)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomConnectionPinStateSerializer">A delegate to serialize custom connection PIN states.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<ConnectionPinState>?  CustomConnectionPinStateSerializer   = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("pinState",          PinState.             ToString()),

                           InputPermission.HasValue
                               ? new JProperty("inputPermission",   InputPermission.Value.ToString())
                               : null

                       );

            return CustomConnectionPinStateSerializer is not null
                       ? CustomConnectionPinStateSerializer(this, json)
                       : json;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{PinState}{(InputPermission.HasValue ? $", input permission: {InputPermission}" : "")}";

        #endregion

    }

}
