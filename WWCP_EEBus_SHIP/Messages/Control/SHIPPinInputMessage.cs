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
    /// A PIN sent to the communication partner (SHIP TS 1.0.1, chapter 13.4.5).
    /// </summary>
    /// <param name="ConnectionPinInput">The connection PIN input.</param>
    public class SHIPPinInputMessage(ConnectionPinInput  ConnectionPinInput)

        : ASHIPMessage(SHIPMessageTypes.CONTROL)

    {

        #region Properties

        /// <summary>
        /// The connection PIN input.
        /// </summary>
        [Mandatory]
        public ConnectionPinInput  ConnectionPinInput    { get; } = ConnectionPinInput;

        #endregion


        #region (static) Parse   (JSON, CustomSHIPPinInputMessageParser = null)

        /// <summary>
        /// Parse the given JSON representation of a SHIP-Pin-Input Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomSHIPPinInputMessageParser">A delegate to parse custom SHIP-Pin-Input Messages.</param>
        public static SHIPPinInputMessage Parse(JObject                                         JSON,
                                             CustomJObjectParserDelegate<SHIPPinInputMessage>?  CustomSHIPPinInputMessageParser   = null)
        {

            if (TryParse(JSON,
                         out var sHIPPinInputMessage,
                         out var errorResponse,
                         CustomSHIPPinInputMessageParser))
            {
                return sHIPPinInputMessage;
            }

            throw new ArgumentException("The given JSON representation of a SHIP-Pin-Input-Message is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out SHIPPinInputMessage, CustomSHIPPinInputMessageParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Pin-Input-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPPinInputMessage">The parsed sHIPPinInputMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                     JSON,
                                       [NotNullWhen(true)]  out SHIPPinInputMessage?  SHIPPinInputMessage,
                                       [NotNullWhen(false)] out String?            ErrorResponse)

            => TryParse(JSON,
                        out SHIPPinInputMessage,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Pin-Input-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPPinInputMessage">The parsed sHIPPinInputMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomSHIPPinInputMessageParser">A delegate to parse custom SHIP-Pin-Input Messages.</param>
        public static Boolean TryParse(JObject                                         JSON,
                                       [NotNullWhen(true)]  out SHIPPinInputMessage?      SHIPPinInputMessage,
                                       [NotNullWhen(false)] out String?                ErrorResponse,
                                       CustomJObjectParserDelegate<SHIPPinInputMessage>?  CustomSHIPPinInputMessageParser)
        {

            try
            {

                SHIPPinInputMessage = default;

                #region ConnectionPinInput    [mandatory]

                if (!JSON.ParseMandatoryJSON("connectionPinInput",
                                             "connection hello",
                                             SHIP.ConnectionPinInput.TryParse,
                                             out ConnectionPinInput? ConnectionPinInput,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion



                SHIPPinInputMessage = new SHIPPinInputMessage(
                                       ConnectionPinInput
                                   );

                if (CustomSHIPPinInputMessageParser is not null)
                    SHIPPinInputMessage = CustomSHIPPinInputMessageParser(JSON,
                                                                    SHIPPinInputMessage);

                return true;

            }
            catch (Exception e)
            {
                SHIPPinInputMessage  = default;
                ErrorResponse     = "The given JSON representation of a SHIP-Pin-Input-Message is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomSHIPPinInputMessageSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomSHIPPinInputMessageSerializer">A delegate to serialize custom event data objects.</param>
        /// <param name="CustomConnectionPinInputSerializer">A delegate to serialize custom ConnectionPinInput objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<SHIPPinInputMessage>?  CustomSHIPPinInputMessageSerializer      = null,
                              CustomJObjectSerializerDelegate<ConnectionPinInput>?   CustomConnectionPinInputSerializer   = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("connectionPinInput",    ConnectionPinInput.ToJSON(CustomConnectionPinInputSerializer))

                       );

            return CustomSHIPPinInputMessageSerializer is not null
                       ? CustomSHIPPinInputMessageSerializer(this, json)
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
