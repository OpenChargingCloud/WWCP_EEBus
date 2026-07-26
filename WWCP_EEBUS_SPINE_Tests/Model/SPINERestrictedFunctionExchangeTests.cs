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

using cloud.charging.open.protocols.EEBUS.SHIP;
using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// The restricted function exchange, against the examples of the
    /// specification itself (SPINE 1.3.0, Annex A, Table 21).
    ///
    /// The specification ships 29 example datagrams which show, one combination
    /// at a time, what a partial write, a partial notify, a partial read and a
    /// partial reply look like - and states for each of them which rules apply.
    /// They are the closest thing to a normative test suite the update system
    /// has, and every one of them is used here.
    ///
    /// The specifications are licensed material and are not committed; without
    /// them these tests report "inconclusive".
    /// </summary>
    [TestFixture]
    public class SPINERestrictedFunctionExchangeTests
    {

        #region (private) The examples

        /// <summary>
        /// The command of one official example.
        /// </summary>
        private static CmdType Cmd(String Name)
        {

            if (SPINEExampleXML.Directory() is null)
                Assert.Inconclusive("The SPINE specifications are not checked out below docs/specs, " +
                                    "so the official restricted function exchange examples are missing.");

            var datagram = JsonConvert.DeserializeObject<DatagramType>(
                               SPINEExampleXML.Load(Name).ToString(),
                               SPINEJSON.StrictSettings
                           );

            Assert.That(datagram?.Payload?.Cmd, Is.Not.Null.And.Count.EqualTo(1),
                        $"The example '{Name}' does not hold exactly one command.");

            return datagram!.Payload!.Cmd![0];

        }

        #endregion

        #region (private) The data the examples operate on

        private static ScaledNumberType Number(Int64 Value, Int16? Scale = null)

            => new () { Number = Value, Scale = Scale };


        private static SetpointListDataType Setpoints(params SetpointDataType[] Entries)

            => new () { SetpointData = [.. Entries] };


        /// <summary>
        /// The setpoints a server holds before the examples are applied. Every
        /// entry allows a remote write: the examples do not model the write
        /// mark, and a server refusing everything would prove nothing.
        /// </summary>
        private static SetpointListDataType TwoSetpoints()

            => Setpoints(

                   new SetpointDataType {
                       SetpointId            = 1,
                       Value                 = Number(105, -1),
                       ValueMin              = Number(9),
                       ValueMax              = Number(155, -1),
                       IsSetpointChangeable  = true
                   },

                   new SetpointDataType {
                       SetpointId            = 2,
                       Value                 = Number(200, -1),
                       ValueMin              = Number(9),
                       IsSetpointChangeable  = true
                   }

               );


        private static DeviceClassificationUserDataType UserData()

            => new () {
                   UserNodeIdentification  = "node",
                   UserLabel               = "oldUserLabel",
                   UserDescription         = "oldUserDescription"
               };


        private static SetpointDataType? Entry(SetpointListDataType? List, UInt32 Id)

            => List?.SetpointData?.FirstOrDefault(entry => entry.SetpointId == Id);

        #endregion


        #region Every_OfficialExample_IsReadAndWrittenAgainWithoutLoss()

        /// <summary>
        /// All 29 examples are read by the model, and everything they state
        /// comes out again. An element the model does not know is an error
        /// rather than a value quietly dropped - the strict settings see to
        /// that, and the conversion from XML sees to the other direction.
        /// </summary>
        [Test]
        public void Every_OfficialExample_IsReadAndWrittenAgainWithoutLoss()
        {

            var files = SPINEExampleXML.Files();

            if (files.Count == 0)
                Assert.Inconclusive("The SPINE specifications are not checked out below docs/specs, " +
                                    "so the official restricted function exchange examples are missing.");

            var problems = new List<String>();

            foreach (var file in files)
            {

                var name = Path.GetFileNameWithoutExtension(file);

                try
                {

                    var expected  = SPINEExampleXML.ToJSON(System.Xml.Linq.XDocument.Load(file).Root!);
                    var datagram  = JsonConvert.DeserializeObject<DatagramType>(expected.ToString(),
                                                                               SPINEJSON.StrictSettings);
                    var actual    = ParseWithoutTouchingTheTimestamps(SPINEJSON.ToJSON(datagram));

                    var difference = FirstDifference(expected, actual, "datagram");

                    if (difference is not null)
                        problems.Add($"{name}: {difference}");

                }
                catch (Exception e)
                {
                    problems.Add($"{name}: {e.Message}");
                }

            }

            Assert.Multiple(() => {

                Assert.That(files,    Has.Count.EqualTo(29),
                            "The specification ships 29 restricted function exchange examples.");

                Assert.That(problems, Is.Empty,
                            $"{problems.Count} example(s) are not read and written unchanged:{Environment.NewLine}" +
                            String.Join(Environment.NewLine, problems));

            });

        }

        #endregion

        #region Every_OfficialExample_SurvivesTheEEBUSJSONTransformation()

        /// <summary>
        /// Every example survives the transformation SHIP puts a SPINE datagram
        /// through (SHIP TS 1.0.1, chapter 11): to the ordered array form the
        /// wire uses, and back again.
        ///
        /// The transformation was written against the recorded messages of the
        /// Go reference implementation; here it meets 29 documents of the
        /// specification instead, with lists of one entry, empty elements and
        /// nested selectors among them.
        /// </summary>
        [Test]
        public void Every_OfficialExample_SurvivesTheEEBUSJSONTransformation()
        {

            var files = SPINEExampleXML.Files();

            if (files.Count == 0)
                Assert.Inconclusive("The SPINE specifications are not checked out below docs/specs, " +
                                    "so the official restricted function exchange examples are missing.");

            var problems = new List<String>();

            foreach (var file in files)
            {

                var name      = Path.GetFileNameWithoutExtension(file);
                var expected  = SPINEExampleXML.ToJSON(System.Xml.Linq.XDocument.Load(file).Root!);

                var actual    = EEBUSJSON.ToStandardJSON(
                                    EEBUSJSON.ToEEBUSJSON(expected)
                                );

                if (!JToken.DeepEquals(expected, actual))
                    problems.Add($"{name}: {FirstDifference(expected, actual, "datagram") ?? "they differ"}");

            }

            Assert.That(problems, Is.Empty,
                        $"{problems.Count} example(s) do not survive the EEBUS JSON transformation:" +
                        $"{Environment.NewLine}{String.Join(Environment.NewLine, problems)}");

        }

        #endregion


        #region Write_AddsANewListEntry()                     - W-A-Y_1-1-01

        /// <summary>
        /// "List, list entry affected. Adding content: the identifier must not
        /// be present before, and must be declared in the function."
        ///
        /// The Go reference implementation refuses this: a write which arrived
        /// from another device never appends. The specification devotes an
        /// example to it, so we do it.
        /// </summary>
        [Test]
        public void Write_AddsANewListEntry()
        {

            var cmd     = Cmd("W-A-Y_1-1-01");
            var result  = SPINEUpdate.Apply(new SetpointListDataType { SetpointData = [] },
                                            cmd.Data as SetpointListDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {

                Assert.That(result.Success,               Is.True, result.Problem);
                Assert.That(result.Data!.SetpointData,    Has.Count.EqualTo(1));

                var entry = result.Data.SetpointData![0];

                Assert.That(entry.SetpointId,             Is.EqualTo(1));
                Assert.That(entry.Value!.   Number,       Is.EqualTo(105));
                Assert.That(entry.Value!.   Scale,        Is.EqualTo(-1));
                Assert.That(entry.ValueMin!.Number,       Is.EqualTo(9));
                Assert.That(entry.ValueMin!.Scale,        Is.Null);

            });

        }

        #endregion

        #region Write_AddsAnElementOfAListEntry()             - W-A-Y_1-2-01

        /// <summary>
        /// "List, element affected in list entry. Adding content: the element
        /// must not be present before, the identifier must be declared in the
        /// function." Everything else of the entry stays as it was.
        /// </summary>
        [Test]
        public void Write_AddsAnElementOfAListEntry()
        {

            var cmd     = Cmd("W-A-Y_1-2-01");

            var before  = Setpoints(new SetpointDataType {
                                        SetpointId            = 1,
                                        Value                 = Number(105, -1),
                                        ValueMin              = Number(9),
                                        IsSetpointChangeable  = true
                                    });

            var result  = SPINEUpdate.Apply(before,
                                            cmd.Data as SetpointListDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            var entry   = Entry(result.Data, 1);

            Assert.Multiple(() => {

                Assert.That(result.Success,          Is.True, result.Problem);
                Assert.That(entry!.ValueMax!.Number, Is.EqualTo(155));
                Assert.That(entry!.ValueMax!.Scale,  Is.EqualTo(-1));

                // Untouched:
                Assert.That(entry!.Value!.   Number, Is.EqualTo(105));
                Assert.That(entry!.ValueMin!.Number, Is.EqualTo(9));

            });

        }

        #endregion

        #region Write_IsRefusedWhereTheDataSaysItMayNotBeChanged()

        /// <summary>
        /// The same write, onto data which does not allow it.
        ///
        /// SPINE 1.3.0, 5.3.4.2: "a write operation with restricted function
        /// exchange SHALL ONLY be executed by a server if it can execute the
        /// received operation completely" - so a refused write leaves the data
        /// exactly as it was, rather than half applied.
        /// </summary>
        [Test]
        public void Write_IsRefusedWhereTheDataSaysItMayNotBeChanged()
        {

            var cmd     = Cmd("W-A-Y_1-2-01");

            var before  = Setpoints(new SetpointDataType {
                                        SetpointId            = 1,
                                        Value                 = Number(105, -1),
                                        IsSetpointChangeable  = false
                                    });

            var result  = SPINEUpdate.Apply(before,
                                            cmd.Data as SetpointListDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {

                Assert.That(result.Success,                  Is.False);
                Assert.That(result.Problem,                  Does.Contain("does not allow"));
                Assert.That(Entry(result.Data, 1)!.ValueMax, Is.Null);
                Assert.That(Entry(before,      1)!.ValueMax, Is.Null, "The data of the device was changed anyway.");

            });

        }

        #endregion

        #region Write_ModifiesAnElementOfAListEntry()         - W-P-Y_1-1-01

        /// <summary>
        /// "List, element affected in list entry. Modifying content: the element
        /// must be present before, identifier and element must be declared in
        /// the function."
        /// </summary>
        [Test]
        public void Write_ModifiesAnElementOfAListEntry()
        {

            var cmd     = Cmd("W-P-Y_1-1-01");
            var result  = SPINEUpdate.Apply(TwoSetpoints(),
                                            cmd.Data as SetpointListDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {

                Assert.That(result.Success,                       Is.True, result.Problem);
                Assert.That(Entry(result.Data, 1)!.Value!.Number, Is.EqualTo(181));
                Assert.That(Entry(result.Data, 1)!.Value!.Scale,  Is.EqualTo(-1));

                // The other entry is none of this command's business.
                Assert.That(Entry(result.Data, 2)!.Value!.Number, Is.EqualTo(200));

            });

        }

        #endregion

        #region Write_DeletesAListEntry()                     - W-D-Y_1-1-01

        /// <summary>
        /// "List, list entry affected. Deleting content: the list entry must be
        /// present before, the identifier must be declared in the selectors."
        /// </summary>
        [Test]
        public void Write_DeletesAListEntry()
        {

            var cmd     = Cmd("W-D-Y_1-1-01");
            var result  = SPINEUpdate.Apply(TwoSetpoints(),
                                            cmd.Data as SetpointListDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {

                Assert.That(result.Success,             Is.True, result.Problem);
                Assert.That(result.Data!.SetpointData,  Has.Count.EqualTo(1));
                Assert.That(Entry(result.Data, 1),      Is.Not.Null);
                Assert.That(Entry(result.Data, 2),      Is.Null);

            });

        }

        #endregion

        #region Write_DeletesAnElementOfOneListEntryOnly()    - W-D-Y_1-2-01

        /// <summary>
        /// "List, element affected in list entry. Deleting content: the
        /// identifier must be declared in the selectors, the element must be
        /// identified in the elements."
        ///
        /// SPINE 1.3.0, 5.3.4.8, rule 2: where selectors are used, the elements
        /// are applied only to the entries the selectors select. Both entries
        /// have a "valueMin"; only the selected one loses it.
        /// </summary>
        [Test]
        public void Write_DeletesAnElementOfOneListEntryOnly()
        {

            var cmd     = Cmd("W-D-Y_1-2-01");
            var result  = SPINEUpdate.Apply(TwoSetpoints(),
                                            cmd.Data as SetpointListDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {

                Assert.That(result.Success,                  Is.True, result.Problem);
                Assert.That(Entry(result.Data, 2)!.ValueMin, Is.Null);
                Assert.That(Entry(result.Data, 1)!.ValueMin, Is.Not.Null, "The element was deleted from the wrong entry.");
                Assert.That(result.Data!.SetpointData,       Has.Count.EqualTo(2), "The entry itself was deleted.");

            });

        }

        #endregion

        #region Write_DeletesAndModifiesWithinOneMessage()    - W-M-Y_1-2-01

        /// <summary>
        /// "Elements in one list entry may be added AND modified AND deleted
        /// within one message" - the delete filter first, the partial filter
        /// afterwards (SPINE 1.3.0, 5.3.4.2).
        /// </summary>
        [Test]
        public void Write_DeletesAndModifiesWithinOneMessage()
        {

            var cmd     = Cmd("W-M-Y_1-2-01");
            var result  = SPINEUpdate.Apply(TwoSetpoints(),
                                            cmd.Data as SetpointListDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            var entry   = Entry(result.Data, 1);

            Assert.Multiple(() => {

                Assert.That(result.Success,                        Is.True, result.Problem);
                Assert.That(entry!.ValueMax,                       Is.Null,           "deleted");
                Assert.That(entry!.ValueMin!.Number,               Is.EqualTo(4),     "modified");
                Assert.That(entry!.ValueToleranceAbsolute!.Number, Is.EqualTo(1),     "added");
                Assert.That(entry!.Value!.Number,                  Is.EqualTo(105),   "untouched");

            });

        }

        #endregion

        #region Write_AddsModifiesAndDeletesAListEntryInTurn() - W-M-Y_1-1-01/02/03

        /// <summary>
        /// "A complete list entry may be added OR modified OR deleted" - the
        /// three examples of that row of Table 21, one after the other on the
        /// same list.
        ///
        /// The second of them is the one which decides whether an element is
        /// merged or replaced: it modifies a value of "105 with scale -1" by
        /// sending nothing but "14". SPINE 1.3.0, 5.3.4.7.1 lets an omitted
        /// child fall back to its default value, so the answer is 14 and not
        /// 1.4 - the element is replaced as a whole.
        /// </summary>
        [Test]
        public void Write_AddsModifiesAndDeletesAListEntryInTurn()
        {

            var add     = Cmd("W-M-Y_1-1-01");
            var modify  = Cmd("W-M-Y_1-1-02");
            var delete  = Cmd("W-M-Y_1-1-03");

            var after1  = SPINEUpdate.Apply(Setpoints(new SetpointDataType {
                                                          SetpointId            = 1,
                                                          Value                 = Number(105, -1),
                                                          IsSetpointChangeable  = true
                                                      }),
                                            add.Data as SetpointListDataType,
                                            add,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {
                Assert.That(after1.Success,                       Is.True, after1.Problem);
                Assert.That(after1.Data!.SetpointData,            Has.Count.EqualTo(2));
                Assert.That(Entry(after1.Data, 2)!.Value!.Number, Is.EqualTo(15));
            });

            var after2  = SPINEUpdate.Apply(TwoSetpoints(),
                                            modify.Data as SetpointListDataType,
                                            modify,
                                            SPINEUpdateOptions.Write);

            var modified = Entry(after2.Data, 1);

            Assert.Multiple(() => {
                Assert.That(after2.Success,             Is.True, after2.Problem);
                Assert.That(modified!.Value!.Number,    Is.EqualTo(14));
                Assert.That(modified!.Value!.Scale,     Is.Null, "The scale of the value it replaced was kept.");
                Assert.That(modified!.ValueMin!.Number, Is.EqualTo(75));
                Assert.That(modified!.ValueMin!.Scale,  Is.EqualTo(-1));
                Assert.That(modified!.ValueMax!.Number, Is.EqualTo(215));
            });

            var after3  = SPINEUpdate.Apply(TwoSetpoints(),
                                            delete.Data as SetpointListDataType,
                                            delete,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {
                Assert.That(after3.Success,            Is.True, after3.Problem);
                Assert.That(after3.Data!.SetpointData, Has.Count.EqualTo(1));
                Assert.That(Entry(after3.Data, 2),     Is.Null);
            });

        }

        #endregion


        #region Write_AddsAnElementOfAFunctionWhichIsNoList()  - W-A-N-1-01

        /// <summary>
        /// "No list, element affected. Adding content: the element must not be
        /// present before."
        /// </summary>
        [Test]
        public void Write_AddsAnElementOfAFunctionWhichIsNoList()
        {

            var cmd     = Cmd("W-A-N-1-01");
            var result  = SPINEUpdate.Apply(new DeviceClassificationUserDataType { UserDescription = "oldUserDescription" },
                                            cmd.Data as DeviceClassificationUserDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {
                Assert.That(result.Success,               Is.True, result.Problem);
                Assert.That(result.Data!.UserLabel,       Is.EqualTo("newUserLabel"));
                Assert.That(result.Data!.UserDescription, Is.EqualTo("oldUserDescription"));
            });

        }

        #endregion

        #region Write_ModifiesAnElementOfAFunctionWhichIsNoList() - W-P-N-1-01

        /// <summary>
        /// "No list, element affected. Modifying content: only the modified
        /// element must be stated in the function."
        /// </summary>
        [Test]
        public void Write_ModifiesAnElementOfAFunctionWhichIsNoList()
        {

            var cmd     = Cmd("W-P-N-1-01");
            var result  = SPINEUpdate.Apply(UserData(),
                                            cmd.Data as DeviceClassificationUserDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {
                Assert.That(result.Success,                      Is.True, result.Problem);
                Assert.That(result.Data!.UserLabel,              Is.EqualTo("anotherNewUserLabel"));
                Assert.That(result.Data!.UserDescription,        Is.EqualTo("oldUserDescription"));
                Assert.That(result.Data!.UserNodeIdentification, Is.EqualTo("node"));
            });

        }

        #endregion

        #region Write_DeletesAnElementOfAFunctionWhichIsNoList()  - W-D-N-1-01

        /// <summary>
        /// "No list, element affected. Deleting content: the element must be
        /// present before and must be identified in the elements."
        /// </summary>
        [Test]
        public void Write_DeletesAnElementOfAFunctionWhichIsNoList()
        {

            var cmd     = Cmd("W-D-N-1-01");
            var result  = SPINEUpdate.Apply(UserData(),
                                            cmd.Data as DeviceClassificationUserDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {
                Assert.That(result.Success,               Is.True, result.Problem);
                Assert.That(result.Data!.UserLabel,       Is.Null);
                Assert.That(result.Data!.UserDescription, Is.EqualTo("oldUserDescription"));
            });

        }

        #endregion

        #region Write_AddsModifiesAndDeletesElementsWithinOneMessage() - W-M-N-1-01

        /// <summary>
        /// "For non-list functions add AND modify AND delete of element(s) is
        /// possible within one message."
        /// </summary>
        [Test]
        public void Write_AddsModifiesAndDeletesElementsWithinOneMessage()
        {

            var cmd     = Cmd("W-M-N-1-01");
            var result  = SPINEUpdate.Apply(new DeviceClassificationUserDataType {
                                                UserNodeIdentification  = "node",
                                                UserDescription         = "oldUserDescription"
                                            },
                                            cmd.Data as DeviceClassificationUserDataType,
                                            cmd,
                                            SPINEUpdateOptions.Write);

            Assert.Multiple(() => {
                Assert.That(result.Success,                      Is.True, result.Problem);
                Assert.That(result.Data!.UserNodeIdentification, Is.Null,                            "deleted");
                Assert.That(result.Data!.UserLabel,              Is.EqualTo("newUserLabel"),         "added");
                Assert.That(result.Data!.UserDescription,        Is.EqualTo("newUserDescription"),   "modified");
            });

        }

        #endregion


        #region ANotify_ChangesTheDataLikeTheWriteItReports()

        /// <summary>
        /// "A notify command is very similar constructed like a write command,
        /// because a notify is mostly used to communicate what has changed after
        /// a write process. [...] Therefore, the notify cmdClassifier permits
        /// also the same combinations as the write cmdClassifier"
        /// (SPINE 1.3.0, 5.3.4.3).
        ///
        /// The specification ships both halves of eleven of those pairs, so this
        /// is a statement which can be checked rather than believed: applied to
        /// the same data, the notify and the write it reports leave the same
        /// data behind.
        /// </summary>
        [Test]
        public void ANotify_ChangesTheDataLikeTheWriteItReports()
        {

            (String Notify, String Write, Boolean List)[] pairs = [
                ("N-A-N-1-01",   "W-A-N-1-01",   false),
                ("N-D-N-1-01",   "W-D-N-1-01",   false),
                ("N-M-N-1-01",   "W-M-N-1-01",   false),
                ("N-A-Y_1-1-01", "W-A-Y_1-1-01", true),
                ("N-A-Y_1-2-01", "W-A-Y_1-2-01", true),
                ("N-D-Y_1-1-01", "W-D-Y_1-1-01", true),
                ("N-D-Y_1-2-01", "W-D-Y_1-2-01", true),
                ("N-M-Y_1-1-01", "W-M-Y_1-1-01", true),
                ("N-M-Y_1-1-02", "W-M-Y_1-1-02", true),
                ("N-M-Y_1-1-03", "W-M-Y_1-1-03", true),
                ("N-M-Y_1-2-01", "W-M-Y_1-2-01", true)
            ];

            var problems = new List<String>();

            foreach (var (notifyName, writeName, isList) in pairs)
            {

                var notify  = Cmd(notifyName);
                var write   = Cmd(writeName);

                var after   = isList

                                  ? (Notify: SPINEUpdate.Apply((Object?) TwoSetpoints(), notify.Data, notify, SPINEUpdateOptions.Notify),
                                     Write:  SPINEUpdate.Apply((Object?) TwoSetpoints(), write. Data, write,  SPINEUpdateOptions.Write))

                                  : (Notify: SPINEUpdate.Apply((Object?) UserData(),     notify.Data, notify, SPINEUpdateOptions.Notify),
                                     Write:  SPINEUpdate.Apply((Object?) UserData(),     write. Data, write,  SPINEUpdateOptions.Write));

                if (!after.Notify.Success)
                    problems.Add($"{notifyName}: {after.Notify.Problem}");

                if (!after.Write.Success)
                    problems.Add($"{writeName}: {after.Write.Problem}");

                var left   = SPINEJSON.ToJSON(after.Notify.Data);
                var right  = SPINEJSON.ToJSON(after.Write. Data);

                if (left != right)
                    problems.Add($"{notifyName} leaves '{left}', {writeName} leaves '{right}'.");

            }

            Assert.That(problems, Is.Empty,
                        String.Join(Environment.NewLine, problems));

        }

        #endregion


        #region Read_AnswersWithTheSelectedEntry()            - RD-P-Y_1-1-01

        /// <summary>
        /// "List, list entry affected. Reading partial content: the identifier
        /// that defines the list entry that shall be read is stated in the
        /// selectors."
        /// </summary>
        [Test]
        public void Read_AnswersWithTheSelectedEntry()
        {

            var cmd     = Cmd("RD-P-Y_1-1-01");
            var answer  = SPINERead.Apply(TwoSetpoints(), cmd);

            Assert.Multiple(() => {
                Assert.That(answer!.SetpointData,               Has.Count.EqualTo(1));
                Assert.That(answer!.SetpointData![0].SetpointId, Is.EqualTo(1));
                Assert.That(answer!.SetpointData![0].ValueMax,   Is.Not.Null, "The whole entry was asked for.");
            });

        }

        #endregion

        #region Read_AnswersWithTheSelectedElementOfTheSelectedEntry() - RD-P-Y_1-2-01

        /// <summary>
        /// "List, element affected in list entry. Reading partial content: the
        /// identifier is stated in the selectors, the element in the elements."
        ///
        /// The identifier comes along whether it was asked for or not: SPINE
        /// 1.3.0, 5.3.4.5 requires the identifiers of a reply to be complete
        /// "even if the corresponding read operation made use of elements
        /// selection but did not specify the elements of the identifier".
        ///
        /// And because the specification also ships the reply belonging to this
        /// read, the answer can be compared with it rather than with an
        /// expectation of ours.
        /// </summary>
        [Test]
        public void Read_AnswersWithTheSelectedElementOfTheSelectedEntry()
        {

            var read    = Cmd("RD-P-Y_1-2-01");
            var reply   = Cmd("RY-P-Y_1-1-01");

            var data    = Setpoints(

                              new SetpointDataType {
                                  SetpointId            = 1,
                                  Value                 = Number(181, -1),
                                  ValueMin              = Number(9),
                                  IsSetpointChangeable  = true
                              },

                              new SetpointDataType {
                                  SetpointId            = 2,
                                  Value                 = Number(200, -1)
                              }

                          );

            var answer  = SPINERead.Apply(data, read);

            Assert.That(SPINEJSON.ToJSON(answer),
                        Is.EqualTo(SPINEJSON.ToJSON(reply.Data)),
                        "The answer to the read of the specification is not the reply of the specification.");

        }

        #endregion

        #region Read_AnswersWithTheNamedElements()            - RD-P-N-1-01

        /// <summary>
        /// "No list, element affected. Reading partial content: the element that
        /// shall be read is stated in the elements."
        /// </summary>
        [Test]
        public void Read_AnswersWithTheNamedElements()
        {

            var read    = Cmd("RD-P-N-1-01");
            var answer  = SPINERead.Apply(new DeviceClassificationUserDataType {
                                              UserNodeIdentification  = "node",
                                              UserLabel               = "newUserLabel",
                                              UserDescription         = "newUserDescription"
                                          },
                                          read);

            Assert.Multiple(() => {
                Assert.That(answer!.UserLabel,              Is.EqualTo("newUserLabel"));
                Assert.That(answer!.UserDescription,        Is.EqualTo("newUserDescription"));
                Assert.That(answer!.UserNodeIdentification, Is.Null, "Something which was not asked for was answered.");
            });

            // And that is exactly the reply the specification ships for it.
            Assert.That(SPINEJSON.ToJSON(answer),
                        Is.EqualTo(SPINEJSON.ToJSON(Cmd("RY-P-N-1-01").Data)));

        }

        #endregion


        #region (private static) ParseWithoutTouchingTheTimestamps(Text)

        /// <summary>
        /// Read JSON without letting the library interpret anything.
        ///
        /// "JObject.Parse" turns everything which looks like a timestamp into a
        /// DateTime, and a comparison of the result then compares whatever the
        /// machine's locale made of it rather than what was written. That is the
        /// very reason <see cref="SPINEJSON"/> exists, and a test which falls
        /// for it reports the model as broken while the model is fine.
        /// </summary>
        private static JObject ParseWithoutTouchingTheTimestamps(String Text)
        {

            using var reader = new JsonTextReader(new StringReader(Text)) {
                                   DateParseHandling = DateParseHandling.None
                               };

            return JObject.Load(reader);

        }

        #endregion

        #region (private static) FirstDifference(Expected, Actual, Path)

        /// <summary>
        /// Where two JSON documents first differ, or null when they do not.
        ///
        /// The values of the one side come from XML and are therefore text,
        /// while the ones of the other side have been through the data model and
        /// are numbers and booleans. Comparing them as text is what the XSD
        /// says: "181" and 181 are the same element with the same value.
        /// </summary>
        private static String? FirstDifference(JToken Expected, JToken Actual, String Path)
        {

            if (Expected is JObject expectedObject &&
                Actual   is JObject actualObject)
            {

                foreach (var property in expectedObject.Properties())
                {

                    var value = actualObject[property.Name];

                    if (value is null)
                        return $"{Path}.{property.Name} is missing";

                    var difference = FirstDifference(property.Value, value, $"{Path}.{property.Name}");

                    if (difference is not null)
                        return difference;

                }

                foreach (var property in actualObject.Properties())
                    if (expectedObject[property.Name] is null)
                        return $"{Path}.{property.Name} was not expected";

                return null;

            }

            if (Expected is JArray expectedArray &&
                Actual   is JArray actualArray)
            {

                if (expectedArray.Count != actualArray.Count)
                    return $"{Path} holds {actualArray.Count} entries instead of {expectedArray.Count}";

                for (var i = 0; i < expectedArray.Count; i++)
                {

                    var difference = FirstDifference(expectedArray[i], actualArray[i], $"{Path}[{i}]");

                    if (difference is not null)
                        return difference;

                }

                return null;

            }

            if (Expected.Type != Actual.Type &&
                (Expected is JObject || Actual is JObject || Expected is JArray || Actual is JArray))
                return $"{Path} is {Actual.Type} instead of {Expected.Type}";

            var left   = Text(Expected);
            var right  = Text(Actual);

            return left == right
                       ? null
                       : $"{Path} is '{right}' instead of '{left}'";

        }

        private static String Text(JToken Token)

            => Token is JValue value && value.Value is not null
                   ? value.Type == JTokenType.Boolean
                         ? (Boolean) value.Value! ? "true" : "false"
                         : Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
                   : Token.ToString(Formatting.None);

        #endregion

    }

}
