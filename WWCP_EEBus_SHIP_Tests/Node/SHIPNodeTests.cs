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

using Microsoft.Extensions.Time.Testing;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP.tests
{

    /// <summary>
    /// Tests for the SHIP node: connection registry, double connections and pairing.
    /// </summary>
    [TestFixture]
    public class SHIPNodeTests
    {

        #region Data

        private static readonly SKI  lowSKI   = SKI.Parse("1111111111111111111111111111111111111111");
        private static readonly SKI  highSKI  = SKI.Parse("9999999999999999999999999999999999999999");

        private FakeTimeProvider  timeProvider = null!;
        private SHIPWire          wire         = null!;

        #endregion

        #region SetUp

        [SetUp]
        public void SetUp()
        {
            timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));
            wire         = new SHIPWire();
        }

        private SHIPNode Node(SKI SKI, Boolean AutoAccept = false, ISHIPTrustStore? TrustStore = null)

            => new (SKI,
                    SHIP_Id.Parse($"node-{SKI.ToString()[..4]}"),
                    TrustStore,
                    AutoAccept,
                    TimeProvider: timeProvider);

        #endregion


        #region TwoNodes_TrustingEachOther_CompleteTheHandshake()

        /// <summary>
        /// Two SHIP nodes which already trust each other complete the handshake
        /// and can exchange SPINE data.
        /// </summary>
        [Test]
        public async Task TwoNodes_TrustingEachOther_CompleteTheHandshake()
        {

            var nodeA       = Node(lowSKI,  TrustStore: new InMemoryTrustStore([ highSKI ]));
            var nodeB       = Node(highSKI, TrustStore: new InMemoryTrustStore([ lowSKI  ]));

            var transportA  = new RecordingTransport(wire);
            var transportB  = new RecordingTransport(wire);

            // The accepting node waits silently, so its connection exists before
            // the connecting node sends its first frame.
            var connectionB = await nodeB.AcceptAsync(lowSKI, transportB);
            transportA.Peer = connectionB;

            var connectionA = await nodeA.ConnectAsync(highSKI, transportA);
            transportB.Peer = connectionA;

            await wire.DeliverAsync();

            Assert.Multiple(() => {
                Assert.That(connectionA!.State,  Is.EqualTo(SHIPMessageExchangeStates.SmeStateComplete), connectionA.Error);
                Assert.That(connectionB!.State,  Is.EqualTo(SHIPMessageExchangeStates.SmeStateComplete), connectionB.Error);
                Assert.That(connectionA!.RemoteSHIPId,  Is.EqualTo(nodeB.SHIPId));
                Assert.That(connectionB!.RemoteSHIPId,  Is.EqualTo(nodeA.SHIPId));
            });

            JObject? received = null;
            nodeB.OnSPINEDataReceived += (node, ski, datagram) => received = datagram;

            await nodeA.SendSPINEDataAsync(highSKI, JObject.Parse(EEBusJSONTests.StandardJSON));
            await wire.DeliverAsync();

            Assert.That(received, Is.Not.Null, "The SPINE datagram has to reach the other node.");

        }

        #endregion

        #region KeepThisConnection_ResolvesDoubleConnectionsBySKI()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 12.2.2: a double connection is resolved by
        /// comparing the SKI values. The connection initiated by the higher SKI
        /// is the one that survives - the rule the Go reference implementation
        /// and therefore every device tested against it follows.
        /// </summary>
        [Test]
        public async Task KeepThisConnection_ResolvesDoubleConnectionsBySKI()
        {

            var lowNode   = Node(lowSKI,  AutoAccept: true);
            var highNode  = Node(highSKI, AutoAccept: true);

            // Without an existing connection everything is kept.
            Assert.Multiple(() => {
                Assert.That(lowNode. KeepThisConnection(highSKI, Incoming: true),   Is.True);
                Assert.That(highNode.KeepThisConnection(lowSKI,  Incoming: false),  Is.True);
            });

            await lowNode. AcceptAsync (highSKI, new RecordingTransport(wire));
            await highNode.ConnectAsync(lowSKI,  new RecordingTransport(wire));

            Assert.Multiple(() => {

                // The node with the LOWER SKI keeps the connection which the
                // higher one opened - and discards its own outgoing attempt.
                Assert.That(lowNode.KeepThisConnection(highSKI, Incoming: true),   Is.True,
                            "An incoming connection of the higher SKI wins.");
                Assert.That(lowNode.KeepThisConnection(highSKI, Incoming: false),  Is.False,
                            "Our own outgoing connection loses against the higher SKI.");

                // Mirrored at the node with the higher SKI.
                Assert.That(highNode.KeepThisConnection(lowSKI, Incoming: false),  Is.True,
                            "Our own outgoing connection wins, because we have the higher SKI.");
                Assert.That(highNode.KeepThisConnection(lowSKI, Incoming: true),   Is.False,
                            "An incoming connection of the lower SKI loses.");

            });

        }

        #endregion

        #region SecondConnection_FromTheHigherSKI_ReplacesTheFirst()

        [Test]
        public async Task SecondConnection_FromTheHigherSKI_ReplacesTheFirst()
        {

            var node       = Node(lowSKI, AutoAccept: true);

            var first      = new RecordingTransport(wire);
            var second     = new RecordingTransport(wire);

            await node.AcceptAsync(highSKI, first);
            var replaced = await node.AcceptAsync(highSKI, second);

            Assert.Multiple(() => {
                Assert.That(replaced,                       Is.Not.Null, "The incoming connection of the higher SKI has to replace the older one.");
                Assert.That(first.IsClosed,                 Is.True,     "The older connection has to be closed.");
                Assert.That(node.Connections.Count(),       Is.EqualTo(1));
            });

        }

        #endregion

        #region SecondConnection_FromTheLowerSKI_IsRefused()

        [Test]
        public async Task SecondConnection_FromTheLowerSKI_IsRefused()
        {

            var node    = Node(highSKI, AutoAccept: true);

            var first   = new RecordingTransport(wire);
            var second  = new RecordingTransport(wire);

            await node.AcceptAsync(lowSKI, first);
            var refused = await node.AcceptAsync(lowSKI, second);

            Assert.Multiple(() => {
                Assert.That(refused,                   Is.Null,  "The incoming connection of the lower SKI has to be refused.");
                Assert.That(second.IsClosed,           Is.True);
                Assert.That(first.IsClosed,            Is.False, "The existing connection has to be kept.");
                Assert.That(node.Connections.Count(),  Is.EqualTo(1));
            });

        }

        #endregion

        #region ConnectingToItself_IsRefused()

        [Test]
        public async Task ConnectingToItself_IsRefused()
        {

            var node       = Node(lowSKI, AutoAccept: true);
            var transport  = new RecordingTransport(wire);

            var connection = await node.ConnectAsync(lowSKI, transport);

            Assert.Multiple(() => {
                Assert.That(connection,       Is.Null);
                Assert.That(transport.IsClosed, Is.True);
            });

        }

        #endregion

        #region UnknownPartner_WithoutPairingListener_IsRejected()

        /// <summary>
        /// A node which neither trusts the communication partner, nor accepts
        /// everybody, nor has anybody who could ask the user, aborts right away
        /// instead of letting the partner wait.
        /// </summary>
        [Test]
        public async Task UnknownPartner_WithoutPairingListener_IsRejected()
        {

            var node       = Node(lowSKI);
            var transport  = new RecordingTransport(wire);

            var connection = await node.AcceptAsync(highSKI, transport);

            await connection!.ReceiveAsync(new SHIPInitMessage().ToByteArray());

            Assert.Multiple(() => {
                Assert.That(transport.LastSent<SHIPHelloMessage>()!.ConnectionHello.Phase,
                            Is.EqualTo(ConnectionHelloPhase.Aborted));
                Assert.That(transport.IsClosed, Is.True);
            });

        }

        #endregion

        #region PairingRequest_IsReportedAndCanBeApproved()

        /// <summary>
        /// An unknown communication partner triggers a pairing request; once the
        /// user approves it, the SKI is trusted and the handshake continues.
        /// </summary>
        [Test]
        public async Task PairingRequest_IsReportedAndCanBeApproved()
        {

            var node        = Node(lowSKI);
            var transport   = new RecordingTransport(wire);

            SKI? requested  = null;
            node.OnPairingRequest += (n, ski) => requested = ski;

            var connection  = await node.AcceptAsync(highSKI, transport);

            await connection!.ReceiveAsync(new SHIPInitMessage().ToByteArray());

            Assert.Multiple(() => {
                Assert.That(requested,                    Is.EqualTo(highSKI));
                Assert.That(node.PairingStateOf(highSKI), Is.EqualTo(SHIPPairingStates.ReceivedPairingRequest));
                Assert.That(transport.LastSent<SHIPHelloMessage>()!.ConnectionHello.Phase,
                            Is.EqualTo(ConnectionHelloPhase.Pending));
            });

            // The communication partner announces that it is ready and waiting.
            await connection.ReceiveAsync(
                      new SHIPHelloMessage(new ConnectionHello(ConnectionHelloPhase.Ready, 60000)).ToByteArray()
                  );

            // The user accepts.
            await node.ApprovePairingAsync(highSKI);

            Assert.Multiple(() => {
                Assert.That(node.TrustStore.IsTrusted(highSKI),  Is.True);
                Assert.That(node.PairingStateOf(highSKI),        Is.EqualTo(SHIPPairingStates.Trusted));
                // Our node has the server role here, so it does not send a
                // proposal but waits for the one of the communication partner.
                Assert.That(connection.State,
                            Is.EqualTo(SHIPMessageExchangeStates.SmeProtHStateServerListenProposal));
            });

        }

        #endregion

        #region RejectPairing_ClosesTheConnection()

        [Test]
        public async Task RejectPairing_ClosesTheConnection()
        {

            var node       = Node(lowSKI, TrustStore: new InMemoryTrustStore([ highSKI ]));
            var transport  = new RecordingTransport(wire);

            await node.AcceptAsync(highSKI, transport);

            await node.RejectPairingAsync(highSKI);

            Assert.Multiple(() => {
                Assert.That(node.TrustStore.IsTrusted(highSKI),  Is.False);
                Assert.That(node.PairingStateOf(highSKI),        Is.EqualTo(SHIPPairingStates.Rejected));
                Assert.That(transport.IsClosed,                  Is.True);
                Assert.That(node.Connections.Count(),            Is.EqualTo(0));
            });

        }

        #endregion

        #region AutoAccept_TrustsAndRemembersThePartner()

        /// <summary>
        /// A device in its pairing mode accepts everybody - and remembers the
        /// decision, so that it stays connected afterwards.
        /// </summary>
        [Test]
        public async Task AutoAccept_TrustsAndRemembersThePartner()
        {

            var node       = Node(lowSKI, AutoAccept: true);
            var transport  = new RecordingTransport(wire);

            var connection = await node.AcceptAsync(highSKI, transport);

            await connection!.ReceiveAsync(new SHIPInitMessage().ToByteArray());

            Assert.Multiple(() => {
                Assert.That(transport.LastSent<SHIPHelloMessage>()!.ConnectionHello.Phase,
                            Is.EqualTo(ConnectionHelloPhase.Ready));
                Assert.That(node.TrustStore.IsTrusted(highSKI), Is.True);
            });

        }

        #endregion

    }

}
