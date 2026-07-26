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

using Microsoft.Extensions.Time.Testing;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP.tests
{

    /// <summary>
    /// Tests for the SHIP message exchange state machines
    /// (SHIP TS 1.0.1, chapter 13.4).
    ///
    /// Not a single test waits for real time: the protocol timers run on a
    /// FakeTimeProvider which is advanced explicitly.
    /// </summary>
    [TestFixture]
    public class SHIPConnectionTests
    {

        #region Data

        private static readonly SKI      clientSKI    = SKI.Parse("1111111111111111111111111111111111111111");
        private static readonly SKI      serverSKI    = SKI.Parse("2222222222222222222222222222222222222222");
        private static readonly SHIP_Id  clientSHIPId = SHIP_Id.Parse("client-0001");
        private static readonly SHIP_Id  serverSHIPId = SHIP_Id.Parse("server-0001");

        private SHIPWire             wire           = null!;
        private FakeTimeProvider     timeProvider   = null!;
        private RecordingTransport   clientTransport = null!;
        private RecordingTransport   serverTransport = null!;
        private StaticTrustProvider  clientTrust    = null!;
        private StaticTrustProvider  serverTrust    = null!;
        private SHIPConnection       client         = null!;
        private SHIPConnection       server         = null!;

        #endregion

        #region SetUp

        [SetUp]
        public void SetUp()
        {

            timeProvider     = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            wire             = new SHIPWire();

            clientTransport  = new RecordingTransport(wire);
            serverTransport  = new RecordingTransport(wire);

            clientTrust      = new StaticTrustProvider();
            serverTrust      = new StaticTrustProvider();

            client           = new SHIPConnection(SHIPRoles.Client, serverSKI, clientSHIPId, clientTransport, clientTrust, TimeProvider: timeProvider);
            server           = new SHIPConnection(SHIPRoles.Server, clientSKI, serverSHIPId, serverTransport, serverTrust, TimeProvider: timeProvider);

            // Wire both connections back to back.
            clientTransport.Peer = server;
            serverTransport.Peer = client;

        }

        #endregion


        #region CompleteHandshake_BothSidesTrusted_Completes()

        /// <summary>
        /// The whole handshake of two mutually trusting SHIP nodes:
        /// CMI, Hello, protocol handshake, PIN and access methods.
        /// </summary>
        [Test]
        public async Task CompleteHandshake_BothSidesTrusted_Completes()
        {

            await server.StartAsync();
            await client.StartAsync();
            await wire.DeliverAsync();

            Assert.Multiple(() => {

                Assert.That(client.State,         Is.EqualTo(SHIPMessageExchangeStates.SmeStateComplete), client.Error);
                Assert.That(server.State,         Is.EqualTo(SHIPMessageExchangeStates.SmeStateComplete), server.Error);

                // The access methods exchange the SHIP identifiers of both nodes.
                Assert.That(client.RemoteSHIPId,  Is.EqualTo(serverSHIPId));
                Assert.That(server.RemoteSHIPId,  Is.EqualTo(clientSHIPId));

                Assert.That(clientTransport.IsClosed,  Is.False);

            });

        }

        #endregion

        #region CompleteHandshake_SendsTheSpecifiedMessageSequence()

        /// <summary>
        /// The client has to open with the connection mode initialisation and
        /// then follow the message sequence of chapter 13.4.
        /// </summary>
        [Test]
        public async Task CompleteHandshake_SendsTheSpecifiedMessageSequence()
        {

            await server.StartAsync();
            await client.StartAsync();
            await wire.DeliverAsync();

            var clientMessages = clientTransport.SentMessages().ToList();

            Assert.Multiple(() => {
                Assert.That(clientMessages[0],  Is.InstanceOf<SHIPInitMessage>());
                Assert.That(clientMessages[1],  Is.InstanceOf<SHIPHelloMessage>());
                Assert.That(clientMessages[2],  Is.InstanceOf<SHIPHandshakeMessage>());        // announceMax
                Assert.That(clientMessages[3],  Is.InstanceOf<SHIPHandshakeMessage>());        // select (confirmation)
                Assert.That(clientMessages[4],  Is.InstanceOf<SHIPPinStateMessage>());
                Assert.That(clientMessages[5],  Is.InstanceOf<SHIPAccessMethodsRequestMessage>());
                Assert.That(clientMessages[6],  Is.InstanceOf<SHIPAccessMethodsMessage>());
            });

        }

        #endregion

        #region CMI_NoAnswerWithinTimeout_Fails()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 13.4.3: the connection mode initialisation has
        /// to be answered within 10 seconds.
        /// </summary>
        [Test]
        public async Task CMI_NoAnswerWithinTimeout_Fails()
        {

            clientTransport.Peer = null;   // nobody answers

            await client.StartAsync();
            await wire.DeliverAsync();

            Assert.That(client.State, Is.EqualTo(SHIPMessageExchangeStates.CmiStateClientWait));

            timeProvider.Advance(TimeSpan.FromSeconds(9));
            Assert.That(client.State, Is.EqualTo(SHIPMessageExchangeStates.CmiStateClientWait), "The timeout must not fire too early.");

            timeProvider.Advance(TimeSpan.FromSeconds(2));
            await wire.DeliverAsync();

            Assert.Multiple(() => {
                Assert.That(client.State,              Is.EqualTo(SHIPMessageExchangeStates.SmeStateError));
                Assert.That(client.Error,              Does.Contain("connection mode initialisation"));
                Assert.That(clientTransport.IsClosed,  Is.True);
            });

        }

        #endregion

        #region CMI_UnexpectedMessage_Fails()

        /// <summary>
        /// Corresponds to TC_SHIP_CMI_001/002 of the official SHIP test specification.
        /// </summary>
        [Test]
        public async Task CMI_UnexpectedMessage_Fails()
        {

            clientTransport.Peer = null;

            await client.StartAsync();

            // A "connectionHello" instead of the expected init message.
            await client.ReceiveAsync(
                      new SHIPHelloMessage(new ConnectionHello(ConnectionHelloPhase.Ready)).ToByteArray()
                  );
            await wire.DeliverAsync();

            Assert.Multiple(() => {
                Assert.That(client.State,  Is.EqualTo(SHIPMessageExchangeStates.SmeStateError));
                Assert.That(client.Error,  Does.Contain("init"));
            });

        }

        #endregion

        #region Hello_UntrustedPartner_AnnouncesPending()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 13.4.4.1: a node which does not trust its
        /// communication partner yet announces "pending" and waits for a trust
        /// decision of the user.
        /// </summary>
        [Test]
        public async Task Hello_UntrustedPartner_AnnouncesPending()
        {

            serverTrust.Trusted = false;

            await server.StartAsync();
            await client.StartAsync();
            await wire.DeliverAsync();

            var hello = serverTransport.LastSent<SHIPHelloMessage>();

            Assert.Multiple(() => {
                Assert.That(hello,                                   Is.Not.Null);
                Assert.That(hello!.ConnectionHello.Phase,            Is.EqualTo(ConnectionHelloPhase.Pending));
                Assert.That(hello!.ConnectionHello.Waiting,          Is.EqualTo(60000));
                Assert.That(server.State,                            Is.EqualTo(SHIPMessageExchangeStates.SmeHelloStatePendingListen));
                Assert.That(client.State,                            Is.EqualTo(SHIPMessageExchangeStates.SmeHelloStateReadyListen));
            });

        }

        #endregion

        #region Hello_UntrustedAndNotWaiting_Aborts()

        /// <summary>
        /// A node which is not willing to wait for a trust decision aborts right
        /// away instead of letting the communication partner wait.
        /// </summary>
        [Test]
        public async Task Hello_UntrustedAndNotWaiting_Aborts()
        {

            serverTrust.Trusted       = false;
            serverTrust.WaitForTrust  = false;

            await server.StartAsync();
            await client.StartAsync();
            await wire.DeliverAsync();

            var abort = serverTransport.LastSent<SHIPHelloMessage>();

            Assert.Multiple(() => {
                Assert.That(abort!.ConnectionHello.Phase,  Is.EqualTo(ConnectionHelloPhase.Aborted));
                Assert.That(serverTransport.IsClosed,      Is.True);

                // The client has to react to the abort of its partner.
                Assert.That(client.State,                  Is.EqualTo(SHIPMessageExchangeStates.SmeHelloStateRemoteAbortDone));
            });

        }

        #endregion

        #region Hello_ProlongationRequest_IsAnswered()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 13.4.4.1.3: the waiting node asks for a
        /// prolongation before the timer of its partner expires, and the partner
        /// answers with a fresh waiting time.
        /// </summary>
        [Test]
        public async Task Hello_ProlongationRequest_IsAnswered()
        {

            serverTrust.Trusted = false;

            await server.StartAsync();
            await client.StartAsync();
            await wire.DeliverAsync();

            var messagesBefore = clientTransport.SentFrames.Count;

            // The server asks for a prolongation 30 seconds before the 60 second
            // timer of the client expires.
            timeProvider.Advance(TimeSpan.FromSeconds(31));
            await wire.DeliverAsync();

            var prolongationRequest = serverTransport.LastSent<SHIPHelloMessage>();

            Assert.Multiple(() => {

                Assert.That(prolongationRequest!.ConnectionHello.Phase,                Is.EqualTo(ConnectionHelloPhase.Pending));
                Assert.That(prolongationRequest!.ConnectionHello.ProlongationRequest,  Is.True);

                // The client answers with a new waiting time and keeps waiting.
                Assert.That(clientTransport.SentFrames.Count,                          Is.GreaterThan(messagesBefore));
                Assert.That(clientTransport.LastSent<SHIPHelloMessage>()!.ConnectionHello.Phase,
                            Is.EqualTo(ConnectionHelloPhase.Ready));
                Assert.That(client.State,                                              Is.EqualTo(SHIPMessageExchangeStates.SmeHelloStateReadyListen));

            });

        }

        #endregion

        #region Hello_TrustGrantedWhileWaiting_CompletesHandshake()

        /// <summary>
        /// The user accepts the communication partner while it is waiting - the
        /// handshake then continues as usual.
        /// </summary>
        [Test]
        public async Task Hello_TrustGrantedWhileWaiting_CompletesHandshake()
        {

            serverTrust.Trusted = false;

            await server.StartAsync();
            await client.StartAsync();
            await wire.DeliverAsync();

            Assert.That(server.State, Is.EqualTo(SHIPMessageExchangeStates.SmeHelloStatePendingListen));

            // The user accepts the SKI; the application tells the connection.
            serverTrust.Trusted = true;
            await server.ApproveTrustAsync();
            await wire.DeliverAsync();

            Assert.Multiple(() => {
                Assert.That(server.State,  Is.EqualTo(SHIPMessageExchangeStates.SmeStateComplete), server.Error);
                Assert.That(client.State,  Is.EqualTo(SHIPMessageExchangeStates.SmeStateComplete), client.Error);
            });

        }

        #endregion

        #region Hello_ReadyTimeout_Aborts()

        /// <summary>
        /// Corresponds to TC_SHIP_HELLO_003: the Wait-For-Ready timer expires.
        /// </summary>
        [Test]
        public async Task Hello_ReadyTimeout_Aborts()
        {

            await server.StartAsync();

            // Only the CMI is answered, then the partner goes silent.
            await server.ReceiveAsync(new SHIPInitMessage().ToByteArray());
            await wire.DeliverAsync();

            serverTransport.Peer = null;

            Assert.That(server.State, Is.EqualTo(SHIPMessageExchangeStates.SmeHelloStateReadyListen));

            timeProvider.Advance(TimeSpan.FromSeconds(61));
            await wire.DeliverAsync();

            Assert.Multiple(() => {
                Assert.That(serverTransport.LastSent<SHIPHelloMessage>()!.ConnectionHello.Phase,
                            Is.EqualTo(ConnectionHelloPhase.Aborted));
                Assert.That(serverTransport.IsClosed, Is.True);
            });

        }

        #endregion

        #region Hello_WaitingTimeBelowMinimum_Aborts()

        /// <summary>
        /// A waiting time below one second leaves no room for the prolongation
        /// mechanism and is therefore a protocol violation (chapter 13.4.4.1.3).
        /// </summary>
        [Test]
        public async Task Hello_WaitingTimeBelowMinimum_Aborts()
        {

            serverTrust.Trusted = false;

            await server.StartAsync();
            await server.ReceiveAsync(new SHIPInitMessage().ToByteArray());
            await wire.DeliverAsync();

            serverTransport.Peer = null;

            await server.ReceiveAsync(
                      new SHIPHelloMessage(
                          new ConnectionHello(ConnectionHelloPhase.Ready, 500)
                      ).ToByteArray()
                  );
            await wire.DeliverAsync();

            Assert.Multiple(() => {
                Assert.That(serverTransport.LastSent<SHIPHelloMessage>()!.ConnectionHello.Phase,
                            Is.EqualTo(ConnectionHelloPhase.Aborted));
                Assert.That(serverTransport.IsClosed, Is.True);
            });

        }

        #endregion

        #region ProtocolHandshake_UnsupportedVersion_IsRejected()

        /// <summary>
        /// A communication partner announcing a protocol version we cannot speak
        /// has to receive a "selection mismatch" error.
        /// </summary>
        [Test]
        public async Task ProtocolHandshake_UnsupportedVersion_IsRejected()
        {

            await server.StartAsync();
            await server.ReceiveAsync(new SHIPInitMessage().ToByteArray());
            await wire.DeliverAsync();
            await server.ReceiveAsync(new SHIPHelloMessage(new ConnectionHello(ConnectionHelloPhase.Ready, 60000)).ToByteArray());
            await wire.DeliverAsync();

            Assert.That(server.State, Is.EqualTo(SHIPMessageExchangeStates.SmeProtHStateServerListenProposal));

            serverTransport.Peer = null;

            await server.ReceiveAsync(
                      new SHIPHandshakeMessage(
                          new MessageProtocolHandshake(
                              ProtocolHandshakeTypeTypes.announceMax,
                              new MessageProtocolHandshakeVersion(2, 0),
                              [ MessageProtocolFormat.JSON_UTF8 ]
                          )
                      ).ToByteArray()
                  );
            await wire.DeliverAsync();

            var error = serverTransport.LastSent<SHIPHandshakeErrorMessage>();

            Assert.Multiple(() => {
                Assert.That(error,                                        Is.Not.Null);
                Assert.That(error!.MessageProtocolHandshakeError.Error,   Is.EqualTo((Byte) MessageProtocolHandshakeErrors.SelectionMismatch));
                Assert.That(server.State,                                 Is.EqualTo(SHIPMessageExchangeStates.SmeStateError));
            });

        }

        #endregion

        #region PinState_OtherThanNone_IsRejected()

        /// <summary>
        /// This implementation only supports the PIN state "none"; anything else
        /// ends the handshake with a clear error instead of a silent failure.
        /// </summary>
        [Test]
        public async Task PinState_OtherThanNone_IsRejected()
        {

            await server.StartAsync();
            await client.StartAsync();
            await wire.DeliverAsync();

            // Both sides completed; now replay a PIN state on a fresh connection.
            var transport   = new RecordingTransport();
            var connection  = new SHIPConnection(SHIPRoles.Server, clientSKI, serverSHIPId, transport,
                                                 new StaticTrustProvider(), TimeProvider: timeProvider);

            await connection.StartAsync();
            await connection.ReceiveAsync(new SHIPInitMessage().ToByteArray());
            await wire.DeliverAsync();
            await connection.ReceiveAsync(new SHIPHelloMessage(new ConnectionHello(ConnectionHelloPhase.Ready, 60000)).ToByteArray());
            await wire.DeliverAsync();
            await connection.ReceiveAsync(new SHIPHandshakeMessage(
                                              new MessageProtocolHandshake(
                                                  ProtocolHandshakeTypeTypes.announceMax,
                                                  new MessageProtocolHandshakeVersion(1, 0),
                                                  [ MessageProtocolFormat.JSON_UTF8 ]
                                              )).ToByteArray());
            await wire.DeliverAsync();
            await connection.ReceiveAsync(new SHIPHandshakeMessage(
                                              new MessageProtocolHandshake(
                                                  ProtocolHandshakeTypeTypes.select,
                                                  new MessageProtocolHandshakeVersion(1, 0),
                                                  [ MessageProtocolFormat.JSON_UTF8 ]
                                              )).ToByteArray());
            await wire.DeliverAsync();

            Assert.That(connection.State, Is.EqualTo(SHIPMessageExchangeStates.SmePinStateCheckListen));

            await connection.ReceiveAsync(
                      new SHIPPinStateMessage(new ConnectionPinState(PinState.Required)).ToByteArray()
                  );
            await wire.DeliverAsync();

            Assert.Multiple(() => {
                Assert.That(connection.State,  Is.EqualTo(SHIPMessageExchangeStates.SmeStateError));
                Assert.That(connection.Error,  Does.Contain("required"));
            });

        }

        #endregion

        #region SPINEData_AfterHandshake_IsDelivered()

        /// <summary>
        /// After the handshake the connection carries SPINE datagrams.
        /// </summary>
        [Test]
        public async Task SPINEData_AfterHandshake_IsDelivered()
        {

            await server.StartAsync();
            await client.StartAsync();
            await wire.DeliverAsync();

            JObject? received = null;
            server.OnSPINEDataReceived += (connection, datagram) => received = datagram;

            var datagram = JObject.Parse(EEBUSJSONTests.StandardJSON);

            await client.SendSPINEDataAsync(datagram);
            await wire.DeliverAsync();

            Assert.Multiple(() => {
                Assert.That(received,                                                    Is.Not.Null);
                Assert.That(received!.ToString(Newtonsoft.Json.Formatting.None),         Is.EqualTo(datagram.ToString(Newtonsoft.Json.Formatting.None)));
            });

        }

        #endregion

        #region SPINEData_BeforeHandshakeCompleted_IsRefused()

        [Test]
        public void SPINEData_BeforeHandshakeCompleted_IsRefused()
        {

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await client.SendSPINEDataAsync(new JObject())
            );

        }

        #endregion

        #region Close_IsAnnouncedAndConfirmed()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 13.4.7: a connection close is announced and
        /// confirmed by the communication partner.
        /// </summary>
        [Test]
        public async Task Close_IsAnnouncedAndConfirmed()
        {

            await server.StartAsync();
            await client.StartAsync();
            await wire.DeliverAsync();

            await client.CloseAsync(ConnectionCloseReasons.RemovedConnection);
            await wire.DeliverAsync();

            var announce  = clientTransport.LastSent<SHIPCloseMessage>();
            var confirm   = serverTransport.LastSent<SHIPCloseMessage>();

            Assert.Multiple(() => {
                Assert.That(announce!.ConnectionClose.Phase,   Is.EqualTo(ConnectionClosePhases.Announce));
                Assert.That(announce!.ConnectionClose.Reason,  Is.EqualTo(ConnectionCloseReasons.RemovedConnection));
                Assert.That(confirm !.ConnectionClose.Phase,   Is.EqualTo(ConnectionClosePhases.Confirm));
                Assert.That(clientTransport.IsClosed,          Is.True);
                Assert.That(serverTransport.IsClosed,          Is.True);
            });

        }

        #endregion

    }

}
