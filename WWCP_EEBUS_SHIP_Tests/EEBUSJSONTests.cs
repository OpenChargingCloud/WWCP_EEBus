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

using NUnit.Framework;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP.tests
{

    /// <summary>
    /// Tests for the conversion between ordinary JSON and the EEBUS JSON
    /// representation (SHIP TS 1.0.1, chapter 11).
    /// </summary>
    [TestFixture]
    public class EEBUSJSONTests
    {

        #region Data

        /// <summary>
        /// A SPINE datagram in ordinary JSON, taken from the test data of the
        /// ship-go reference implementation, so that both stacks are verified
        /// against the very same vector.
        /// </summary>
        public static readonly String StandardJSON =
            """{"datagram":{"header":{"specificationVersion":"1.2.0","addressSource":{"device":"d:_i:3210_EVSE","entity":[1,1],"feature":6},"addressDestination":{"device":"d:_i:3210_HEMS","entity":[1],"feature":1},"msgCounter":194,"msgCounterReference":4890,"cmdClassifier":"reply"},"payload":{"cmd":[{"deviceClassificationManufacturerData":{"deviceName":"","deviceCode":"","brandName":"","powerSource":"mains3Phase"}}]}}}""";

        /// <summary>
        /// The same SPINE datagram in the EEBUS JSON representation.
        /// </summary>
        public static readonly String EEBUSJSONText =
            """{"datagram":[{"header":[{"specificationVersion":"1.2.0"},{"addressSource":[{"device":"d:_i:3210_EVSE"},{"entity":[1,1]},{"feature":6}]},{"addressDestination":[{"device":"d:_i:3210_HEMS"},{"entity":[1]},{"feature":1}]},{"msgCounter":194},{"msgCounterReference":4890},{"cmdClassifier":"reply"}]},{"payload":[{"cmd":[[{"deviceClassificationManufacturerData":[{"deviceName":""},{"deviceCode":""},{"brandName":""},{"powerSource":"mains3Phase"}]}]]}]}]}""";

        #endregion


        #region ToEEBUSJSON_SpineDatagram_MatchesReferenceImplementation()

        [Test]
        public void ToEEBUSJSON_SpineDatagram_MatchesReferenceImplementation()
        {

            var converted = EEBUSJSON.ToEEBUSJSON(JObject.Parse(StandardJSON));

            Assert.That(converted.ToString(Formatting.None), Is.EqualTo(EEBUSJSONText));

        }

        #endregion

        #region ToStandardJSON_SpineDatagram_MatchesReferenceImplementation()

        [Test]
        public void ToStandardJSON_SpineDatagram_MatchesReferenceImplementation()
        {

            var converted = EEBUSJSON.ToStandardJSON(JObject.Parse(EEBUSJSONText));

            Assert.That(converted.ToString(Formatting.None), Is.EqualTo(StandardJSON));

        }

        #endregion

        #region Roundtrip_SpineDatagram_IsLossless()

        [Test]
        public void Roundtrip_SpineDatagram_IsLossless()
        {

            var json = JObject.Parse(StandardJSON);

            Assert.That(EEBUSJSON.ToStandardJSON(EEBUSJSON.ToEEBUSJSON(json)).ToString(Formatting.None),
                        Is.EqualTo(StandardJSON));

        }

        #endregion

        #region ToEEBUSJSON_PreservesPropertyOrder()

        /// <summary>
        /// EEBUS messages are derived from XSD sequences, so the order of the
        /// elements is significant and must survive the conversion.
        /// </summary>
        [Test]
        public void ToEEBUSJSON_PreservesPropertyOrder()
        {

            var json = EEBUSJSON.ToEEBUSJSON(
                           JObject.Parse("""{"connectionHello":{"phase":"pending","waiting":60000,"prolongationRequest":true}}""")
                       );

            Assert.That(json.ToString(Formatting.None),
                        Is.EqualTo("""{"connectionHello":[{"phase":"pending"},{"waiting":60000},{"prolongationRequest":true}]}"""));

        }

        #endregion

        #region ToEEBUSJSON_EmptyObject_BecomesEmptyArray()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 11.4.6, rule 4: empty elements become an empty array.
        /// This is how "accessMethodsRequest" goes over the wire.
        /// </summary>
        [Test]
        public void ToEEBUSJSON_EmptyObject_BecomesEmptyArray()
        {

            var json = EEBUSJSON.ToEEBUSJSON(JObject.Parse("""{"accessMethodsRequest":{}}"""));

            Assert.That(json.ToString(Formatting.None), Is.EqualTo("""{"accessMethodsRequest":[]}"""));

        }

        #endregion

        #region ToStandardJSON_EmptyArray_BecomesEmptyObject()

        [Test]
        public void ToStandardJSON_EmptyArray_BecomesEmptyObject()
        {

            var json = EEBUSJSON.ToStandardJSON(JObject.Parse("""{"accessMethodsRequest":[]}"""));

            Assert.That(json.ToString(Formatting.None), Is.EqualTo("""{"accessMethodsRequest":{}}"""));

        }

        #endregion

        #region ToEEBUSJSON_ArrayOfSimpleValues_StaysAnArray()

        /// <summary>
        /// A list of simple values (xs:list, or maxOccurs > 1 of a simple type)
        /// stays a JSON array - unlike complex types.
        /// </summary>
        [Test]
        public void ToEEBUSJSON_ArrayOfSimpleValues_StaysAnArray()
        {

            var json = EEBUSJSON.ToEEBUSJSON(JObject.Parse("""{"formats":{"format":["JSON-UTF8","JSON-UTF16"]}}"""));

            Assert.That(json.ToString(Formatting.None),
                        Is.EqualTo("""{"formats":[{"format":["JSON-UTF8","JSON-UTF16"]}]}"""));

        }

        #endregion

        #region ToEEBUSJSON_RepeatedComplexElements_BecomeNestedArrays()

        /// <summary>
        /// Repeated complex elements - such as the SPINE "cmd" - become an array
        /// of arrays, because every object itself becomes an array.
        /// </summary>
        [Test]
        public void ToEEBUSJSON_RepeatedComplexElements_BecomeNestedArrays()
        {

            var standard = """{"payload":{"cmd":[{"a":{"x":1}},{"b":{"y":2}}]}}""";
            var eebus    = """{"payload":[{"cmd":[[{"a":[{"x":1}]}],[{"b":[{"y":2}]}]]}]}""";

            Assert.Multiple(() => {

                Assert.That(EEBUSJSON.ToEEBUSJSON  (JObject.Parse(standard)).ToString(Formatting.None),
                            Is.EqualTo(eebus));

                Assert.That(EEBUSJSON.ToStandardJSON(JObject.Parse(eebus)).   ToString(Formatting.None),
                            Is.EqualTo(standard));

            });

        }

        #endregion

        #region ToStandardJSON_DuplicateProperties_Fails()

        /// <summary>
        /// Two elements of the same name within one array cannot be merged into
        /// a JSON object without losing data.
        /// </summary>
        [Test]
        public void ToStandardJSON_DuplicateProperties_Fails()
        {

            var json = JObject.Parse("""{"items":[{"item":1},{"item":2}]}""");

            Assert.Multiple(() => {
                Assert.That(EEBUSJSON.TryToStandardJSON(json, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                    Does.Contain("Duplicate"));
            });

        }

        #endregion

        #region ToStandardJSON_NullValues_ArePreserved()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 11.4.6, rule 5: "nil" elements become JSON null.
        /// </summary>
        [Test]
        public void ToStandardJSON_NullValues_ArePreserved()
        {

            var json = EEBUSJSON.ToStandardJSON(JObject.Parse("""{"element":[{"nillable":null}]}"""));

            Assert.That(json.ToString(Formatting.None), Is.EqualTo("""{"element":{"nillable":null}}"""));

        }

        #endregion

    }

}
