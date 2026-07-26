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

using System.Globalization;

using Newtonsoft.Json;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// Reads and writes any <see cref="ISPINEStringType{TSelf}"/> as a JSON string.
    ///
    /// The converter is declared on the data types themselves, so that it also
    /// applies to nullable properties - which is what the generated model uses
    /// throughout, because every element of the SPINE data model is optional.
    /// </summary>
    /// <typeparam name="T">A SPINE data type which is a string on the wire.</typeparam>
    public sealed class SPINEStringTypeConverter<T> : JsonConverter

        where T : struct, ISPINEStringType<T>

    {

        /// <summary>
        /// Whether this converter can convert the given type.
        /// </summary>
        /// <param name="ObjectType">A type.</param>
        public override Boolean CanConvert(Type ObjectType)

            => ObjectType == typeof(T) ||
               Nullable.GetUnderlyingType(ObjectType) == typeof(T);


        /// <summary>
        /// Read a SPINE string type.
        /// </summary>
        public override Object? ReadJson(JsonReader      Reader,
                                         Type            ObjectType,
                                         Object?         ExistingValue,
                                         JsonSerializer  Serializer)
        {

            if (Reader.TokenType == JsonToken.Null)
                return null;

            // Everything here is a string on the wire, but the reader may have
            // turned it into something else before this converter is asked:
            // "DateParseHandling" makes a DateTime out of anything which looks
            // like a timestamp. Formatting it back with the culture of the
            // machine would turn "2022-11-19T15:21:50.003Z" into whatever the
            // locale happens to prefer, so the invariant ISO 8601 form is used.
            //
            // Reading SPINE through SPINEJSON avoids the situation altogether;
            // this is here so that the wrong settings cost a wrong format and
            // not a silently mangled datagram.
            var text = Reader.Value switch {
                           String         text1        => text1,
                           DateTimeOffset timestamp    => timestamp.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture),
                           DateTime       timestamp    => timestamp.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture),
                           IFormattable   formattable  => formattable.ToString(null, CultureInfo.InvariantCulture),
                           null                        => null,
                           var            other        => other.ToString()
                       };

            if (text is null)
                return null;

            if (T.TryParse(text, out var value))
                return value;

            return null;

        }


        /// <summary>
        /// Write a SPINE string type.
        /// </summary>
        public override void WriteJson(JsonWriter      Writer,
                                       Object?         Value,
                                       JsonSerializer  Serializer)
        {

            if (Value is T value)
                Writer.WriteValue(value.ToString());
            else
                Writer.WriteNull();

        }

    }

}
