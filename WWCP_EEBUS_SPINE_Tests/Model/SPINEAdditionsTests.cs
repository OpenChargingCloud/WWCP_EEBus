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

using Microsoft.Extensions.Time.Testing;

using Newtonsoft.Json;

using NUnit.Framework;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// Tests for the hand-written part of the SPINE data model (WP06b): what
    /// the XSD describes but cannot express - what a scaled number is worth,
    /// how an address reads, which function a command carries.
    /// </summary>
    [TestFixture]
    public class SPINEAdditionsTests
    {

        #region ScaledNumber_IsTheNumberTimesTenToTheScale()

        [Test]
        public void ScaledNumber_IsTheNumberTimesTenToTheScale()
        {

            Assert.Multiple(() => {

                Assert.That(new ScaledNumberType { Number = 1185, Scale =  0 }.Value,  Is.EqualTo(1185m));
                Assert.That(new ScaledNumberType { Number = 1185, Scale = -1 }.Value,  Is.EqualTo(118.5m));
                Assert.That(new ScaledNumberType { Number = 1185, Scale = -3 }.Value,  Is.EqualTo(1.185m));
                Assert.That(new ScaledNumberType { Number =   12, Scale =  2 }.Value,  Is.EqualTo(1200m));

                // No number is no value, and is not zero.
                Assert.That(new ScaledNumberType { Scale = -1 }.Value,                 Is.Null);

                // A scale beyond what a decimal can represent: saying nothing is
                // better than saying something wrong.
                Assert.That(new ScaledNumberType { Number = 1, Scale = 30 }.Value,     Is.Null);

            });

        }

        #endregion

        #region ScaledNumber_FromValue_UsesTheSmallestScale()

        [Test]
        public void ScaledNumber_FromValue_UsesTheSmallestScale()
        {

            Assert.Multiple(() => {

                var whole = ScaledNumberType.FromValue(12m);
                Assert.That(whole.Number,  Is.EqualTo(12));
                Assert.That(whole.Scale,   Is.EqualTo(0), "A whole number does not need a scale.");

                var fraction = ScaledNumberType.FromValue(118.5m);
                Assert.That(fraction.Number,  Is.EqualTo(1185));
                Assert.That(fraction.Scale,   Is.EqualTo(-1));

                var negative = ScaledNumberType.FromValue(-4.2m);
                Assert.That(negative.Number,  Is.EqualTo(-42));
                Assert.That(negative.Scale,   Is.EqualTo(-1));

                Assert.That(ScaledNumberType.FromValue(0m).Value,  Is.EqualTo(0m));

            });

        }

        #endregion

        #region ScaledNumber_Roundtrips()

        /// <summary>
        /// Every value which SPINE can carry has to survive the way through a
        /// scaled number and back. These are the numbers of the grid use cases:
        /// a limit in watts, a current in ampere, a price per kilowatt hour.
        /// </summary>
        [Test]
        public void ScaledNumber_Roundtrips()
        {

            Decimal[] values = [ 0m, 1m, -1m, 12m, 118.5m, 4200m, 0.1m, -0.25m, 11000m, 32.0m, 0.0815m ];

            Assert.Multiple(() => {
                foreach (var value in values)
                    Assert.That(ScaledNumberType.FromValue(value).Value,
                                Is.EqualTo(value),
                                $"{value} did not survive the roundtrip.");
            });

        }

        #endregion


        #region Addresses_ReadAsInTheGoReferenceImplementation()

        [Test]
        public void Addresses_ReadAsInTheGoReferenceImplementation()
        {

            Assert.Multiple(() => {

                Assert.That(new DeviceAddressType { Device = "d:_i:19667_HEMS" }.ToString(),
                            Is.EqualTo("d:_i:19667_HEMS"));

                Assert.That(new EntityAddressType { Device = "d:_i:19667_HEMS", Entity = [ 1, 1 ] }.ToString(),
                            Is.EqualTo("d:_i:19667_HEMS:[1,1]:"));

                Assert.That(new FeatureAddressType { Device = "d:_i:19667_HEMS", Entity = [ 1, 1 ], Feature = 6 }.ToString(),
                            Is.EqualTo("d:_i:19667_HEMS:[1,1]:6"));

                // A destination without a device is the usual case within a
                // connection: the partner is the one at the other end.
                Assert.That(new FeatureAddressType { Entity = [ 1 ], Feature = 2 }.ToString(),
                            Is.EqualTo(":[1]:2"));

            });

        }

        #endregion

        #region Address_Matches_TreatsAMissingPartAsAny()

        /// <summary>
        /// SPINE addresses a datagram to a device, to an entity of it or to a
        /// single feature, and which of the three it is can be seen from what is
        /// missing.
        /// </summary>
        [Test]
        public void Address_Matches_TreatsAMissingPartAsAny()
        {

            var feature = new FeatureAddressType { Device = "HEMS", Entity = [ 1, 1 ], Feature = 6 };

            Assert.Multiple(() => {

                Assert.That(new FeatureAddressType { Device = "HEMS" }.Matches(feature),                        Is.True,
                            "The whole device is addressed.");

                Assert.That(new FeatureAddressType { Device = "HEMS", Entity = [ 1, 1 ] }.Matches(feature),     Is.True,
                            "The entity is addressed.");

                Assert.That(feature.Matches(feature),                                                          Is.True);

                Assert.That(new FeatureAddressType().Matches(feature),                                         Is.True,
                            "An empty address addresses anything.");

                Assert.That(new FeatureAddressType { Device = "hems" }.Matches(feature),                        Is.True,
                            "A device address is a name, not a byte string.");

                Assert.That(new FeatureAddressType { Device = "EVSE" }.Matches(feature),                        Is.False);
                Assert.That(new FeatureAddressType { Entity = [ 1 ] }.Matches(feature),                         Is.False,
                            "[1] and [1,1] are different entities.");
                Assert.That(new FeatureAddressType { Feature = 7 }.Matches(feature),                            Is.False);

                Assert.That(feature.Matches(null),                                                             Is.False);

            });

        }

        #endregion

        #region Address_Clone_IsIndependent()

        /// <summary>
        /// The model is mutable, so an address which is kept has to be copied -
        /// including its list of entities.
        /// </summary>
        [Test]
        public void Address_Clone_IsIndependent()
        {

            var original = new FeatureAddressType { Device = "HEMS", Entity = [ 1, 1 ], Feature = 6 };
            var copy     = original.Clone();

            original.Entity![1] = 2;
            original.Feature    = 7;

            Assert.Multiple(() => {
                Assert.That(copy.Entity,   Is.EqualTo(new List<UInt32> { 1, 1 }));
                Assert.That(copy.Feature,  Is.EqualTo(6));
                Assert.That(copy.IsComplete, Is.True);
            });

        }

        #endregion


        #region TimePeriod_Duration_IsCountedFromTheGivenTime()

        /// <summary>
        /// A limit of load control expires; how much time is left is asked for
        /// with a TimeProvider and never against the wall clock.
        /// </summary>
        [Test]
        public void TimePeriod_Duration_IsCountedFromTheGivenTime()
        {

            var now           = new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
            var timeProvider  = new FakeTimeProvider(now);

            Assert.Multiple(() => {

                // A relative end time is the duration itself.
                Assert.That(TimePeriodType.FromDuration(TimeSpan.FromMinutes(10)).Duration(timeProvider),
                            Is.EqualTo(TimeSpan.FromMinutes(10)));

                // An absolute end time is the time left until then.
                Assert.That(new TimePeriodType {
                                EndTime = AbsoluteOrRelativeTimeType.Parse(now.AddMinutes(5))
                            }.Duration(timeProvider),
                            Is.EqualTo(TimeSpan.FromMinutes(5)));

                // A period which has passed has no time left, rather than a
                // negative one.
                Assert.That(new TimePeriodType {
                                EndTime = AbsoluteOrRelativeTimeType.Parse(now.AddMinutes(-5))
                            }.Duration(timeProvider),
                            Is.EqualTo(TimeSpan.Zero));

                // With a start time it is a period and not a countdown.
                Assert.That(new TimePeriodType {
                                StartTime  = AbsoluteOrRelativeTimeType.Parse(now),
                                EndTime    = AbsoluteOrRelativeTimeType.Parse(now.AddMinutes(5))
                            }.Duration(timeProvider),
                            Is.Null);

            });

        }

        #endregion

        #region TimePeriod_IsNotRewrittenWhileReading()

        /// <summary>
        /// The Go reference implementation turns a relative end time into an
        /// absolute one while reading and back while writing. We do not: a test
        /// bench has to be able to say what stood in the datagram.
        /// </summary>
        [Test]
        public void TimePeriod_IsNotRewrittenWhileReading()
        {

            const String json = "{\"endTime\":\"PT10M\"}";

            var period = JsonConvert.DeserializeObject<TimePeriodType>(json, SPINEJSON.StrictSettings);

            Assert.That(period, Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(period!.EndTime?.ToString(),  Is.EqualTo("PT10M"));
                Assert.That(JsonConvert.SerializeObject(period, Formatting.None, SPINEJSON.StrictSettings),
                            Is.EqualTo(json));
            });

        }

        #endregion


        #region PossibleOperations_TellReadFromWrite()

        [Test]
        public void PossibleOperations_TellReadFromWrite()
        {

            var readOnly  = PossibleOperationsType.ReadAndMaybeWrite();
            var writable  = PossibleOperationsType.ReadAndMaybeWrite(Write: true, PartialWrite: true);

            Assert.Multiple(() => {

                Assert.That(readOnly.CanRead,          Is.True);
                Assert.That(readOnly.CanWrite,         Is.False);
                Assert.That(readOnly.CanReadPartial,   Is.False);

                Assert.That(writable.CanRead,          Is.True);
                Assert.That(writable.CanWrite,         Is.True);
                Assert.That(writable.CanWritePartial,  Is.True);

                // "read":{} and "write":{"partial":{}} - the element tags are
                // empty objects, as everywhere in SPINE.
                Assert.That(JsonConvert.SerializeObject(writable, Formatting.None, SPINEJSON.StrictSettings),
                            Is.EqualTo("{\"read\":{},\"write\":{\"partial\":{}}}"));

            });

        }

        #endregion


        #region Cmd_KnowsWhichFunctionItCarries()

        /// <summary>
        /// The payload of a command is a choice of all 142 functions. Which one
        /// is set is read from the generated metadata, so that nothing in the
        /// stack has to know the functions by name.
        /// </summary>
        [Test]
        public void Cmd_KnowsWhichFunctionItCarries()
        {

            var data = new LoadControlLimitListDataType {
                           LoadControlLimitData = [
                               new LoadControlLimitDataType { LimitId = 1, Value = ScaledNumberType.FromValue(4200m) }
                           ]
                       };

            var cmd  = CmdType.For("loadControlLimitListData", data);

            Assert.That(cmd, Is.Not.Null);

            Assert.Multiple(() => {

                Assert.That(cmd!.DataFunction,                              Is.EqualTo("loadControlLimitListData"));
                Assert.That(cmd.Data,                                       Is.SameAs(data));
                Assert.That(cmd.DataCount,                                  Is.EqualTo(1));
                Assert.That(cmd.LoadControlLimitListData,                   Is.SameAs(data));
                Assert.That(cmd.GetData("loadControlLimitListData"),        Is.SameAs(data));
                Assert.That(cmd.GetData("measurementListData"),             Is.Null);
                Assert.That(cmd.ToString(),                                 Is.EqualTo("loadControlLimitListData"));

            });

        }

        #endregion

        #region Cmd_RefusesAnUnknownFunctionAndAWrongType()

        [Test]
        public void Cmd_RefusesAnUnknownFunctionAndAWrongType()
        {

            var cmd = new CmdType();

            Assert.Multiple(() => {

                Assert.That(cmd.SetData("thereIsNoSuchFunction", new MeasurementListDataType()),  Is.False,
                            "An unknown function has no property to put anything into.");

                Assert.That(cmd.SetData("loadControlLimitListData", new MeasurementListDataType()),  Is.False,
                            "The data of a function has to be of its type.");

                Assert.That(cmd.DataFunction,  Is.Null);
                Assert.That(cmd.DataCount,     Is.EqualTo(0));
                Assert.That(cmd.ToString(),    Is.EqualTo("(empty)"));

                Assert.That(CmdType.For("thereIsNoSuchFunction", new MeasurementListDataType()),  Is.Null);

            });

        }

        #endregion

        #region Cmd_EveryFunctionOfTheRegistryCanBeSet()

        /// <summary>
        /// Not one of the 142 functions may be unreachable through the metadata.
        /// </summary>
        [Test]
        public void Cmd_EveryFunctionOfTheRegistryCanBeSet()
        {

            var problems = new List<String>();

            foreach (var function in SPINEFunctions.All)
            {

                var cmd  = new CmdType();
                var data = Activator.CreateInstance(function.DataType);

                if (!cmd.SetData(function.Name, data))
                {
                    problems.Add($"{function.Name} could not be set.");
                    continue;
                }

                if (cmd.DataFunction != function.Name)
                    problems.Add($"{function.Name} was set, but the command reports '{cmd.DataFunction}'.");

                if (!ReferenceEquals(cmd.GetData(function.Name), data))
                    problems.Add($"{function.Name} did not come back.");

            }

            Assert.That(problems, Is.Empty,
                        String.Join(Environment.NewLine, problems));

        }

        #endregion


        #region Filter_CarriesSelectorsAndElementsOfAFunction()

        [Test]
        public void Filter_CarriesSelectorsAndElementsOfAFunction()
        {

            var filter = new FilterType {
                             CmdControl = CmdControlType.ForPartial
                         };

            var selectors = new LoadControlLimitListDataSelectorsType { LimitId = 1 };
            var elements  = new LoadControlLimitDataElementsType      { Value   = new ScaledNumberElementsType() };

            Assert.Multiple(() => {

                Assert.That(filter.SetSelectors("loadControlLimitListData", selectors),  Is.True);
                Assert.That(filter.SetElements ("loadControlLimitListData", elements),   Is.True);

                Assert.That(filter.GetSelectors("loadControlLimitListData"),  Is.SameAs(selectors));
                Assert.That(filter.GetElements ("loadControlLimitListData"),  Is.SameAs(elements));

                Assert.That(filter.FilterFunction,  Is.EqualTo("loadControlLimitListData"));
                Assert.That(filter.IsPartial,       Is.True);
                Assert.That(filter.IsDelete,        Is.False);
                Assert.That(filter.ToString(),      Is.EqualTo("partial loadControlLimitListData"));

                Assert.That(filter.SetSelectors("loadControlLimitListData", elements),  Is.False,
                            "The selectors of a function have to be of its selectors type.");

            });

        }

        #endregion

        #region Filter_WithoutSelectors_MeansEverything()

        /// <summary>
        /// A partial filter without selectors and without elements is legal and
        /// means "all fields"; which function it is about is then the one of the
        /// command.
        /// </summary>
        [Test]
        public void Filter_WithoutSelectors_MeansEverything()
        {

            var filter = new FilterType { CmdControl = CmdControlType.ForPartial };

            Assert.Multiple(() => {
                Assert.That(filter.FilterFunction,  Is.Null);
                Assert.That(filter.IsPartial,       Is.True);
                Assert.That(filter.ToString(),      Is.EqualTo("partial all"));
            });

        }

        #endregion

        #region CmdControl_IsAnElementTagEitherWay()

        [Test]
        public void CmdControl_IsAnElementTagEitherWay()
        {

            Assert.Multiple(() => {

                Assert.That(JsonConvert.SerializeObject(CmdControlType.ForPartial, Formatting.None, SPINEJSON.StrictSettings),
                            Is.EqualTo("{\"partial\":{}}"));

                Assert.That(JsonConvert.SerializeObject(CmdControlType.ForDelete,  Formatting.None, SPINEJSON.StrictSettings),
                            Is.EqualTo("{\"delete\":{}}"));

                Assert.That(CmdControlType.ForDelete.ToString(),   Is.EqualTo("delete"));
                Assert.That(CmdControlType.ForPartial.ToString(),  Is.EqualTo("partial"));

            });

        }

        #endregion


        #region Datagram_DescribesItselfInOneLine()

        [Test]
        public void Datagram_DescribesItselfInOneLine()
        {

            var datagram = new DatagramType {

                               Header   = new HeaderType {
                                              AddressSource       = new FeatureAddressType { Device = "d:_i:19667_HEMS", Entity = [ 1 ], Feature = 6 },
                                              AddressDestination  = new FeatureAddressType { Entity = [ 1 ], Feature = 2 },
                                              MsgCounter          = 42,
                                              CmdClassifier       = CmdClassifierType.Read
                                          },

                               Payload  = new PayloadType {
                                              Cmd = [ CmdType.For("loadControlLimitListData", new LoadControlLimitListDataType())! ]
                                          }

                           };

            Assert.Multiple(() => {

                Assert.That(datagram.ToString(),
                            Is.EqualTo("read 42: d:_i:19667_HEMS:[1]:6 -> :[1]:2 loadControlLimitListData"));

                Assert.That(datagram.Command,   Is.Not.Null);
                Assert.That(datagram.Commands.Count(),  Is.EqualTo(1));

            });

        }

        #endregion

        #region Datagram_OfAResult_ShowsTheError()

        [Test]
        public void Datagram_OfAResult_ShowsTheError()
        {

            var datagram = new DatagramType {

                               Header   = new HeaderType {
                                              AddressSource        = new FeatureAddressType { Entity = [ 1 ], Feature = 2 },
                                              AddressDestination   = new FeatureAddressType { Entity = [ 1 ], Feature = 6 },
                                              MsgCounter           = 43,
                                              MsgCounterReference  = 42,
                                              CmdClassifier        = CmdClassifierType.Result
                                          },

                               Payload  = new PayloadType {
                                              Cmd = [ new CmdType {
                                                          ResultData = ResultDataType.Error(
                                                                           SPINEErrorNumbers.BindingIsNecessaryForThisCommand
                                                                       )
                                                      } ]
                                          }

                           };

            Assert.That(datagram.ToString(),
                        Is.EqualTo("result 43 ref 42: :[1]:2 -> :[1]:6 resultData " +
                                   "(binding is necessary for this command)"));

        }

        #endregion


        #region Result_WithoutAnErrorNumber_IsSuccess()

        /// <summary>
        /// SPINE acknowledges a write with "resultData" and the error number 0;
        /// leaving the number out means the same thing.
        /// </summary>
        [Test]
        public void Result_WithoutAnErrorNumber_IsSuccess()
        {

            Assert.Multiple(() => {

                Assert.That(new ResultDataType().IsSuccess,                                 Is.True);
                Assert.That(ResultDataType.Success().IsSuccess,                             Is.True);
                Assert.That(ResultDataType.Error(SPINEErrorNumbers.Timeout).IsError,        Is.True);

                Assert.That(ResultDataType.Error(SPINEErrorNumbers.CommandRejected, "no").ToString(),
                            Is.EqualTo("command rejected: no"));

                // An error number the specification does not list is still a
                // legal error number.
                Assert.That(SPINEErrorNumbers.Name(4711),  Is.EqualTo("error 4711"));

            });

        }

        #endregion


        #region UseCaseInformation_IsSearchedByNameAndScenario()

        [Test]
        public void UseCaseInformation_IsSearchedByNameAndScenario()
        {

            var information = new UseCaseInformationDataType {
                                  Actor    = "ControllableSystem",
                                  Address  = new FeatureAddressType { Entity = [ 1 ], Feature = 1 }
                              };

            information.Set(new UseCaseSupportType {
                                UseCaseName      = "limitationOfPowerConsumption",
                                UseCaseVersion   = "1.0.0",
                                ScenarioSupport  = [ 1, 2, 3, 4 ]
                            });

            Assert.Multiple(() => {

                Assert.That(information.Supports("limitationOfPowerConsumption"),          Is.True);
                Assert.That(information.Supports("limitationOfPowerConsumption", 2),       Is.True);
                Assert.That(information.Supports("limitationOfPowerConsumption", 9),       Is.False);
                Assert.That(information.Supports("monitoringOfPowerConsumption"),          Is.False);

                Assert.That(information.Find("limitationOfPowerConsumption")?.UseCaseVersion,  Is.EqualTo("1.0.0"));

            });

            // Setting it again replaces it rather than announcing it twice.
            information.Set(new UseCaseSupportType {
                                UseCaseName       = "limitationOfPowerConsumption",
                                UseCaseVersion    = "1.0.0",
                                UseCaseAvailable  = false,
                                ScenarioSupport   = [ 1 ]
                            });

            Assert.Multiple(() => {

                Assert.That(information.UseCaseSupport!.Count,                       Is.EqualTo(1));
                Assert.That(information.Supports("limitationOfPowerConsumption"),    Is.False,
                            "A use case which is not available is not supported right now.");

                Assert.That(information.Remove("limitationOfPowerConsumption"),      Is.True);
                Assert.That(information.UseCaseSupport,                              Is.Empty);
                Assert.That(information.Remove("limitationOfPowerConsumption"),      Is.False);

            });

        }

        #endregion

    }

}
