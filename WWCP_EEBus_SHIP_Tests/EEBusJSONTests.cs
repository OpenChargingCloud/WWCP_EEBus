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

using NUnit.Framework;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP.tests
{

    /// <summary>
    /// Tests for the conversion between ordinary JSON and the EEBus JSON
    /// representation (SHIP TS 1.0.1, chapter 11).
    /// </summary>
    [TestFixture]
    public class EEBusJSONTests
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
        /// The same SPINE datagram in the EEBus JSON representation.
        /// </summary>
        public static readonly String EEBusJSONText =
            """{"datagram":[{"header":[{"specificationVersion":"1.2.0"},{"addressSource":[{"device":"d:_i:3210_EVSE"},{"entity":[1,1]},{"feature":6}]},{"addressDestination":[{"device":"d:_i:3210_HEMS"},{"entity":[1]},{"feature":1}]},{"msgCounter":194},{"msgCounterReference":4890},{"cmdClassifier":"reply"}]},{"payload":[{"cmd":[[{"deviceClassificationManufacturerData":[{"deviceName":""},{"deviceCode":""},{"brandName":""},{"powerSource":"mains3Phase"}]}]]}]}]}""";

        #endregion


        #region ToEEBusJSON_SpineDatagram_MatchesReferenceImplementation()

        [Test]
        public void ToEEBusJSON_SpineDatagram_MatchesReferenceImplementation()
        {

            var converted = EEBusJSON.ToEEBusJSON(JObject.Parse(StandardJSON));

            Assert.That(converted.ToString(Formatting.None), Is.EqualTo(EEBusJSONText));

        }

        #endregion

        #region ToStandardJSON_SpineDatagram_MatchesReferenceImplementation()

        [Test]
        public void ToStandardJSON_SpineDatagram_MatchesReferenceImplementation()
        {

            var converted = EEBusJSON.ToStandardJSON(JObject.Parse(EEBusJSONText));

            Assert.That(converted.ToString(Formatting.None), Is.EqualTo(StandardJSON));

        }

        #endregion

        #region Roundtrip_SpineDatagram_IsLossless()

        [Test]
        public void Roundtrip_SpineDatagram_IsLossless()
        {

            var json = JObject.Parse(StandardJSON);

            Assert.That(EEBusJSON.ToStandardJSON(EEBusJSON.ToEEBusJSON(json)).ToString(Formatting.None),
                        Is.EqualTo(StandardJSON));

        }

        #endregion

        #region ToEEBusJSON_PreservesPropertyOrder()

        /// <summary>
        /// EEBus messages are derived from XSD sequences, so the order of the
        /// elements is significant and must survive the conversion.
        /// </summary>
        [Test]
        public void ToEEBusJSON_PreservesPropertyOrder()
        {

            var json = EEBusJSON.ToEEBusJSON(
                           JObject.Parse("""{"connectionHello":{"phase":"pending","waiting":60000,"prolongationRequest":true}}""")
                       );

            Assert.That(json.ToString(Formatting.None),
                        Is.EqualTo("""{"connectionHello":[{"phase":"pending"},{"waiting":60000},{"prolongationRequest":true}]}"""));

        }

        #endregion

        #region ToEEBusJSON_EmptyObject_BecomesEmptyArray()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 11.4.6, rule 4: empty elements become an empty array.
        /// This is how "accessMethodsRequest" goes over the wire.
        /// </summary>
        [Test]
        public void ToEEBusJSON_EmptyObject_BecomesEmptyArray()
        {

            var json = EEBusJSON.ToEEBusJSON(JObject.Parse("""{"accessMethodsRequest":{}}"""));

            Assert.That(json.ToString(Formatting.None), Is.EqualTo("""{"accessMethodsRequest":[]}"""));

        }

        #endregion

        #region ToStandardJSON_EmptyArray_BecomesEmptyObject()

        [Test]
        public void ToStandardJSON_EmptyArray_BecomesEmptyObject()
        {

            var json = EEBusJSON.ToStandardJSON(JObject.Parse("""{"accessMethodsRequest":[]}"""));

            Assert.That(json.ToString(Formatting.None), Is.EqualTo("""{"accessMethodsRequest":{}}"""));

        }

        #endregion

        #region ToEEBusJSON_ArrayOfSimpleValues_StaysAnArray()

        /// <summary>
        /// A list of simple values (xs:list, or maxOccurs > 1 of a simple type)
        /// stays a JSON array - unlike complex types.
        /// </summary>
        [Test]
        public void ToEEBusJSON_ArrayOfSimpleValues_StaysAnArray()
        {

            var json = EEBusJSON.ToEEBusJSON(JObject.Parse("""{"formats":{"format":["JSON-UTF8","JSON-UTF16"]}}"""));

            Assert.That(json.ToString(Formatting.None),
                        Is.EqualTo("""{"formats":[{"format":["JSON-UTF8","JSON-UTF16"]}]}"""));

        }

        #endregion

        #region ToEEBusJSON_RepeatedComplexElements_BecomeNestedArrays()

        /// <summary>
        /// Repeated complex elements - such as the SPINE "cmd" - become an array
        /// of arrays, because every object itself becomes an array.
        /// </summary>
        [Test]
        public void ToEEBusJSON_RepeatedComplexElements_BecomeNestedArrays()
        {

            var standard = """{"payload":{"cmd":[{"a":{"x":1}},{"b":{"y":2}}]}}""";
            var eebus    = """{"payload":[{"cmd":[[{"a":[{"x":1}]}],[{"b":[{"y":2}]}]]}]}""";

            Assert.Multiple(() => {

                Assert.That(EEBusJSON.ToEEBusJSON  (JObject.Parse(standard)).ToString(Formatting.None),
                            Is.EqualTo(eebus));

                Assert.That(EEBusJSON.ToStandardJSON(JObject.Parse(eebus)).   ToString(Formatting.None),
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
                Assert.That(EEBusJSON.TryToStandardJSON(json, out _, out var errorResponse),  Is.False);
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

            var json = EEBusJSON.ToStandardJSON(JObject.Parse("""{"element":[{"nillable":null}]}"""));

            Assert.That(json.ToString(Formatting.None), Is.EqualTo("""{"element":{"nillable":null}}"""));

        }

        #endregion

    }

}
