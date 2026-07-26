/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of WWCP EEBUS <https://github.com/OpenChargingCloud/WWCP_EEBUS>
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

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// The PIN state of a SHIP connection (SHIP TS 1.0.1, chapter 13.4.5).
    /// </summary>
    /// <param name="ConnectionPinState">The connection PIN state.</param>
    public class SHIPPinStateMessage(ConnectionPinState  ConnectionPinState)

        : ASHIPMessage(SHIPMessageTypes.CONTROL)

    {

        #region Properties

        /// <summary>
        /// The connection PIN state.
        /// </summary>
        [Mandatory]
        public ConnectionPinState  ConnectionPinState    { get; } = ConnectionPinState;

        #endregion


        #region (static) Parse   (JSON, CustomSHIPPinStateMessageParser = null)

        /// <summary>
        /// Parse the given JSON representation of a SHIP-Pin-State Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomSHIPPinStateMessageParser">A delegate to parse custom SHIP-Pin-State Messages.</param>
        public static SHIPPinStateMessage Parse(JObject                                         JSON,
                                             CustomJObjectParserDelegate<SHIPPinStateMessage>?  CustomSHIPPinStateMessageParser   = null)
        {

            if (TryParse(JSON,
                         out var sHIPPinStateMessage,
                         out var errorResponse,
                         CustomSHIPPinStateMessageParser))
            {
                return sHIPPinStateMessage;
            }

            throw new ArgumentException("The given JSON representation of a SHIP-Pin-State-Message is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out SHIPPinStateMessage, CustomSHIPPinStateMessageParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Pin-State-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPPinStateMessage">The parsed sHIPPinStateMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                     JSON,
                                       [NotNullWhen(true)]  out SHIPPinStateMessage?  SHIPPinStateMessage,
                                       [NotNullWhen(false)] out String?            ErrorResponse)

            => TryParse(JSON,
                        out SHIPPinStateMessage,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Pin-State-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPPinStateMessage">The parsed sHIPPinStateMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomSHIPPinStateMessageParser">A delegate to parse custom SHIP-Pin-State Messages.</param>
        public static Boolean TryParse(JObject                                         JSON,
                                       [NotNullWhen(true)]  out SHIPPinStateMessage?      SHIPPinStateMessage,
                                       [NotNullWhen(false)] out String?                ErrorResponse,
                                       CustomJObjectParserDelegate<SHIPPinStateMessage>?  CustomSHIPPinStateMessageParser)
        {

            try
            {

                SHIPPinStateMessage = default;

                #region ConnectionPinState    [mandatory]

                if (!JSON.ParseMandatoryJSON("connectionPinState",
                                             "connection hello",
                                             SHIP.ConnectionPinState.TryParse,
                                             out ConnectionPinState? ConnectionPinState,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion



                SHIPPinStateMessage = new SHIPPinStateMessage(
                                       ConnectionPinState
                                   );

                if (CustomSHIPPinStateMessageParser is not null)
                    SHIPPinStateMessage = CustomSHIPPinStateMessageParser(JSON,
                                                                    SHIPPinStateMessage);

                return true;

            }
            catch (Exception e)
            {
                SHIPPinStateMessage  = default;
                ErrorResponse     = "The given JSON representation of a SHIP-Pin-State-Message is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomSHIPPinStateMessageSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomSHIPPinStateMessageSerializer">A delegate to serialize custom event data objects.</param>
        /// <param name="CustomConnectionPinStateSerializer">A delegate to serialize custom ConnectionPinState objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<SHIPPinStateMessage>?  CustomSHIPPinStateMessageSerializer      = null,
                              CustomJObjectSerializerDelegate<ConnectionPinState>?   CustomConnectionPinStateSerializer   = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("connectionPinState",    ConnectionPinState.ToJSON(CustomConnectionPinStateSerializer))

                       );

            return CustomSHIPPinStateMessageSerializer is not null
                       ? CustomSHIPPinStateMessageSerializer(this, json)
                       : json;

        }

        #endregion



        #region ToMessageJSON()

        /// <summary>
        /// Return the JSON representation of this message.
        /// </summary>
        public override JObject ToMessageJSON()

            => ToJSON();

        #endregion


    }

}
