/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Logging;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// The EEBus/SHIP HTTP WebSocket client.
    /// </summary>
    public partial class SHIPWebSocketClient : WebSocketClient
    {

        #region Data

        /// <summary>
        /// The default HTTP user agent string.
        /// </summary>
        public new const String  DefaultHTTPUserAgent   = $"GraphDefined EEBus/SHIP {EEBus.Version.String} WebSocket Client";

        private    const String  LogfileName            = "NetworkingNodeWSClient.log";

        #endregion

        #region Properties

        public IEEBusAdapter  EEBusAdapter    { get; }

        #endregion

        #region Events

        #region Common Connection Management

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
        public event OnWebSocketTextErrorResponseDelegate?    OnJSONRequestErrorSent;


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
        public event OnWebSocketTextErrorResponseDelegate?    OnJSONRequestErrorReceived;

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
        /// Create a new EEBus/SHIP HTTP WebSocket client.
        /// </summary>
        /// <param name="RemoteURL">The remote URL of the HTTP endpoint to connect to.</param>
        /// <param name="VirtualHostname">An optional HTTP virtual hostname.</param>
        /// <param name="Description">An optional description of this HTTP/websocket client.</param>
        /// <param name="RemoteCertificateValidator">The remote SSL/TLS certificate validator.</param>
        /// <param name="LocalCertificateSelector">A delegate to select a TLS client certificate.</param>
        /// <param name="ClientCert">The SSL/TLS client certificate to use of HTTP authentication.</param>
        /// <param name="HTTPUserAgent">The HTTP user agent identification.</param>
        /// <param name="URLPathPrefix">An optional default URL path prefix.</param>
        /// <param name="HTTPAuthentication">The WebService-Security username/password.</param>
        /// <param name="RequestTimeout">An optional Request timeout.</param>
        /// <param name="TransmissionRetryDelay">The delay between transmission retries.</param>
        /// <param name="MaxNumberOfRetries">The maximum number of transmission retries for HTTP request.</param>
        /// <param name="LoggingPath">The logging path.</param>
        /// <param name="LoggingContext">An optional context for logging client methods.</param>
        /// <param name="LogfileCreator">A delegate to create a log file from the given context and log file name.</param>
        /// <param name="HTTPLogger">A HTTP logger.</param>
        /// <param name="DNSClient">The DNS client to use.</param>
        public SHIPWebSocketClient(IEEBusAdapter                                                   EEBusAdapter,

                                   URL                                                             RemoteURL,
                                   HTTPHostname?                                                   VirtualHostname              = null,
                                   String?                                                         Description                  = null,
                                   Boolean?                                                        PreferIPv4                   = null,
                                   RemoteTLSServerCertificateValidationHandler<org.GraphDefined.Vanaheimr.Hermod.WebSocket.IWebSocketClient>?  RemoteCertificateValidator   = null,
                                   LocalCertificateSelectionHandler?                               LocalCertificateSelector     = null,
                                   X509Certificate?                                                ClientCert                   = null,
                                   SslProtocols?                                                   TLSProtocol                  = null,
                                   String                                                          HTTPUserAgent                = DefaultHTTPUserAgent,
                                   IHTTPAuthentication?                                            HTTPAuthentication           = null,
                                   TimeSpan?                                                       RequestTimeout               = null,
                                   TransmissionRetryDelayDelegate?                                 TransmissionRetryDelay       = null,
                                   UInt16?                                                         MaxNumberOfRetries           = 3,
                                   UInt32?                                                         InternalBufferSize           = null,

                                   NetworkingMode?                                                 NetworkingMode               = null,

                                   Boolean                                                         DisableWebSocketPings        = false,
                                   TimeSpan?                                                       WebSocketPingEvery           = null,
                                   TimeSpan?                                                       SlowNetworkSimulationDelay   = null,

                                   Boolean                                                         DisableMaintenanceTasks      = false,
                                   TimeSpan?                                                       MaintenanceEvery             = null,

                                   String?                                                         LoggingPath                  = null,
                                   String                                                          LoggingContext               = null, //CPClientLogger.DefaultContext,
                                   LogfileCreatorDelegate?                                         LogfileCreator               = null,
                                   HTTPClientLogger?                                               HTTPLogger                   = null,
                                   DNSClient?                                                      DNSClient                    = null)

            : base(RemoteURL,
                   VirtualHostname,
                   Description,
                   PreferIPv4,
                   RemoteCertificateValidator,
                   LocalCertificateSelector,
                   ClientCert,
                   TLSProtocol,
                   HTTPUserAgent ?? DefaultHTTPUserAgent,
                   HTTPAuthentication,
                   RequestTimeout,
                   TransmissionRetryDelay,
                   MaxNumberOfRetries,
                   InternalBufferSize,

                   [ "ship" ],

                   DisableWebSocketPings,
                   WebSocketPingEvery,
                   SlowNetworkSimulationDelay,

                   DisableMaintenanceTasks,
                   MaintenanceEvery,

                   LoggingPath,
                   LoggingContext,
                   LogfileCreator,
                   HTTPLogger,
                   DNSClient)

        {

            this.EEBusAdapter  = EEBusAdapter;

            //this.Logger          = new ChargePointwebsocketClient.CPClientLogger(this,
            //                                                                LoggingPath,
            //                                                                LoggingContext,
            //                                                                LogfileCreator);

        }

        #endregion

//Therefore, all data frames (i.e. non-control frames) used with this specification
//MUST be of type 0x2 (binary frames).
//
//A SHIP node that receives a data frame with type (0x3–0x7, 0xB-0xF) MUST
//644 terminate the connection with status code 1002 (protocol error).



        #region ProcessWebSocketTextFrame   (RequestTimestamp, ClientConnection, TextMessage,   EventTrackingId, CancellationToken)

        public override async Task ProcessWebSocketTextFrame(DateTime                   RequestTimestamp,
                                                             WebSocketClientConnection  ClientConnection,
                                                             EventTracking_Id           EventTrackingId,
                                                             String                     TextMessage,
                                                             CancellationToken          CancellationToken)
        {

            // A SHIP node that receives a data frame with another type (0x1)
            // MUST terminate the connection with status code 1003 (unacceptable data).
            await ClientConnection.Close(
                      WebSocketFrame.ClosingStatusCode.UnsupportedData,
                      "HTTP Web Socket Text frames are not allowed by EEBus SHIP!",
                      CancellationToken
                  );

        }

        #endregion

        #region ProcessWebSocketBinaryFrame (RequestTimestamp, ClientConnection, BinaryMessage, EventTrackingId, CancellationToken)

        public override async Task ProcessWebSocketBinaryFrame(DateTime                   RequestTimestamp,
                                                               WebSocketClientConnection  ClientConnection,
                                                               EventTracking_Id           EventTrackingId,
                                                               Byte[]                     BinaryMessage,
                                                               CancellationToken          CancellationToken)
        {

            if (BinaryMessage.Length == 0)
            {
                DebugX.Log($"Received an empty binary message within {nameof(SHIPWebSocketClient)}!");
                return;
            }

            try
            {

                var binaryMessageResponse = await EEBusAdapter.IN.ProcessBinaryMessage(
                                                      RequestTimestamp,
                                                      ClientConnection,
                                                      BinaryMessage,
                                                      EventTrackingId,
                                                      CancellationToken
                                                  );

            }
            catch (Exception e)
            {

                DebugX.LogException(e, nameof(SHIPWebSocketClient) + "." + nameof(ProcessWebSocketBinaryFrame));

                //EEBusErrorResponse = new EEBus_WebSocket_ErrorMessage(
                //                        Request_Id.Zero,
                //                        ResultCodes.InternalError,
                //                        $"The EEBus message '{EEBusTextMessage}' received in " + nameof(AChargingStationWSClient) + " led to an exception!",
                //                        new JObject(
                //                            new JProperty("request",      EEBusTextMessage),
                //                            new JProperty("exception",    e.Message),
                //                            new JProperty("stacktrace",   e.StackTrace)
                //                        )
                //                    );

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

                //RequestMessage.RequestTimeout ??= RequestMessage.RequestTimestamp + (RequestTimeout ?? DefaultRequestTimeout);

                var ocppBinaryMessage = BinaryRequestMessage.ToByteArray();

                if (SendStatus.Success == await SendBinaryMessage(
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

                }

                return SendWebSocketMessageResult.Success;

            }
            catch (Exception)
            {
                return SendWebSocketMessageResult.TransmissionFailed;
            }

        }

        #endregion

        #region SendBinaryResponse    (BinaryResponseMessage)

        /// <summary>
        /// Send (and forget) the given binary EEBus request message.
        /// </summary>
        /// <param name="BinaryResponseMessage">A binary EEBus request message.</param>
        public async Task<SendWebSocketMessageResult> SendBinaryResponse(BinaryResponseMessage BinaryResponseMessage)
        {

            try
            {

                //ResponseMessage.ResponseTimeout ??= ResponseMessage.ResponseTimestamp + (ResponseTimeout ?? DefaultResponseTimeout);

                var ocppBinaryMessage = BinaryResponseMessage.ToByteArray();

                if (SendStatus.Success == await SendBinaryMessage(
                                                    ocppBinaryMessage,
                                                    BinaryResponseMessage.EventTrackingId,
                                                    BinaryResponseMessage.CancellationToken
                                                ))
                {

                    //requests.TryAdd(ResponseMessage.ResponseId,
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

                }

                return SendWebSocketMessageResult.Success;

            }
            catch (Exception)
            {
                return SendWebSocketMessageResult.TransmissionFailed;
            }

        }

        #endregion


    }

}
