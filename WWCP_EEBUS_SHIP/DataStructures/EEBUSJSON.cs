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

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// Conversion between ordinary JSON and the JSON representation used on the wire
    /// by EEBUS (SHIP TS 1.0.1, chapter 11 "Message Representation Using JSON Text Format").
    ///
    /// EEBUS derives its messages from XSDs. An XSD complex type built from a sequence or
    /// a choice is not represented as a JSON object, but as an *ordered array* of objects
    /// with a single property each (chapter 11.4.5, table 6), so that the order of the
    /// elements - which is significant in XML - survives:
    ///
    /// <code>
    /// ordinary:  { "connectionHello": { "phase": "ready", "waiting": 60000 } }
    /// EEBUS:     { "connectionHello": [ { "phase": "ready" }, { "waiting": 60000 } ] }
    /// </code>
    ///
    /// Empty elements become an empty array (chapter 11.4.6, rule 4), arrays of simple
    /// values stay as they are, and every object within an array becomes an array itself:
    ///
    /// <code>
    /// ordinary:  { "cmd": [ { "resultData": { "errorNumber": 0 } } ] }
    /// EEBUS:     { "cmd": [ [ { "resultData": [ { "errorNumber": 0 } ] } ] ] }
    /// </code>
    /// </summary>
    public static class EEBUSJSON
    {

        #region ToEEBUSJSON  (JSON)

        /// <summary>
        /// Convert the given ordinary JSON object into the EEBUS JSON representation.
        /// </summary>
        /// <param name="JSON">An ordinary JSON object.</param>
        public static JObject ToEEBUSJSON(JObject JSON)
        {

            // The root object keeps its single property; only its value is converted.
            // (SHIP messages always have exactly one root property: the message name.)
            var result = new JObject();

            foreach (var property in JSON.Properties())
                result.Add(property.Name, ToEEBUSToken(property.Value));

            return result;

        }

        #endregion

        #region ToStandardJSON(JSON)

        /// <summary>
        /// Convert the given EEBUS JSON object into an ordinary JSON object.
        /// </summary>
        /// <param name="JSON">An EEBUS JSON object.</param>
        /// <exception cref="ArgumentException">When the given JSON is not a valid EEBUS JSON representation.</exception>
        public static JObject ToStandardJSON(JObject JSON)
        {

            if (TryToStandardJSON(JSON, out var standardJSON, out var errorResponse))
                return standardJSON;

            throw new ArgumentException("The given EEBUS JSON representation is invalid: " + errorResponse,
                                        nameof(JSON));

        }

        #endregion

        #region TryToStandardJSON(JSON, out StandardJSON, out ErrorResponse)

        /// <summary>
        /// Try to convert the given EEBUS JSON object into an ordinary JSON object.
        /// </summary>
        /// <param name="JSON">An EEBUS JSON object.</param>
        /// <param name="StandardJSON">The converted ordinary JSON object.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryToStandardJSON(JObject                              JSON,
                                                [NotNullWhen(true)]  out JObject?    StandardJSON,
                                                [NotNullWhen(false)] out String?     ErrorResponse)
        {

            StandardJSON   = null;
            ErrorResponse  = null;

            var result     = new JObject();

            foreach (var property in JSON.Properties())
            {

                if (!TryToStandardToken(property.Value, out var value, out ErrorResponse))
                    return false;

                result.Add(property.Name, value);

            }

            StandardJSON = result;
            return true;

        }

        #endregion


        #region (private) ToEEBUSToken(Token)

        private static JToken ToEEBUSToken(JToken Token)
        {

            switch (Token.Type)
            {

                // An object becomes an ordered array of single property objects.
                case JTokenType.Object:
                    {

                        var jsonObject  = (JObject) Token;
                        var array       = new JArray();

                        foreach (var property in jsonObject.Properties())
                            array.Add(
                                new JObject(
                                    new JProperty(
                                        property.Name,
                                        ToEEBUSToken(property.Value)
                                    )
                                )
                            );

                        return array;

                    }

                // Arrays keep their length; their items are converted individually,
                // which turns arrays of objects into arrays of arrays.
                case JTokenType.Array:
                    {

                        var array = new JArray();

                        foreach (var item in (JArray) Token)
                            array.Add(ToEEBUSToken(item));

                        return array;

                    }

                // Simple values are transferred unchanged (chapter 11.4.2).
                default:
                    return Token.DeepClone();

            }

        }

        #endregion

        #region (private) TryToStandardToken(Token, out Result, out ErrorResponse)

        private static Boolean TryToStandardToken(JToken                            Token,
                                                  [NotNullWhen(true)]  out JToken?  Result,
                                                  [NotNullWhen(false)] out String?  ErrorResponse)
        {

            Result         = null;
            ErrorResponse  = null;

            switch (Token.Type)
            {

                case JTokenType.Array:
                    {

                        var array = (JArray) Token;

                        // An empty element is encoded as an empty array (chapter 11.4.6, rule 4).
                        if (array.Count == 0)
                        {
                            Result = new JObject();
                            return true;
                        }

                        // An array of single property objects is an object.
                        if (array.All(item => item is JObject jsonObject && jsonObject.Count == 1))
                        {

                            var jsonObject = new JObject();

                            foreach (var item in array)
                            {

                                var property = ((JObject) item).Properties().First();

                                if (jsonObject.ContainsKey(property.Name))
                                {
                                    ErrorResponse = $"Duplicate property '{property.Name}' within an EEBUS JSON array!";
                                    return false;
                                }

                                if (!TryToStandardToken(property.Value, out var value, out ErrorResponse))
                                    return false;

                                jsonObject.Add(property.Name, value);

                            }

                            Result = jsonObject;
                            return true;

                        }

                        // Anything else stays an array: simple values, or nested
                        // arrays which represent repeated complex elements.
                        var resultArray = new JArray();

                        foreach (var item in array)
                        {

                            if (!TryToStandardToken(item, out var value, out ErrorResponse))
                                return false;

                            resultArray.Add(value);

                        }

                        Result = resultArray;
                        return true;

                    }

                // A JSON object at this place is not valid EEBUS JSON: complex types
                // are always encoded as arrays. Accepted anyway, to stay interoperable
                // with implementations sending ordinary JSON.
                case JTokenType.Object:
                    {

                        var jsonObject = new JObject();

                        foreach (var property in ((JObject) Token).Properties())
                        {

                            if (!TryToStandardToken(property.Value, out var value, out ErrorResponse))
                                return false;

                            jsonObject.Add(property.Name, value);

                        }

                        Result = jsonObject;
                        return true;

                    }

                default:
                    Result = Token.DeepClone();
                    return true;

            }

        }

        #endregion

    }

}
