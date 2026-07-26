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
    /// An error reported for a PIN verification (SHIP TS 1.0.1, chapter 13.4.5).
    /// </summary>
    /// <param name="Error">The error number.</param>
    public class ConnectionPinError(Byte Error)
    {

        #region Properties

        /// <summary>
        /// The error number.
        /// </summary>
        [Mandatory]
        public Byte  Error    { get; } = Error;

        #endregion


        #region (static) Parse   (JSON, CustomConnectionPinErrorParser = null)

        /// <summary>
        /// Parse the given JSON representation of a connection PIN error.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomConnectionPinErrorParser">A delegate to parse custom connection PIN errors.</param>
        public static ConnectionPinError Parse(JObject                                           JSON,
                                               CustomJObjectParserDelegate<ConnectionPinError>?  CustomConnectionPinErrorParser   = null)
        {

            if (TryParse(JSON,
                         out var connectionPinError,
                         out var errorResponse,
                         CustomConnectionPinErrorParser))
            {
                return connectionPinError;
            }

            throw new ArgumentException("The given JSON representation of a connection PIN error is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out ConnectionPinError, out ErrorResponse, CustomConnectionPinErrorParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a connection PIN error.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionPinError">The parsed connection PIN error.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                       JSON,
                                       [NotNullWhen(true)]  out ConnectionPinError?  ConnectionPinError,
                                       [NotNullWhen(false)] out String?              ErrorResponse)

            => TryParse(JSON,
                        out ConnectionPinError,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a connection PIN error.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="ConnectionPinError">The parsed connection PIN error.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomConnectionPinErrorParser">A delegate to parse custom connection PIN errors.</param>
        public static Boolean TryParse(JObject                                           JSON,
                                       [NotNullWhen(true)]  out ConnectionPinError?      ConnectionPinError,
                                       [NotNullWhen(false)] out String?                  ErrorResponse,
                                       CustomJObjectParserDelegate<ConnectionPinError>?  CustomConnectionPinErrorParser)
        {

            try
            {

                ConnectionPinError = default;

                #region Error    [mandatory]

                if (!JSON.ParseMandatory("error",
                                         "error number",
                                         out Byte Error,
                                         out ErrorResponse))
                {
                    ErrorResponse ??= "The given connection PIN error number is invalid!";
                    return false;
                }

                #endregion


                ConnectionPinError = new ConnectionPinError(Error);

                if (CustomConnectionPinErrorParser is not null)
                    ConnectionPinError = CustomConnectionPinErrorParser(JSON,
                                                                        ConnectionPinError);

                return true;

            }
            catch (Exception e)
            {
                ConnectionPinError  = default;
                ErrorResponse       = "The given JSON representation of a connection PIN error is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomConnectionPinErrorSerializer = null)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomConnectionPinErrorSerializer">A delegate to serialize custom connection PIN errors.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<ConnectionPinError>?  CustomConnectionPinErrorSerializer   = null)
        {

            var json = JSONObject.Create(
                           new JProperty("error", Error)
                       );

            return CustomConnectionPinErrorSerializer is not null
                       ? CustomConnectionPinErrorSerializer(this, json)
                       : json;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"error {Error}";

        #endregion

    }

}
