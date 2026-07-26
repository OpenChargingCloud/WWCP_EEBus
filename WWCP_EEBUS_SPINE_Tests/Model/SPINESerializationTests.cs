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

using NUnit.Framework;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// Tests for the serialisation of the generated SPINE data model.
    ///
    /// SPINE datagrams travel as JSON, and EEBUS JSON is an ordered format: the
    /// SHIP layer turns every object into an array of single property objects,
    /// so the order of the properties is part of the message rather than a
    /// detail of the encoder.
    /// </summary>
    [TestFixture]
    public class SPINESerializationTests
    {

        #region Data

        // The strict settings: a property of a golden datagram which the model
        // does not know is a hole in the model, and a test which silently
        // ignores it is worth nothing.
        private static readonly JsonSerializerSettings settings = SPINEJSON.StrictSettings;

        /// <summary>
        /// Recorded datagrams which are read correctly but are not written back
        /// character for character, with the reason.
        /// </summary>
        private static readonly Dictionary<String, String> knownGoldenDatagramDeviations = new (StringComparer.Ordinal) {

            [ "nm_detaileddiscovery_emptyarray.json" ]
                = "It holds an empty list in the form the EEBUS JSON transformation of SHIP produces, \"{}\". " +
                  "We read that (see EmptyList_MayArriveAsTheEmptyObject) and write the ordinary \"[]\" back, " +
                  "which is the same list and not the same characters."

        };

        #endregion


        #region Datagram_IsWrittenInTheOrderOfTheSpecification()

        /// <summary>
        /// A read of the load control limits, written out completely: the
        /// properties appear in the order of the XSD, and nothing which was not
        /// set appears at all.
        /// </summary>
        [Test]
        public void Datagram_IsWrittenInTheOrderOfTheSpecification()
        {

            var datagram = new DatagramType {

                               Header   = new HeaderType {
                                              SpecificationVersion  = "1.3.0",
                                              AddressSource         = new FeatureAddressType {
                                                                          Device   = "d:_i:19667_HEMS",
                                                                          Entity   = [ 1 ],
                                                                          Feature  = 6
                                                                      },
                                              AddressDestination    = new FeatureAddressType {
                                                                          Entity   = [ 1 ],
                                                                          Feature  = 2
                                                                      },
                                              MsgCounter            = 42,
                                              CmdClassifier         = CmdClassifierType.Read
                                          },

                               Payload  = new PayloadType {
                                              Cmd = [
                                                  new CmdType {
                                                      LoadControlLimitListData = new LoadControlLimitListDataType()
                                                  }
                                              ]
                                          }

                           };

            var json = JsonConvert.SerializeObject(datagram, Formatting.None, settings);

            Assert.That(json,
                        Is.EqualTo(
                            "{\"header\":{" +
                                "\"specificationVersion\":\"1.3.0\"," +
                                "\"addressSource\":{\"device\":\"d:_i:19667_HEMS\",\"entity\":[1],\"feature\":6}," +
                                "\"addressDestination\":{\"entity\":[1],\"feature\":2}," +
                                "\"msgCounter\":42," +
                                "\"cmdClassifier\":\"read\"" +
                            "},\"payload\":{" +
                                "\"cmd\":[{\"loadControlLimitListData\":{}}]" +
                            "}}"
                        ));

        }

        #endregion

        #region ElementTag_IsAnEmptyObject()

        /// <summary>
        /// An element tag says "this field" and carries nothing. On the wire it
        /// is the empty object - a partial write is marked with
        /// "cmdControl":{"partial":{}}.
        /// </summary>
        [Test]
        public void ElementTag_IsAnEmptyObject()
        {

            var json = JsonConvert.SerializeObject(
                           new CmdControlType { Partial = new ElementTagType() },
                           Formatting.None,
                           settings
                       );

            Assert.That(json, Is.EqualTo("{\"partial\":{}}"));

        }

        #endregion


        #region EmptyList_MayArriveAsTheEmptyObject()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 11 turns every object into an array of objects
        /// with one property each, and the way back cannot tell an object which
        /// had no properties from an array which had no entries. An empty list
        /// therefore arrives as "{}" from a good many devices - the recorded
        /// datagram "nm_detaileddiscovery_emptyarray" of the Go reference
        /// implementation is one of them.
        /// </summary>
        [Test]
        public void EmptyList_MayArriveAsTheEmptyObject()
        {

            var description = JsonConvert.DeserializeObject<NetworkManagementFeatureDescriptionDataType>(
                                  "{\"featureType\":\"Generic\",\"supportedFunction\":{}}",
                                  settings
                              );

            Assert.That(description, Is.Not.Null);

            Assert.Multiple(() => {

                Assert.That(description!.SupportedFunction,       Is.Not.Null);
                Assert.That(description!.SupportedFunction,       Is.Empty);

                // Written back it is an ordinary empty list again.
                Assert.That(JsonConvert.SerializeObject(description, Formatting.None, settings),
                            Is.EqualTo("{\"featureType\":\"Generic\",\"supportedFunction\":[]}"));

            });

        }

        #endregion

        #region ANonEmptyObject_WhereAListIsExpected_IsRefused()

        /// <summary>
        /// Only the empty object stands for a list. An object with properties
        /// is a real error and has to be reported as one.
        /// </summary>
        [Test]
        public void ANonEmptyObject_WhereAListIsExpected_IsRefused()

            => Assert.That(
                   () => JsonConvert.DeserializeObject<NetworkManagementFeatureDescriptionDataType>(
                             "{\"supportedFunction\":{\"function\":\"resultData\"}}",
                             settings
                         ),
                   Throws.InstanceOf<JsonSerializationException>()
               );

        #endregion

        #region ExtensibleEnumeration_KeepsAnUnknownValue()

        /// <summary>
        /// SPINE enumerations are extensible: a manufacturer may add values, and
        /// a value we do not know has to survive being received and sent again.
        /// </summary>
        [Test]
        public void ExtensibleEnumeration_KeepsAnUnknownValue()
        {

            var json     = "{\"limitType\":\"_i:19667_ourOwnLimit\"}";

            var parsed   = JsonConvert.DeserializeObject<LoadControlLimitDescriptionDataType>(json, settings);

            Assert.That(parsed,              Is.Not.Null);
            Assert.That(parsed!.LimitType,   Is.Not.Null);

            Assert.Multiple(() => {

                Assert.That(parsed.LimitType!.Value.ToString(),  Is.EqualTo("_i:19667_ourOwnLimit"));
                Assert.That(parsed.LimitType!.Value.IsDefined,   Is.False,
                            "The specification does not define this value.");

                Assert.That(LoadControlLimitTypeType.IsExtensible, Is.True);

                Assert.That(JsonConvert.SerializeObject(parsed, Formatting.None, settings),
                            Is.EqualTo(json),
                            "An unknown value has to be sent again unchanged.");

            });

        }

        #endregion

        #region DefinedEnumeration_IsRecognised()

        [Test]
        public void DefinedEnumeration_IsRecognised()
        {

            Assert.Multiple(() => {

                Assert.That(LoadControlLimitTypeType.MaxValueLimit.ToString(),  Is.EqualTo("maxValueLimit"));
                Assert.That(LoadControlLimitTypeType.MaxValueLimit.IsDefined,   Is.True);

                Assert.That(LoadControlLimitTypeType.Parse("maxValueLimit"),
                            Is.EqualTo(LoadControlLimitTypeType.MaxValueLimit));

                // SPINE 1.3.0 defines exactly these three.
                Assert.That(LoadControlLimitTypeType.All.Count(), Is.EqualTo(3));

            });

        }

        #endregion

        #region UnitsOfMeasurement_AreCaseSensitive()

        /// <summary>
        /// "s" is a second and "S" is a siemens. An enumeration of SPINE which
        /// compared its values without regard to case would silently turn one
        /// into the other.
        /// </summary>
        [Test]
        public void UnitsOfMeasurement_AreCaseSensitive()
        {

            Assert.Multiple(() => {

                Assert.That(UnitOfMeasurementType.Parse("s"),
                            Is.Not.EqualTo(UnitOfMeasurementType.Parse("S")));

                Assert.That(UnitOfMeasurementType.Parse("s").IsDefined,  Is.True);
                Assert.That(UnitOfMeasurementType.Parse("S").IsDefined,  Is.True);

                // The values which are not C# identifiers keep their exact text.
                Assert.That(UnitOfMeasurementType.Parse("m^3/h").ToString(),  Is.EqualTo("m^3/h"));
                Assert.That(UnitOfMeasurementType.Parse("1").     ToString(),  Is.EqualTo("1"));

            });

        }

        #endregion


        #region Duration_KeepsItsText()

        /// <summary>
        /// "PT2M" and "PT120S" are the same duration and not the same datagram.
        /// </summary>
        [Test]
        public void Duration_KeepsItsText()
        {

            var duration = DurationType.Parse("PT2M");

            Assert.Multiple(() => {

                Assert.That(duration.ToString(),   Is.EqualTo("PT2M"));
                Assert.That(duration.AsTimeSpan,   Is.EqualTo(TimeSpan.FromMinutes(2)));

                Assert.That(duration,              Is.Not.EqualTo(DurationType.Parse("PT120S")),
                            "Equality is equality of the text.");

                Assert.That(duration.CompareTo(DurationType.Parse("PT120S")), Is.Zero,
                            "The comparison is by length.");

            });

        }

        #endregion

        #region AbsoluteOrRelativeTime_TellsTheTwoApart()

        [Test]
        public void AbsoluteOrRelativeTime_TellsTheTwoApart()
        {

            var relative = AbsoluteOrRelativeTimeType.Parse("PT10M");
            var absolute = AbsoluteOrRelativeTimeType.Parse("2026-07-26T14:00:00Z");

            Assert.Multiple(() => {

                Assert.That(relative.IsRelative,        Is.True);
                Assert.That(relative.AsTimeSpan,        Is.EqualTo(TimeSpan.FromMinutes(10)));
                Assert.That(relative.AsDateTimeOffset,  Is.Null);

                Assert.That(absolute.IsAbsolute,        Is.True);
                Assert.That(absolute.AsDateTimeOffset,  Is.EqualTo(new DateTimeOffset(2026, 7, 26, 14, 0, 0, TimeSpan.Zero)));
                Assert.That(absolute.AsTimeSpan,        Is.Null);

                Assert.That(absolute.ToString(),        Is.EqualTo("2026-07-26T14:00:00Z"));

            });

        }

        #endregion


        #region GoldenDatagrams_OfTheGoReferenceImplementation_Roundtrip()

        /// <summary>
        /// Every recorded datagram of spine-go is read into the model and written
        /// out again; nothing may be lost and nothing added.
        ///
        /// These files are the messages the Go stack actually exchanges, so this
        /// is the closest thing to a wire test that does not need a peer. They
        /// are part of the test bench, not of WWCP_EEBUS, so the test reports
        /// itself inconclusive where the reference repositories are not checked
        /// out next to us.
        /// </summary>
        [Test]
        public void GoldenDatagrams_OfTheGoReferenceImplementation_Roundtrip()
        {

            var directories = GoldenDatagramDirectories();

            if (directories.Count == 0)
                Assert.Inconclusive("The Go reference implementation is not checked out below libs/spine-go, " +
                                    "so there are no golden datagrams to read.");

            var checked_  = 0;
            var problems  = new List<String>();

            foreach (var directory in directories)
                foreach (var file in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
                {

                    var text     = File.ReadAllText(file);
                    var expected = JObject.Parse(text)["datagram"];

                    if (expected is null)
                        continue;

                    checked_++;

                    try
                    {

                        var datagram = JsonConvert.DeserializeObject<DatagramType>(expected.ToString(), settings);
                        var actual   = JObject.Parse(JsonConvert.SerializeObject(datagram, Formatting.None, settings));

                        if (!JToken.DeepEquals(expected, actual) &&
                            !knownGoldenDatagramDeviations.ContainsKey(Path.GetFileName(file)))
                        {
                            problems.Add($"{Path.GetFileName(file)}: {FirstDifference(expected, actual, "datagram")}");
                        }

                    }
                    catch (JsonSerializationException e)
                    {
                        problems.Add($"{Path.GetFileName(file)}: {e.Message}");
                    }

                }

            Assert.Multiple(() => {

                Assert.That(checked_, Is.GreaterThan(0), "No golden datagram was read.");

                Assert.That(problems, Is.Empty,
                            $"{problems.Count} of {checked_} golden datagram(s) did not survive the roundtrip:{Environment.NewLine}" +
                            String.Join(Environment.NewLine, problems));

            });

        }

        #endregion

        #region (private static) FirstDifference(Expected, Actual, Path)

        /// <summary>
        /// Where two datagrams first differ, as a path a human can follow.
        /// "They are not equal" is not a useful thing to be told about a
        /// datagram with two hundred properties.
        /// </summary>
        private static String FirstDifference(JToken? Expected, JToken? Actual, String Path)
        {

            if (JToken.DeepEquals(Expected, Actual))
                return "no difference";

            if (Expected is JObject expectedObject && Actual is JObject actualObject)
            {

                foreach (var property in expectedObject.Properties())
                {

                    if (actualObject[property.Name] is null)
                        return $"{Path}.{property.Name} is missing; it was '{property.Value.ToString(Formatting.None)}'.";

                    if (!JToken.DeepEquals(property.Value, actualObject[property.Name]))
                        return FirstDifference(property.Value, actualObject[property.Name], $"{Path}.{property.Name}");

                }

                foreach (var property in actualObject.Properties())
                    if (expectedObject[property.Name] is null)
                        return $"{Path}.{property.Name} was added; it is '{property.Value.ToString(Formatting.None)}'.";

                return $"{Path}: the properties differ in order.";

            }

            if (Expected is JArray expectedArray && Actual is JArray actualArray)
            {

                if (expectedArray.Count != actualArray.Count)
                    return $"{Path} has {actualArray.Count} entries instead of {expectedArray.Count}.";

                for (var i = 0; i < expectedArray.Count; i++)
                    if (!JToken.DeepEquals(expectedArray[i], actualArray[i]))
                        return FirstDifference(expectedArray[i], actualArray[i], $"{Path}[{i}]");

                return $"{Path}: the entries differ in order.";

            }

            return $"{Path}: expected '{Expected?.ToString(Formatting.None)}' " +
                   $"({Expected?.Type}), got '{Actual?.ToString(Formatting.None)}' ({Actual?.Type}).";

        }

        #endregion

        #region (private static) GoldenDatagramDirectories()

        /// <summary>
        /// The directories of the recorded datagrams of spine-go, where the
        /// reference repositories are checked out next to us.
        /// </summary>
        private static List<String> GoldenDatagramDirectories()
        {

            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (directory is not null &&
                   !Directory.Exists(Path.Combine(directory.FullName, "libs", "spine-go")))
            {
                directory = directory.Parent;
            }

            if (directory is null)
                return [];

            return [.. new[] {
                       Path.Combine(directory.FullName, "libs", "spine-go", "spine",             "testdata"),
                       Path.Combine(directory.FullName, "libs", "spine-go", "integration_tests", "testdata")
                   }.Where(Directory.Exists)];

        }

        #endregion

    }

}
