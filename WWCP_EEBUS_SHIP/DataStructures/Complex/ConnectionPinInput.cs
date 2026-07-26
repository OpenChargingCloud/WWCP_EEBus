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
    /// A PIN sent to the communication partner (SHIP TS 1.0.1, chapter 13.4.5).
    /// </summary>
    /// <param name="Pin">The PIN.</param>
    public class ConnectionPinInput(String Pin)
    {

        #region Properties

        /// <summary>
        /// The PIN.
        /// </summary>
        [Mandatory]
        public String  Pin    { get; } = Pin;

        #endregion


        #region (static) Parse   (JSON, CustomConnectionPinInputParser = null)

        /// <summary>
        /// Parse the given JSON representation of a connection PIN input.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomConnectionPinInputParser">A delegate to parse custom connection PIN inputs.</param>
        public static ConnectionPinInput Parse(JObject                                           JSON,
                                               CustomJObjectParserDelegate<ConnectionPinInput>?  CustomConnectionPinInputParser   = null)
        {

            if (TryParse(JSON,
                         out var connectionPinInput,
                         out var errorResponse,
                         CustomConnectionPinInputParser))
            {
                return connectionPinInput;
            }

            throw new ArgumentException("The given JSON representation of a connection PIN input is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out ConnectionPinInput, out ErrorResponse, CustomConnectionPinInputParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a connection PIN input.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionPinInput">The parsed connection PIN input.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                       JSON,
                                       [NotNullWhen(true)]  out ConnectionPinInput?  ConnectionPinInput,
                                       [NotNullWhen(false)] out String?              ErrorResponse)

            => TryParse(JSON,
                        out ConnectionPinInput,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a connection PIN input.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionPinInput">The parsed connection PIN input.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomConnectionPinInputParser">A delegate to parse custom connection PIN inputs.</param>
        public static Boolean TryParse(JObject                                           JSON,
                                       [NotNullWhen(true)]  out ConnectionPinInput?      ConnectionPinInput,
                                       [NotNullWhen(false)] out String?                  ErrorResponse,
                                       CustomJObjectParserDelegate<ConnectionPinInput>?  CustomConnectionPinInputParser)
        {

            try
            {

                ConnectionPinInput = default;

                #region Pin    [mandatory]

                if (!JSON.ParseMandatoryText("pin",
                                             "PIN",
                                             out String? Pin,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion


                ConnectionPinInput = new ConnectionPinInput(Pin);

                if (CustomConnectionPinInputParser is not null)
                    ConnectionPinInput = CustomConnectionPinInputParser(JSON,
                                                                        ConnectionPinInput);

                return true;

            }
            catch (Exception e)
            {
                ConnectionPinInput  = default;
                ErrorResponse       = "The given JSON representation of a connection PIN input is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomConnectionPinInputSerializer = null)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomConnectionPinInputSerializer">A delegate to serialize custom connection PIN inputs.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<ConnectionPinInput>?  CustomConnectionPinInputSerializer   = null)
        {

            var json = JSONObject.Create(
                           new JProperty("pin", Pin)
                       );

            return CustomConnectionPinInputSerializer is not null
                       ? CustomConnectionPinInputSerializer(this, json)
                       : json;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            // Never log the PIN itself!
            => $"PIN of {Pin.Length} characters";

        #endregion

    }

}
