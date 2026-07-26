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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// How SPINE datagrams are read and written.
    ///
    /// The defaults of the JSON library are wrong for this protocol in two ways,
    /// and both of them are the quiet kind of wrong:
    ///
    /// * "DateParseHandling" turns anything which looks like a timestamp into a
    ///   DateTime while reading, before any converter of ours is asked. A SPINE
    ///   timestamp is a string whose exact text is part of the message, and
    ///   letting it become a DateTime and back again loses the fractional digits,
    ///   the offset notation - and, if the machine happens to run in a German
    ///   locale, the ISO 8601 format altogether.
    /// * An empty list is sent as "{}" by a good many devices, because the EEBUS
    ///   JSON transformation of SHIP has no way to tell an empty object from an
    ///   empty array. Refusing to read that would mean refusing to talk to them.
    ///
    /// Anything which serialises a SPINE data type should use these settings.
    /// </summary>
    public static class SPINEJSON
    {

        #region Properties

        /// <summary>
        /// The settings for reading and writing SPINE datagrams.
        /// Properties which are not known are ignored - a communication partner
        /// may implement a newer version of the specification than we do.
        /// </summary>
        public static JsonSerializerSettings  Settings          { get; }
            = Create(MissingMemberHandling.Ignore);

        /// <summary>
        /// The same, but a property which the data model does not know is an
        /// error. This is what the conformance tests use: there, an unknown
        /// property is exactly what one wants to hear about.
        /// </summary>
        public static JsonSerializerSettings  StrictSettings    { get; }
            = Create(MissingMemberHandling.Error);

        #endregion

        #region (private static) Create(MissingMembers)

        private static JsonSerializerSettings Create(MissingMemberHandling MissingMembers)

            => new () {
                   DateParseHandling      = DateParseHandling.None,
                   NullValueHandling      = NullValueHandling.Ignore,
                   MissingMemberHandling  = MissingMembers,
                   Converters             = { new SPINEListConverter() }
               };

        #endregion


        #region Parse   <T>(JSON)

        /// <summary>
        /// Read a SPINE data type.
        /// </summary>
        /// <typeparam name="T">A SPINE data type.</typeparam>
        /// <param name="JSON">Its JSON representation.</param>
        public static T? Parse<T>(String JSON)

            => JsonConvert.DeserializeObject<T>(JSON, Settings);

        #endregion

        #region ToJSON   (Value, Formatting = None)

        /// <summary>
        /// Write a SPINE data type.
        /// </summary>
        /// <param name="Value">A SPINE data type.</param>
        /// <param name="Formatting">Whether to indent the result.</param>
        public static String ToJSON(Object?     Value,
                                    Formatting  Formatting = Formatting.None)

            => JsonConvert.SerializeObject(Value, Formatting, Settings);

        #endregion

    }


    /// <summary>
    /// Reads a list which arrived as the empty object "{}".
    ///
    /// SHIP TS 1.0.1, chapter 11 transforms every JSON object into an array of
    /// objects with one property each. The transformation back cannot tell an
    /// object which had no properties from an array which had no entries, so an
    /// empty list turns up as "{}" - as it does in the recorded datagrams of the
    /// Go reference implementation ("nm_detaileddiscovery_emptyarray").
    /// </summary>
    public sealed class SPINEListConverter : JsonConverter
    {

        #region Properties

        /// <summary>
        /// Lists are written by the library itself; only reading needs help.
        /// </summary>
        public override Boolean CanWrite
            => false;

        #endregion


        /// <summary>
        /// Whether this converter can convert the given type.
        /// </summary>
        /// <param name="ObjectType">A type.</param>
        public override Boolean CanConvert(Type ObjectType)

            => ObjectType.IsGenericType &&
               ObjectType.GetGenericTypeDefinition() == typeof(List<>);


        /// <summary>
        /// Read a list.
        /// </summary>
        public override Object? ReadJson(JsonReader      Reader,
                                         Type            ObjectType,
                                         Object?         ExistingValue,
                                         JsonSerializer  Serializer)
        {

            if (Reader.TokenType == JsonToken.Null)
                return null;

            var path   = Reader.Path;
            var token  = JToken.Load(Reader);

            var list   = Activator.CreateInstance(ObjectType) as System.Collections.IList
                             ?? throw new JsonSerializationException($"'{ObjectType}' is not a list.");

            if (token is JObject @object)
                return @object.Count == 0
                           ? list
                           : throw new JsonSerializationException(
                                 $"Expected a list or the empty object, but found an object with {@object.Count} " +
                                 $"property/properties at '{path}'."
                             );

            if (token is not JArray array)
                throw new JsonSerializationException($"Expected a list at '{path}', but found {token.Type}.");

            // The entries are read one by one with the very serializer this
            // converter was called from, rather than the list as a whole: asking
            // it for the list again would land back here, and a list within a
            // list has to reach this converter as well.
            var itemType = ObjectType.GetGenericArguments()[0];

            foreach (var item in array)
                list.Add(item.ToObject(itemType, Serializer));

            return list;

        }


        /// <summary>
        /// Never called, see <see cref="CanWrite"/>.
        /// </summary>
        public override void WriteJson(JsonWriter      Writer,
                                       Object?         Value,
                                       JsonSerializer  Serializer)

            => throw new NotSupportedException("Lists are written by the JSON library itself.");

    }

}
