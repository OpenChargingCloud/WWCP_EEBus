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

using System.Security.Cryptography.X509Certificates;

using NUnit.Framework;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Hermod;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP.tests
{

    /// <summary>
    /// Two SHIP nodes talking to each other over the network: binary WebSocket
    /// frames over TLS with mutual authentication and the sub protocol "ship".
    ///
    /// These tests use real sockets on the loopback interface and therefore run
    /// against the wall clock - unlike the state machine tests.
    /// </summary>
    [TestFixture]
    [Category("LocalNetwork")]
    public class SHIPWebSocketEndpointTests
    {

        #region Data

        private X509Certificate2       serverCertificate  = null!;
        private X509Certificate2       clientCertificate  = null!;
        private SKI                    serverSKI;
        private SKI                    clientSKI;

        private SHIPNode               serverNode         = null!;
        private SHIPNode               clientNode         = null!;
        private SHIPWebSocketEndpoint  serverEndpoint     = null!;
        private SHIPWebSocketEndpoint  clientEndpoint     = null!;

        #endregion

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {

            serverCertificate = SHIPCertificates.GenerateCertificate("EVSE-Server");
            clientCertificate = SHIPCertificates.GenerateCertificate("CEM-Client");

            Assert.That(SHIPCertificates.TryGetSKI(serverCertificate, out serverSKI, out _), Is.True);
            Assert.That(SHIPCertificates.TryGetSKI(clientCertificate, out clientSKI, out _), Is.True);

            // Both nodes know each other, so no user interaction is needed.
            serverNode      = new SHIPNode(serverSKI, SHIP_Id.Parse("evse-0001"), new InMemoryTrustStore([ clientSKI ]));
            clientNode      = new SHIPNode(clientSKI, SHIP_Id.Parse("cem-0001"),  new InMemoryTrustStore([ serverSKI ]));

            // Port 0 lets the operating system choose a free port.
            serverEndpoint  = new SHIPWebSocketEndpoint(serverNode, serverCertificate, IPPort.Parse(0));
            clientEndpoint  = new SHIPWebSocketEndpoint(clientNode, clientCertificate, IPPort.Parse(0));

        }

        [TearDown]
        public async Task TearDown()
        {

            if (clientEndpoint is not null)
                await clientEndpoint.ShutdownAsync();

            if (serverEndpoint is not null)
                await serverEndpoint.ShutdownAsync();

            serverCertificate?.Dispose();
            clientCertificate?.Dispose();

        }

        #endregion

        #region (private) WaitFor(Condition, Timeout)

        private static async Task<Boolean> WaitFor(Func<Boolean> Condition,
                                                   TimeSpan?     Timeout   = null)
        {

            var timeout = Timeout ?? TimeSpan.FromSeconds(20);
            var until   = DateTimeOffset.UtcNow + timeout;

            while (DateTimeOffset.UtcNow < until)
            {

                if (Condition())
                    return true;

                await Task.Delay(50);

            }

            return Condition();

        }

        #endregion


        #region TwoNodes_OverTLSWebSocket_CompleteTheHandshake()

        /// <summary>
        /// The complete stack: TLS with mutual authentication, the "ship" sub
        /// protocol, the SHIP handshake, and a SPINE datagram on top.
        /// </summary>
        [Test]
        public async Task TwoNodes_OverTLSWebSocket_CompleteTheHandshake()
        {

            await serverEndpoint.StartAsync();

            SHIPConnection? serverSide = null;
            serverNode.OnConnected += (node, connection) => serverSide = connection;

            var connection = await clientEndpoint.ConnectToAsync(
                                 "localhost",
                                 serverEndpoint.TCPPort
                             );

            Assert.That(connection, Is.Not.Null, "The SHIP connection could not be established.");

            Assert.That(await WaitFor(() => connection!.IsCompleted && serverSide is not null),
                        Is.True,
                        $"The SHIP handshake did not complete: client is in state '{connection!.State}' ({connection.Error}).");

            Assert.Multiple(() => {

                // The identities of both sides come from the TLS handshake.
                Assert.That(connection!.RemoteSKI,     Is.EqualTo(serverSKI));
                Assert.That(serverSide!.RemoteSKI,     Is.EqualTo(clientSKI));

                // The SHIP identifiers were exchanged via the access methods.
                Assert.That(connection!.RemoteSHIPId,  Is.EqualTo(serverNode.SHIPId));
                Assert.That(serverSide!.RemoteSHIPId,  Is.EqualTo(clientNode.SHIPId));

                Assert.That(connection!.Role,          Is.EqualTo(SHIPRoles.Client));
                Assert.That(serverSide!.Role,          Is.EqualTo(SHIPRoles.Server));

            });

        }

        #endregion

        #region SPINEDatagram_TravelsOverTheNetwork()

        [Test]
        public async Task SPINEDatagram_TravelsOverTheNetwork()
        {

            await serverEndpoint.StartAsync();

            JObject? received = null;
            serverNode.OnSPINEDataReceived += (node, ski, datagram) => received = datagram;

            var connection = await clientEndpoint.ConnectToAsync("localhost", serverEndpoint.TCPPort);

            Assert.That(connection, Is.Not.Null);
            Assert.That(await WaitFor(() => connection!.IsCompleted), Is.True, connection!.Error);

            var datagram = JObject.Parse(EEBusJSONTests.StandardJSON);

            await clientNode.SendSPINEDataAsync(serverSKI, datagram);

            Assert.That(await WaitFor(() => received is not null), Is.True, "The SPINE datagram did not arrive.");

            Assert.That(received!.ToString(Newtonsoft.Json.Formatting.None),
                        Is.EqualTo(datagram.ToString(Newtonsoft.Json.Formatting.None)));

        }

        #endregion

        #region UntrustedNode_IsNotAccepted()

        /// <summary>
        /// A node the server does not know - and for which nobody could ask the
        /// user - has to be rejected instead of being kept waiting.
        /// </summary>
        [Test]
        public async Task UntrustedNode_IsNotAccepted()
        {

            // A server which trusts nobody and has no pairing user interface.
            var lonelyNode      = new SHIPNode(serverSKI, SHIP_Id.Parse("evse-0002"), new InMemoryTrustStore());
            var lonelyEndpoint  = new SHIPWebSocketEndpoint(lonelyNode, serverCertificate, IPPort.Parse(0));

            try
            {

                await lonelyEndpoint.StartAsync();

                var connection = await clientEndpoint.ConnectToAsync("localhost", lonelyEndpoint.TCPPort);

                Assert.That(connection, Is.Not.Null);

                Assert.That(await WaitFor(() => connection!.State == SHIPMessageExchangeStates.SmeHelloStateRemoteAbortDone ||
                                                connection!.State == SHIPMessageExchangeStates.SmeStateError),
                            Is.True,
                            $"The connection should have been aborted, but is in state '{connection!.State}'.");

                Assert.That(connection!.IsCompleted, Is.False);

            }
            finally
            {
                await lonelyEndpoint.ShutdownAsync();
            }

        }

        #endregion

    }

}
