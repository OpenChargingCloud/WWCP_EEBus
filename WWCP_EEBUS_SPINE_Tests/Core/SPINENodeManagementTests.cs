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
    /// Node management: how two devices find out what the other one is, and what
    /// they agree to let each other do.
    ///
    /// Nothing here is set up by hand. The two devices start knowing nothing
    /// about each other but the address of the node management feature - which
    /// every device has at entity 0, feature 0 - and everything else is asked
    /// for over the wire.
    /// </summary>
    [TestFixture]
    public class SPINENodeManagementTests
    {

        #region Data

        private const String limits = "loadControlLimitListData";

        private SPINELoopback      loopback  = null!;
        private SPINELocalFeature  hemsLoadControl  = null!;
        private SPINELocalFeature  evseLoadControl  = null!;

        #endregion

        #region Setup()

        [SetUp]
        public void Setup()
        {

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation);

            var cem  = hems.AddEntity(EntityTypeType.CEM);
            cem.Description  = "The energy manager";

            hemsLoadControl  = cem.AddFeature(FeatureTypeType.LoadControl, RoleType.Client);
            hemsLoadControl.Description = "Load control client";

            var evseEntity   = evse.AddEntity(EntityTypeType.EVSE);
            evseEntity.Description = "The charging station";

            evseLoadControl  = evseEntity.AddFeature(FeatureTypeType.LoadControl, RoleType.Server);
            evseLoadControl.Description = "Load control server";

            evseLoadControl.AddFunction(limits, Read: true, Write: true, PartialRead: true, PartialWrite: true);
            evseLoadControl.AddFunction("loadControlLimitDescriptionListData");

            // No mirroring: the two devices know nothing about each other.
            loopback = new SPINELoopback(hems, evse);

        }

        #endregion


        #region ADetailedDiscoveryTellsTheOtherSideEverything()

        /// <summary>
        /// SPINE 1.3.0, 7.1.1: the detailed discovery lists the device, its
        /// entities, its features and the functions of every feature together
        /// with what may be done with them.
        /// </summary>
        [Test]
        public async Task ADetailedDiscoveryTellsTheOtherSideEverything()
        {

            var discovered = new List<SPINERemoteDevice>();

            loopback.A.NodeManagement.OnDeviceDiscovered += (_, device) => discovered.Add(device);

            var response = await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            Assert.Multiple(() => {

                Assert.That(response.IsError, Is.False, response.Result?.Description);

                // The device itself.
                Assert.That(loopback.BAsSeenByA.DeviceAddress,  Is.EqualTo("d:_i:19667_EVSE"));
                Assert.That(loopback.BAsSeenByA.DeviceType,     Is.EqualTo(DeviceTypeType.ChargingStation));

                // Its entities: the device information and the charging station.
                Assert.That(loopback.BAsSeenByA.Entities.Count(), Is.EqualTo(2));

                var entity = loopback.BAsSeenByA.Entity([ 1 ]);

                Assert.That(entity?.EntityType,   Is.EqualTo(EntityTypeType.EVSE));
                Assert.That(entity?.Description,  Is.EqualTo("The charging station"));

                // Its features, and what they can do.
                var feature = entity?.Feature(FeatureTypeType.LoadControl, RoleType.Server);

                Assert.That(feature,              Is.Not.Null);
                Assert.That(feature?.Role,        Is.EqualTo(RoleType.Server));
                Assert.That(feature?.Description, Is.EqualTo("Load control server"));
                Assert.That(feature?.Functions,   Is.EquivalentTo(new[] { limits, "loadControlLimitDescriptionListData" }));

                Assert.That(feature?.FunctionData(limits)?.Operations.CanWrite,      Is.True);
                Assert.That(feature?.FunctionData(limits)?.Operations.CanReadPartial, Is.True);
                Assert.That(feature?.FunctionData("loadControlLimitDescriptionListData")?.Operations.CanWrite, Is.False);

                Assert.That(discovered, Has.Count.EqualTo(1));

            });

        }

        #endregion

        #region TheDetailedDiscoveryGoesToNodeManagementOfBothSides()

        /// <summary>
        /// The two node management features talk to each other: entity 0,
        /// feature 0 on both sides, whatever the features it is about are.
        /// </summary>
        [Test]
        public async Task TheDetailedDiscoveryGoesToNodeManagementOfBothSides()
        {

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            var read = loopback.AToB.Datagrams[0];

            Assert.Multiple(() => {
                Assert.That(read.Header?.AddressSource?.     ToString(), Is.EqualTo("d:_i:19667_HEMS:[0]:0"));
                Assert.That(read.Header?.AddressDestination?.ToString(), Is.EqualTo("d:_i:19667_EVSE:[0]:0"));
                Assert.That(read.Header?.CmdClassifier,                  Is.EqualTo(CmdClassifierType.Read));
                Assert.That(read.Payload?.Cmd?[0].DataFunction,          Is.EqualTo("nodeManagementDetailedDiscoveryData"));
            });

        }

        #endregion

        #region ASubscriptionIsAskedForAndAgreedTo()

        /// <summary>
        /// SPINE 1.3.0, 7.5: a client asks the node management of the other
        /// device for a subscription to one of its server features, and from
        /// then on the data comes by itself.
        /// </summary>
        [Test]
        public async Task ASubscriptionIsAskedForAndAgreedTo()
        {

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            var evseServer = loopback.BAsSeenByA.Entity([ 1 ])!.
                                 Feature(FeatureTypeType.LoadControl, RoleType.Server)!;

            var response = await loopback.A.NodeManagement.Subscribe(hemsLoadControl, evseServer);

            Assert.Multiple(() => {

                Assert.That(response.IsError, Is.False, response.Result?.Description);

                Assert.That(loopback.B.Subscriptions.All.Count(), Is.EqualTo(1));

                var subscription = loopback.B.Subscriptions.All.First();

                Assert.That(subscription.ClientAddress.ToString(), Is.EqualTo("d:_i:19667_HEMS:[1]:1"));
                Assert.That(subscription.ServerAddress.ToString(), Is.EqualTo("d:_i:19667_EVSE:[1]:1"));

            });

            // ... and it works: a change of the data arrives without asking.
            await evseLoadControl.SetData(limits,
                                          new LoadControlLimitListDataType {
                                              LoadControlLimitData = [
                                                  new LoadControlLimitDataType {
                                                      LimitId  = 1,
                                                      Value    = new ScaledNumberType { Number = 1600 }
                                                  }
                                              ]
                                          });

            Assert.That(evseServer.DataCopy<LoadControlLimitListDataType>(limits)?.
                            LoadControlLimitData?[0].Value?.Number,
                        Is.EqualTo(1600));

        }

        #endregion

        #region ASubscriptionCanBeGivenUp()

        /// <summary>
        /// After a delete call, the notifies stop.
        /// </summary>
        [Test]
        public async Task ASubscriptionCanBeGivenUp()
        {

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            var evseServer = loopback.BAsSeenByA.Entity([ 1 ])!.
                                 Feature(FeatureTypeType.LoadControl, RoleType.Server)!;

            await loopback.A.NodeManagement.Subscribe  (hemsLoadControl, evseServer);
            var response = await loopback.A.NodeManagement.Unsubscribe(hemsLoadControl, evseServer);

            var before = loopback.BToA.Datagrams.Count;

            await evseLoadControl.SetData(limits, new LoadControlLimitListDataType());

            Assert.Multiple(() => {
                Assert.That(response.IsError,                     Is.False, response.Result?.Description);
                Assert.That(loopback.B.Subscriptions.All,         Is.Empty);
                Assert.That(loopback.BToA.Datagrams,              Has.Count.EqualTo(before),
                            "A notify was sent although the subscription was given up.");
            });

        }

        #endregion

        #region ASubscriptionToAFeatureWhichDoesNotExistIsRefused()

        /// <summary>
        /// A subscription to something which is not there is answered with
        /// "destination unknown" rather than agreed to.
        /// </summary>
        [Test]
        public async Task ASubscriptionToAFeatureWhichDoesNotExistIsRefused()
        {

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            var nowhere = loopback.BAsSeenByA.
                              GetOrAddEntity([ 9 ], EntityTypeType.Generic).
                              GetOrAddFeature(9, FeatureTypeType.LoadControl, RoleType.Server);

            var response = await loopback.A.NodeManagement.Subscribe(hemsLoadControl, nowhere);

            Assert.Multiple(() => {
                Assert.That(response.IsError,              Is.True);
                Assert.That(response.Result?.ErrorNumber,  Is.EqualTo(SPINEErrorNumbers.DestinationUnknown));
                Assert.That(loopback.B.Subscriptions.All,  Is.Empty);
            });

        }

        #endregion


        #region ABindingIsWhatMakesAWriteGoThrough()

        /// <summary>
        /// The whole sequence of the load control use case, over the wire and
        /// with nothing arranged by hand: discover, bind, write.
        /// </summary>
        [Test]
        public async Task ABindingIsWhatMakesAWriteGoThrough()
        {

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            var evseServer = loopback.BAsSeenByA.Entity([ 1 ])!.
                                 Feature(FeatureTypeType.LoadControl, RoleType.Server)!;

            var limit      = new LoadControlLimitListDataType {
                                 LoadControlLimitData = [
                                     new LoadControlLimitDataType {
                                         LimitId  = 1,
                                         Value    = new ScaledNumberType { Number = 800 }
                                     }
                                 ]
                             };

            var refused    = await hemsLoadControl.Write(limits, limit, evseServer, Partial: true);

            var binding    = await loopback.A.NodeManagement.Bind(hemsLoadControl, evseServer);

            await evseLoadControl.SetData(limits,
                                          new LoadControlLimitListDataType {
                                              LoadControlLimitData = [
                                                  new LoadControlLimitDataType {
                                                      LimitId            = 1,
                                                      IsLimitChangeable  = true,
                                                      Value              = new ScaledNumberType { Number = 1600 }
                                                  }
                                              ]
                                          });

            var accepted   = await hemsLoadControl.Write(limits, limit, evseServer, Partial: true);

            Assert.Multiple(() => {

                Assert.That(refused.IsError,              Is.True);
                Assert.That(refused.Result?.ErrorNumber,  Is.EqualTo(SPINEErrorNumbers.BindingIsNecessaryForThisCommand));

                Assert.That(binding.IsError,              Is.False, binding.Result?.Description);
                Assert.That(loopback.B.Bindings.All.Count(), Is.EqualTo(1));

                Assert.That(accepted.IsError,             Is.False, accepted.Result?.Description);
                Assert.That(evseLoadControl.DataCopy<LoadControlLimitListDataType>(limits)?.
                                LoadControlLimitData?[0].Value?.Number,
                            Is.EqualTo(800));

            });

        }

        #endregion

        #region TheBindingsAndSubscriptionsCanBeRead()

        /// <summary>
        /// SPINE 1.3.0, 7.5.3 and 7.6.3: a device can ask what it has agreed to
        /// with the other one - and gets its own, not everybody's.
        /// </summary>
        [Test]
        public async Task TheBindingsAndSubscriptionsCanBeRead()
        {

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            var evseServer = loopback.BAsSeenByA.Entity([ 1 ])!.
                                 Feature(FeatureTypeType.LoadControl, RoleType.Server)!;

            await loopback.A.NodeManagement.Bind     (hemsLoadControl, evseServer);
            await loopback.A.NodeManagement.Subscribe(hemsLoadControl, evseServer);

            // Somebody else agreed to something, too.
            loopback.B.Bindings.Add(SPINEAddresses.Feature("d:_i:19667_Other", [ 1 ], 1),
                                    evseLoadControl.Address);

            var bindings      = await loopback.A.NodeManagement.Read("nodeManagementBindingData",
                                                                     loopback.BAsSeenByA.NodeManagement());

            var subscriptions = await loopback.A.NodeManagement.Read("nodeManagementSubscriptionData",
                                                                     loopback.BAsSeenByA.NodeManagement());

            Assert.Multiple(() => {

                var bindingEntries = (bindings.Data as NodeManagementBindingDataType)?.BindingEntry;

                Assert.That(bindingEntries,                            Has.Count.EqualTo(1),
                            "The binding of another device was answered as well.");
                Assert.That(bindingEntries?[0].ClientAddress?.ToString(), Is.EqualTo("d:_i:19667_HEMS:[1]:1"));
                Assert.That(bindingEntries?[0].BindingId,               Is.Not.Null);

                var subscriptionEntries = (subscriptions.Data as NodeManagementSubscriptionDataType)?.SubscriptionEntry;

                Assert.That(subscriptionEntries,                       Has.Count.EqualTo(1));
                Assert.That(subscriptionEntries?[0].ServerAddress?.ToString(), Is.EqualTo("d:_i:19667_EVSE:[1]:1"));

            });

        }

        #endregion

        #region AnAddressWithoutADeviceIsCompletedByTheReceiver()

        /// <summary>
        /// SPINE 1.3.0, 7.6.2: "if absent, the receiver has to identify the
        /// device via some other method". For a request that is unambiguous -
        /// only a client asks, so the server is the receiver.
        /// </summary>
        [Test]
        public async Task AnAddressWithoutADeviceIsCompletedByTheReceiver()
        {

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            var cmd = new CmdType {
                          NodeManagementBindingRequestCall = new NodeManagementBindingRequestCallType {
                              BindingRequest = new BindingManagementRequestCallType {
                                                   ClientAddress      = SPINEAddresses.Feature(null, [ 1 ], 1),
                                                   ServerAddress      = SPINEAddresses.Feature(null, [ 1 ], 1),
                                                   ServerFeatureType  = FeatureTypeType.LoadControl
                                               }
                          }
                      };

            await loopback.BAsSeenByA.Sender.Call(loopback.A.NodeManagement.Address,
                                                  loopback.BAsSeenByA.NodeManagement().Address,
                                                  cmd);

            Assert.Multiple(() => {

                Assert.That(loopback.B.Bindings.All.Count(), Is.EqualTo(1));

                var binding = loopback.B.Bindings.All.First();

                Assert.That(binding.ClientAddress.Device, Is.EqualTo("d:_i:19667_HEMS"),
                            "The client is the sender.");
                Assert.That(binding.ServerAddress.Device, Is.EqualTo("d:_i:19667_EVSE"),
                            "The server is the receiver.");

            });

        }

        #endregion


        #region AnEntityWhichAppearsIsAnnouncedAsAPartialNotify()

        /// <summary>
        /// SPINE 1.3.0, 7.1.1.5: a device which gains an entity says so with a
        /// partial notify of the detailed discovery, carrying that entity, its
        /// features and "lastStateChange: added" - not the whole device again.
        /// </summary>
        [Test]
        public async Task AnEntityWhichAppearsIsAnnouncedAsAPartialNotify()
        {

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            // The HEMS wants to hear about the charging station's entities.
            loopback.B.Subscriptions.Add(loopback.A.NodeManagement.Address,
                                         loopback.B.NodeManagement.Address);

            var changes = new List<(SPINERemoteEntity Entity, Boolean Added)>();

            loopback.A.NodeManagement.OnEntityChanged += (_, entity, added) => changes.Add((entity, added));

            var newEntity = loopback.B.AddEntity(EntityTypeType.EV);
            newEntity.Description = "The car";
            newEntity.AddFeature(FeatureTypeType.DeviceClassification, RoleType.Server).
                      AddFunction("deviceClassificationManufacturerData");

            await loopback.B.NodeManagement.NotifyEntityAdded(newEntity);

            var notify = loopback.BToA.Datagrams.Last();
            var data   = notify.Payload?.Cmd?[0].NodeManagementDetailedDiscoveryData;

            Assert.Multiple(() => {

                Assert.That(notify.Header?.CmdClassifier,      Is.EqualTo(CmdClassifierType.Notify));
                Assert.That(notify.Payload?.Cmd?[0].Filter?[0].IsPartial, Is.True);

                Assert.That(data?.EntityInformation,           Has.Count.EqualTo(1),
                            "The whole device was announced instead of the new entity.");
                Assert.That(data?.EntityInformation?[0].Description?.LastStateChange,
                            Is.EqualTo(NetworkManagementStateChangeType.Added));

                // The other side knows the entity now.
                Assert.That(loopback.BAsSeenByA.Entity([ 2 ])?.EntityType,  Is.EqualTo(EntityTypeType.EV));
                Assert.That(loopback.BAsSeenByA.Entity([ 2 ])?.Description, Is.EqualTo("The car"));
                Assert.That(loopback.BAsSeenByA.Entity([ 2 ])?.
                                Feature(FeatureTypeType.DeviceClassification, RoleType.Server)?.
                                HasFunction("deviceClassificationManufacturerData"), Is.True);

                Assert.That(changes, Has.Count.EqualTo(1));
                Assert.That(changes[0].Added, Is.True);

            });

        }

        #endregion

        #region AnEntityWhichDisappearsIsAnnouncedAndForgotten()

        /// <summary>
        /// The other half: an entity which is gone is announced as removed, and
        /// everything which was agreed with its features goes with it.
        ///
        /// The relation used here runs the other way round - the charging
        /// station is bound to a feature of the energy manager - because that is
        /// the one the energy manager itself holds, and therefore the one it has
        /// to clean up when the entity behind it disappears.
        /// </summary>
        [Test]
        public async Task AnEntityWhichDisappearsIsAnnouncedAndForgotten()
        {

            var hemsServer = loopback.A.Entities.
                                 First(entity => entity.EntityId is [ 1 ]).
                                 AddFeature(FeatureTypeType.Measurement, RoleType.Server);

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            loopback.A.Bindings.     Add(evseLoadControl.Address, hemsServer.Address);
            loopback.A.Subscriptions.Add(evseLoadControl.Address, hemsServer.Address);

            loopback.B.Subscriptions.Add(loopback.A.NodeManagement.Address,
                                         loopback.B.NodeManagement.Address);

            var changes = new List<(SPINERemoteEntity Entity, Boolean Added)>();

            loopback.A.NodeManagement.OnEntityChanged += (_, entity, added) => changes.Add((entity, added));

            await loopback.B.NodeManagement.NotifyEntityRemoved(evseLoadControl.Entity);

            Assert.Multiple(() => {

                Assert.That(loopback.BAsSeenByA.Entity([ 1 ]), Is.Null,
                            "The entity which is gone is still known.");

                Assert.That(changes,           Has.Count.EqualTo(1));
                Assert.That(changes[0].Added,  Is.False);

                Assert.That(loopback.A.Bindings.All,      Is.Empty,
                            "A binding of a feature which no longer exists was kept.");
                Assert.That(loopback.A.Subscriptions.All, Is.Empty,
                            "A subscription of a feature which no longer exists was kept.");

            });

        }

        #endregion

        #region AFullDetailedDiscoveryReplacesWhatWasKnown()

        /// <summary>
        /// A reply, and a notify without a filter, is the whole device: an
        /// entity which it no longer lists, it no longer has.
        /// </summary>
        [Test]
        public async Task AFullDetailedDiscoveryReplacesWhatWasKnown()
        {

            var extra = loopback.B.AddEntity(EntityTypeType.EV);
            extra.AddFeature(FeatureTypeType.DeviceClassification, RoleType.Server);

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            Assert.That(loopback.BAsSeenByA.Entity([ 2 ]), Is.Not.Null);

            // The car drives away, and the charging station is asked again.
            loopback.B.RemoveEntity([ 2 ]);

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            Assert.That(loopback.BAsSeenByA.Entity([ 2 ]), Is.Null,
                        "The entity is still known although the device no longer lists it.");

        }

        #endregion

        #region TheDestinationListNamesTheDeviceItself()

        /// <summary>
        /// SPINE 1.3.0, 7.2: a device which can forward says which devices are
        /// reachable through it. A device which forwards nothing answers with
        /// itself.
        /// </summary>
        [Test]
        public async Task TheDestinationListNamesTheDeviceItself()
        {

            var response = await loopback.A.NodeManagement.Read("nodeManagementDestinationListData",
                                                                loopback.BAsSeenByA.NodeManagement());

            var data = (response.Data as NodeManagementDestinationListDataType)?.NodeManagementDestinationData;

            Assert.Multiple(() => {
                Assert.That(response.IsError,                                   Is.False);
                Assert.That(data,                                               Has.Count.EqualTo(1));
                Assert.That(data?[0].DeviceDescription?.DeviceAddress?.Device,  Is.EqualTo("d:_i:19667_EVSE"));
                Assert.That(data?[0].DeviceDescription?.DeviceType,             Is.EqualTo(DeviceTypeType.ChargingStation));
            });

        }

        #endregion

        #region ASimpleDeviceHasNoDestinationList()

        /// <summary>
        /// A device of the "simple" feature set forwards nothing, so it does not
        /// offer the function at all.
        /// </summary>
        [Test]
        public void ASimpleDeviceHasNoDestinationList()
        {

            var simple = new SPINELocalDevice("d:_i:19667_Simple",
                                              DeviceTypeType.Generic,
                                              NetworkManagementFeatureSetType.Simple);

            Assert.Multiple(() => {
                Assert.That(simple.NodeManagement.HasFunction("nodeManagementDestinationListData"), Is.False);
                Assert.That(simple.NodeManagement.HasFunction("nodeManagementDetailedDiscoveryData"), Is.True);
            });

        }

        #endregion

        #region TheUseCasesOfADeviceCanBeRead()

        /// <summary>
        /// SPINE 1.3.0, 7.3: which use cases a device supports is a function of
        /// node management like any other.
        /// </summary>
        [Test]
        public async Task TheUseCasesOfADeviceCanBeRead()
        {

            loopback.B.NodeManagement.SetUseCaseData([
                new UseCaseInformationDataType {
                    Actor           = "ControllableSystem",
                    UseCaseSupport  = [
                        new UseCaseSupportType {
                            UseCaseName     = "limitationOfPowerConsumption",
                            UseCaseVersion  = "1.0.0",
                            ScenarioSupport = [ 1, 2, 3, 4 ]
                        }
                    ]
                }
            ]);

            var response = await loopback.A.NodeManagement.RequestUseCaseData(loopback.BAsSeenByA);

            var data = (response.Data as NodeManagementUseCaseDataType)?.UseCaseInformation;

            Assert.Multiple(() => {
                Assert.That(response.IsError,                                    Is.False);
                Assert.That(data,                                                Has.Count.EqualTo(1));
                Assert.That(data?[0].Actor?.ToString(),                          Is.EqualTo("ControllableSystem"));
                Assert.That(data?[0].UseCaseSupport?[0].UseCaseName?.ToString(), Is.EqualTo("limitationOfPowerConsumption"));
                Assert.That(data?[0].UseCaseSupport?[0].ScenarioSupport,         Is.EqualTo(new UInt32[] { 1, 2, 3, 4 }));
            });

        }

        #endregion

    }

}
