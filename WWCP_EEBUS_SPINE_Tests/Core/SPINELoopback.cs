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

using Newtonsoft.Json.Linq;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// Two devices talking to each other with nothing in between.
    ///
    /// The SPINE core knows one way out - <see cref="ISPINEWriter"/> - so the
    /// simplest possible partner is one which hands the datagram straight to the
    /// other device. Everything the two of them say to each other is recorded on
    /// the way through, which is what makes the assertions in these tests
    /// possible: not only what the data ended up being, but which datagrams
    /// there were.
    ///
    /// The message counters start at 1 on both sides and count up, so a
    /// recorded exchange can be compared datagram by datagram.
    /// </summary>
    public sealed class SPINELoopback
    {

        #region (class) Wire

        /// <summary>
        /// One direction of the loopback.
        /// </summary>
        public sealed class Wire : ISPINEWriter
        {

            /// <summary>
            /// Everything which was sent through this wire.
            /// </summary>
            public List<DatagramType>   Datagrams    { get; } = [];

            /// <summary>
            /// The device at the other end.
            /// </summary>
            public SPINELocalDevice?    Target       { get; set; }

            /// <summary>
            /// How the device at the other end knows the sender.
            /// </summary>
            public SPINERemoteDevice?   Sender       { get; set; }

            /// <summary>
            /// Whether to deliver at all. A wire which is cut records what would
            /// have been sent.
            /// </summary>
            public Boolean              Connected    { get; set; } = true;


            /// <summary>
            /// Hand a datagram to the device at the other end.
            /// </summary>
            public async Task SendSPINEDatagram(JObject            Datagram,
                                                CancellationToken  CancellationToken   = default)
            {

                var datagram = SPINEJSON.Read<DatagramType>(Datagram["datagram"]!)!;

                Datagrams.Add(datagram);

                if (Connected && Target is not null && Sender is not null)
                    await Target.ProcessDatagram(datagram, Sender, CancellationToken);

            }

        }

        #endregion

        #region Properties

        /// <summary>
        /// One of the two devices.
        /// </summary>
        public SPINELocalDevice   A            { get; }

        /// <summary>
        /// The other one.
        /// </summary>
        public SPINELocalDevice   B            { get; }

        /// <summary>
        /// How A knows B.
        /// </summary>
        public SPINERemoteDevice  BAsSeenByA   { get; }

        /// <summary>
        /// How B knows A.
        /// </summary>
        public SPINERemoteDevice  AAsSeenByB   { get; }

        /// <summary>
        /// The datagrams from A to B.
        /// </summary>
        public Wire               AToB         { get; }

        /// <summary>
        /// The datagrams from B to A.
        /// </summary>
        public Wire               BToA         { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Wire two devices to each other.
        /// </summary>
        /// <param name="A">One device.</param>
        /// <param name="B">The other one.</param>
        public SPINELoopback(SPINELocalDevice A,
                             SPINELocalDevice B)
        {

            this.A            = A;
            this.B            = B;

            this.AToB         = new Wire();
            this.BToA         = new Wire();

            var counterA      = 0UL;
            var counterB      = 0UL;

            this.BAsSeenByA   = A.AddRemoteDevice($"ski-of-{B.DeviceAddress}", AToB, () => Interlocked.Increment(ref counterA));
            this.AAsSeenByB   = B.AddRemoteDevice($"ski-of-{A.DeviceAddress}", BToA, () => Interlocked.Increment(ref counterB));

            this.BAsSeenByA.DeviceAddress  = B.DeviceAddress;
            this.BAsSeenByA.DeviceType     = B.DeviceType;

            this.AAsSeenByB.DeviceAddress  = A.DeviceAddress;
            this.AAsSeenByB.DeviceType     = A.DeviceType;

            this.AToB.Target  = B;
            this.AToB.Sender  = AAsSeenByB;

            this.BToA.Target  = A;
            this.BToA.Sender  = BAsSeenByA;

        }

        #endregion


        #region MirrorEntity(Entity, Into)

        /// <summary>
        /// Let the other side know an entity and its features, as a detailed
        /// discovery would - which is WP07b, and until then this is how the
        /// tests get a remote feature to talk to.
        /// </summary>
        /// <param name="Entity">An entity of one device.</param>
        /// <param name="Into">How the other device knows that device.</param>
        public static SPINERemoteEntity MirrorEntity(SPINELocalEntity   Entity,
                                                     SPINERemoteDevice  Into)
        {

            var remoteEntity = Into.GetOrAddEntity(Entity.EntityId,
                                                   Entity.EntityType);

            foreach (var feature in Entity.Features)
            {

                var remoteFeature = remoteEntity.GetOrAddFeature(feature.Id,
                                                                 feature.FeatureType,
                                                                 feature.Role);

                remoteFeature.SetOperations(feature.Information().Description?.SupportedFunction);

            }

            return remoteEntity;

        }

        #endregion

        #region Mirror()

        /// <summary>
        /// Let both sides know everything the other one has.
        /// </summary>
        public SPINELoopback Mirror()
        {

            foreach (var entity in A.Entities)
                MirrorEntity(entity, AAsSeenByB);

            foreach (var entity in B.Entities)
                MirrorEntity(entity, BAsSeenByA);

            return this;

        }

        #endregion

    }

}
