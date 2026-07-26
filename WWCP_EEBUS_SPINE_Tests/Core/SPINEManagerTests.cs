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

using NUnit.Framework;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// The managers of the SPINE core: the event bus and the heartbeat.
    ///
    /// No test here waits for anything. Time is a fake clock which the test
    /// moves, so a heartbeat every four seconds and a partner which goes silent
    /// for a minute both happen instantly and exactly.
    /// </summary>
    [TestFixture]
    public class SPINEManagerTests
    {

        #region Data

        private FakeTimeProvider   time      = null!;
        private SPINELoopback      loopback  = null!;

        /// <summary>The device diagnosis server of the charging station.</summary>
        private SPINELocalFeature  evseDiagnosis   = null!;

        /// <summary>The device diagnosis client of the energy manager.</summary>
        private SPINELocalFeature  hemsDiagnosis   = null!;

        #endregion

        #region Setup()

        [SetUp]
        public void Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            hemsDiagnosis = hems.AddEntity(EntityTypeType.CEM).
                                 AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Client);

            evseDiagnosis = evse.AddEntity(EntityTypeType.EVSE).
                                 AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Server);

            evseDiagnosis.AddFunction(SPINEHeartbeat.Function);

            loopback = new SPINELoopback(hems, evse).Mirror();

        }

        #endregion

        #region (private)

        private SPINERemoteFeature EVSEDiagnosis

            => loopback.BAsSeenByA.
                   Entity([ 1 ])!.
                   Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Server)!;

        #endregion


        #region CoreHandlersSeeAnEventBeforeTheApplicationDoes()

        /// <summary>
        /// The order between the two levels is the whole reason for having them:
        /// the stack reacts first, so that what the application then sees is
        /// already consistent.
        /// </summary>
        [Test]
        public void CoreHandlersSeeAnEventBeforeTheApplicationDoes()
        {

            var order = new List<String>();

            loopback.A.Events.Subscribe(_ => order.Add("application 1"), SPINEEventLevel.Application);
            loopback.A.Events.Subscribe(_ => order.Add("core 1"),        SPINEEventLevel.Core);
            loopback.A.Events.Subscribe(_ => order.Add("application 2"), SPINEEventLevel.Application);
            loopback.A.Events.Subscribe(_ => order.Add("core 2"),        SPINEEventLevel.Core);

            loopback.A.Events.Publish(new SPINEDeviceDiscovered(time.GetUtcNow(), loopback.BAsSeenByA));

            Assert.That(order, Is.EqualTo(new[] { "core 1", "core 2", "application 1", "application 2" }));

        }

        #endregion

        #region AnEventHandlerWhichThrowsDoesNotStopTheOthers()

        /// <summary>
        /// A handler is somebody else's code. It must not be able to stop a
        /// datagram from being processed - but it must not fail silently either.
        /// </summary>
        [Test]
        public void AnEventHandlerWhichThrowsDoesNotStopTheOthers()
        {

            var seen      = 0;
            var failures  = new List<Exception>();

            loopback.A.Events.OnHandlerFailed += (_, exception) => failures.Add(exception);

            loopback.A.Events.Subscribe(_ => throw new InvalidOperationException("no"));
            loopback.A.Events.Subscribe(_ => seen++);

            loopback.A.Events.Publish(new SPINEDeviceDiscovered(time.GetUtcNow(), loopback.BAsSeenByA));

            Assert.Multiple(() => {
                Assert.That(seen,                 Is.EqualTo(1));
                Assert.That(failures,             Has.Count.EqualTo(1));
                Assert.That(failures[0].Message,  Is.EqualTo("no"));
            });

        }

        #endregion

        #region AHandlerCanBeUnsubscribed()

        [Test]
        public void AHandlerCanBeUnsubscribed()
        {

            var seen    = 0;
            var handler = loopback.A.Events.Subscribe<SPINEDeviceDiscovered>(_ => seen++);

            loopback.A.Events.Publish(new SPINEDeviceDiscovered(time.GetUtcNow(), loopback.BAsSeenByA));

            Assert.That(loopback.A.Events.Unsubscribe(handler), Is.True);

            loopback.A.Events.Publish(new SPINEDeviceDiscovered(time.GetUtcNow(), loopback.BAsSeenByA));

            Assert.That(seen, Is.EqualTo(1));

        }

        #endregion

        #region EventsCarryTheTimeOfTheDeviceRatherThanTheClock()

        /// <summary>
        /// Every timestamp of the core comes from the time provider of the
        /// device. A test bench which records an exchange has to be able to say
        /// when, and a test has to be able to decide when.
        /// </summary>
        [Test]
        public async Task EventsCarryTheTimeOfTheDeviceRatherThanTheClock()
        {

            var events = new List<SPINEEvent>();

            loopback.A.Events.Subscribe(events.Add);

            time.Advance(TimeSpan.FromMinutes(5));

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            Assert.Multiple(() => {
                Assert.That(events, Is.Not.Empty);
                Assert.That(events[0].Timestamp,
                            Is.EqualTo(new DateTimeOffset(2026, 7, 26, 12, 5, 0, TimeSpan.Zero)));
            });

        }

        #endregion


        #region AHeartbeatIsAFunctionWhoseDataChangesOnATimer()

        /// <summary>
        /// There is no heartbeat message. A heartbeat is
        /// "deviceDiagnosisHeartbeatData" changing, and every subscriber getting
        /// a notify because it changed.
        /// </summary>
        [Test]
        public async Task AHeartbeatIsAFunctionWhoseDataChangesOnATimer()
        {

            loopback.B.Subscriptions.Add(hemsDiagnosis.Address, evseDiagnosis.Address);

            using var heartbeat = new SPINEHeartbeat(evseDiagnosis);

            await heartbeat.Start(TimeSpan.FromSeconds(60));

            var afterFirst = loopback.BToA.Datagrams.Count;

            time.Advance(TimeSpan.FromSeconds(58));
            time.Advance(TimeSpan.FromSeconds(58));

            var data = EVSEDiagnosis.DataCopy<DeviceDiagnosisHeartbeatDataType>(SPINEHeartbeat.Function);

            Assert.Multiple(() => {

                Assert.That(afterFirst,                    Is.EqualTo(1),
                            "The first heartbeat is not sent at once.");
                Assert.That(loopback.BToA.Datagrams,       Has.Count.EqualTo(3));

                Assert.That(loopback.BToA.Datagrams[0].Header?.CmdClassifier, Is.EqualTo(CmdClassifierType.Notify));

                Assert.That(data?.HeartbeatCounter,        Is.EqualTo(3));
                Assert.That(data?.HeartbeatTimeout?.ToString(), Is.EqualTo("PT1M"));
                Assert.That(data?.Timestamp?.AsDateTimeOffset,
                            Is.EqualTo(new DateTimeOffset(2026, 7, 26, 12, 1, 56, TimeSpan.Zero)));

            });

        }

        #endregion

        #region AHeartbeatIsSentTwoSecondsEarlyToSurviveTheDevicesWhichReadItStrictly()

        /// <summary>
        /// The Go reference implementation sends every two seconds earlier than
        /// announced, because devices exist which read the interval as "one has
        /// to arrive within this time" rather than "one is sent this often" -
        /// an Elli Connect goes into its fallback mode otherwise.
        ///
        /// It costs nothing, it is what the certified stack does, and it is the
        /// kind of thing a test bench should know it is doing.
        /// </summary>
        [Test]
        public async Task AHeartbeatIsSentTwoSecondsEarlyToSurviveTheDevicesWhichReadItStrictly()
        {

            loopback.B.Subscriptions.Add(hemsDiagnosis.Address, evseDiagnosis.Address);

            using var heartbeat = new SPINEHeartbeat(evseDiagnosis);

            await heartbeat.Start(TimeSpan.FromSeconds(60));

            time.Advance(TimeSpan.FromSeconds(58));

            Assert.That(loopback.BToA.Datagrams, Has.Count.EqualTo(2),
                        "The second heartbeat did not arrive after 58 seconds.");

        }

        #endregion

        #region AShortHeartbeatIntervalIsNotShortenedFurther()

        /// <summary>
        /// The OPEV and OSCEV use cases run a heartbeat of four seconds.
        /// Subtracting two from that would double the rate rather than shift it,
        /// so short intervals are left alone.
        /// </summary>
        [Test]
        public async Task AShortHeartbeatIntervalIsNotShortenedFurther()
        {

            loopback.B.Subscriptions.Add(hemsDiagnosis.Address, evseDiagnosis.Address);

            using var heartbeat = new SPINEHeartbeat(evseDiagnosis);

            await heartbeat.Start(TimeSpan.FromSeconds(4));

            time.Advance(TimeSpan.FromSeconds(3));

            var early = loopback.BToA.Datagrams.Count;

            time.Advance(TimeSpan.FromSeconds(1));

            Assert.Multiple(() => {
                Assert.That(early,                   Is.EqualTo(1));
                Assert.That(loopback.BToA.Datagrams, Has.Count.EqualTo(2));
            });

        }

        #endregion

        #region AHeartbeatWhichStopsIsReportedOnce()

        /// <summary>
        /// The other half: a client which stops hearing from a server has to
        /// act - in the load control use cases, by falling back to a safe limit.
        /// The core says when it stopped; what to do about it is a decision of
        /// the use case.
        /// </summary>
        [Test]
        public async Task AHeartbeatWhichStopsIsReportedOnce()
        {

            loopback.B.Subscriptions.Add(hemsDiagnosis.Address, evseDiagnosis.Address);

            using var heartbeat = new SPINEHeartbeat(evseDiagnosis);
            using var watcher   = new SPINEHeartbeat(
                                      loopback.A.Entities.
                                          First(entity => entity.EntityId is [ 1 ]).
                                          AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Server)
                                  );

            var missing = new List<SPINEHeartbeatMissing>();

            loopback.A.Events.Subscribe<SPINEHeartbeatMissing>(missing.Add);

            watcher.Watch(EVSEDiagnosis);

            await heartbeat.Start(TimeSpan.FromSeconds(60));

            // Two heartbeats arrive, and nothing is missing.
            time.Advance(TimeSpan.FromSeconds(58));

            var whileAlive = watcher.Check().Count();

            // The charging station is unplugged.
            heartbeat.Stop();

            // One second before twice the announced interval, nothing is wrong
            // yet - a heartbeat may still be on its way.
            time.Advance(TimeSpan.FromSeconds(119));

            var justInTime    = watcher.Check().ToList();

            time.Advance(TimeSpan.FromSeconds(2));

            var afterSilence  = watcher.Check().ToList();
            var askedAgain    = watcher.Check().ToList();

            Assert.Multiple(() => {

                Assert.That(whileAlive,        Is.EqualTo(0));
                Assert.That(justInTime,        Is.Empty,
                            "A heartbeat was reported missing before its time was up.");

                Assert.That(afterSilence,      Has.Count.EqualTo(1));
                Assert.That(afterSilence[0].RemoteFeature.Address.ToString(), Is.EqualTo("d:_i:19667_EVSE:[1]:1"));
                Assert.That(afterSilence[0].Timeout,                          Is.EqualTo(TimeSpan.FromSeconds(120)),
                            "A heartbeat counts as missing after twice the announced interval.");

                Assert.That(askedAgain,        Is.Empty,
                            "The same silence was reported twice.");

                Assert.That(missing,           Has.Count.EqualTo(1));

            });

            // ... and when it comes back, so does the watching.
            await heartbeat.Start(TimeSpan.FromSeconds(60));

            Assert.That(watcher.Check(), Is.Empty);

        }

        #endregion

        #region AHeartbeatOfADeviceNobodyWatchesChangesNothing()

        /// <summary>
        /// Watching is asked for, one feature at a time: a device may talk to
        /// many partners and depend on none of them.
        /// </summary>
        [Test]
        public async Task AHeartbeatOfADeviceNobodyWatchesChangesNothing()
        {

            using var watcher = new SPINEHeartbeat(
                                    loopback.A.Entities.
                                        First(entity => entity.EntityId is [ 1 ]).
                                        AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Server)
                                );

            using var heartbeat = new SPINEHeartbeat(evseDiagnosis);

            await heartbeat.Start(TimeSpan.FromSeconds(60));

            heartbeat.Stop();

            time.Advance(TimeSpan.FromHours(1));

            Assert.That(watcher.Check(), Is.Empty);

        }

        #endregion

        #region AHeartbeatNeedsADeviceDiagnosisServerFeature()

        /// <summary>
        /// A client feature has no data of its own, so it cannot send a
        /// heartbeat.
        /// </summary>
        [Test]
        public void AHeartbeatNeedsADeviceDiagnosisServerFeature()
        {

            Assert.Multiple(() => {

                Assert.That(() => new SPINEHeartbeat(hemsDiagnosis),
                            Throws.ArgumentException);

                Assert.That(() => new SPINEHeartbeat(
                                      loopback.A.Entities.
                                          First(entity => entity.EntityId is [ 1 ]).
                                          AddFeature(FeatureTypeType.LoadControl, RoleType.Server)),
                            Throws.ArgumentException);

            });

        }

        #endregion

        #region GivingUpADeviceGivesUpEverythingAgreedWithIt()

        /// <summary>
        /// A connection which is gone takes its subscriptions and its bindings
        /// with it. A binding which outlives the connection it was agreed on is
        /// a write permission nobody is watching.
        /// </summary>
        [Test]
        public async Task GivingUpADeviceGivesUpEverythingAgreedWithIt()
        {

            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);

            loopback.B.Subscriptions.Add(hemsDiagnosis.Address, evseDiagnosis.Address);
            loopback.B.Bindings.     Add(hemsDiagnosis.Address, evseDiagnosis.Address);

            Assert.That(loopback.B.RemoveRemoteDevice(loopback.AAsSeenByB.SKI), Is.True);

            Assert.Multiple(() => {
                Assert.That(loopback.B.Subscriptions.All, Is.Empty);
                Assert.That(loopback.B.Bindings.All,      Is.Empty);
                Assert.That(loopback.B.RemoteDevices,     Is.Empty);
            });

        }

        #endregion

    }

}
