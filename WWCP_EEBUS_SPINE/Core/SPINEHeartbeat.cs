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

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// The heartbeat of a device (SPINE 1.3.0, DeviceDiagnosis).
    ///
    /// A device which controls something another device depends on has to keep
    /// saying that it is still there. The mechanism is nothing but a function
    /// whose data changes on a timer: "deviceDiagnosisHeartbeatData" carries a
    /// timestamp, a counter and the interval, and every subscriber gets a notify
    /// because the data changed. There is no heartbeat message.
    ///
    /// The other half is watching one: a client which stops hearing from a
    /// server has to act - in the load control use cases, by falling back to a
    /// safe limit. This class does not decide what to do; it says when the
    /// heartbeat stopped, and the use case layer decides.
    ///
    /// Everything here runs on the <see cref="TimeProvider"/> of the device, so
    /// the tests move time rather than wait for it.
    /// </summary>
    public class SPINEHeartbeat : IDisposable
    {

        #region Data

        /// <summary>
        /// The function whose data is the heartbeat.
        /// </summary>
        public const     String              Function          = "deviceDiagnosisHeartbeatData";

        /// <summary>
        /// How long after the announced interval a heartbeat counts as missing.
        /// Twice the interval is what the load control use cases expect.
        /// </summary>
        public const     Double              MissingAfter      = 2.0;

        private readonly Lock                heartbeatLock     = new ();

        private          ITimer?             timer;

        private          UInt64              counter;

        private readonly Dictionary<String, (SPINERemoteFeature Feature, DateTimeOffset LastSeen, TimeSpan Timeout, Boolean Reported)> watched
                                                               = new (StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>
        /// The device diagnosis server feature which sends the heartbeat.
        /// </summary>
        public SPINELocalFeature  Feature     { get; }

        /// <summary>
        /// The device this heartbeat belongs to.
        /// </summary>
        public SPINELocalDevice   Device
            => Feature.Device;

        /// <summary>
        /// How often a heartbeat is sent, once it is running.
        /// </summary>
        public TimeSpan?          Interval    { get; private set; }

        /// <summary>
        /// Whether the heartbeat is running.
        /// </summary>
        public Boolean            IsRunning
            => timer is not null;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the heartbeat of a device diagnosis server feature.
        /// </summary>
        /// <param name="Feature">A device diagnosis server feature.</param>
        public SPINEHeartbeat(SPINELocalFeature Feature)
        {

            if (Feature.FeatureType != FeatureTypeType.DeviceDiagnosis ||
                Feature.Role        != RoleType.Server)
                throw new ArgumentException("A heartbeat is sent by a device diagnosis server feature.",
                                            nameof(Feature));

            this.Feature = Feature;

            if (!Feature.HasFunction(Function))
                Feature.AddFunction(Function);

            Device.Events.Subscribe<SPINEDataChanged>(Received,
                                                      SPINEEventLevel.Core);

        }

        #endregion


        #region Start(Interval, CancellationToken = default)

        /// <summary>
        /// Start saying that this device is still there.
        ///
        /// The first heartbeat goes out at once, so that a subscriber does not
        /// have to wait a whole interval to learn that the device is alive.
        /// </summary>
        /// <param name="Interval">How often to send one.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task Start(TimeSpan           Interval,
                                CancellationToken  CancellationToken   = default)
        {

            Stop();

            this.Interval = Interval;

            // The Go reference implementation sends every two seconds earlier
            // than announced, because devices exist which read the interval as
            // "a heartbeat has to arrive within this time" rather than "one is
            // sent this often" - an Elli Connect goes into its fallback mode
            // otherwise. Sending a little early costs nothing and is what the
            // certified stack does.
            var period = Interval > TimeSpan.FromSeconds(4)
                             ? Interval - TimeSpan.FromSeconds(2)
                             : Interval;

            lock (heartbeatLock)
            {
                timer = Device.TimeProvider.CreateTimer(
                            _ => Beat(CancellationToken).GetAwaiter().GetResult(),
                            null,
                            period,
                            period
                        );
            }

            await Beat(CancellationToken);

        }

        #endregion

        #region Stop()

        /// <summary>
        /// Stop sending heartbeats. Whoever subscribed hears nothing more, which
        /// is exactly what they are watching for.
        /// </summary>
        public void Stop()
        {
            lock (heartbeatLock)
            {
                timer?.Dispose();
                timer = null;
            }
        }

        #endregion

        #region SendOnce(Interval = null, CancellationToken = default)

        /// <summary>
        /// Send a single heartbeat, now, without starting or disturbing the
        /// timer.
        ///
        /// The limitation use cases ask for exactly this. Rule 913: after the
        /// initial connection or the restoration of communication, the energy
        /// guard sends "a heartbeat and a following APCL within 60 seconds ...
        /// after having determined that the communication is possible again" -
        /// which is a beat caused by an event rather than by a period, and
        /// waiting for the next tick of the timer would miss the window.
        /// </summary>
        /// <param name="Interval">Which timeout to announce, when no timer is running.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task SendOnce(TimeSpan?          Interval            = null,
                                   CancellationToken  CancellationToken   = default)
        {

            lock (heartbeatLock)
            {
                this.Interval ??= Interval ?? TimeSpan.FromSeconds(60);
            }

            await Beat(CancellationToken);

        }

        #endregion

        #region (private) Beat(CancellationToken)

        private async Task Beat(CancellationToken CancellationToken)
        {

            UInt64    beat;
            TimeSpan  interval;

            lock (heartbeatLock)
            {

                if (Interval is null)
                    return;

                beat      = ++counter;
                interval  = Interval.Value;

            }

            // Setting the data notifies every subscriber - which is the whole
            // mechanism.
            await Feature.SetData(
                      Function,
                      new DeviceDiagnosisHeartbeatDataType {
                          Timestamp         = AbsoluteOrRelativeTimeType.Parse(Device.TimeProvider.GetUtcNow()),
                          HeartbeatCounter  = beat,
                          HeartbeatTimeout  = DurationType.Parse(interval)
                      },
                      CancellationToken: CancellationToken
                  );

        }

        #endregion


        #region Watch(RemoteFeature, Timeout = null) / Forget(RemoteFeature)

        /// <summary>
        /// Watch the heartbeat of a remote feature, and say when it stops.
        /// </summary>
        /// <param name="RemoteFeature">A device diagnosis server feature of another device.</param>
        /// <param name="Timeout">How long to wait before saying so. The interval the other device announces, doubled, by default.</param>
        public void Watch(SPINERemoteFeature  RemoteFeature,
                          TimeSpan?           Timeout   = null)
        {
            lock (heartbeatLock)
            {
                watched[SPINEAddresses.KeyOf(RemoteFeature.Address)]
                    = (RemoteFeature,
                       Device.TimeProvider.GetUtcNow(),
                       Timeout ?? TimeSpan.Zero,
                       false);
            }
        }


        /// <summary>
        /// Stop watching a remote feature.
        /// </summary>
        /// <param name="RemoteFeature">A feature of another device.</param>
        public Boolean Forget(SPINERemoteFeature RemoteFeature)
        {
            lock (heartbeatLock)
            {
                return watched.Remove(SPINEAddresses.KeyOf(RemoteFeature.Address));
            }
        }

        #endregion

        #region Check()

        /// <summary>
        /// Look at every watched feature and report the ones which have gone
        /// quiet.
        ///
        /// This is called by whoever drives the time - a timer of the
        /// application, or a test moving a fake clock. The heartbeat itself does
        /// not poll: a device which sends nothing gives nothing to react to, so
        /// somebody has to look.
        /// </summary>
        public IEnumerable<SPINEHeartbeatMissing> Check()
        {

            var now      = Device.TimeProvider.GetUtcNow();
            var missing  = new List<SPINEHeartbeatMissing>();

            lock (heartbeatLock)
            {
                foreach (var key in watched.Keys.ToArray())
                {

                    var entry    = watched[key];

                    var timeout  = entry.Timeout > TimeSpan.Zero
                                       ? entry.Timeout
                                       : AnnouncedInterval(entry.Feature) * MissingAfter;

                    if (timeout <= TimeSpan.Zero || entry.Reported || now - entry.LastSeen <= timeout)
                        continue;

                    missing.Add(new SPINEHeartbeatMissing(now,
                                                          entry.Feature,
                                                          entry.LastSeen,
                                                          timeout));

                    watched[key] = entry with { Reported = true };

                }
            }

            foreach (var entry in missing)
                Device.Events.Publish(entry);

            return missing;

        }

        #endregion

        #region (private) Received(Event) / AnnouncedInterval(Feature)

        /// <summary>
        /// A heartbeat of a watched feature arrived.
        /// </summary>
        private void Received(SPINEDataChanged Event)
        {

            if (Event.Change.Function != Function)
                return;

            lock (heartbeatLock)
            {

                var key = SPINEAddresses.KeyOf(Event.Change.RemoteFeature.Address);

                if (watched.TryGetValue(key, out var entry))
                    watched[key] = entry with {
                                       LastSeen  = Device.TimeProvider.GetUtcNow(),
                                       Reported  = false
                                   };

            }

        }


        /// <summary>
        /// How often the other device says it sends a heartbeat.
        /// </summary>
        private static TimeSpan AnnouncedInterval(SPINERemoteFeature Feature)

            => Feature.DataCopy<DeviceDiagnosisHeartbeatDataType>(Function)?.
                   HeartbeatTimeout?.AsTimeSpan ?? TimeSpan.Zero;

        #endregion

        #region Dispose()

        /// <summary>
        /// Stop the heartbeat.
        /// </summary>
        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        #endregion

    }

}
