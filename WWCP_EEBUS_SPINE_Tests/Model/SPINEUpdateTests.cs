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

using System.Collections;
using System.Reflection;

using Newtonsoft.Json;

using NUnit.Framework;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// The update system of SPINE, where the official examples do not reach.
    ///
    /// The 29 example datagrams of the specification cover the combinations it
    /// recommends for standard feature types; the specification is explicit that
    /// they are "not exhaustive". What is tested here is the rest of section
    /// 5.3.4 - selectors which select more than one entry, list items without
    /// identifiers, lists whose entries cannot be identified at all - together
    /// with the properties the whole thing stands on: that it never changes the
    /// data it was given, and that a refused write changes nothing.
    ///
    /// None of it needs the specifications to be present.
    /// </summary>
    [TestFixture]
    public class SPINEUpdateTests
    {

        #region (private) Building blocks

        private static ScaledNumberType Number(Int64 Value, Int16? Scale = null)

            => new () { Number = Value, Scale = Scale };


        private static CmdType Partial(String Function, Object? Data, Object? Selectors = null)
        {

            var cmd     = new CmdType {
                              Function  = FunctionType.Parse(Function),
                              Filter    = [ new FilterType { CmdControl = CmdControlType.ForPartial } ]
                          };

            if (Selectors is not null)
                cmd.Filter![0].SetSelectors(Function, Selectors);

            if (Data is not null)
                cmd.SetData(Function, Data);

            return cmd;

        }


        private static CmdType Delete(String Function, Object? Selectors = null, Object? Elements = null)
        {

            var cmd     = new CmdType {
                              Function  = FunctionType.Parse(Function),
                              Filter    = [ new FilterType { CmdControl = CmdControlType.ForDelete } ]
                          };

            if (Selectors is not null)
                cmd.Filter![0].SetSelectors(Function, Selectors);

            if (Elements is not null)
                cmd.Filter![0].SetElements (Function, Elements);

            return cmd;

        }


        private static MeasurementListDataType Measurements(params MeasurementDataType[] Entries)

            => new () { MeasurementData = [.. Entries] };


        private static MeasurementDataType Measurement(UInt32 Id, String ValueType, Int64 Value)

            => new () {
                   MeasurementId  = Id,
                   ValueType      = MeasurementValueTypeType.Parse(ValueType),
                   Value          = Number(Value)
               };


        private static MeasurementDataType? Entry(MeasurementListDataType? List, UInt32 Id, String ValueType)

            => List?.MeasurementData?.FirstOrDefault(entry => entry.MeasurementId == Id &&
                                                              entry.ValueType     == MeasurementValueTypeType.Parse(ValueType));

        #endregion


        #region EveryProperty_IsAModelTypeAListOrAnImmutableValue()

        /// <summary>
        /// The update system copies the data before it changes it, and it does
        /// so by walking the model: a data type is copied property by property,
        /// a list entry by entry, and everything else is passed on as it is.
        ///
        /// That last step is only sound while "everything else" cannot be
        /// changed behind our back. This is the test which says so - for all
        /// 2133 properties of the model at once.
        /// </summary>
        [Test]
        public void EveryProperty_IsAModelTypeAListOrAnImmutableValue()
        {

            var problems = new List<String>();

            foreach (var type in typeof(CmdType).Assembly.
                                     GetTypes().
                                     Where(type => SPINETypeInfo.IsModelType(type) &&
                                                   type.IsPublic && !type.IsNested && !type.IsAbstract))

                foreach (var property in SPINETypeInfo.Of(type).Properties)
                {

                    var valueType = property.ValueType;

                    if (property.IsModelType     ||
                        valueType.IsValueType    ||   // numbers, booleans and the ISO 8601 structs
                        valueType == typeof(String))
                        continue;

                    problems.Add($"{type.Name}.{property.JSONName} holds a '{valueType.Name}', " +
                                  "which is neither a data type of the model nor an immutable value.");

                }

            Assert.Multiple(() => {

                Assert.That(problems, Is.Empty,
                            String.Join(Environment.NewLine, problems));

                Assert.That(typeof(CmdType).Assembly.GetTypes().
                                Count(type => SPINETypeInfo.IsModelType(type) && type.IsPublic && !type.IsNested),
                            Is.GreaterThan(500),
                            "The model was not found.");

            });

        }

        #endregion

        #region AnUpdate_NeverChangesTheDataItWasGiven()

        /// <summary>
        /// Neither the data of the device nor the data of the command comes back
        /// changed. A device which hands its function data to the update system
        /// and then finds it modified would have no way to refuse anything.
        /// </summary>
        [Test]
        public void AnUpdate_NeverChangesTheDataItWasGiven()
        {

            var before    = Measurements(Measurement(1, "value", 100));
            var incoming  = Measurements(new MeasurementDataType {
                                             MeasurementId  = 1,
                                             ValueType      = MeasurementValueTypeType.Parse("value"),
                                             Value          = Number(250)
                                         });

            var result    = SPINEUpdate.Apply(before,
                                              incoming,
                                              Partial("measurementListData", incoming));

            Assert.Multiple(() => {

                Assert.That(result.Success,                                    Is.True, result.Problem);
                Assert.That(Entry(result.Data, 1, "value")!.Value!.Number,     Is.EqualTo(250));

                Assert.That(before.MeasurementData![0].Value!.Number,          Is.EqualTo(100), "The data of the device was changed.");
                Assert.That(incoming.MeasurementData![0].Value!.Number,        Is.EqualTo(250), "The data of the command was changed.");
                Assert.That(ReferenceEquals(result.Data!.MeasurementData![0],
                                            before.MeasurementData![0]),       Is.False,        "The result shares an entry with the data of the device.");

            });

        }

        #endregion

        #region WithoutAFilter_TheFunctionIsExchangedAsAWhole()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.1: "if full functions are requested or exchanged,
        /// cmdOptions are NOT used". Without a filter there is nothing partial
        /// about the command, and what arrives is the function from now on -
        /// including the entries it does not mention, which are gone.
        /// </summary>
        [Test]
        public void WithoutAFilter_TheFunctionIsExchangedAsAWhole()
        {

            var before    = Measurements(Measurement(1, "value", 100),
                                         Measurement(2, "value", 200));

            var incoming  = Measurements(Measurement(3, "value", 300));

            var result    = SPINEUpdate.Apply(before,
                                              incoming,
                                              new CmdType { Function = FunctionType.Parse("measurementListData") });

            Assert.Multiple(() => {
                Assert.That(result.Success,                    Is.True, result.Problem);
                Assert.That(result.Data!.MeasurementData,      Has.Count.EqualTo(1));
                Assert.That(Entry(result.Data, 3, "value"),    Is.Not.Null);
            });

        }

        #endregion

        #region AListEntryWithoutIdentifiers_IsAppliedToEveryEntry()

        /// <summary>
        /// SPINE 1.3.0, Table 6 and Table 7, note *1: "list items in
        /// &lt;FUNCTION&gt; that have NO identifier SHALL be applied to all
        /// corresponding list entries of the data owner".
        /// </summary>
        [Test]
        public void AListEntryWithoutIdentifiers_IsAppliedToEveryEntry()
        {

            var before    = Measurements(Measurement(1, "value",    100),
                                         Measurement(2, "minValue", 200));

            var incoming  = Measurements(new MeasurementDataType {
                                             ValueSource = MeasurementValueSourceType.Parse("measuredValue")
                                         });

            var result    = SPINEUpdate.Apply(before,
                                              incoming,
                                              Partial("measurementListData", incoming));

            Assert.Multiple(() => {

                Assert.That(result.Success,                Is.True, result.Problem);
                Assert.That(result.Data!.MeasurementData,  Has.Count.EqualTo(2), "An entry was added instead.");

                foreach (var entry in result.Data!.MeasurementData!)
                    Assert.That(entry.ValueSource?.ToString(), Is.EqualTo("measuredValue"));

                // ... and nothing else was touched.
                Assert.That(Entry(result.Data, 1, "value")!.   Value!.Number, Is.EqualTo(100));
                Assert.That(Entry(result.Data, 2, "minValue")!.Value!.Number, Is.EqualTo(200));

            });

        }

        #endregion

        #region SelectorsOfAPartialFilter_ChangeEveryEntryTheySelect()

        /// <summary>
        /// SPINE 1.3.0, Table 6: "&lt;SELECTORS&gt; specify locations (specific
        /// identifiable list items) where &lt;FUNCTION&gt; data is added or
        /// modified. &lt;FUNCTION&gt; SHALL NOT use identifiers.
        /// &lt;SELECTORS&gt; SHALL match with already existing locations.
        /// Therefore, it is not possible to add new list entries."
        ///
        /// Locations, in the plural: a selector which names something that is
        /// not an identifier - here the type of a measured value - selects every
        /// entry which has it. The Go reference implementation stops at the
        /// first one and reads only the first entry of the command; see
        /// docs/spec-deviations.md, S4.
        /// </summary>
        [Test]
        public void SelectorsOfAPartialFilter_ChangeEveryEntryTheySelect()
        {

            var before     = Measurements(Measurement(1, "minValue", 100),
                                          Measurement(2, "minValue", 200),
                                          Measurement(3, "value",    300));

            var incoming   = Measurements(new MeasurementDataType {
                                              ValueState = MeasurementValueStateType.Parse("outOfRange")
                                          });

            var selectors  = new MeasurementListDataSelectorsType {
                                 ValueType = MeasurementValueTypeType.Parse("minValue")
                             };

            var result     = SPINEUpdate.Apply(before,
                                               incoming,
                                               Partial("measurementListData", incoming, selectors));

            Assert.Multiple(() => {

                Assert.That(result.Success,                                     Is.True, result.Problem);
                Assert.That(result.Data!.MeasurementData,                       Has.Count.EqualTo(3), "An entry was added.");

                Assert.That(Entry(result.Data, 1, "minValue")!.ValueState?.ToString(), Is.EqualTo("outOfRange"));
                Assert.That(Entry(result.Data, 2, "minValue")!.ValueState?.ToString(), Is.EqualTo("outOfRange"),
                            "Only the first of the selected entries was changed.");
                Assert.That(Entry(result.Data, 3, "value")!.   ValueState,             Is.Null,
                            "An entry which was not selected was changed.");

            });

        }

        #endregion

        #region Selectors_AreAnAndAcrossEverythingTheyName()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.7.1: "all identifiable list entries, where every
        /// referenced child element has a corresponding value, are selected [...]
        /// an identifiable list entry is not selected if no or just some of the
        /// referenced child elements match".
        /// </summary>
        [Test]
        public void Selectors_AreAnAndAcrossEverythingTheyName()
        {

            var one    = Measurement(1, "minValue", 100);
            var two    = Measurement(1, "maxValue", 200);
            var three  = Measurement(2, "minValue", 300);

            var both   = new MeasurementListDataSelectorsType {
                             MeasurementId  = 1,
                             ValueType      = MeasurementValueTypeType.Parse("minValue")
                         };

            Assert.Multiple(() => {

                Assert.That(SPINESelectors.Matches(both, one),   Is.True);
                Assert.That(SPINESelectors.Matches(both, two),   Is.False, "Only the identifier matched.");
                Assert.That(SPINESelectors.Matches(both, three), Is.False, "Only the value type matched.");

                Assert.That(SPINESelectors.Matches(null, one),   Is.True,  "A command without selectors selects everything.");

            });

        }

        #endregion

        #region AnAddressSelector_MatchesOnlyTheExactEntityAddress()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.7.2: "still only exact matches of an entity
        /// address part of a selectors with an entity address part within a
        /// function are considered as valid matches". The example of the
        /// specification: a selector for entity 4 matches the entity "4", but
        /// not the entity "1/4" - which is a different entity that happens to
        /// end in the same number.
        /// </summary>
        [Test]
        public void AnAddressSelector_MatchesOnlyTheExactEntityAddress()
        {

            var deep     = new BindingManagementEntryDataType {
                               BindingId      = 1,
                               ClientAddress  = new FeatureAddressType {
                                                    Device   = "d:_i:46925_someDevice",
                                                    Entity   = [ 1, 4 ],
                                                    Feature  = 7
                                                }
                           };

            var shallow  = new BindingManagementEntryDataType {
                               BindingId      = 2,
                               ClientAddress  = new FeatureAddressType {
                                                    Device   = "d:_i:46925_someDevice",
                                                    Entity   = [ 4 ],
                                                    Feature  = 1
                                                }
                           };

            var selector = new BindingManagementEntryListDataSelectorsType {
                               ClientAddress = new FeatureAddressType { Entity = [ 4 ] }
                           };

            Assert.Multiple(() => {
                Assert.That(SPINESelectors.Matches(selector, shallow), Is.True);
                Assert.That(SPINESelectors.Matches(selector, deep),    Is.False,
                            "The entity 1/4 was selected by a selector for the entity 4.");
            });

        }

        #endregion

        #region Entries_AreMergedByTheirIdentifiersRatherThanTheirPosition()

        /// <summary>
        /// A composite identifier names one entry: a measurement has a primary
        /// identifier and a sub-identifier, and "1/minValue" is not "1/value".
        /// </summary>
        [Test]
        public void Entries_AreMergedByTheirIdentifiersRatherThanTheirPosition()
        {

            var before    = Measurements(Measurement(1, "value",    100),
                                         Measurement(1, "minValue",  50));

            var incoming  = Measurements(Measurement(1, "minValue",  75),
                                         Measurement(2, "value",    200));

            var result    = SPINEUpdate.Apply(before,
                                              incoming,
                                              Partial("measurementListData", incoming));

            Assert.Multiple(() => {
                Assert.That(result.Success,                                    Is.True, result.Problem);
                Assert.That(result.Data!.MeasurementData,                      Has.Count.EqualTo(3));
                Assert.That(Entry(result.Data, 1, "value")!.   Value!.Number,  Is.EqualTo(100), "untouched");
                Assert.That(Entry(result.Data, 1, "minValue")!.Value!.Number,  Is.EqualTo(75),  "merged");
                Assert.That(Entry(result.Data, 2, "value")!.   Value!.Number,  Is.EqualTo(200), "added");
            });

        }

        #endregion

        #region EntriesWhichSayNothingButTheirName_AreKeptUnlessAskedOtherwise()

        /// <summary>
        /// A device which announces the structure of a list before it sends the
        /// data sends entries which carry nothing but their identifiers. The Go
        /// reference implementation drops them, and there is a good reason for
        /// that in a stack: they would become empty rows.
        ///
        /// A test bench cannot afford it. An entry added by a write has to be
        /// complete (SPINE 1.3.0, Annex A), so an entry which is not is a
        /// finding - and one which was dropped on the way in cannot be reported.
        /// The behaviour of the reference implementation is one option away.
        /// </summary>
        [Test]
        public void EntriesWhichSayNothingButTheirName_AreKeptUnlessAskedOtherwise()
        {

            var incoming  = Measurements(new MeasurementDataType {
                                             MeasurementId  = 7,
                                             ValueType      = MeasurementValueTypeType.Parse("value")
                                         });

            var cmd       = Partial("measurementListData", incoming);

            var kept      = SPINEUpdate.Apply(Measurements(), incoming, cmd);

            var dropped   = SPINEUpdate.Apply(Measurements(), incoming, cmd,
                                              new SPINEUpdateOptions(IgnoreEntriesWithoutData: true));

            Assert.Multiple(() => {
                Assert.That(kept.   Data!.MeasurementData, Has.Count.EqualTo(1));
                Assert.That(dropped.Data!.MeasurementData, Is.Empty);
            });

        }

        #endregion

        #region TheResult_IsOrderedByTheIdentifiersOfItsEntries()

        /// <summary>
        /// SPINE identifies the entries of a list rather than ordering them, so
        /// ordering them changes no meaning - and makes two runs of the same
        /// exchange comparable, which a test bench lives on.
        /// </summary>
        [Test]
        public void TheResult_IsOrderedByTheIdentifiersOfItsEntries()
        {

            var before    = Measurements(Measurement(3, "value", 300));
            var incoming  = Measurements(Measurement(1, "value", 100),
                                         Measurement(2, "value", 200));

            var result    = SPINEUpdate.Apply(before,
                                              incoming,
                                              Partial("measurementListData", incoming));

            Assert.That(result.Data!.MeasurementData!.Select(entry => entry.MeasurementId),
                        Is.EqualTo(new UInt32?[] { 1, 2, 3 }));

        }

        #endregion


        #region ARefusedWrite_ChangesNothingAtAll()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.2: "a write operation with restricted function
        /// exchange SHALL ONLY be executed by a server if it can execute the
        /// received operation completely."
        ///
        /// The command changes two limits, and the device allows one of them to
        /// be changed. Neither of them is changed. The Go reference
        /// implementation applies what it can and reports the failure
        /// afterwards; see docs/spec-deviations.md, S6.
        /// </summary>
        [Test]
        public void ARefusedWrite_ChangesNothingAtAll()
        {

            var before    = new LoadControlLimitListDataType {
                                LoadControlLimitData = [
                                    new LoadControlLimitDataType { LimitId = 1, IsLimitChangeable = true,  Value = Number(1000) },
                                    new LoadControlLimitDataType { LimitId = 2, IsLimitChangeable = false, Value = Number(2000) }
                                ]
                            };

            var incoming  = new LoadControlLimitListDataType {
                                LoadControlLimitData = [
                                    new LoadControlLimitDataType { LimitId = 1, Value = Number(1500) },
                                    new LoadControlLimitDataType { LimitId = 2, Value = Number(2500) }
                                ]
                            };

            var result    = SPINEUpdate.Apply(before,
                                              incoming,
                                              Partial("loadControlLimitListData", incoming),
                                              SPINEUpdateOptions.Write);

            Assert.Multiple(() => {

                Assert.That(result.Success, Is.False);
                Assert.That(result.Problem, Does.Contain("does not allow"));

                Assert.That(result.Data!.LoadControlLimitData![0].Value!.Number, Is.EqualTo(1000),
                            "The limit which may be changed was changed, although the command as a whole was refused.");
                Assert.That(result.Data!.LoadControlLimitData![1].Value!.Number, Is.EqualTo(2000));

            });

        }

        #endregion

        #region ANotifyOfTheSameChange_IsNotRefused()

        /// <summary>
        /// The write mark answers "may somebody else change this?", and a notify
        /// is not somebody else changing anything - it is the owner of the data
        /// saying what it changed. Refusing that would mean refusing to believe
        /// a device about its own state.
        /// </summary>
        [Test]
        public void ANotifyOfTheSameChange_IsNotRefused()
        {

            var before    = new LoadControlLimitListDataType {
                                LoadControlLimitData = [
                                    new LoadControlLimitDataType { LimitId = 1, IsLimitChangeable = false, Value = Number(1000) }
                                ]
                            };

            var incoming  = new LoadControlLimitListDataType {
                                LoadControlLimitData = [
                                    new LoadControlLimitDataType { LimitId = 1, Value = Number(1500) }
                                ]
                            };

            var result    = SPINEUpdate.Apply(before,
                                              incoming,
                                              Partial("loadControlLimitListData", incoming),
                                              SPINEUpdateOptions.Notify);

            Assert.Multiple(() => {
                Assert.That(result.Success,                                      Is.True, result.Problem);
                Assert.That(result.Data!.LoadControlLimitData![0].Value!.Number, Is.EqualTo(1500));
            });

        }

        #endregion


        #region ADeleteOfANestedElement_ReachesIntoIt()

        /// <summary>
        /// An elements instance which names elements of its own reaches one
        /// level deeper: naming "timePeriod" deletes the whole time period,
        /// naming "timePeriod.startTime" deletes only its start.
        /// </summary>
        [Test]
        public void ADeleteOfANestedElement_ReachesIntoIt()
        {

            var before    = new LoadControlLimitListDataType {
                                LoadControlLimitData = [
                                    new LoadControlLimitDataType {
                                        LimitId     = 1,
                                        TimePeriod  = new TimePeriodType {
                                                          StartTime  = AbsoluteOrRelativeTimeType.Parse("2026-07-26T10:00:00Z"),
                                                          EndTime    = AbsoluteOrRelativeTimeType.Parse("2026-07-26T12:00:00Z")
                                                      }
                                    }
                                ]
                            };

            var deep      = SPINEUpdate.Apply(before,
                                              new LoadControlLimitListDataType(),
                                              Delete("loadControlLimitListData",
                                                     Elements: new LoadControlLimitDataElementsType {
                                                                   TimePeriod = new TimePeriodElementsType { StartTime = ElementTagType.Set }
                                                               }));

            var whole     = SPINEUpdate.Apply(before,
                                              new LoadControlLimitListDataType(),
                                              Delete("loadControlLimitListData",
                                                     Elements: new LoadControlLimitDataElementsType {
                                                                   TimePeriod = new TimePeriodElementsType()
                                                               }));

            Assert.Multiple(() => {

                Assert.That(deep. Data!.LoadControlLimitData![0].TimePeriod,            Is.Not.Null);
                Assert.That(deep. Data!.LoadControlLimitData![0].TimePeriod!.StartTime, Is.Null);
                Assert.That(deep. Data!.LoadControlLimitData![0].TimePeriod!.EndTime,   Is.Not.Null);

                Assert.That(whole.Data!.LoadControlLimitData![0].TimePeriod,            Is.Null);

            });

        }

        #endregion

        #region ARead_KeepsTheIdentifiersEvenWhereItWasNotAskedTo()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.5: "in case of lists with identifiable list items,
        /// identifiers of each list item in the reply SHALL be full, even if the
        /// corresponding read operation made use of elements selection with
        /// &lt;ELEMENTS&gt; but did not specify the elements of the identifier."
        /// </summary>
        [Test]
        public void ARead_KeepsTheIdentifiersEvenWhereItWasNotAskedTo()
        {

            var data    = Measurements(Measurement(1, "value",    100),
                                       Measurement(2, "minValue", 200));

            var cmd     = new CmdType {
                              Function  = FunctionType.Parse("measurementListData"),
                              Filter    = [ new FilterType { CmdControl = CmdControlType.ForPartial } ]
                          };

            cmd.Filter![0].SetSelectors("measurementListData", new MeasurementListDataSelectorsType { MeasurementId = 1 });
            cmd.Filter![0].SetElements ("measurementListData", new MeasurementDataElementsType      { Value         = new ScaledNumberElementsType() });

            var answer  = SPINERead.Apply(data, cmd);

            Assert.Multiple(() => {
                Assert.That(answer!.MeasurementData,                 Has.Count.EqualTo(1));
                Assert.That(answer!.MeasurementData![0].MeasurementId, Is.EqualTo(1));
                Assert.That(answer!.MeasurementData![0].ValueType,     Is.Not.Null, "The sub-identifier of the entry is missing.");
                Assert.That(answer!.MeasurementData![0].Value!.Number, Is.EqualTo(100));
            });

        }

        #endregion


        #region AListWhoseEntriesHaveNoIdentifiers_IsReportedAndExchangedAsAWhole()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.1: "for non-identifiable list entries [...] it is
        /// not possible to transport only some entries of the list. This means
        /// that a list with non-identifiable list entries can only be
        /// transmitted as a whole."
        ///
        /// A partial notify of such a list is therefore a message which should
        /// not exist. It is reported, and the list is taken as a whole - a test
        /// bench which threw the data away would have nothing left to show.
        /// </summary>
        [Test]
        public void AListWhoseEntriesHaveNoIdentifiers_IsReportedAndExchangedAsAWhole()
        {

            var before    = new SensingListDataType {
                                SensingData = [
                                    new SensingDataType { State = SensingStateType.Parse("on") }
                                ]
                            };

            var incoming  = new SensingListDataType {
                                SensingData = [
                                    new SensingDataType { State = SensingStateType.Parse("off") }
                                ]
                            };

            var result    = SPINEUpdate.Apply(before,
                                              incoming,
                                              Partial("sensingListData", incoming));

            Assert.Multiple(() => {
                Assert.That(result.Success,                             Is.False);
                Assert.That(result.Problem,                             Does.Contain("no identifiers"));
                Assert.That(result.Data!.SensingData,                   Has.Count.EqualTo(1));
                Assert.That(result.Data!.SensingData![0].State?.ToString(), Is.EqualTo("off"));
            });

        }

        #endregion

        #region TwoFiltersOfTheSameKind_AreRefused()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.2: "in this version of the specification, at
        /// maximum one delete filter and at maximum one partial filter SHALL be
        /// used in one command."
        /// </summary>
        [Test]
        public void TwoFiltersOfTheSameKind_AreRefused()
        {

            var incoming  = Measurements(Measurement(1, "value", 100));

            var cmd       = Partial("measurementListData", incoming);
            cmd.Filter!.Add(new FilterType { CmdControl = CmdControlType.ForPartial });

            var result    = SPINEUpdate.Apply(Measurements(), incoming, cmd);

            Assert.Multiple(() => {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Problem, Does.Contain("5.3.4.2"));
                Assert.That(result.Data!.MeasurementData, Is.Empty, "The command was carried out anyway.");
            });

        }

        #endregion

        #region AFilterWithoutTheNameOfItsFunction_IsRefused()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.1: "the cmdOption datagram.payload.cmd.function
        /// SHALL be used and include the correct function name if and only if
        /// any of the other cmdOptions is used."
        ///
        /// The payload and the filter name their function themselves, so such a
        /// command can still be carried out - and is reported, because a device
        /// which leaves the name out will meet one which insists on it.
        /// </summary>
        [Test]
        public void AFilterWithoutTheNameOfItsFunction_IsRefused()
        {

            var before    = Measurements(Measurement(1, "value", 100));
            var incoming  = Measurements(Measurement(1, "value", 250));

            var cmd       = new CmdType {
                                Filter = [ new FilterType { CmdControl = CmdControlType.ForPartial } ]
                            };

            cmd.SetData("measurementListData", incoming);

            var result    = SPINEUpdate.Apply(before, incoming, cmd);

            Assert.Multiple(() => {
                Assert.That(result.Success,                                   Is.False);
                Assert.That(result.Problem,                                   Does.Contain("5.3.4.1"));
                Assert.That(result.Data!.MeasurementData![0].Value!.Number,   Is.EqualTo(250),
                            "The command names its function through its payload, so it can be carried out.");
            });

        }

        #endregion

        #region ElementsOnAPartialWriteFilter_AreReported()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.8: "&lt;ELEMENTS&gt; SHALL only be used in the
        /// following two cases: data deletion (write/notify), partial read."
        ///
        /// A partial write which names elements is therefore out of spec. The
        /// data is still updated - what the command says is unambiguous - but
        /// the command is reported for what it is, which is the whole point of
        /// running this within a test bench.
        /// </summary>
        [Test]
        public void ElementsOnAPartialWriteFilter_AreReported()
        {

            var before    = Measurements(Measurement(1, "value", 100));
            var incoming  = Measurements(Measurement(1, "value", 250));

            var cmd       = Partial("measurementListData", incoming);
            cmd.Filter![0].SetElements("measurementListData", new MeasurementDataElementsType { Value = new ScaledNumberElementsType() });

            var result    = SPINEUpdate.Apply(before, incoming, cmd);

            Assert.Multiple(() => {
                Assert.That(result.Success,                                  Is.False);
                Assert.That(result.Problem,                                  Does.Contain("5.3.4.8"));
                Assert.That(Entry(result.Data, 1, "value")!.Value!.Number,   Is.EqualTo(250));
            });

        }

        #endregion


        #region TheWriteMarks_AreThoseOfTheGoReferenceImplementation()

        /// <summary>
        /// Which property says that a remote peer may change a data type cannot
        /// be read from the XSD: "isLimitChangeable" is a boolean like any
        /// other there. The generator takes the three of them from the struct
        /// tags of spine-go, and this is the fixture saying so.
        /// </summary>
        [Test]
        public void TheWriteMarks_AreThoseOfTheGoReferenceImplementation()
        {

            var fixture = JsonConvert.DeserializeObject<GoModel>(
                              File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
                                                            "TestData", "spine-go-model.json"))
                          );

            var expected = fixture!.Types.
                               Where     (type => type.Value.WriteCheck is not null).
                               SelectMany(type => type.Value.WriteCheck!.Select(field => $"{type.Key}.{field}")).
                               Order(StringComparer.Ordinal).
                               ToList();

            var actual   = typeof(CmdType).Assembly.GetTypes().
                               Where     (type => SPINETypeInfo.IsModelType(type) && type.IsPublic && !type.IsNested && !type.IsAbstract).
                               Where     (type => SPINETypeInfo.Of(type).WriteCheck is not null).
                               Select    (type => $"{type.Name}.{SPINETypeInfo.Of(type).WriteCheck!.JSONName}").
                               Order(StringComparer.Ordinal).
                               ToList();

            Assert.Multiple(() => {
                Assert.That(expected, Has.Count.EqualTo(3), "The Go model no longer marks three properties.");
                Assert.That(actual,   Is.EqualTo(expected));
            });

        }


        private sealed class GoModel
        {
            [JsonProperty("types")]
            public Dictionary<String, GoType> Types { get; set; } = [];
        }

        private sealed class GoType
        {
            [JsonProperty("writeCheck")]
            public List<String>? WriteCheck { get; set; }
        }

        #endregion

    }

}
