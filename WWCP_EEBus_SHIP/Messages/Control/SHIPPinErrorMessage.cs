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
    /// <param name="ConnectionPinError">The connection PIN error.</param>
    public class SHIPPinErrorMessage(ConnectionPinError  ConnectionPinError)

        : ASHIPMessage(SHIPMessageTypes.CONTROL)

    {

        #region Properties

        /// <summary>
        /// The connection PIN error.
        /// </summary>
        [Mandatory]
        public ConnectionPinError  ConnectionPinError    { get; } = ConnectionPinError;

        #endregion


        #region (static) Parse   (JSON, CustomSHIPPinErrorMessageParser = null)

        /// <summary>
        /// Parse the given JSON representation of a SHIP-Pin-Error Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="CustomSHIPPinErrorMessageParser">A delegate to parse custom SHIP-Pin-Error Messages.</param>
        public static SHIPPinErrorMessage Parse(JObject                                         JSON,
                                             CustomJObjectParserDelegate<SHIPPinErrorMessage>?  CustomSHIPPinErrorMessageParser   = null)
        {

            if (TryParse(JSON,
                         out var sHIPPinErrorMessage,
                         out var errorResponse,
                         CustomSHIPPinErrorMessageParser))
            {
                return sHIPPinErrorMessage;
            }

            throw new ArgumentException("The given JSON representation of a SHIP-Pin-Error-Message is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region (static) TryParse(JSON, out SHIPPinErrorMessage, CustomSHIPPinErrorMessageParser = null)

        // Note: The following is needed to satisfy pattern matching delegates! Do not refactor it!

        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Pin-Error-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPPinErrorMessage">The parsed sHIPPinErrorMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(JObject                                     JSON,
                                       [NotNullWhen(true)]  out SHIPPinErrorMessage?  SHIPPinErrorMessage,
                                       [NotNullWhen(false)] out String?            ErrorResponse)

            => TryParse(JSON,
                        out SHIPPinErrorMessage,
                        out ErrorResponse,
                        null);


        /// <summary>
        /// Try to parse the given JSON representation of a SHIP-Pin-Error-Message.
        /// </summary>
        /// <param name="JSON">The JSON to be parsed.</param>
        /// <param name="SHIPPinErrorMessage">The parsed sHIPPinErrorMessage.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="CustomSHIPPinErrorMessageParser">A delegate to parse custom SHIP-Pin-Error Messages.</param>
        public static Boolean TryParse(JObject                                         JSON,
                                       [NotNullWhen(true)]  out SHIPPinErrorMessage?      SHIPPinErrorMessage,
                                       [NotNullWhen(false)] out String?                ErrorResponse,
                                       CustomJObjectParserDelegate<SHIPPinErrorMessage>?  CustomSHIPPinErrorMessageParser)
        {

            try
            {

                SHIPPinErrorMessage = default;

                #region ConnectionPinError    [mandatory]

                if (!JSON.ParseMandatoryJSON("connectionPinError",
                                             "connection hello",
                                             SHIP.ConnectionPinError.TryParse,
                                             out ConnectionPinError? ConnectionPinError,
                                             out ErrorResponse))
                {
                    return false;
                }

                #endregion



                SHIPPinErrorMessage = new SHIPPinErrorMessage(
                                       ConnectionPinError
                                   );

                if (CustomSHIPPinErrorMessageParser is not null)
                    SHIPPinErrorMessage = CustomSHIPPinErrorMessageParser(JSON,
                                                                    SHIPPinErrorMessage);

                return true;

            }
            catch (Exception e)
            {
                SHIPPinErrorMessage  = default;
                ErrorResponse     = "The given JSON representation of a SHIP-Pin-Error-Message is invalid: " + e.Message;
                return false;
            }

        }

        #endregion

        #region ToJSON(CustomSHIPPinErrorMessageSerializer = null, CustomComponentSerializer = null, ...)

        /// <summary>
        /// Return a JSON representation of this object.
        /// </summary>
        /// <param name="CustomSHIPPinErrorMessageSerializer">A delegate to serialize custom event data objects.</param>
        /// <param name="CustomConnectionPinErrorSerializer">A delegate to serialize custom ConnectionPinError objects.</param>
        public JObject ToJSON(CustomJObjectSerializerDelegate<SHIPPinErrorMessage>?  CustomSHIPPinErrorMessageSerializer      = null,
                              CustomJObjectSerializerDelegate<ConnectionPinError>?   CustomConnectionPinErrorSerializer   = null)
        {

            var json = JSONObject.Create(

                                 new JProperty("connectionPinError",    ConnectionPinError.ToJSON(CustomConnectionPinErrorSerializer))

                       );

            return CustomSHIPPinErrorMessageSerializer is not null
                       ? CustomSHIPPinErrorMessageSerializer(this, json)
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
