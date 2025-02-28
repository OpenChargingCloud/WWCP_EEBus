/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Reflection;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    #region Common Connection Management

    /// <summary>
    /// A delegate for logging new HTTP WebSocket connections.
    /// </summary>
    /// <param name="Timestamp">The logging timestamp.</param>
    /// <param name="NetworkingNodeChannel">The HTTP WebSocket channel.</param>
    /// <param name="NewConnection">The new HTTP WebSocket connection.</param>
    /// <param name="NetworkingNodeId">The sending EEBus networking node/charging station identification.</param>
    /// <param name="SharedSubprotocols">An enumeration of shared HTTP WebSockets subprotocols.</param>
    /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task OnNetworkingNodeNewWebSocketConnectionDelegate        (DateTime                           Timestamp,
                                                                                SHIPWebSocketServer                NetworkingNodeChannel,
                                                                                WebSocketServerConnection          NewConnection,
                                                                                NetworkingNode_Id                  NetworkingNodeId,
                                                                                IEnumerable<String>                SharedSubprotocols,
                                                                                EventTracking_Id                   EventTrackingId,
                                                                                CancellationToken                  CancellationToken);

    /// <summary>
    /// A delegate for logging a HTTP WebSocket CLOSE message.
    /// </summary>
    /// <param name="Timestamp">The logging timestamp.</param>
    /// <param name="NetworkingNodeChannel">The HTTP WebSocket channel.</param>
    /// <param name="Connection">The HTTP WebSocket connection to be closed.</param>
    /// <param name="NetworkingNodeId">The sending EEBus networking node/charging station identification.</param>
    /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
    /// <param name="StatusCode">The HTTP WebSocket Closing Status Code.</param>
    /// <param name="Reason">An optional HTTP WebSocket closing reason.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task OnNetworkingNodeCloseMessageReceivedDelegate          (DateTime                           Timestamp,
                                                                                SHIPWebSocketServer                NetworkingNodeChannel,
                                                                                WebSocketServerConnection          Connection,
                                                                                NetworkingNode_Id                  NetworkingNodeId,
                                                                                EventTracking_Id                   EventTrackingId,
                                                                                WebSocketFrame.ClosingStatusCode   StatusCode,
                                                                                String?                            Reason,
                                                                                CancellationToken                  CancellationToken);

    /// <summary>
    /// A delegate for logging a closed TCP connection.
    /// </summary>
    /// <param name="Timestamp">The logging timestamp.</param>
    /// <param name="NetworkingNodeChannel">The HTTP WebSocket channel.</param>
    /// <param name="Connection">The HTTP WebSocket connection to be closed.</param>
    /// <param name="NetworkingNodeId">The sending EEBus networking node/charging station identification.</param>
    /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
    /// <param name="Reason">An optional closing reason.</param>
    /// <param name="CancellationToken">A token to cancel the processing.</param>
    public delegate Task OnNetworkingNodeTCPConnectionClosedDelegate           (DateTime                           Timestamp,
                                                                                SHIPWebSocketServer                NetworkingNodeChannel,
                                                                                WebSocketServerConnection          Connection,
                                                                                NetworkingNode_Id                  NetworkingNodeId,
                                                                                EventTracking_Id                   EventTrackingId,
                                                                                String?                            Reason,
                                                                                CancellationToken                  CancellationToken);

    #endregion


    /// <summary>
    /// The EEBus/SHIP HTTP WebSocket server.
    /// </summary>
    public partial class SHIPWebSocketServer : WebSocketServer
    {

        #region Data

        /// <summary>
        /// The default HTTP server name.
        /// </summary>
        public  const           String                                                                               DefaultHTTPServiceName
            = $"GraphDefined EEBus/SHIP {EEBus.Version.String} Networking Node HTTP/WebSocket/JSON API";

        /// <summary>
        /// The default HTTP server TCP port.
        /// </summary>
        public static readonly  IPPort                                                                               DefaultHTTPServerPort             = IPPort.Parse(2010);

        /// <summary>
        /// The default HTTP server URI prefix.
        /// </summary>
        public static readonly  HTTPPath                                                                             DefaultURLPrefix                  = HTTPPath.Parse("/" + EEBus.Version.String);

        protected readonly      Dictionary<String, MethodInfo>                                                       incomingMessageProcessorsLookup   = [];
        protected readonly      ConcurrentDictionary<NetworkingNode_Id, Tuple<WebSocketServerConnection, DateTime>>  connectedNetworkingNodes          = [];
        protected readonly      ConcurrentDictionary<NetworkingNode_Id, NetworkingNode_Id>                           reachableViaNetworkingHubs        = [];
        protected readonly      ConcurrentDictionary<Request_Id, SendRequestState>                                   requests                          = [];

        public const            String                                                                               LogfileName                       = "CSMSWSServer.log";

        #endregion

        #region Properties

        /// <summary>
        /// The parent EEBus adapter.
        /// </summary>
        public IEEBusAdapter                                      EEBusAdapter { get; }

        /// <summary>
        /// The enumeration of all connected networking nodes.
        /// </summary>
        public IEnumerable<NetworkingNode_Id>                    NetworkingNodeIds
            => connectedNetworkingNodes.Keys;

        /// <summary>
        /// Require a HTTP Basic Authentication of all networking nodes.
        /// </summary>
        public Boolean                                           RequireAuthentication    { get; }

        /// <summary>
        /// Logins and passwords for HTTP Basic Authentication.
        /// </summary>
        public ConcurrentDictionary<NetworkingNode_Id, String?>  NetworkingNodeLogins     { get; }
            = new();

        /// <summary>
        /// The JSON formatting to use.
        /// </summary>
        public Formatting                                        JSONFormatting           { get; set; }
            = Formatting.None;

        /// <summary>
        /// The request timeout for messages sent by this HTTP WebSocket server.
        /// </summary>
        public TimeSpan?                                         RequestTimeout           { get; set; }

        #endregion

        #region Events

        #region Common Connection Management

        /// <summary>
        /// An event sent whenever the HTTP connection switched successfully to web socket.
        /// </summary>
        public event OnNetworkingNodeNewWebSocketConnectionDelegate?  OnNetworkingNodeNewWebSocketConnection;

        /// <summary>
        /// An event sent whenever a web socket close frame was received.
        /// </summary>
        public event OnNetworkingNodeCloseMessageReceivedDelegate?    OnNetworkingNodeCloseMessageReceived;

        /// <summary>
        /// An event sent whenever a TCP connection was closed.
        /// </summary>
        public event OnNetworkingNodeTCPConnectionClosedDelegate?     OnNetworkingNodeTCPConnectionClosed;

        #endregion

        #region Generic JSON Messages

        /// <summary>
        /// An event sent whenever a text message request was received.
        /// </summary>
        public event OnWebSocketJSONMessageRequestDelegate?   OnJSONMessageRequestReceived;

        /// <summary>
        /// An event sent whenever the response to a text message was sent.
        /// </summary>
        public event OnWebSocketJSONMessageResponseDelegate?  OnJSONMessageResponseSent;

        /// <summary>
        /// An event sent whenever the error response to a text message was sent.
        /// </summary>
        public event OnWebSocketTextErrorResponseDelegate?    OnJSONErrorResponseSent;


        /// <summary>
        /// An event sent whenever a text message request was sent.
        /// </summary>
        public event OnWebSocketJSONMessageRequestDelegate?   OnJSONMessageRequestSent;

        /// <summary>
        /// An event sent whenever the response to a text message request was received.
        /// </summary>
        public event OnWebSocketJSONMessageResponseDelegate?  OnJSONMessageResponseReceived;

        /// <summary>
        /// An event sent whenever an error response to a text message request was received.
        /// </summary>
        public event OnWebSocketTextErrorResponseDelegate?    OnJSONErrorResponseReceived;

        #endregion

        #region Generic Binary Messages

        /// <summary>
        /// An event sent whenever a binary message request was received.
        /// </summary>
        public event OnWebSocketBinaryMessageRequestDelegate?   OnBinaryMessageRequestReceived;

        /// <summary>
        /// An event sent whenever the response to a binary message was sent.
        /// </summary>
        public event OnWebSocketBinaryMessageResponseDelegate?  OnBinaryMessageResponseSent;

        /// <summary>
        /// An event sent whenever the error response to a binary message was sent.
        /// </summary>
        //public event OnWebSocketBinaryErrorResponseDelegate?      OnBinaryErrorResponseSent;


        /// <summary>
        /// An event sent whenever a binary message request was sent.
        /// </summary>
        public event OnWebSocketBinaryMessageRequestDelegate?   OnBinaryMessageRequestSent;

        /// <summary>
        /// An event sent whenever the response to a binary message request was received.
        /// </summary>
        public event OnWebSocketBinaryMessageResponseDelegate?  OnBinaryMessageResponseReceived;

        /// <summary>
        /// An event sent whenever the error response to a binary message request was sent.
        /// </summary>
        //public event OnWebSocketBinaryErrorResponseDelegate?      OnBinaryErrorResponseReceived;

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EEBus/SHIP HTTP WebSocket server.
        /// </summary>
        /// <param name="EEBusAdapter">The parent EEBus adapter.</param>
        /// 
        /// <param name="HTTPServiceName">An optional identification string for the HTTP service.</param>
        /// <param name="IPAddress">An IP address to listen on.</param>
        /// <param name="TCPPort">An optional TCP port for the HTTP server.</param>
        /// <param name="Description">An optional description of this HTTP WebSocket service.</param>
        /// 
        /// <param name="RequireAuthentication">Require a HTTP Basic Authentication of all charging boxes.</param>
        /// 
        /// <param name="DNSClient">An optional DNS client to use.</param>
        /// <param name="AutoStart">Start the server immediately.</param>
        public SHIPWebSocketServer(IEEBusAdapter                                                    EEBusAdapter,

                                   String?                                                         HTTPServiceName              = DefaultHTTPServiceName,
                                   IIPAddress?                                                     IPAddress                    = null,
                                   IPPort?                                                         TCPPort                      = null,
                                   I18NString?                                                     Description                  = null,

                                   Boolean                                                         RequireAuthentication        = true,
                                   Boolean                                                         DisableWebSocketPings        = false,
                                   TimeSpan?                                                       WebSocketPingEvery           = null,
                                   TimeSpan?                                                       SlowNetworkSimulationDelay   = null,

                                   Func<X509Certificate2>?                                         ServerCertificateSelector    = null,
                                   RemoteTLSClientCertificateValidationHandler<org.GraphDefined.Vanaheimr.Hermod.WebSocket.IWebSocketServer>?  ClientCertificateValidator   = null,
                                   LocalCertificateSelectionHandler?                               LocalCertificateSelector     = null,
                                   SslProtocols?                                                   AllowedTLSProtocols          = null,
                                   Boolean?                                                        ClientCertificateRequired    = null,
                                   Boolean?                                                        CheckCertificateRevocation   = null,

                                   ServerThreadNameCreatorDelegate?                                ServerThreadNameCreator      = null,
                                   ServerThreadPriorityDelegate?                                   ServerThreadPrioritySetter   = null,
                                   Boolean?                                                        ServerThreadIsBackground     = null,
                                   ConnectionIdBuilder?                                            ConnectionIdBuilder          = null,
                                   TimeSpan?                                                       ConnectionTimeout            = null,
                                   UInt32?                                                         MaxClientConnections         = null,

                                   DNSClient?                                                      DNSClient                    = null,
                                   Boolean                                                         AutoStart                    = false)

            : base(IPAddress,
                   TCPPort         ?? IPPort.Parse(8000),
                   HTTPServiceName ?? DefaultHTTPServiceName,
                   Description,

                   RequireAuthentication,
                   [ "ship" ],
                   null,
                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

                   ServerCertificateSelector,
                   ClientCertificateValidator,
                   LocalCertificateSelector,
                   AllowedTLSProtocols,
                   ClientCertificateRequired,
                   CheckCertificateRevocation,

                   ServerThreadNameCreator,
                   ServerThreadPrioritySetter,
                   ServerThreadIsBackground,
                   ConnectionIdBuilder,
                   ConnectionTimeout,
                   MaxClientConnections,

                   DNSClient,
                   false)

        {

            this.EEBusAdapter = EEBusAdapter;

            this.RequireAuthentication = RequireAuthentication;

            //this.Logger          = new ChargePointwebsocketClient.CPClientLogger(this,
            //                                                                LoggingPath,
            //                                                                LoggingContext,
            //                                                                LogfileCreator);

            base.OnValidateTCPConnection        += ValidateTCPConnection;
            base.OnValidateWebSocketConnection  += ValidateWebSocketConnection;
            base.OnNewWebSocketConnection       += ProcessNewWebSocketConnection;
            base.OnCloseMessageReceived         += ProcessCloseMessage;

            if (AutoStart)
                Start();

        }

        #endregion


        #region AddOrUpdateHTTPBasicAuth(NetworkingNodeId, Password)

        /// <summary>
        /// Add the given HTTP Basic Authentication password for the given networking node.
        /// </summary>
        /// <param name="NetworkingNodeId">The unique identification of the networking node.</param>
        /// <param name="Password">The password of the charging station.</param>
        public void AddOrUpdateHTTPBasicAuth(NetworkingNode_Id  NetworkingNodeId,
                                             String             Password)
        {

            NetworkingNodeLogins.AddOrUpdate(
                                     NetworkingNodeId,
                                     Password,
                                     (chargingStationId, password) => Password
                                 );

        }

        #endregion

        #region RemoveHTTPBasicAuth     (NetworkingNodeId)

        /// <summary>
        /// Remove the given HTTP Basic Authentication for the given networking node.
        /// </summary>
        /// <param name="NetworkingNodeId">The unique identification of the networking node.</param>
        public Boolean RemoveHTTPBasicAuth(NetworkingNode_Id NetworkingNodeId)
        {

            if (NetworkingNodeLogins.ContainsKey(NetworkingNodeId))
                return NetworkingNodeLogins.TryRemove(NetworkingNodeId, out _);

            return true;

        }

        #endregion


        // Connection management...

        #region (protected) ValidateTCPConnection         (LogTimestamp, Server, Connection, EventTrackingId, CancellationToken)

        private Task<ConnectionFilterResponse> ValidateTCPConnection(DateTime                      LogTimestamp,
                                                                     org.GraphDefined.Vanaheimr.Hermod.WebSocket.IWebSocketServer              Server,
                                                                     System.Net.Sockets.TcpClient  Connection,
                                                                     EventTracking_Id              EventTrackingId,
                                                                     CancellationToken             CancellationToken)
        {

            return Task.FromResult(ConnectionFilterResponse.Accepted());

        }

        #endregion

        #region (protected) ValidateWebSocketConnection   (LogTimestamp, Server, Connection, EventTrackingId, CancellationToken)

        private Task<HTTPResponse?> ValidateWebSocketConnection(DateTime                   LogTimestamp,
                                                                org.GraphDefined.Vanaheimr.Hermod.WebSocket.IWebSocketServer           Server,
                                                                WebSocketServerConnection  Connection,
                                                                EventTracking_Id           EventTrackingId,
                                                                CancellationToken          CancellationToken)
        {

            #region Verify 'Sec-WebSocket-Protocol'...

            if (Connection.HTTPRequest?.SecWebSocketProtocol is null ||
                Connection.HTTPRequest?.SecWebSocketProtocol.Any() == false)
            {

                DebugX.Log($"{nameof(AOverlayWebSocketServer)} connection from {Connection.RemoteSocket}: Missing 'Sec-WebSocket-Protocol' HTTP header!");

                return Task.FromResult<HTTPResponse?>(
                           new HTTPResponse.Builder() {
                               HTTPStatusCode  = HTTPStatusCode.BadRequest,
                               Server          = HTTPServiceName,
                               Date            = Timestamp.Now,
                               ContentType     = HTTPContentType.Application.JSON_UTF8,
                               Content         = JSONObject.Create(
                                                     new JProperty("description",
                                                     JSONObject.Create(
                                                         new JProperty("en", "Missing 'Sec-WebSocket-Protocol' HTTP header!")
                                                     ))).ToUTF8Bytes(),
                               Connection      = ConnectionType.Close
                           }.AsImmutable);

            }
            else if (!new HashSet<String>(SecWebSocketProtocols).Overlaps(Connection.HTTPRequest?.SecWebSocketProtocol ?? []))
            {

                var error = $"This WebSocket service only supports {SecWebSocketProtocols.Select(id => $"'{id}'").AggregateWith(", ")}!";

                DebugX.Log($"{nameof(AOverlayWebSocketServer)} connection from {Connection.RemoteSocket}: {error}");

                return Task.FromResult<HTTPResponse?>(
                           new HTTPResponse.Builder() {
                               HTTPStatusCode  = HTTPStatusCode.BadRequest,
                               Server          = HTTPServiceName,
                               Date            = Timestamp.Now,
                               ContentType     = HTTPContentType.Application.JSON_UTF8,
                               Content         = JSONObject.Create(
                                                     new JProperty("description",
                                                         JSONObject.Create(
                                                             new JProperty("en", error)
                                                     ))).ToUTF8Bytes(),
                               Connection      = ConnectionType.Close
                           }.AsImmutable);

            }

            #endregion

            #region Verify HTTP Authentication

            if (RequireAuthentication)
            {

                if (Connection.HTTPRequest?.Authorization is HTTPBasicAuthentication basicAuthentication)
                {

                    if (NetworkingNodeLogins.TryGetValue(NetworkingNode_Id.Parse(basicAuthentication.Username), out var password) &&
                        basicAuthentication.Password == password)
                    {
                        DebugX.Log($"{nameof(AOverlayWebSocketServer)} connection from {Connection.RemoteSocket} using authorization: '{basicAuthentication.Username}' / '{basicAuthentication.Password}'");
                        return Task.FromResult<HTTPResponse?>(null);
                    }
                    else
                        DebugX.Log($"{nameof(AOverlayWebSocketServer)} connection from {Connection.RemoteSocket} invalid authorization: '{basicAuthentication.Username}' / '{basicAuthentication.Password}'!");

                }
                else
                    DebugX.Log($"{nameof(AOverlayWebSocketServer)} connection from {Connection.RemoteSocket} missing authorization!");

                return Task.FromResult<HTTPResponse?>(
                           new HTTPResponse.Builder() {
                               HTTPStatusCode  = HTTPStatusCode.Unauthorized,
                               Server          = HTTPServiceName,
                               Date            = Timestamp.Now,
                               Connection      = ConnectionType.Close
                           }.AsImmutable
                       );

            }

            #endregion

            return Task.FromResult<HTTPResponse?>(null);

        }

        #endregion

        #region (protected) ProcessNewWebSocketConnection (LogTimestamp, Server, Connection, SharedSubprotocols, EventTrackingId, CancellationToken)

        protected async Task ProcessNewWebSocketConnection(//DateTime                   LogTimestamp,
                                                           //org.GraphDefined.Vanaheimr.Hermod.WebSocket.IWebSocketServer           Server,
                                                           //WebSocketServerConnection  Connection,
                                                           //IEnumerable<String>        SharedSubprotocols,
                                                           //EventTracking_Id           EventTrackingId,
                                                           //CancellationToken          CancellationToken)
                                                           DateTime                           LogTimestamp,
                                                           org.GraphDefined.Vanaheimr.Hermod.WebSocket.IWebSocketServer  Server,
                                                           WebSocketServerConnection          Connection,
                                                           IEnumerable<String>                SharedSubprotocols,
                                                           String?                            SelectedSubprotocol,
                                                           EventTracking_Id                   EventTrackingId,
                                                           CancellationToken                  CancellationToken)


        {

            if (Connection.HTTPRequest is null)
                return;

            NetworkingNode_Id? networkingNodeId = null;

            #region Parse TLS Client Certificate CommonName, or...

            // We already validated and therefore trust this certificate!
            if (Connection.HTTPRequest.ClientCertificate is not null)
            {

                var x509CommonName = Connection.HTTPRequest.ClientCertificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);

                if (NetworkingNode_Id.TryParse(x509CommonName, out var networkingNodeId1))
                {
                    networkingNodeId = networkingNodeId1;
                }

            }

            #endregion

            #region ...check HTTP Basic Authentication, or...

            else if (Connection.HTTPRequest.Authorization is HTTPBasicAuthentication httpBasicAuthentication &&
                     NetworkingNode_Id.TryParse(httpBasicAuthentication.Username, out var networkingNodeId2))
            {
                networkingNodeId = networkingNodeId2;
            }

            #endregion


            //ToDo: This might be a DOS attack vector!

            #region ...try to get the NetworkingNodeId from the HTTP request path suffix

            else
            {

                var path = Connection.HTTPRequest.Path.ToString();

                if (NetworkingNode_Id.TryParse(path[(path.LastIndexOf('/') + 1)..],
                    out var networkingNodeId3))
                {
                    networkingNodeId = networkingNodeId3;
                }

            }

            #endregion


            if (networkingNodeId.HasValue)
            {

                #region Store the NetworkingNodeId within the HTTP WebSocket connection

                Connection.TryAddCustomData(
                                EEBus.EEBusAdapter.NetworkingNodeId_WebSocketKey,
                                networkingNodeId.Value
                            );

                #endregion

                #region Register new Networking Node

                if (!connectedNetworkingNodes.TryAdd(networkingNodeId.Value,
                                                     new Tuple<WebSocketServerConnection, DateTime>(
                                                         Connection,
                                                         Timestamp.Now
                                                     )))
                {

                    DebugX.Log($"{nameof(SHIPWebSocketServer)} Duplicate networking node '{networkingNodeId.Value}' detected: Trying to close old one!");

                    if (connectedNetworkingNodes.TryRemove(networkingNodeId.Value, out var oldConnection))
                    {
                        try
                        {
                            await oldConnection.Item1.Close(
                                      WebSocketFrame.ClosingStatusCode.NormalClosure,
                                      "Newer connection detected!",
                                      CancellationToken
                                  );
                        }
                        catch (Exception e)
                        {
                            DebugX.Log($"{nameof(SHIPWebSocketServer)} Closing old HTTP WebSocket connection from {oldConnection.Item1.RemoteSocket} failed: {e.Message}");
                        }
                    }

                    connectedNetworkingNodes.TryAdd(networkingNodeId.Value,
                                                    new Tuple<WebSocketServerConnection, DateTime>(
                                                        Connection,
                                                        Timestamp.Now
                                                    ));

                }

                #endregion


                #region Send OnNewNetworkingNodeWSConnection event

                var onNetworkingNodeNewWebSocketConnection = OnNetworkingNodeNewWebSocketConnection;
                if (onNetworkingNodeNewWebSocketConnection is not null)
                {
                    try
                    {

                        await Task.WhenAll(onNetworkingNodeNewWebSocketConnection.GetInvocationList().
                                               OfType<OnNetworkingNodeNewWebSocketConnectionDelegate>().
                                               Select(loggingDelegate => loggingDelegate.Invoke(
                                                                             LogTimestamp,
                                                                             this,
                                                                             Connection,
                                                                             networkingNodeId.Value,
                                                                             SharedSubprotocols,
                                                                             EventTrackingId,
                                                                             CancellationToken
                                                                         )).
                                               ToArray());

                    }
                    catch (Exception e)
                    {
                        await HandleErrors(
                                  nameof(SHIPWebSocketServer),
                                  nameof(OnNetworkingNodeNewWebSocketConnection),
                                  e
                              );
                    }

                }

                #endregion

            }

            #region Close connection

            else
            {

                DebugX.Log($"{nameof(SHIPWebSocketServer)} Could not get NetworkingNodeId from HTTP WebSocket connection ({Connection.RemoteSocket}): Closing connection!");

                try
                {
                    await Connection.Close(
                              WebSocketFrame.ClosingStatusCode.PolicyViolation,
                              "Could not get NetworkingNodeId from HTTP WebSocket connection!",
                              CancellationToken
                          );
                }
                catch (Exception e)
                {
                    DebugX.Log($"{nameof(SHIPWebSocketServer)} Closing HTTP WebSocket connection ({Connection.RemoteSocket}) failed: {e.Message}");
                }

            }

            #endregion

        }

        #endregion

        #region (protected) ProcessCloseMessage           (LogTimestamp, Server, Connection, Frame, EventTrackingId, StatusCode, Reason, CancellationToken)

        protected async Task ProcessCloseMessage(DateTime                          LogTimestamp,
                                                 org.GraphDefined.Vanaheimr.Hermod.WebSocket.IWebSocketServer                  Server,
                                                 WebSocketServerConnection         Connection,
                                                 WebSocketFrame                    Frame,
                                                 EventTracking_Id                  EventTrackingId,
                                                 WebSocketFrame.ClosingStatusCode  StatusCode,
                                                 String?                           Reason,
                                                 CancellationToken                 CancellationToken)
        {

            if (Connection.TryGetCustomDataAs<NetworkingNode_Id>(EEBus.EEBusAdapter.NetworkingNodeId_WebSocketKey, out var networkingNodeId))
            {

                connectedNetworkingNodes.TryRemove(networkingNodeId, out _);

                #region Send OnNetworkingNodeCloseMessageReceived event

                var logger = OnNetworkingNodeCloseMessageReceived;
                if (logger is not null)
                {

                    try
                    {
                        await Task.WhenAll(logger.GetInvocationList().
                                                  OfType<OnNetworkingNodeCloseMessageReceivedDelegate>().
                                                  Select(loggingDelegate => loggingDelegate.Invoke(LogTimestamp,
                                                                                                   this,
                                                                                                   Connection,
                                                                                                   networkingNodeId,
                                                                                                   EventTrackingId,
                                                                                                   StatusCode,
                                                                                                   Reason,
                                                                                                   CancellationToken)).
                                                  ToArray());

                    }
                    catch (Exception e)
                    {
                        await HandleErrors(
                                  nameof(SHIPWebSocketServer),
                                  nameof(OnNetworkingNodeCloseMessageReceived),
                                  e
                              );
                    }

                }

                #endregion

            }

        }

        #endregion


        // Receive data...

        #region (protected) ProcessTextMessage   (RequestTimestamp, ServerConnection, TextMessage,   EventTrackingId, CancellationToken)

        /// <summary>
        /// Process all text messages of this WebSocket API.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request.</param>
        /// <param name="ServerConnection">The WebSocket connection.</param>
        /// <param name="TextMessage">The received text message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="CancellationToken">The cancellation token.</param>
        public override async Task<WebSocketTextMessageResponse> ProcessTextMessage(DateTime                   RequestTimestamp,
                                                                                    WebSocketServerConnection  ServerConnection,
                                                                                    String                     TextMessage,
                                                                                    EventTracking_Id           EventTrackingId,
                                                                                    CancellationToken          CancellationToken)
        {

            // A SHIP node that receives a data frame with another type (0x1)
            // MUST terminate the connection with status code 1003 (unacceptable data).
            await ServerConnection.Close(
                      WebSocketFrame.ClosingStatusCode.UnsupportedData,
                      "HTTP WebSocket Text frames are not allowed by EEBus SHIP!",
                      CancellationToken
                  );

            return new WebSocketTextMessageResponse(
                       RequestTimestamp,
                       TextMessage,
                       Timestamp.Now,
                       "",
                       EventTrackingId,
                       CancellationToken
                   );

        }

        #endregion

        #region (protected) ProcessBinaryMessage (RequestTimestamp, ServerConnection, BinaryMessage, EventTrackingId, CancellationToken)

        /// <summary>
        /// Process all text messages of this WebSocket API.
        /// </summary>
        /// <param name="RequestTimestamp">The timestamp of the request.</param>
        /// <param name="Connection">The WebSocket connection.</param>
        /// <param name="BinaryMessage">The received binary message.</param>
        /// <param name="EventTrackingId">An optional event tracking identification.</param>
        /// <param name="CancellationToken">The cancellation token.</param>
        public override async Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime                   RequestTimestamp,
                                                                                        WebSocketServerConnection  ServerConnection,
                                                                                        Byte[]                     BinaryMessage,
                                                                                        EventTracking_Id           EventTrackingId,
                                                                                        CancellationToken          CancellationToken)
        {

           // BinaryResponseMessage?    EEBusResponse       = null;
           // JSONRequestErrorMessage?  EEBusErrorResponse  = null;

            try
            {

                var sourceNodeId = ServerConnection.TryGetCustomDataAs<NetworkingNode_Id>(EEBus.EEBusAdapter.NetworkingNodeId_WebSocketKey);

                var binaryMessageResponse = await EEBusAdapter.IN.ProcessBinaryMessage(
                                                      RequestTimestamp,
                                                      ServerConnection,
                                                      BinaryMessage,
                                                      EventTrackingId,
                                                      CancellationToken
                                                  );

                return binaryMessageResponse;

            }
            catch (Exception e)
            {

                var EEBusErrorResponse = JSONRequestErrorMessage.InternalError(
                                             nameof(SHIPWebSocketServer),
                                             EventTrackingId,
                                             BinaryMessage,
                                             e
                                         );

            }

            return null;

        }

        #endregion


        // Send data...

        #region SendJSONRequest       (JSONRequestMessage)

        /// <summary>
        /// Send (and forget) the given JSON EEBus request message.
        /// </summary>
        /// <param name="JSONRequestMessage">A JSON EEBus request message.</param>
        public async Task<SendWebSocketMessageResult> SendJSONRequest(JSONRequestMessage JSONRequestMessage)
        {

            try
            {

                var webSocketConnections = LookupNetworkingNode(JSONRequestMessage.DestinationId).ToArray();

                if (webSocketConnections.Length != 0)
                {

                    var networkingMode = webSocketConnections.First().Item1.TryGetCustomDataAs<NetworkingMode>(EEBus.EEBusAdapter.NetworkingMode_WebSocketKey);

                    JSONRequestMessage.NetworkingMode = webSocketConnections.First().Item2;
                    //RequestMessage.RequestTimeout ??= RequestMessage.RequestTimestamp + (RequestTimeout ?? DefaultRequestTimeout);

                    var ocppTextMessage = JSONRequestMessage.ToJSON().ToString(Formatting.None);


                    foreach (var webSocketConnection in webSocketConnections)
                    {

                        if (SentStatus.Success == await SendTextMessage(
                                                            webSocketConnection.Item1,
                                                            ocppTextMessage,
                                                            JSONRequestMessage.EventTrackingId,
                                                            JSONRequestMessage.CancellationToken
                                                        ))
                        {

                            //requests.TryAdd(RequestMessage.RequestId,
                            //                SendRequestState.FromJSONRequest(
                            //                    Timestamp.Now,
                            //                    RequestMessage.DestinationNodeId,
                            //                    RequestMessage.RequestTimeout ?? (RequestMessage.RequestTimestamp + (RequestTimeout ?? DefaultRequestTimeout)),
                            //                    RequestMessage
                            //                ));

                            #region OnJSONMessageRequestSent

                            //var onJSONMessageRequestSent = OnJSONMessageRequestSent;
                            //if (onJSONMessageRequestSent is not null)
                            //{
                            //    try
                            //    {

                            //        await Task.WhenAll(onJSONMessageRequestSent.GetInvocationList().
                            //                               OfType<OnWebSocketTextMessageDelegate>().
                            //                               Select(loggingDelegate => loggingDelegate.Invoke(
                            //                                                              Timestamp.Now,
                            //                                                              this,
                            //                                                              webSocketConnection.Item1,
                            //                                                              EventTrackingId,
                            //                                                              ocppTextMessage,
                            //                                                              CancellationToken
                            //                                                          )).
                            //                               ToArray());

                            //    }
                            //    catch (Exception e)
                            //    {
                            //        DebugX.Log(e, nameof(AEEBusWebSocketServer) + "." + nameof(OnJSONMessageRequestSent));
                            //    }
                            //}

                            #endregion

                            break;

                        }

                        RemoveConnection(webSocketConnection.Item1);

                    }

                    return SendWebSocketMessageResult.Success;

                }
                else
                    return SendWebSocketMessageResult.UnknownClient;

            }
            catch (Exception)
            {
                return SendWebSocketMessageResult.TransmissionFailed;
            }

        }

        #endregion

        #region SendJSONResponse      (JSONResponseMessage)

        /// <summary>
        /// Send (and forget) the given JSON EEBus response message.
        /// </summary>
        /// <param name="JSONResponseMessage">A JSON EEBus response message.</param>
        public async Task<SendWebSocketMessageResult> SendJSONResponse(JSONResponseMessage JSONResponseMessage)
        {

            try
            {

                var webSocketConnections = LookupNetworkingNode(JSONResponseMessage.DestinationId).ToArray();

                if (webSocketConnections.Length != 0)
                {

                    var networkingMode = webSocketConnections.First().Item1.TryGetCustomDataAs<NetworkingMode>(EEBus.EEBusAdapter.NetworkingMode_WebSocketKey);

                    JSONResponseMessage.NetworkingMode = webSocketConnections.First().Item2;
                    //ResponseMessage.ResponseTimeout ??= ResponseMessage.ResponseTimestamp + (ResponseTimeout ?? DefaultResponseTimeout);

                    var ocppTextMessage = JSONResponseMessage.ToJSON().ToString(Formatting.None);


                    foreach (var webSocketConnection in webSocketConnections)
                    {

                        if (SentStatus.Success == await SendTextMessage(
                                                            webSocketConnection.Item1,
                                                            ocppTextMessage,
                                                            JSONResponseMessage.EventTrackingId,
                                                            JSONResponseMessage.CancellationToken
                                                        ))
                        {

                            //responses.TryAdd(ResponseMessage.ResponseId,
                            //                SendResponseState.FromJSONResponse(
                            //                    Timestamp.Now,
                            //                    ResponseMessage.DestinationNodeId,
                            //                    ResponseMessage.ResponseTimeout ?? (ResponseMessage.ResponseTimestamp + (ResponseTimeout ?? DefaultResponseTimeout)),
                            //                    ResponseMessage
                            //                ));

                            #region OnJSONMessageResponseSent

                            //var onJSONMessageResponseSent = OnJSONMessageResponseSent;
                            //if (onJSONMessageResponseSent is not null)
                            //{
                            //    try
                            //    {

                            //        await Task.WhenAll(onJSONMessageResponseSent.GetInvocationList().
                            //                               OfType<OnWebSocketTextMessageDelegate>().
                            //                               Select(loggingDelegate => loggingDelegate.Invoke(
                            //                                                              Timestamp.Now,
                            //                                                              this,
                            //                                                              webSocketConnection.Item1,
                            //                                                              EventTrackingId,
                            //                                                              ocppTextMessage,
                            //                                                              CancellationToken
                            //                                                          )).
                            //                               ToArray());

                            //    }
                            //    catch (Exception e)
                            //    {
                            //        DebugX.Log(e, nameof(AEEBusWebSocketServer) + "." + nameof(OnJSONMessageResponseSent));
                            //    }
                            //}

                            #endregion

                            break;

                        }

                        RemoveConnection(webSocketConnection.Item1);

                    }

                    return SendWebSocketMessageResult.Success;

                }
                else
                    return SendWebSocketMessageResult.UnknownClient;

            }
            catch (Exception)
            {
                return SendWebSocketMessageResult.TransmissionFailed;
            }

        }

        #endregion

        #region SendJSONRequestError  (JSONRequestErrorMessage)

        /// <summary>
        /// Send (and forget) the given JSON EEBus error message.
        /// </summary>
        /// <param name="JSONRequestErrorMessage">A JSON EEBus error message.</param>
        public async Task<SendWebSocketMessageResult> SendJSONRequestError(JSONRequestErrorMessage JSONRequestErrorMessage)
        {

            try
            {

                var webSocketConnections = LookupNetworkingNode(JSONRequestErrorMessage.DestinationNodeId).ToArray();

                if (webSocketConnections.Length != 0)
                {

                    var networkingMode = webSocketConnections.First().Item1.TryGetCustomDataAs<NetworkingMode>(EEBus.EEBusAdapter.NetworkingMode_WebSocketKey);

                    JSONRequestErrorMessage.NetworkingMode = webSocketConnections.First().Item2;
                    //ResponseMessage.ResponseTimeout ??= ResponseMessage.ResponseTimestamp + (ResponseTimeout ?? DefaultResponseTimeout);

                    var ocppTextMessage = JSONRequestErrorMessage.ToJSON().ToString(Formatting.None);


                    foreach (var webSocketConnection in webSocketConnections)
                    {

                        if (SentStatus.Success == await SendTextMessage(
                                                            webSocketConnection.Item1,
                                                            ocppTextMessage,
                                                            JSONRequestErrorMessage.EventTrackingId,
                                                            JSONRequestErrorMessage.CancellationToken
                                                        ))
                        {

                            //responses.TryAdd(ResponseMessage.ResponseId,
                            //                SendResponseState.FromJSONResponse(
                            //                    Timestamp.Now,
                            //                    ResponseMessage.DestinationNodeId,
                            //                    ResponseMessage.ResponseTimeout ?? (ResponseMessage.ResponseTimestamp + (ResponseTimeout ?? DefaultResponseTimeout)),
                            //                    ResponseMessage
                            //                ));

                            #region OnJSONMessageResponseSent

                            //var onJSONMessageResponseSent = OnJSONMessageResponseSent;
                            //if (onJSONMessageResponseSent is not null)
                            //{
                            //    try
                            //    {

                            //        await Task.WhenAll(onJSONMessageResponseSent.GetInvocationList().
                            //                               OfType<OnWebSocketTextMessageDelegate>().
                            //                               Select(loggingDelegate => loggingDelegate.Invoke(
                            //                                                              Timestamp.Now,
                            //                                                              this,
                            //                                                              webSocketConnection.Item1,
                            //                                                              EventTrackingId,
                            //                                                              ocppTextMessage,
                            //                                                              CancellationToken
                            //                                                          )).
                            //                               ToArray());

                            //    }
                            //    catch (Exception e)
                            //    {
                            //        DebugX.Log(e, nameof(AEEBusWebSocketServer) + "." + nameof(OnJSONMessageResponseSent));
                            //    }
                            //}

                            #endregion

                            break;

                        }

                        RemoveConnection(webSocketConnection.Item1);

                    }

                    return SendWebSocketMessageResult.Success;

                }
                else
                    return SendWebSocketMessageResult.UnknownClient;

            }
            catch (Exception)
            {
                return SendWebSocketMessageResult.TransmissionFailed;
            }

        }

        #endregion

        #region SendJSONResponseError (JSONResponseErrorMessage)

        /// <summary>
        /// Send (and forget) the given JSON EEBus error message.
        /// </summary>
        /// <param name="JSONResponseErrorMessage">A JSON EEBus error message.</param>
        public async Task<SendWebSocketMessageResult> SendJSONResponseError(JSONResponseErrorMessage JSONResponseErrorMessage)
        {

            try
            {

                var webSocketConnections = LookupNetworkingNode(JSONResponseErrorMessage.DestinationNodeId).ToArray();

                if (webSocketConnections.Length != 0)
                {

                    var networkingMode = webSocketConnections.First().Item1.TryGetCustomDataAs<NetworkingMode>(EEBus.EEBusAdapter.NetworkingMode_WebSocketKey);

                    JSONResponseErrorMessage.NetworkingMode = webSocketConnections.First().Item2;
                    //ResponseMessage.ResponseTimeout ??= ResponseMessage.ResponseTimestamp + (ResponseTimeout ?? DefaultResponseTimeout);

                    var ocppTextMessage = JSONResponseErrorMessage.ToJSON().ToString(Formatting.None);


                    foreach (var webSocketConnection in webSocketConnections)
                    {

                        if (SentStatus.Success == await SendTextMessage(
                                                            webSocketConnection.Item1,
                                                            ocppTextMessage,
                                                            JSONResponseErrorMessage.EventTrackingId,
                                                            JSONResponseErrorMessage.CancellationToken
                                                        ))
                        {

                            //responses.TryAdd(ResponseMessage.ResponseId,
                            //                SendResponseState.FromJSONResponse(
                            //                    Timestamp.Now,
                            //                    ResponseMessage.DestinationNodeId,
                            //                    ResponseMessage.ResponseTimeout ?? (ResponseMessage.ResponseTimestamp + (ResponseTimeout ?? DefaultResponseTimeout)),
                            //                    ResponseMessage
                            //                ));

                            #region OnJSONMessageResponseSent

                            //var onJSONMessageResponseSent = OnJSONMessageResponseSent;
                            //if (onJSONMessageResponseSent is not null)
                            //{
                            //    try
                            //    {

                            //        await Task.WhenAll(onJSONMessageResponseSent.GetInvocationList().
                            //                               OfType<OnWebSocketTextMessageDelegate>().
                            //                               Select(loggingDelegate => loggingDelegate.Invoke(
                            //                                                              Timestamp.Now,
                            //                                                              this,
                            //                                                              webSocketConnection.Item1,
                            //                                                              EventTrackingId,
                            //                                                              ocppTextMessage,
                            //                                                              CancellationToken
                            //                                                          )).
                            //                               ToArray());

                            //    }
                            //    catch (Exception e)
                            //    {
                            //        DebugX.Log(e, nameof(AEEBusWebSocketServer) + "." + nameof(OnJSONMessageResponseSent));
                            //    }
                            //}

                            #endregion

                            break;

                        }

                        RemoveConnection(webSocketConnection.Item1);

                    }

                    return SendWebSocketMessageResult.Success;

                }
                else
                    return SendWebSocketMessageResult.UnknownClient;

            }
            catch (Exception)
            {
                return SendWebSocketMessageResult.TransmissionFailed;
            }

        }

        #endregion


        #region SendBinaryRequest     (BinaryRequestMessage)

        /// <summary>
        /// Send (and forget) the given binary EEBus request message.
        /// </summary>
        /// <param name="BinaryRequestMessage">A binary EEBus request message.</param>
        public async Task<SendWebSocketMessageResult> SendBinaryRequest(BinaryRequestMessage BinaryRequestMessage)
        {

            try
            {

                var webSocketConnections = LookupNetworkingNode(BinaryRequestMessage.DestinationId).ToArray();

                if (webSocketConnections.Length != 0)
                {

                    var networkingMode = webSocketConnections.First().Item1.TryGetCustomDataAs<NetworkingMode>(EEBus.EEBusAdapter.NetworkingMode_WebSocketKey);

                    BinaryRequestMessage.NetworkingMode = webSocketConnections.First().Item2;
                    //RequestMessage.RequestTimeout ??= RequestMessage.RequestTimestamp + (RequestTimeout ?? DefaultRequestTimeout);

                    var ocppBinaryMessage = BinaryRequestMessage.ToByteArray();


                    foreach (var webSocketConnection in webSocketConnections)
                    {

                        if (SentStatus.Success == await SendBinaryMessage(
                                                            webSocketConnection.Item1,
                                                            ocppBinaryMessage,
                                                            BinaryRequestMessage.EventTrackingId,
                                                            BinaryRequestMessage.CancellationToken
                                                        ))
                        {

                            //requests.TryAdd(RequestMessage.RequestId,
                            //                SendRequestState.FromJSONRequest(
                            //                    Timestamp.Now,
                            //                    RequestMessage.DestinationNodeId,
                            //                    RequestMessage.RequestTimeout ?? (RequestMessage.RequestTimestamp + (RequestTimeout ?? DefaultRequestTimeout)),
                            //                    RequestMessage
                            //                ));

                            #region OnBinaryMessageRequestSent

                            //var onBinaryMessageRequestSent = OnBinaryMessageRequestSent;
                            //if (onBinaryMessageRequestSent is not null)
                            //{
                            //    try
                            //    {

                            //        await Task.WhenAll(onBinaryMessageRequestSent.GetInvocationList().
                            //                               OfType<OnWebSocketTextMessageDelegate>().
                            //                               Select(loggingDelegate => loggingDelegate.Invoke(
                            //                                                              Timestamp.Now,
                            //                                                              this,
                            //                                                              webSocketConnection.Item1,
                            //                                                              EventTrackingId,
                            //                                                              ocppTextMessage,
                            //                                                              CancellationToken
                            //                                                          )).
                            //                               ToArray());

                            //    }
                            //    catch (Exception e)
                            //    {
                            //        DebugX.Log(e, nameof(AEEBusWebSocketServer) + "." + nameof(OnBinaryMessageRequestSent));
                            //    }
                            //}

                            #endregion

                            break;

                        }

                        RemoveConnection(webSocketConnection.Item1);

                    }

                    return SendWebSocketMessageResult.Success;

                }
                else
                    return SendWebSocketMessageResult.UnknownClient;

            }
            catch (Exception)
            {
                return SendWebSocketMessageResult.TransmissionFailed;
            }

        }

        #endregion

        #region SendBinaryResponse    (BinaryResponseMessage)

        /// <summary>
        /// Send (and forget) the given binary EEBus response message.
        /// </summary>
        /// <param name="BinaryResponseMessage">A binary EEBus response message.</param>
        public async Task<SendWebSocketMessageResult> SendBinaryResponse(BinaryResponseMessage BinaryResponseMessage)
        {

            try
            {

                var webSocketConnections = LookupNetworkingNode(BinaryResponseMessage.DestinationId).ToArray();

                if (webSocketConnections.Length != 0)
                {

                    var networkingMode = webSocketConnections.First().Item1.TryGetCustomDataAs<NetworkingMode>(EEBus.EEBusAdapter.NetworkingMode_WebSocketKey);

                    BinaryResponseMessage.NetworkingMode = webSocketConnections.First().Item2;
                    //ResponseMessage.ResponseTimeout ??= ResponseMessage.ResponseTimestamp + (ResponseTimeout ?? DefaultResponseTimeout);

                    var ocppBinaryMessage = BinaryResponseMessage.ToByteArray();


                    foreach (var webSocketConnection in webSocketConnections)
                    {

                        if (SentStatus.Success == await SendBinaryMessage(
                                                            webSocketConnection.Item1,
                                                            ocppBinaryMessage,
                                                            BinaryResponseMessage.EventTrackingId,
                                                            BinaryResponseMessage.CancellationToken
                                                        ))
                        {

                            //responses.TryAdd(ResponseMessage.ResponseId,
                            //                SendResponseState.FromJSONResponse(
                            //                    Timestamp.Now,
                            //                    ResponseMessage.DestinationNodeId,
                            //                    ResponseMessage.ResponseTimeout ?? (ResponseMessage.ResponseTimestamp + (ResponseTimeout ?? DefaultResponseTimeout)),
                            //                    ResponseMessage
                            //                ));

                            #region OnBinaryMessageResponseSent

                            //var onBinaryMessageResponseSent = OnBinaryMessageResponseSent;
                            //if (onBinaryMessageResponseSent is not null)
                            //{
                            //    try
                            //    {

                            //        await Task.WhenAll(onBinaryMessageResponseSent.GetInvocationList().
                            //                               OfType<OnWebSocketTextMessageDelegate>().
                            //                               Select(loggingDelegate => loggingDelegate.Invoke(
                            //                                                              Timestamp.Now,
                            //                                                              this,
                            //                                                              webSocketConnection.Item1,
                            //                                                              EventTrackingId,
                            //                                                              ocppTextMessage,
                            //                                                              CancellationToken
                            //                                                          )).
                            //                               ToArray());

                            //    }
                            //    catch (Exception e)
                            //    {
                            //        DebugX.Log(e, nameof(AEEBusWebSocketServer) + "." + nameof(OnBinaryMessageResponseSent));
                            //    }
                            //}

                            #endregion

                            break;

                        }

                        RemoveConnection(webSocketConnection.Item1);

                    }

                    return SendWebSocketMessageResult.Success;

                }
                else
                    return SendWebSocketMessageResult.UnknownClient;

            }
            catch (Exception)
            {
                return SendWebSocketMessageResult.TransmissionFailed;
            }

        }

        #endregion



        private IEnumerable<Tuple<WebSocketServerConnection, NetworkingMode>> LookupNetworkingNode(NetworkingNode_Id NetworkingNodeId)
        {

            if (NetworkingNodeId == NetworkingNode_Id.Zero)
                return Array.Empty<Tuple<WebSocketServerConnection, NetworkingMode>>();

            var lookUpNetworkingNodeId = NetworkingNodeId;

            if (reachableViaNetworkingHubs.TryGetValue(lookUpNetworkingNodeId, out var networkingHubId))
            {
                lookUpNetworkingNodeId = networkingHubId;
                return WebSocketConnections.Where(connection => connection.TryGetCustomDataAs<NetworkingNode_Id>(EEBus.EEBusAdapter.NetworkingNodeId_WebSocketKey) == lookUpNetworkingNodeId).
                    Select(x => new Tuple<WebSocketServerConnection, NetworkingMode>(x, NetworkingMode.OverlayNetwork));
            }

            return WebSocketConnections.Where(connection => connection.TryGetCustomDataAs<NetworkingNode_Id>(EEBus.EEBusAdapter.NetworkingNodeId_WebSocketKey) == lookUpNetworkingNodeId).
                                        Select(x => new Tuple<WebSocketServerConnection, NetworkingMode>(x, NetworkingMode.Standard));

        }

        public void AddStaticRouting(NetworkingNode_Id DestinationNodeId,
                                     NetworkingNode_Id NetworkingHubId)
        {

            reachableViaNetworkingHubs.TryAdd(DestinationNodeId,
                                              NetworkingHubId);

        }

        public void RemoveStaticRouting(NetworkingNode_Id DestinationNodeId,
                                        NetworkingNode_Id NetworkingHubId)
        {

            reachableViaNetworkingHubs.TryRemove(new KeyValuePair<NetworkingNode_Id, NetworkingNode_Id>(DestinationNodeId, NetworkingHubId));

        }



        #region (protected) HandleErrors(Module, Caller, Exception, Description = null)
        protected Task HandleErrors(String     Module,
                                    String     Caller,
                                    Exception  Exception,
                                    String?    Description   = null)
        {

            DebugX.LogException(Exception, $"{Module}.{Caller}{(Description is not null ? $" {Description}" : "")}");

            return Task.CompletedTask;

        }

        #endregion


    }

}
