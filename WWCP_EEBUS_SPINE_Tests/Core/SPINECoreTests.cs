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

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// The SPINE core: two devices, the datagrams between them, and what each of
    /// them is allowed to do to the other.
    ///
    /// The setup is the one of the load control use case, because it is the one
    /// where every rule of the core matters at once: a home energy management
    /// system limits a charging station, which means a client feature on one
    /// side, a server feature on the other, a binding before the write is
    /// allowed, and a subscription before the answer comes back by itself.
    /// </summary>
    [TestFixture]
    public class SPINECoreTests
    {

        #region Data

        private const String limits = "loadControlLimitListData";

        private SPINELoopback      loopback  = null!;

        /// <summary>
        /// The home energy management system, which does the limiting.
        /// </summary>
        private SPINELocalFeature  hemsLoadControl    = null!;

        /// <summary>
        /// The charging station, which is being limited.
        /// </summary>
        private SPINELocalFeature  evseLoadControl    = null!;

        #endregion

        #region Setup()

        [SetUp]
        public void Setup()
        {

            var hems = new SPINELocalDevice("d:_i:19667_HEMS",
                                            DeviceTypeType.EnergyManagementSystem);

            var evse = new SPINELocalDevice("d:_i:19667_EVSE",
                                            DeviceTypeType.ChargingStation);

            // The one which limits asks; the one which is limited holds the data.
            hemsLoadControl  = hems.AddEntity(EntityTypeType.CEM).
                                    AddFeature(FeatureTypeType.LoadControl, RoleType.Client);

            var evseEntity   = evse.AddEntity(EntityTypeType.EVSE);

            evseLoadControl  = evseEntity.AddFeature(FeatureTypeType.LoadControl, RoleType.Server);

            evseLoadControl.AddFunction(limits,
                                        Read:          true,
                                        Write:         true,
                                        PartialRead:   true,
                                        PartialWrite:  true);

            loopback = new SPINELoopback(hems, evse).Mirror();

        }

        #endregion

        #region (private) The features as the other side sees them

        private SPINERemoteFeature EVSELoadControl

            => loopback.BAsSeenByA.
                   Entity([ 1 ])!.
                   Feature(FeatureTypeType.LoadControl, RoleType.Server)!;

        private SPINERemoteFeature HEMSLoadControl

            => loopback.AAsSeenByB.
                   Entity([ 1 ])!.
                   Feature(FeatureTypeType.LoadControl, RoleType.Client)!;


        private static LoadControlLimitListDataType Limits(params (UInt32 Id, Int64 Value, Boolean Changeable)[] Entries)

            => new () {
                   LoadControlLimitData = [.. Entries.Select(entry => new LoadControlLimitDataType {
                                                                          LimitId            = entry.Id,
                                                                          IsLimitChangeable  = entry.Changeable,
                                                                          Value              = new ScaledNumberType { Number = entry.Value }
                                                                      })]
               };

        #endregion


        #region ADeviceHasAnEntityZeroWithFeatureZero()

        /// <summary>
        /// SPINE 1.3.0, 7.1: every device has the entity 0, and node management
        /// sits on its feature 0. The features of entity 0 are counted from 0,
        /// the features of every other entity from 1.
        /// </summary>
        [Test]
        public void ADeviceHasAnEntityZeroWithFeatureZero()
        {

            var device = new SPINELocalDevice("d:_i:19667_Test", DeviceTypeType.Generic);

            // Nobody adds it: a device without node management is not a SPINE
            // device, so it comes with the device.
            var nodeManagement = device.NodeManagement;

            var other          = device.AddEntity(EntityTypeType.EVSE).
                                     AddFeature(FeatureTypeType.LoadControl, RoleType.Server);

            Assert.Multiple(() => {

                Assert.That(device.DeviceInformation.EntityId,  Is.EqualTo(new UInt32[] { 0 }));
                Assert.That(nodeManagement.Id,                  Is.EqualTo(0));
                Assert.That(nodeManagement.Address.ToString(),  Is.EqualTo("d:_i:19667_Test:[0]:0"));

                Assert.That(other.Entity.EntityId,              Is.EqualTo(new UInt32[] { 1 }));
                Assert.That(other.Id,                           Is.EqualTo(1),
                            "The features of an ordinary entity start at 1.");

                Assert.That(device.Feature(nodeManagement.Address), Is.SameAs(nodeManagement));

            });

        }

        #endregion

        #region AReadIsAnsweredWithAReply()

        /// <summary>
        /// The plainest exchange there is: one device asks a server feature of
        /// the other for a function, and gets it.
        /// </summary>
        [Test]
        public async Task AReadIsAnsweredWithAReply()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            var response = await hemsLoadControl.Read(limits, EVSELoadControl);

            Assert.Multiple(() => {

                Assert.That(response.IsError,   Is.False);
                Assert.That(response.Function,  Is.EqualTo(limits));

                var data = response.Data as LoadControlLimitListDataType;

                Assert.That(data?.LoadControlLimitData,                  Has.Count.EqualTo(1));
                Assert.That(data?.LoadControlLimitData?[0].LimitId,      Is.EqualTo(1));
                Assert.That(data?.LoadControlLimitData?[0].Value?.Number, Is.EqualTo(1600));

                // ... and the client remembers it, so the next reader of the
                // cache does not have to ask again.
                Assert.That(EVSELoadControl.DataCopy<LoadControlLimitListDataType>(limits)?.
                                LoadControlLimitData?[0].Value?.Number,  Is.EqualTo(1600));

            });

        }

        #endregion

        #region TheDatagramsOfAReadAreTheOnesTheSpecificationDescribes()

        /// <summary>
        /// Two datagrams, in this order, with the reply pointing back at the
        /// read through its message counter (SPINE 1.3.0, 5.2).
        /// </summary>
        [Test]
        public async Task TheDatagramsOfAReadAreTheOnesTheSpecificationDescribes()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            await hemsLoadControl.Read(limits, EVSELoadControl);

            Assert.Multiple(() => {

                Assert.That(loopback.AToB.Datagrams, Has.Count.EqualTo(1));
                Assert.That(loopback.BToA.Datagrams, Has.Count.EqualTo(1));

                var read  = loopback.AToB.Datagrams[0];
                var reply = loopback.BToA.Datagrams[0];

                Assert.That(read.Header?.CmdClassifier,        Is.EqualTo(CmdClassifierType.Read));
                Assert.That(read.Header?.MsgCounter,           Is.EqualTo(1),
                            "The message counters of a connection start at 1.");
                Assert.That(read.Header?.AddressSource?.ToString(),      Is.EqualTo("d:_i:19667_HEMS:[1]:1"));
                Assert.That(read.Header?.AddressDestination?.ToString(), Is.EqualTo("d:_i:19667_EVSE:[1]:1"));
                Assert.That(read.Payload?.Cmd?[0].DataFunction, Is.EqualTo(limits),
                            "A read carries its function as an empty payload.");

                Assert.That(reply.Header?.CmdClassifier,         Is.EqualTo(CmdClassifierType.Reply));
                Assert.That(reply.Header?.MsgCounterReference,   Is.EqualTo(1),
                            "The reply does not refer back to the read.");
                Assert.That(reply.Header?.AddressSource?.ToString(),      Is.EqualTo("d:_i:19667_EVSE:[1]:1"));
                Assert.That(reply.Header?.AddressDestination?.ToString(), Is.EqualTo("d:_i:19667_HEMS:[1]:1"));

            });

        }

        #endregion

        #region AReadOfAClientFeatureIsRejected()

        /// <summary>
        /// SPINE 1.3.0, 2.1.3: a client asks and a server answers. Reading a
        /// client feature asks somebody who has nothing to say.
        /// </summary>
        [Test]
        public async Task AReadOfAClientFeatureIsRejected()
        {

            var response = await evseLoadControl.Read(limits, HEMSLoadControl);

            Assert.Multiple(() => {
                Assert.That(response.IsError,                Is.True);
                Assert.That(response.Result?.ErrorNumber,    Is.EqualTo(SPINEErrorNumbers.CommandRejected));
                Assert.That(response.Result?.Description,    Does.Contain("client"));
            });

        }

        #endregion

        #region AReadOfAnUnknownFeatureIsAnsweredWithDestinationUnknown()

        /// <summary>
        /// A message to a feature which does not exist is answered rather than
        /// dropped: the sender is waiting for something.
        /// </summary>
        [Test]
        public async Task AReadOfAnUnknownFeatureIsAnsweredWithDestinationUnknown()
        {

            var nowhere = loopback.BAsSeenByA.
                              GetOrAddEntity([ 9 ], EntityTypeType.Generic).
                              GetOrAddFeature(9, FeatureTypeType.LoadControl, RoleType.Server);

            var response = await hemsLoadControl.Read(limits, nowhere);

            Assert.Multiple(() => {
                Assert.That(response.IsError,             Is.True);
                Assert.That(response.Result?.ErrorNumber, Is.EqualTo(SPINEErrorNumbers.DestinationUnknown));
            });

        }

        #endregion


        #region AWriteWithoutABindingIsRefused()

        /// <summary>
        /// SPINE 1.3.0, 7.6: a client may write to a server feature only where a
        /// binding says so. Error 9 is the one the specification gives that
        /// answer.
        /// </summary>
        [Test]
        public async Task AWriteWithoutABindingIsRefused()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            var response = await hemsLoadControl.Write(limits,
                                                       Limits((1, 800, true)),
                                                       EVSELoadControl,
                                                       Partial: true);

            Assert.Multiple(() => {

                Assert.That(response.IsError,              Is.True);
                Assert.That(response.Result?.ErrorNumber,  Is.EqualTo(SPINEErrorNumbers.BindingIsNecessaryForThisCommand));
                Assert.That(response.Result?.ErrorNumber,  Is.EqualTo(9));

                Assert.That(evseLoadControl.DataCopy<LoadControlLimitListDataType>(limits)?.
                                LoadControlLimitData?[0].Value?.Number,
                            Is.EqualTo(1600),
                            "The limit was changed although the write was refused.");

            });

        }

        #endregion

        #region AWriteWithABindingIsCarriedOut()

        /// <summary>
        /// With a binding, the same write goes through - and the data of the
        /// device changes.
        /// </summary>
        [Test]
        public async Task AWriteWithABindingIsCarriedOut()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            loopback.B.Bindings.Add(HEMSLoadControl.Address,
                                    evseLoadControl.Address);

            var response = await hemsLoadControl.Write(limits,
                                                       new LoadControlLimitListDataType {
                                                           LoadControlLimitData = [
                                                               new LoadControlLimitDataType {
                                                                   LimitId  = 1,
                                                                   Value    = new ScaledNumberType { Number = 800 }
                                                               }
                                                           ]
                                                       },
                                                       EVSELoadControl,
                                                       Partial: true);

            Assert.Multiple(() => {

                Assert.That(response.IsError,             Is.False, response.Result?.Description);
                Assert.That(response.Result?.ErrorNumber, Is.EqualTo(SPINEErrorNumbers.NoError));

                var data = evseLoadControl.DataCopy<LoadControlLimitListDataType>(limits);

                Assert.That(data?.LoadControlLimitData?[0].Value?.Number,       Is.EqualTo(800));
                Assert.That(data?.LoadControlLimitData?[0].IsLimitChangeable,   Is.True,
                            "The partial write dropped what it did not mention.");

            });

        }

        #endregion

        #region AWriteOfDataWhichMayNotBeChangedIsRefused()

        /// <summary>
        /// The binding says the client may ask; "isLimitChangeable" says whether
        /// this particular limit may be changed. Both have to agree.
        /// </summary>
        [Test]
        public async Task AWriteOfDataWhichMayNotBeChangedIsRefused()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, false)));

            loopback.B.Bindings.Add(HEMSLoadControl.Address,
                                    evseLoadControl.Address);

            var response = await hemsLoadControl.Write(limits,
                                                       Limits((1, 800, false)),
                                                       EVSELoadControl,
                                                       Partial: true);

            Assert.Multiple(() => {

                Assert.That(response.IsError,              Is.True);
                Assert.That(response.Result?.ErrorNumber,  Is.EqualTo(SPINEErrorNumbers.CommandRejected));
                Assert.That(response.Result?.Description,  Does.Contain("does not allow"));

                Assert.That(evseLoadControl.DataCopy<LoadControlLimitListDataType>(limits)?.
                                LoadControlLimitData?[0].Value?.Number,
                            Is.EqualTo(1600));

            });

        }

        #endregion

        #region AWriteOfAFunctionWhichIsReadOnlyIsRefused()

        /// <summary>
        /// A feature which does not offer a write for a function says so in its
        /// possible operations, and a write is refused before anything is
        /// touched.
        /// </summary>
        [Test]
        public async Task AWriteOfAFunctionWhichIsReadOnlyIsRefused()
        {

            const String state = "loadControlStateListData";

            evseLoadControl.AddFunction(state, Read: true, Write: false);

            EVSELoadControl.SetOperations(evseLoadControl.Information().Description?.SupportedFunction);

            loopback.B.Bindings.Add(HEMSLoadControl.Address,
                                    evseLoadControl.Address);

            var response = await hemsLoadControl.Write(state,
                                                       new LoadControlStateListDataType(),
                                                       EVSELoadControl);

            Assert.Multiple(() => {
                Assert.That(response.IsError,              Is.True);
                Assert.That(response.Result?.ErrorNumber,  Is.EqualTo(SPINEErrorNumbers.CommandNotSupported));
                Assert.That(response.Result?.Description,  Does.Contain("may not be written"));
            });

        }

        #endregion


        #region ASubscriberIsNotifiedWhenTheDataChanges()

        /// <summary>
        /// SPINE 1.3.0, 7.5: whoever subscribed to a server feature is told when
        /// its data changes - without asking again.
        /// </summary>
        [Test]
        public async Task ASubscriberIsNotifiedWhenTheDataChanges()
        {

            var changes = new List<SPINEDataChange>();

            loopback.A.Events.Subscribe<SPINEDataChanged>(@event => changes.Add(@event.Change));

            loopback.B.Subscriptions.Add(HEMSLoadControl.Address,
                                         evseLoadControl.Address);

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            Assert.Multiple(() => {

                Assert.That(loopback.BToA.Datagrams,                    Has.Count.EqualTo(1));
                Assert.That(loopback.BToA.Datagrams[0].Header?.CmdClassifier, Is.EqualTo(CmdClassifierType.Notify));

                Assert.That(changes,                                    Has.Count.EqualTo(1));
                Assert.That(changes[0].CmdClassifier,                   Is.EqualTo(CmdClassifierType.Notify));
                Assert.That(changes[0].Function,                        Is.EqualTo(limits));

                // The client took the notify over into its picture of the other
                // device, which is the point of subscribing.
                Assert.That(EVSELoadControl.DataCopy<LoadControlLimitListDataType>(limits)?.
                                LoadControlLimitData?[0].Value?.Number, Is.EqualTo(1600));

            });

        }

        #endregion

        #region AWriteNotifiesTheOtherSubscribers()

        /// <summary>
        /// A write changes the data of the server, so everybody who subscribed
        /// hears about it - including the one who wrote it, which is what makes
        /// the two pictures of the data agree again.
        /// </summary>
        [Test]
        public async Task AWriteNotifiesTheOtherSubscribers()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            loopback.B.Bindings.     Add(HEMSLoadControl.Address, evseLoadControl.Address);
            loopback.B.Subscriptions.Add(HEMSLoadControl.Address, evseLoadControl.Address);

            await hemsLoadControl.Write(limits,
                                        Limits((1, 800, true)),
                                        EVSELoadControl,
                                        Partial: true);

            var notifies = loopback.BToA.Datagrams.
                               Where(datagram => datagram.Header?.CmdClassifier == CmdClassifierType.Notify).
                               ToList();

            Assert.Multiple(() => {

                Assert.That(notifies, Has.Count.EqualTo(1));

                Assert.That(EVSELoadControl.DataCopy<LoadControlLimitListDataType>(limits)?.
                                LoadControlLimitData?[0].Value?.Number,
                            Is.EqualTo(800),
                            "The client did not learn what its own write made of the data.");

            });

        }

        #endregion


        #region APartialReadIsAnsweredPartially()

        /// <summary>
        /// A read which asks for one entry of a list gets that entry - because
        /// this feature announced that it can do that.
        /// </summary>
        [Test]
        public async Task APartialReadIsAnsweredPartially()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true),
                                                         (2,  800, true)));

            var response = await hemsLoadControl.Read(limits,
                                                      EVSELoadControl,
                                                      Selectors: new LoadControlLimitListDataSelectorsType { LimitId = 2 });

            var data = response.Data as LoadControlLimitListDataType;

            Assert.Multiple(() => {
                Assert.That(response.IsError,                             Is.False);
                Assert.That(data?.LoadControlLimitData,                   Has.Count.EqualTo(1));
                Assert.That(data?.LoadControlLimitData?[0].LimitId,       Is.EqualTo(2));
                Assert.That(data?.LoadControlLimitData?[0].Value?.Number, Is.EqualTo(800));
            });

        }

        #endregion

        #region APartialReadOfAFeatureWhichCannotDoItIsAnsweredInFull()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.5: "a server MAY ignore unsupported cmdOption
        /// combinations and then replies with more than the requested parts
        /// instead."
        ///
        /// A feature which did not announce a partial read does exactly that,
        /// rather than answering something it never promised.
        /// </summary>
        [Test]
        public async Task APartialReadOfAFeatureWhichCannotDoItIsAnsweredInFull()
        {

            // Not "AddFunction(..., PartialRead: false)": offering a function
            // twice combines the two declarations rather than replacing the
            // first, so a capability the setup announced cannot be taken back
            // that way. What this test needs is a feature which never announced
            // a partial read, and saying so outright is clearer than arranging
            // for it.
            evseLoadControl.AddFunction(limits, Read: true, Write: true, PartialRead: false).Operations
                = PossibleOperationsType.ReadAndMaybeWrite(Write: true, PartialRead: false);

            await evseLoadControl.SetData(limits, Limits((1, 1600, true),
                                                         (2,  800, true)));

            var response = await hemsLoadControl.Read(limits,
                                                      EVSELoadControl,
                                                      Selectors: new LoadControlLimitListDataSelectorsType { LimitId = 2 });

            var data = response.Data as LoadControlLimitListDataType;

            Assert.Multiple(() => {
                Assert.That(response.IsError,           Is.False);
                Assert.That(data?.LoadControlLimitData, Has.Count.EqualTo(2));
            });

        }

        #endregion


        #region OfferingTheSameFunctionTwiceKeepsItsData()

        /// <summary>
        /// SPINE allows at most one feature of a given type and role per entity,
        /// so everything on an entity which needs a load control feature shares
        /// one - and each of them declares the functions it needs. Two use cases
        /// on one entity is not exotic: a battery is limited in both directions
        /// and runs the consumption and the production use case at once.
        ///
        /// The second declaration must therefore not start the function over.
        /// It used to, which silently emptied whatever the first one had put
        /// there and left the device announcing half of what it offered.
        /// </summary>
        [Test]
        public async Task OfferingTheSameFunctionTwiceKeepsItsData()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            // A second party on the same feature needs the same function, and
            // needs to write it.
            var functionData = evseLoadControl.AddFunction(limits,
                                                           Read:          true,
                                                           Write:         true,
                                                           PartialWrite:  true);

            Assert.Multiple(() => {

                Assert.That((functionData.DataCopy() as LoadControlLimitListDataType)?.LoadControlLimitData,
                            Has.Count.EqualTo(1),
                            "The second declaration emptied the function.");

                // The capabilities of the two declarations are combined: the
                // partial read of the first survives ...
                Assert.That(functionData.Operations.CanReadPartial,   Is.True);

                // ... and the partial write the second one needs is added.
                Assert.That(functionData.Operations.CanWrite,         Is.True);
                Assert.That(functionData.Operations.CanWritePartial,  Is.True);

            });

        }

        #endregion

        #region AReadOfAFunctionWhichHoldsNothingStillNamesTheFunction()

        /// <summary>
        /// A function which holds no data yet is answered with an empty instance
        /// of it - the specification writes that as
        /// "&lt;setpointListData/&gt;" - and not with an empty command.
        ///
        /// A reply which names no function cannot be matched to the read it
        /// answers, and the caller waits for something which has already
        /// arrived. This was exactly that, until the use case layer read a
        /// function nobody had filled in yet.
        /// </summary>
        [Test]
        public async Task AReadOfAFunctionWhichHoldsNothingStillNamesTheFunction()
        {

            // No SetData: the feature has the function and no data for it.
            var response = await hemsLoadControl.Read(limits, EVSELoadControl);

            var reply    = loopback.BToA.Datagrams[0];

            Assert.Multiple(() => {
                Assert.That(response.IsError,                   Is.False, response.Result?.Description);
                Assert.That(response.Function,                  Is.EqualTo(limits));
                Assert.That(reply.Payload?.Cmd?[0].DataFunction, Is.EqualTo(limits),
                            "The reply does not say which function it answers.");
                Assert.That((response.Data as LoadControlLimitListDataType)?.LoadControlLimitData,
                            Is.Null.Or.Empty);
            });

        }

        #endregion

        #region APartnerWhichDoesNotAnswerIsReportedRatherThanWaitedForForever()

        /// <summary>
        /// A device which never answers must not be able to stop this one. The
        /// wait ends after the maximum response delay the feature announced, or
        /// after the patience of the device, and the answer is a result saying
        /// so - which a test bench can report.
        /// </summary>
        [Test]
        public async Task APartnerWhichDoesNotAnswerIsReportedRatherThanWaitedForForever()
        {

            var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            var client = hems.AddEntity(EntityTypeType.CEM).
                              AddFeature(FeatureTypeType.LoadControl, RoleType.Client);

            evse.AddEntity(EntityTypeType.EVSE).
                 AddFeature(FeatureTypeType.LoadControl, RoleType.Server).
                 AddFunction(limits);

            var wire   = new SPINELoopback(hems, evse).Mirror();

            var server = wire.BAsSeenByA.Entity([ 1 ])!.
                              Feature(FeatureTypeType.LoadControl, RoleType.Server)!;

            // The cable is cut after the question left.
            wire.AToB.Connected = false;

            var reading = client.Read(limits, server);

            time.Advance(hems.ResponseTimeout + TimeSpan.FromSeconds(1));

            var response = await reading;

            Assert.Multiple(() => {
                Assert.That(response.IsError,             Is.True);
                Assert.That(response.Result?.ErrorNumber, Is.EqualTo(SPINEErrorNumbers.Timeout));
                Assert.That(response.Result?.Description, Does.Contain("did not answer"));
            });

        }

        #endregion

        #region TheSameQuestionIsNotAskedTwiceWhileItIsUnanswered()

        /// <summary>
        /// Two identical reads while the first one is still open are one
        /// datagram: both callers wait for the same answer.
        /// </summary>
        [Test]
        public async Task TheSameQuestionIsNotAskedTwiceWhileItIsUnanswered()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            loopback.AToB.Connected = false;

            var first   = hemsLoadControl.Read(limits, EVSELoadControl);
            var second  = hemsLoadControl.Read(limits, EVSELoadControl);

            Assert.Multiple(() => {
                Assert.That(loopback.AToB.Datagrams, Has.Count.EqualTo(1),
                            "The same read was sent twice.");
                Assert.That(first.IsCompleted,       Is.False);
                Assert.That(second.IsCompleted,      Is.False);
            });

        }

        #endregion

        #region ACommandWhoseFilterIsAboutAnotherFunctionIsRejected()

        /// <summary>
        /// SPINE 1.3.0, 5.3.4.1: the function name shall be stated if and only
        /// if any other cmdOption is used. A command which says one function and
        /// carries another says two different things about which data type it
        /// is - which is how a filter ends up being applied to the wrong type.
        /// </summary>
        [Test]
        public async Task ACommandWhoseFilterIsAboutAnotherFunctionIsRejected()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            loopback.B.Bindings.Add(HEMSLoadControl.Address, evseLoadControl.Address);

            var cmd = new CmdType {
                          Function  = FunctionType.Parse(limits),
                          Filter    = [ new FilterType { CmdControl = CmdControlType.ForPartial } ]
                      };

            cmd.SetData     (limits, Limits((1, 800, true)));
            cmd.Filter![0].SetSelectors("measurementListData", new MeasurementListDataSelectorsType { MeasurementId = 1 });

            await loopback.BAsSeenByA.Sender.Write(hemsLoadControl.Address,
                                                   EVSELoadControl.Address,
                                                   cmd);

            var result = loopback.BToA.Datagrams.
                             LastOrDefault()?.Payload?.Cmd?[0].ResultData;

            Assert.Multiple(() => {

                Assert.That(result?.ErrorNumber, Is.EqualTo(SPINEErrorNumbers.CommandRejected));
                Assert.That(result?.Description, Does.Contain("measurementListData"));

                Assert.That(evseLoadControl.DataCopy<LoadControlLimitListDataType>(limits)?.
                                LoadControlLimitData?[0].Value?.Number,
                            Is.EqualTo(1600));

            });

        }

        #endregion

        #region ADatagramWithoutACommandClassifierIsRefused()

        /// <summary>
        /// Everything which cannot be acted upon is answered with a result, so
        /// that a partner which sends nonsense hears about it instead of waiting.
        /// </summary>
        [Test]
        public async Task ADatagramWithoutACommandClassifierIsRefused()
        {

            var refusals = new List<ResultDataType>();

            loopback.B.Events.Subscribe<SPINEDatagramRefused>(@event => refusals.Add(@event.Result));

            await loopback.B.ProcessDatagram(
                      new DatagramType {
                          Header   = new HeaderType {
                                         SpecificationVersion  = Version.String,
                                         AddressSource         = hemsLoadControl.Address,
                                         AddressDestination    = evseLoadControl.Address,
                                         MsgCounter            = 1
                                     },
                          Payload  = new PayloadType { Cmd = [ new CmdType() ] }
                      },
                      loopback.AAsSeenByB
                  );

            Assert.Multiple(() => {
                Assert.That(refusals,                    Has.Count.EqualTo(1));
                Assert.That(refusals[0].ErrorNumber,     Is.EqualTo(SPINEErrorNumbers.DestinationUnknown));
                Assert.That(loopback.BToA.Datagrams,     Has.Count.EqualTo(1),
                            "Nothing was sent back.");
            });

        }

        #endregion

        #region AResultIsNeverAnsweredWithAResult()

        /// <summary>
        /// An error result which itself cannot be handled must not produce
        /// another error result: that is how two devices talk to each other
        /// until one of them gives up.
        /// </summary>
        [Test]
        public async Task AResultIsNeverAnsweredWithAResult()
        {

            await loopback.B.ProcessDatagram(
                      new DatagramType {
                          Header   = new HeaderType {
                                         SpecificationVersion  = Version.String,
                                         AddressSource         = hemsLoadControl.Address,
                                         AddressDestination    = evseLoadControl.Address,
                                         MsgCounter            = 1,
                                         MsgCounterReference   = 99,
                                         CmdClassifier         = CmdClassifierType.Result,
                                         AckRequest            = true
                                     },
                          Payload  = new PayloadType {
                                         Cmd = [ new CmdType {
                                                     ResultData = ResultDataType.Error(SPINEErrorNumbers.GeneralError, "something went wrong")
                                                 } ]
                                     }
                      },
                      loopback.AAsSeenByB
                  );

            Assert.That(loopback.BToA.Datagrams, Is.Empty,
                        "A result was answered.");

        }

        #endregion

        #region AnAcknowledgementIsSentWhereItWasAskedFor()

        /// <summary>
        /// SPINE 1.3.0, 5.2.4: where the sender asks for an acknowledgement, it
        /// gets one.
        /// </summary>
        [Test]
        public async Task AnAcknowledgementIsSentWhereItWasAskedFor()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            var cmd = new CmdType();
            cmd.SetData(limits, Limits((1, 1600, true)));

            await loopback.AAsSeenByB.Sender.Request(CmdClassifierType.Notify,
                                                     evseLoadControl.Address,
                                                     HEMSLoadControl.Address,
                                                     AckRequest:  true,
                                                     Cmds:        [ cmd ]);

            var results = loopback.AToB.Datagrams.
                              Where(datagram => datagram.Header?.CmdClassifier == CmdClassifierType.Result).
                              ToList();

            Assert.Multiple(() => {
                Assert.That(results,                            Has.Count.EqualTo(1));
                Assert.That(results[0].Payload?.Cmd?[0].ResultData?.IsSuccess, Is.True);
                Assert.That(results[0].Header?.MsgCounterReference,            Is.EqualTo(1));
            });

        }

        #endregion


        #region TheDataOfAFeatureIsOnlyEverHandedOutAsACopy()

        /// <summary>
        /// Whatever a caller does with what it got must not reach into the
        /// device.
        /// </summary>
        [Test]
        public async Task TheDataOfAFeatureIsOnlyEverHandedOutAsACopy()
        {

            await evseLoadControl.SetData(limits, Limits((1, 1600, true)));

            var copy = evseLoadControl.DataCopy<LoadControlLimitListDataType>(limits)!;

            copy.LoadControlLimitData![0].Value!.Number = 1;

            Assert.That(evseLoadControl.DataCopy<LoadControlLimitListDataType>(limits)?.
                            LoadControlLimitData?[0].Value?.Number,
                        Is.EqualTo(1600));

        }

        #endregion

        #region ATimestampSurvivesTheWholeExchange()

        /// <summary>
        /// A timestamp goes over the wire as the text the sender wrote, through
        /// the serialisation, the transport and the data model, and comes out as
        /// that text.
        ///
        /// It has been lost three times in this stack already - to the date
        /// handling of the JSON library, each time in a different place - so it
        /// is worth one test at the level where all of them are involved at once.
        /// </summary>
        [Test]
        public async Task ATimestampSurvivesTheWholeExchange()
        {

            const String state = "loadControlStateListData";

            evseLoadControl.AddFunction(state);

            await evseLoadControl.SetData(
                      state,
                      new LoadControlStateListDataType {
                          LoadControlStateData = [
                              new LoadControlStateDataType {
                                  EventId    = 1,
                                  Timestamp  = AbsoluteOrRelativeTimeType.Parse("2016-03-14T18:19:00.0Z")
                              }
                          ]
                      }
                  );

            EVSELoadControl.SetOperations(evseLoadControl.Information().Description?.SupportedFunction);

            var response  = await hemsLoadControl.Read(state, EVSELoadControl);

            var timestamp = (response.Data as LoadControlStateListDataType)?.
                                LoadControlStateData?[0].Timestamp;

            Assert.Multiple(() => {
                Assert.That(response.IsError,        Is.False);
                Assert.That(timestamp?.ToString(),   Is.EqualTo("2016-03-14T18:19:00.0Z"));
                Assert.That(loopback.BToA.Datagrams[0].Payload?.Cmd?[0].DataFunction, Is.EqualTo(state));
            });

        }

        #endregion

    }

}
