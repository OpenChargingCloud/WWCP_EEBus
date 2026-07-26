/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBUS
{

    /// <summary>
    /// The EEBUS adapter for receiving messages.
    /// </summary>
    public partial class EEBUSWebSocketAdapterIN : IEEBUSWebSocketAdapterIN
    {

        #region Data

        private   readonly  IEEBUSNetworkingNode            parentNetworkingNode;

        protected readonly  Dictionary<String, MethodInfo>  incomingMessageProcessorsLookup   = [];

        #endregion

        #region Properties

        public HashSet<NetworkingNode_Id>  AnycastIds    { get; } = [];

        #endregion

        #region Events

        #region Generic Binary Messages

        /// <summary>
        /// An event sent whenever a binary request was received.
        /// </summary>
        public event OnBinaryMessageRequestReceivedDelegate?     OnBinaryMessageRequestReceived;

        /// <summary>
        /// An event sent whenever a binary response was received.
        /// </summary>
        public event OnBinaryMessageResponseReceivedDelegate?    OnBinaryMessageResponseReceived;

        ///// <summary>
        ///// An event sent whenever a binary error response was received.
        ///// </summary>
        //public event OnBinaryErrorResponseReceivedDelegate?      OnBinaryErrorResponseReceived;

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EEBUS adapter for accepting incoming messages.
        /// </summary>
        /// <param name="NetworkingNode">The parent networking node.</param>
        public EEBUSWebSocketAdapterIN(IEEBUSNetworkingNode NetworkingNode)
        {

            this.parentNetworkingNode = NetworkingNode;

            #region Reflect "Receive_XXX" messages and wire them...

            foreach (var method in typeof(EEBUSWebSocketAdapterIN).
                                       GetMethods(BindingFlags.Public | BindingFlags.Instance).
                                            Where(method            => method.Name.StartsWith("Receive_") &&
                                                 (method.ReturnType == typeof(Task<Tuple<JSONResponseMessage?,   JSONRequestErrorMessage?>>) ||
                                                  method.ReturnType == typeof(Task<Tuple<BinaryResponseMessage?, JSONRequestErrorMessage?>>) ||
                                                  method.ReturnType == typeof(Task<WebSocketResponse>))))
            {

                var processorName = method.Name[8..];

                if (incomingMessageProcessorsLookup.ContainsKey(processorName))
                    throw new ArgumentException("Duplicate processor name: " + processorName);

                incomingMessageProcessorsLookup.Add(processorName,
                                                    method);

            }

            #endregion

        }

        #endregion


        #region ProcessBinaryMessage (RequestTimestamp, WebSocketConnection, BinaryMessage, EventTrackingId, CancellationToken)

        public async Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTimeOffset        RequestTimestamp,
                                                                               IWebSocketConnection  WebSocketConnection,
                                                                               Byte[]                BinaryMessage,
                                                                               EventTracking_Id      EventTrackingId,
                                                                               CancellationToken     CancellationToken)
        {

            #region Initial checks

            if (BinaryMessage.Length < 2)
            {

                await WebSocketConnection.Close(
                          WebSocketFrame.ClosingStatusCode.InvalidPayloadData,
                          "Invalid EEBUS/SHIP payload received, expected a message size of at least 2 bytes!",
                          CancellationToken
                      );

                return new WebSocketBinaryMessageResponse(
                           RequestTimestamp,
                           BinaryMessage,
                           Timestamp.Now,
                           [],
                           EventTrackingId,
                           CancellationToken
                       );

            }

            #endregion

            try
            {

                var shipMessageType  = (SHIPMessageTypes) BinaryMessage[0];
                var shipPayload      = new Byte[BinaryMessage.Length - 1];
                Buffer.BlockCopy(BinaryMessage, 1, BinaryMessage, 0, BinaryMessage.Length - 1);

                var connectedNodeName = "!!!";

                switch (shipMessageType)
                {

                    case SHIPMessageTypes.INIT:

                        DebugX.Log($"Init message received from {connectedNodeName}.");

                        if (shipPayload[0] != SHIPMessageValue.CMI_HEAD)
                            throw new Exception("Expected SMI_HEAD payload in INIT message!");

                        var responseBytes = new Byte[2];
                        responseBytes[0] = (Byte) SHIPMessageTypes.INIT;
                        responseBytes[1] = SHIPMessageValue.CMI_HEAD;

                        await WebSocketConnection.Send(
                                  responseBytes,
                                  CancellationToken
                              );

                        break;

                    case SHIPMessageTypes.CONTROL:

                        var controlMessageHandled  = false;
                        var jsonMessage            = JObject.Parse(shipPayload.ToUTF8String());

                        if (jsonMessage.ContainsKey("connectionHello"))
                        {
                        //    SHIPHelloMessage helloMessageReceived = helloMessageReceived = JsonConvert.DeserializeObject<SHIPHelloMessage>(messageString, jsonSettings);
                        //    if ((helloMessageReceived != null) && (helloMessageReceived.connectionHello != null))
                        //    {
                        //        DebugX.Log($"Hello message received from {connectedNodeName}.");

                        //        if (!await HandleHelloMessage(webSocket, helloMessageReceived.connectionHello).ConfigureAwait(false))
                        //        {
                        //            throw new Exception("Hello aborted!");
                        //        }

                        //        controlMessageHandled = true;
                        //    }
                        }

                        if (jsonMessage.ContainsKey("messageProtocolHandshake"))
                        {
                        //    SHIPHandshakeMessage handshakeMessageReceived = JsonConvert.DeserializeObject<SHIPHandshakeMessage>(Encoding.UTF8.GetString(shipPayload), jsonSettings);

                        //    if ((handshakeMessageReceived != null) && (handshakeMessageReceived.messageProtocolHandshake != null))
                        //    {
                        //        DebugX.Log($"Handshake message received from {connectedNodeName}.");

                        //        if (!await HandleHandshakeMessage(webSocket, handshakeMessageReceived.messageProtocolHandshake).ConfigureAwait(false))
                        //        {
                        //            throw new Exception("Handshake aborted!");
                        //        }

                        //        controlMessageHandled = true;
                        //    }
                        }

                        if (jsonMessage.ContainsKey("messageProtocolHandshakeError"))
                        {
                        //    SHIPHandshakeErrorMessage handshakeErrorMessageReceived = JsonConvert.DeserializeObject<SHIPHandshakeErrorMessage>(Encoding.UTF8.GetString(shipPayload), jsonSettings);
                        //    if ((handshakeErrorMessageReceived != null) && (handshakeErrorMessageReceived.messageProtocolHandshakeError != null))
                        //    {
                        //        DebugX.Log($"Handshake error message received from {connectedNodeName} due to {handshakeErrorMessageReceived.messageProtocolHandshakeError.error}.");

                        //        controlMessageHandled = true;

                        //        throw new Exception("Handshake aborted!");
                        //    }
                        }

                        if (jsonMessage.ContainsKey("accessMethodsRequest"))
                        {
                        //    SHIPAccessMethodsMessage accessMethodsMessageReceived = JsonConvert.DeserializeObject<SHIPAccessMethodsMessage>(Encoding.UTF8.GetString(shipPayload), jsonSettings);
                        //    if ((accessMethodsMessageReceived != null) && (accessMethodsMessageReceived.accessMethodsRequest != null))
                        //    {
                        //        DebugX.Log($"Access Methods message received from {connectedNodeName}.");

                        //        if (!HandleAccessMethodsMessage(connectedNodeName, webSocket, accessMethodsMessageReceived.accessMethodsRequest))
                        //        {
                        //            throw new Exception("Access methods received message aborted!");
                        //        }

                        //        controlMessageHandled = true;
                        //    }
                        }

                        //   PINVerification
                        //   PINVerificationError

                        if (!controlMessageHandled)
                            DebugX.Log($"Control message from {connectedNodeName} ignored!");

                        break;

                    case SHIPMessageTypes.DATA:

                        DebugX.Log($"Data message received from {connectedNodeName}.");

                        //SHIPDataMessage dataMessageReceived = JsonConvert.DeserializeObject<SHIPDataMessage>(Encoding.UTF8.GetString(shipPayload), jsonSettings);
                        //if ((dataMessageReceived != null) && (dataMessageReceived.data != null))
                        //{
                        //    if (!await HandleDataMessage(webSocket, dataMessageReceived.data).ConfigureAwait(false))
                        //    {
                        //        throw new Exception("Data message aborted!");
                        //    }
                        //}

                        break;

                    case SHIPMessageTypes.END:

                        DebugX.Log($"Close message received from {connectedNodeName}.");

                        //SHIPCloseMessage closeMessageReceived = JsonConvert.DeserializeObject<SHIPCloseMessage>(Encoding.UTF8.GetString(shipPayload), jsonSettings);
                        //if ((closeMessageReceived != null) && (closeMessageReceived.connectionClose != null))
                        //{
                        //    if (!await HandleCloseMessage(webSocket, closeMessageReceived.connectionClose).ConfigureAwait(false))
                        //    {
                        //        throw new Exception("Close message aborted!");
                        //    }
                        //}
                        break;

                    default:
                        throw new Exception("Invalid EEBUS message type received!");

                }




                //    JSONResponseMessage?      EEBUSResponse         = null;
                BinaryResponseMessage?    EEBUSBinaryResponse   = null;
                JSONRequestErrorMessage?  EEBUSErrorResponse    = null;

                var sourceNodeId = WebSocketConnection.TryGetCustomDataAs<NetworkingNode_Id>(EEBUSAdapter.NetworkingNodeId_WebSocketKey);

                     if (BinaryRequestMessage. TryParse(BinaryMessage, out var binaryRequest,  out var requestParsingError,  RequestTimestamp, EventTrackingId, sourceNodeId, CancellationToken) && binaryRequest  is not null)
                {

                    #region OnBinaryMessageRequestReceived

                    var logger = OnBinaryMessageRequestReceived;
                    if (logger is not null)
                    {
                        try
                        {

                            await Task.WhenAll(logger.GetInvocationList().
                                                   OfType <OnBinaryMessageRequestReceivedDelegate>().
                                                   Select (loggingDelegate => loggingDelegate.Invoke(
                                                                                  Timestamp.Now,
                                                                                  this,
                                                                                  binaryRequest
                                                                              )).
                                                   ToArray());

                        }
                        catch (Exception e)
                        {
                            DebugX.LogException(e, nameof(EEBUSWebSocketAdapterIN) + "." + nameof(OnBinaryMessageRequestReceived));
                        }
                    }

                    #endregion


                    // When not for this node, send it to the FORWARD processor...
                    if (binaryRequest.DestinationId != parentNetworkingNode.Id)
                        await parentNetworkingNode.EEBUS.FORWARD.ProcessBinaryRequestMessage(binaryRequest);

                    // Directly for this node OR an anycast message for this node...
                    if (binaryRequest.DestinationId == parentNetworkingNode.Id ||
                        parentNetworkingNode.EEBUS.IN.AnycastIds.Contains(binaryRequest.DestinationId))
                    {

                        #region Try to call the matching 'incoming message processor'

                        if (incomingMessageProcessorsLookup.TryGetValue(binaryRequest.Action, out var methodInfo) &&
                            methodInfo is not null)
                        {

                            var result = methodInfo.Invoke(this,
                                                           [ binaryRequest.RequestTimestamp,
                                                             WebSocketConnection,
                                                             binaryRequest.DestinationId,
                                                             binaryRequest.NetworkPath,
                                                             binaryRequest.EventTrackingId,
                                                             binaryRequest.RequestId,
                                                             binaryRequest.Payload,
                                                             binaryRequest.CancellationToken ]);

                            if (result is Task<Tuple<BinaryResponseMessage?, JSONRequestErrorMessage?>> binaryProcessor) {

                                (EEBUSBinaryResponse, EEBUSErrorResponse) = await binaryProcessor;

                                if (EEBUSBinaryResponse is not null)
                                    await parentNetworkingNode.EEBUS.SendBinaryResponse(EEBUSBinaryResponse);

                                //if (EEBUSErrorResponse is not null)
                                //    await parentNetworkingNode.EEBUS.SendJSONRequestError     (EEBUSErrorResponse);

                            }

                            else if (result is Task<WebSocketResponse> ocppProcessor)
                            {

                                var ocppReply = await ocppProcessor;

                                //EEBUSResponse         = ocppReply.JSONResponseMessage;
                                EEBUSErrorResponse    = ocppReply.JSONErrorMessage;
                                EEBUSBinaryResponse   = ocppReply.BinaryResponseMessage;

                                //if (ocppReply.JSONResponseMessage is not null)
                                //    await parentNetworkingNode.EEBUS.SendJSONResponse  (ocppReply.JSONResponseMessage);

                                //if (ocppReply.JSONErrorMessage is not null)
                                //    await parentNetworkingNode.EEBUS.SendJSONRequestError     (ocppReply.JSONErrorMessage);

                                if (ocppReply.BinaryResponseMessage is not null)
                                    await parentNetworkingNode.EEBUS.SendBinaryResponse(ocppReply.BinaryResponseMessage);

                            }

                            else
                                DebugX.Log($"Received undefined '{binaryRequest.Action}' binary request message handler within {nameof(EEBUSWebSocketAdapterIN)}!");

                        }

                        #endregion

                        #region ...or error!

                        else
                        {

                            DebugX.Log($"Received unknown '{binaryRequest.Action}' binary request message handler within {nameof(EEBUSWebSocketAdapterIN)}!");

                            EEBUSErrorResponse = new JSONRequestErrorMessage(
                                                    Timestamp.Now,
                                                    EventTracking_Id.New,
                                                    NetworkingMode.Unknown,
                                                    NetworkingNode_Id.Zero,
                                                    NetworkPath.Empty,
                                                    binaryRequest.RequestId,
                                                    ResultCode.ProtocolError,
                                                    $"The EEBUS message '{binaryRequest.Action}' is unkown!",
                                                    new JObject(
                                                        new JProperty("request", BinaryMessage.ToBase64())
                                                    )
                                                );

                        }

                        #endregion

                    }

                    #region NotifyJSON(Message/Error)ResponseSent

                    //if (EEBUSResponse       is not null)
                    //    await parentNetworkingNode.EEBUS.OUT.NotifyJSONMessageResponseSent  (EEBUSResponse);

                    //if (EEBUSErrorResponse  is not null)
                    //    await parentNetworkingNode.EEBUS.OUT.NotifyJSONErrorResponseSent    (EEBUSErrorResponse);

                    if (EEBUSBinaryResponse is not null)
                        await parentNetworkingNode.EEBUS.OUT.NotifyBinaryMessageResponseSent(EEBUSBinaryResponse);

                    #endregion

                }

                else if (BinaryResponseMessage.TryParse(BinaryMessage, out var binaryResponse, out var responseParsingError,                                    sourceNodeId)                    && binaryResponse is not null)
                {

                    #region OnBinaryMessageResponseReceived

                    var logger = OnBinaryMessageResponseReceived;
                    if (logger is not null)
                    {
                        try
                        {

                            await Task.WhenAll(logger.GetInvocationList().
                                                   OfType <OnBinaryMessageResponseReceivedDelegate>().
                                                   Select (loggingDelegate => loggingDelegate.Invoke(
                                                                                  Timestamp.Now,
                                                                                  this,
                                                                                  binaryResponse
                                                                              )).
                                                   ToArray());

                        }
                        catch (Exception e)
                        {
                            DebugX.LogException(e, nameof(EEBUSWebSocketAdapterIN) + "." + nameof(OnBinaryMessageResponseReceived));
                        }
                    }

                    #endregion

                    parentNetworkingNode.EEBUS.ReceiveBinaryResponse(binaryResponse);

                    // No response to the charging station!

                }

                else if (requestParsingError  is not null)
                    DebugX.Log($"Failed to parse a binary request message within {nameof(EEBUSWebSocketAdapterIN)}: '{requestParsingError}'{Environment.NewLine}'{BinaryMessage.ToBase64()}'!");

                else if (responseParsingError is not null)
                    DebugX.Log($"Failed to parse a binary response message within {nameof(EEBUSWebSocketAdapterIN)}: '{responseParsingError}'{Environment.NewLine}'{BinaryMessage.ToBase64()}'!");

                else
                    DebugX.Log($"Received unknown binary message within {nameof(EEBUSWebSocketAdapterIN)}: '{BinaryMessage.ToBase64()}'!");

            }
            catch (Exception e)
            {

                //EEBUSErrorResponse = JSONRequestErrorMessage.InternalError(
                //                        nameof(EEBUSWebSocketAdapterIN),
                //                        EventTrackingId,
                //                        BinaryMessage,
                //                        e
                //                    );

            }


            // The response is empty!
            return new WebSocketBinaryMessageResponse(
                       RequestTimestamp,
                       BinaryMessage,
                       Timestamp.Now,
                       [],
                       EventTrackingId,
                       CancellationToken
                   );

        }

        #endregion


        #region HandleErrors(Module, Caller, ExceptionOccured)

        private Task HandleErrors(String     Module,
                                  String     Caller,
                                  Exception  ExceptionOccured)
        {

            DebugX.LogException(ExceptionOccured, $"{Module}.{Caller}");

            return Task.CompletedTask;

        }

        #endregion


    }

}
