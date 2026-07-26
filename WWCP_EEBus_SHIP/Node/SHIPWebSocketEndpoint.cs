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

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// The network layer of a SHIP node: a WebSocket server accepting incoming
    /// connections and a WebSocket client for outgoing ones
    /// (SHIP TS 1.0.1, chapter 10).
    ///
    /// SHIP runs binary WebSocket frames over TLS with mutual authentication and
    /// the sub protocol "ship"; the SKI authenticated during the TLS handshake is
    /// the identity of the communication partner and is handed over to the node.
    /// </summary>
    public class SHIPWebSocketEndpoint
    {

        #region Data

        /// <summary>
        /// The WebSocket sub protocol of SHIP (SHIP TS 1.0.1, chapter 10.2).
        /// </summary>
        public const String  SubProtocol  = "ship";

        private readonly SHIPNode                                                     node;
        private readonly X509Certificate2                                             certificate;
        private readonly WebSocketServer                                              webSocketServer;
        private readonly ConcurrentDictionary<WebSocketServerConnection, SHIPConnection>  incomingConnections  = new ();
        private readonly ConcurrentDictionary<WebSocketClient,           SHIPConnection>  outgoingConnections  = new ();

        #endregion

        #region Properties

        /// <summary>
        /// The SHIP node this endpoint belongs to.
        /// </summary>
        public SHIPNode  Node
            => node;

        /// <summary>
        /// The TCP port the WebSocket server listens on.
        /// </summary>
        public IPPort    TCPPort
            => webSocketServer.IPPort;

        /// <summary>
        /// The path of the SHIP WebSocket endpoint, as announced via mDNS.
        /// </summary>
        public String    Path        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the network layer of the given SHIP node.
        /// </summary>
        /// <param name="Node">The SHIP node.</param>
        /// <param name="Certificate">The certificate of the SHIP node, including its private key.</param>
        /// <param name="TCPPort">The TCP port to listen on; 0 chooses a free one.</param>
        /// <param name="Path">The path of the SHIP WebSocket endpoint.</param>
        public SHIPWebSocketEndpoint(SHIPNode          Node,
                                     X509Certificate2  Certificate,
                                     IPPort?           TCPPort   = null,
                                     String?           Path      = null)
        {

            this.node         = Node;
            this.certificate  = Certificate;
            this.Path         = Path ?? SHIPServiceTXT.DefaultPath;

            this.webSocketServer = new WebSocketServer(

                                       HTTPPort:                   TCPPort ?? IPPort.Parse(SHIPServiceInstance.DefaultPort),

                                       // SHIP TS 1.0.1, chapter 10.2: the sub protocol is required.
                                       SecWebSocketProtocols:      [ SubProtocol ],

                                       // Authentication happens via TLS client certificates,
                                       // not via HTTP.
                                       RequireAuthentication:      false,

                                       ServerCertificateSelector:  (tcpServer, tcpClient) => certificate,
                                       AllowedTLSProtocols:        SHIPTLS.Protocols,

                                       // SHIP TS 1.0.1, chapter 12.1.1: a SHIP client MUST
                                       // present a certificate as well.
                                       ClientCertificateRequired:  true,
                                       CheckCertificateRevocation: false,

                                       ClientCertificateValidator: (sender, clientCertificate, chain, tcpServer, policyErrors)
                                                                       => SHIPTLS.ValidateRemoteCertificate(clientCertificate)
                                                                              ? TLSValidationResult.Success()
                                                                              : TLSValidationResult.Failed("The certificate does not identify a SHIP node!"),

                                       AutoStart:                  false

                                   );

            this.webSocketServer.OnNewWebSocketConnection  += OnNewWebSocketConnection;
            this.webSocketServer.OnBinaryMessageReceived   += OnServerBinaryMessage;
            this.webSocketServer.OnCloseMessageReceived    += OnServerCloseMessage;

        }

        #endregion


        #region StartAsync   (CancellationToken = default)

        /// <summary>
        /// Start listening for incoming SHIP connections.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task StartAsync(CancellationToken CancellationToken = default)
        {
            await webSocketServer.Start();
        }

        #endregion

        #region ShutdownAsync(Reason = null, CancellationToken = default)

        /// <summary>
        /// Stop listening and close all connections.
        /// </summary>
        /// <param name="Reason">An optional reason.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task ShutdownAsync(String?            Reason              = null,
                                        CancellationToken  CancellationToken   = default)
        {

            foreach (var connection in node.Connections.ToArray())
                await node.DisconnectAsync(connection.RemoteSKI, CancellationToken: CancellationToken);

            foreach (var webSocketClient in outgoingConnections.Keys.ToArray())
                await webSocketClient.Close();

            await webSocketServer.Shutdown(Reason);

        }

        #endregion

        #region ConnectToAsync(Hostname, TCPPort, Path = null, CancellationToken = default)

        /// <summary>
        /// Open a SHIP connection to the given communication partner.
        ///
        /// Discovery is optional: a SHIP node can always be addressed directly,
        /// which is what conformance test rigs and containers rely on.
        /// </summary>
        /// <param name="Hostname">The host name or IP address of the communication partner.</param>
        /// <param name="TCPPort">The TCP port of the communication partner.</param>
        /// <param name="Path">The path of its SHIP WebSocket endpoint.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SHIPConnection?> ConnectToAsync(String             Hostname,
                                                          IPPort             TCPPort,
                                                          String?            Path                = null,
                                                          CancellationToken  CancellationToken   = default)
        {

            var path             = Path ?? SHIPServiceTXT.DefaultPath;

            SKI? remoteSKI       = null;

            var webSocketClient  = new WebSocketClient(

                                       URL.Parse($"wss://{Hostname}:{TCPPort}{path}"),

                                       // SHIP TS 1.0.1, chapter 10.2
                                       SecWebSocketProtocols:      [ SubProtocol ],

                                       ClientCertificates:         [ certificate ],
                                       TLSProtocols:               SHIPTLS.Protocols,
                                       CipherSuitesPolicy:         SHIPTLS.SHIPCipherSuitesPolicy,
                                       CertificateRevocationCheckMode: X509RevocationMode.NoCheck,

                                       // The identity of the communication partner is the SKI of
                                       // its certificate, not a certificate chain (chapter 12.1).
                                       RemoteCertificateValidator: (sender, serverCertificate, chain, client, policyErrors) =>
                                           SHIPTLS.ValidateRemoteCertificate(
                                               serverCertificate,
                                               (ski, cert) => remoteSKI = ski
                                           )
                                               ? TLSValidationResult.Success()
                                               : TLSValidationResult.Failed("The certificate does not identify a SHIP node!")

                                   );

            webSocketClient.OnBinaryMessageReceived += OnClientBinaryMessage;

            await webSocketClient.Connect(CancellationToken: CancellationToken);

            if (!remoteSKI.HasValue)
            {
                await webSocketClient.Close();
                return null;
            }

            var shipConnection = await node.ConnectAsync(
                                     remoteSKI.Value,
                                     new WebSocketClientTransport(webSocketClient),
                                     CancellationToken
                                 );

            if (shipConnection is null)
            {
                await webSocketClient.Close();
                return null;
            }

            outgoingConnections[webSocketClient] = shipConnection;

            return shipConnection;

        }

        #endregion


        #region (private) Server event handlers

        private async Task OnNewWebSocketConnection(DateTimeOffset             Timestamp,
                                                    AWebSocketServer           Server,
                                                    WebSocketServerConnection  NewConnection,
                                                    IEnumerable<String>        SharedSubprotocols,
                                                    String?                    SelectedSubprotocol,
                                                    EventTracking_Id           EventTrackingId,
                                                    CancellationToken          CancellationToken)
        {

            if (NewConnection.ClientCertificate is null ||
                !SHIPCertificates.TryGetSKI(NewConnection.ClientCertificate, out var remoteSKI, out _))
            {
                await NewConnection.Close(WebSocketFrame.ClosingStatusCode.PolicyViolation);
                return;
            }

            var shipConnection = await node.AcceptAsync(
                                     remoteSKI,
                                     new WebSocketServerTransport(webSocketServer, NewConnection),
                                     CancellationToken
                                 );

            if (shipConnection is null)
            {
                await NewConnection.Close(WebSocketFrame.ClosingStatusCode.NormalClosure);
                return;
            }

            incomingConnections[NewConnection] = shipConnection;

        }

        private async Task OnServerBinaryMessage(DateTimeOffset             Timestamp,
                                                 AWebSocketServer           Server,
                                                 WebSocketServerConnection  Connection,
                                                 WebSocketFrame             Frame,
                                                 EventTracking_Id           EventTrackingId,
                                                 Byte[]                     BinaryMessage,
                                                 CancellationToken          CancellationToken)
        {

            if (incomingConnections.TryGetValue(Connection, out var shipConnection))
                await shipConnection.ReceiveAsync(BinaryMessage, CancellationToken);

        }

        private Task OnServerCloseMessage(DateTimeOffset             Timestamp,
                                          AWebSocketServer           Server,
                                          WebSocketServerConnection  Connection,
                                          WebSocketFrame             Frame,
                                          EventTracking_Id           EventTrackingId,
                                          WebSocketFrame.ClosingStatusCode  StatusCode,
                                          String?                    Reason,
                                          CancellationToken          CancellationToken)
        {

            incomingConnections.TryRemove(Connection, out _);

            return Task.CompletedTask;

        }

        #endregion

        #region (private) Client event handlers

        private async Task OnClientBinaryMessage(DateTimeOffset             Timestamp,
                                                 WebSocketClient            Client,
                                                 WebSocketClientConnection  Connection,
                                                 WebSocketFrame             Frame,
                                                 EventTracking_Id           EventTrackingId,
                                                 Byte[]                     BinaryMessage,
                                                 CancellationToken          CancellationToken)
        {

            if (outgoingConnections.TryGetValue(Client, out var shipConnection))
                await shipConnection.ReceiveAsync(BinaryMessage, CancellationToken);

        }

        #endregion


        #region (class) WebSocketServerTransport

        /// <summary>
        /// A SHIP transport carried by an incoming WebSocket connection.
        /// </summary>
        private class WebSocketServerTransport(WebSocketServer            Server,
                                               WebSocketServerConnection  Connection) : ISHIPTransport
        {

            public Boolean IsClosed
                => Connection.IsClosed;

            public async Task SendAsync(Byte[]             Frame,
                                        CancellationToken  CancellationToken   = default)
            {
                await Server.SendBinaryMessage(Connection, Frame, CancellationToken: CancellationToken);
            }

            public async Task CloseAsync(String?            Reason              = null,
                                         CancellationToken  CancellationToken   = default)
            {
                await Connection.Close(WebSocketFrame.ClosingStatusCode.NormalClosure, Reason);
            }

        }

        #endregion

        #region (class) WebSocketClientTransport

        /// <summary>
        /// A SHIP transport carried by an outgoing WebSocket connection.
        /// </summary>
        private class WebSocketClientTransport(WebSocketClient Client) : ISHIPTransport
        {

            public Boolean IsClosed
                => !Client.Connected;

            public async Task SendAsync(Byte[]             Frame,
                                        CancellationToken  CancellationToken   = default)
            {
                await Client.SendBinaryMessage(Frame, CancellationToken: CancellationToken);
            }

            public async Task CloseAsync(String?            Reason              = null,
                                         CancellationToken  CancellationToken   = default)
            {
                await Client.Close(WebSocketFrame.ClosingStatusCode.NormalClosure, Reason);
            }

        }

        #endregion

    }

}
