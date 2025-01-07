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

using System.Collections.Concurrent;

using Newtonsoft.Json;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBus
{

    /// <summary>
    /// An EEBus adapter.
    /// </summary>
    public class EEBusAdapter : IEEBusAdapter
    {

        #region Data

        private readonly        ConcurrentDictionary<NetworkingNode_Id, List<Reachability>>  reachableNetworkingNodes        = [];
        private readonly        ConcurrentDictionary<Request_Id, SendRequestState>           requests                        = [];
        private readonly        HashSet<SignaturePolicy>                                     signaturePolicies               = [];
        private readonly        HashSet<SignaturePolicy>                                     forwardingSignaturePolicies     = [];
        private                 Int64                                                        internalRequestId               = 800000;

        /// <summary>
        /// The default time span between heartbeat requests.
        /// </summary>
        public static readonly  TimeSpan                                                     DefaultSendHeartbeatsEvery      = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The default request timeout default.
        /// </summary>
        public static readonly  TimeSpan                                                     DefaultRequestTimeoutDefault    = TimeSpan.FromSeconds(30);

        public const            String                                                       NetworkingNodeId_WebSocketKey   = "networkingNodeId";
        public const            String                                                       NetworkingMode_WebSocketKey     = "networkingMode";

        #endregion

        #region Properties

        public NetworkingNode_Id             Id                       { get; }

        /// <summary>
        /// Incoming EEBus messages.
        /// </summary>
        public IEEBusWebSocketAdapterIN       IN                       { get; }

        /// <summary>
        /// Outgoing EEBus messages.
        /// </summary>
        public IEEBusWebSocketAdapterOUT      OUT                      { get; }

        /// <summary>
        /// Forwarded EEBus messages.
        /// </summary>
        public IEEBusWebSocketAdapterFORWARD  FORWARD                  { get; }

        /// <summary>
        /// Disable the sending of heartbeats.
        /// </summary>
        public Boolean                       DisableSendHeartbeats    { get; set; }

        /// <summary>
        /// The time span between heartbeat requests.
        /// </summary>
        public TimeSpan                      SendHeartbeatsEvery      { get; set; } = DefaultSendHeartbeatsEvery;

        /// <summary>
        /// The default request timeout for all requests.
        /// </summary>
        public TimeSpan                      DefaultRequestTimeout    { get; }      = DefaultRequestTimeoutDefault;


        #region NextRequestId

        /// <summary>
        /// Return a new unique request identification.
        /// </summary>
        public Request_Id NextRequestId
        {
            get
            {

                Interlocked.Increment(ref internalRequestId);

                return Request_Id.Parse(internalRequestId.ToString());

            }
        }

        #endregion

        #region SignaturePolicy/-ies

        /// <summary>
        /// The enumeration of all signature policies.
        /// </summary>
        public IEnumerable<SignaturePolicy>  SignaturePolicies
            => signaturePolicies;

        /// <summary>
        /// The currently active signature policy.
        /// </summary>
        public SignaturePolicy               SignaturePolicy
            => signaturePolicies.First();

        #endregion

        #region ForwardingSignaturePolicy/-ies

        /// <summary>
        /// The enumeration of all signature policies.
        /// </summary>
        public IEnumerable<SignaturePolicy>  ForwardingSignaturePolicies
            => forwardingSignaturePolicies;

        /// <summary>
        /// The currently active signature policy.
        /// </summary>
        public SignaturePolicy               ForwardingSignaturePolicy
            => forwardingSignaturePolicies.First();

        #endregion

        #endregion

        #region Custom JSON serializer delegates

        #endregion

        #region Custom JSON parser delegates

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EEBus adapter.
        /// </summary>
        /// <param name="NetworkingNode">The attached networking node.</param>
        /// 
        /// <param name="DisableSendHeartbeats">Whether to send heartbeats or not.</param>
        /// <param name="SendHeartbeatsEvery">The optional time span between heartbeat requests.</param>
        /// 
        /// <param name="DefaultRequestTimeout">The optional default request time out.</param>
        /// 
        /// <param name="SignaturePolicy">An optional signature policy.</param>
        /// <param name="ForwardingSignaturePolicy">An optional signature policy when forwarding EEBus messages.</param>
        public EEBusAdapter(IEEBusNetworkingNode  NetworkingNode,

                            Boolean               DisableSendHeartbeats       = false,
                            TimeSpan?             SendHeartbeatsEvery         = null,

                            TimeSpan?             DefaultRequestTimeout       = null,

                            SignaturePolicy?      SignaturePolicy             = null,
                            SignaturePolicy?      ForwardingSignaturePolicy   = null)

        {

            this.Id                     = NetworkingNode.Id;
            this.DisableSendHeartbeats  = DisableSendHeartbeats;
            this.SendHeartbeatsEvery    = SendHeartbeatsEvery   ?? DefaultSendHeartbeatsEvery;

            this.DefaultRequestTimeout  = DefaultRequestTimeout ?? DefaultRequestTimeoutDefault;

            //this.signaturePolicies.          Add(SignaturePolicy           ?? new SignaturePolicy());
            //this.forwardingSignaturePolicies.Add(ForwardingSignaturePolicy ?? new SignaturePolicy());

            this.IN       = new EEBusWebSocketAdapterIN     (NetworkingNode);
            this.OUT      = new EEBusWebSocketAdapterOUT    (NetworkingNode);
            this.FORWARD  = new EEBusWebSocketAdapterFORWARD(NetworkingNode);

        }

        #endregion



        #region SendJSONRequest          (JSONRequestMessage)

        public async Task<SendWebSocketMessageResult> SendJSONRequest(JSONRequestMessage JSONRequestMessage)
        {

            if (LookupNetworkingNode(JSONRequestMessage.DestinationId, out var reachability) &&
                reachability is not null)
            {

                if (reachability.EEBusWebSocketClient is not null)
                    return await reachability.EEBusWebSocketClient.SendJSONRequest(JSONRequestMessage);

                if (reachability.EEBusWebSocketServer is not null)
                    return await reachability.EEBusWebSocketServer.SendJSONRequest(JSONRequestMessage);

            }

            return SendWebSocketMessageResult.UnknownClient;

        }

        #endregion

        #region SendJSONRequestAndWait   (JSONRequestMessage)

        public async Task<SendRequestState> SendJSONRequestAndWait(JSONRequestMessage JSONRequestMessage)
        {

            var sendEEBusMessageResult = await SendJSONRequest(JSONRequestMessage);

            if (sendEEBusMessageResult == SendWebSocketMessageResult.Success)
            {

                #region 1. Store 'in-flight' request...

                requests.TryAdd(JSONRequestMessage.RequestId,
                                SendRequestState.FromJSONRequest(
                                    Timestamp.Now,
                                    JSONRequestMessage.DestinationId,
                                    JSONRequestMessage.RequestTimeout,
                                    JSONRequestMessage
                                ));

                #endregion

                #region 2. Wait for response... or timeout!

                do
                {

                    try
                    {

                        await Task.Delay(25, JSONRequestMessage.CancellationToken);

                        if (requests.TryGetValue(JSONRequestMessage.RequestId, out var sendRequestState) &&
                           (sendRequestState?.JSONResponse   is not null ||
                            sendRequestState?.BinaryResponse is not null ||
                            sendRequestState?.HasErrors == true))
                        {

                            requests.TryRemove(JSONRequestMessage.RequestId, out _);

                            return sendRequestState;

                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(String.Concat(nameof(EEBusWebSocketAdapterIN), ".", nameof(SendJSONRequestAndWait), " exception occured: ", e.Message));
                    }

                }
                while (Timestamp.Now < JSONRequestMessage.RequestTimeout);

                #endregion

                #region 3. When timed out...

                if (requests.TryGetValue(JSONRequestMessage.RequestId, out var sendRequestState2) &&
                    sendRequestState2 is not null)
                {

                    sendRequestState2.JSONRequestErrorMessage =  new JSONRequestErrorMessage(

                                                                     Timestamp.Now,
                                                                     JSONRequestMessage.EventTrackingId,
                                                                     NetworkingMode.Unknown,
                                                                     JSONRequestMessage.NetworkPath.Source,
                                                                     NetworkPath.From(Id),
                                                                     JSONRequestMessage.RequestId,

                                                                     ErrorCode: ResultCode.Timeout

                                                                 );

                    requests.TryRemove(JSONRequestMessage.RequestId, out _);

                    return sendRequestState2;

                }

                #endregion

            }

            // Just in case...
            return SendRequestState.FromJSONRequest(

                       RequestTimestamp:         JSONRequestMessage.RequestTimestamp,
                       DestinationNodeId:        JSONRequestMessage.DestinationId,
                       Timeout:                  JSONRequestMessage.RequestTimeout,
                       JSONRequest:              JSONRequestMessage,
                       ResponseTimestamp:        Timestamp.Now,

                       JSONRequestErrorMessage:  new JSONRequestErrorMessage(

                                                     Timestamp.Now,
                                                     JSONRequestMessage.EventTrackingId,
                                                     NetworkingMode.Unknown,
                                                     JSONRequestMessage.NetworkPath.Source,
                                                     NetworkPath.From(Id),
                                                     JSONRequestMessage.RequestId,

                                                     ErrorCode: ResultCode.InternalError

                                                 )

                   );

        }

        #endregion

        #region SendJSONResponse         (JSONRequestMessage)

        public async Task<SendWebSocketMessageResult> SendJSONResponse(JSONResponseMessage JSONResponseMessage)
        {

            if (LookupNetworkingNode(JSONResponseMessage.DestinationId, out var reachability) &&
                reachability is not null)
            {

                if (reachability.EEBusWebSocketClient is not null)
                    return await reachability.EEBusWebSocketClient.SendJSONResponse(JSONResponseMessage);

                if (reachability.EEBusWebSocketServer is not null)
                    return await reachability.EEBusWebSocketServer.SendJSONResponse(JSONResponseMessage);

            }

            return SendWebSocketMessageResult.UnknownClient;

        }

        #endregion

        #region SendJSONRequestError     (JSONRequestErrorMessage)

        public async Task<SendWebSocketMessageResult> SendJSONRequestError(JSONRequestErrorMessage JSONRequestErrorMessage)
        {

            if (LookupNetworkingNode(JSONRequestErrorMessage.DestinationNodeId, out var reachability) &&
                reachability is not null)
            {

                if (reachability.EEBusWebSocketClient is not null)
                    return await reachability.EEBusWebSocketClient.SendJSONRequestError(JSONRequestErrorMessage);

                if (reachability.EEBusWebSocketServer is not null)
                    return await reachability.EEBusWebSocketServer.SendJSONRequestError(JSONRequestErrorMessage);

            }

            return SendWebSocketMessageResult.UnknownClient;

        }

        #endregion

        #region SendJSONResponseError    (JSONResponseErrorMessage)

        public async Task<SendWebSocketMessageResult> SendJSONResponseError(JSONResponseErrorMessage JSONResponseErrorMessage)
        {

            if (LookupNetworkingNode(JSONResponseErrorMessage.DestinationNodeId, out var reachability) &&
                reachability is not null)
            {

                if (reachability.EEBusWebSocketClient is not null)
                    return await reachability.EEBusWebSocketClient.SendJSONResponseError(JSONResponseErrorMessage);

                if (reachability.EEBusWebSocketServer is not null)
                    return await reachability.EEBusWebSocketServer.SendJSONResponseError(JSONResponseErrorMessage);

            }

            return SendWebSocketMessageResult.UnknownClient;

        }

        #endregion


        #region SendBinaryRequest        (BinaryRequestMessage)

        public async Task<SendWebSocketMessageResult> SendBinaryRequest(BinaryRequestMessage BinaryRequestMessage)
        {

            if (LookupNetworkingNode(BinaryRequestMessage.DestinationId, out var reachability) &&
                reachability is not null)
            {

                if (reachability.EEBusWebSocketClient is not null)
                    return await reachability.EEBusWebSocketClient.SendBinaryRequest(BinaryRequestMessage);

                if (reachability.EEBusWebSocketServer is not null)
                    return await reachability.EEBusWebSocketServer.SendBinaryRequest(BinaryRequestMessage);

            }

            return SendWebSocketMessageResult.UnknownClient;

        }

        #endregion

        #region SendBinaryRequestAndWait (BinaryRequestMessage)

        public async Task<SendRequestState> SendBinaryRequestAndWait(BinaryRequestMessage BinaryRequestMessage)
        {

            var sendEEBusMessageResult = await SendBinaryRequest(BinaryRequestMessage);

            if (sendEEBusMessageResult == SendWebSocketMessageResult.Success)
            {

                #region 1. Store 'in-flight' request...

                requests.TryAdd(BinaryRequestMessage.RequestId,
                                SendRequestState.FromBinaryRequest(
                                    Timestamp.Now,
                                    BinaryRequestMessage.DestinationId,
                                    BinaryRequestMessage.RequestTimeout,
                                    BinaryRequestMessage
                                ));

                #endregion

                #region 2. Wait for response... or timeout!

                do
                {

                    try
                    {

                        await Task.Delay(25, BinaryRequestMessage.CancellationToken);

                        if (requests.TryGetValue(BinaryRequestMessage.RequestId, out var sendRequestState) &&
                           (sendRequestState?.JSONResponse   is not null ||
                            sendRequestState?.BinaryResponse is not null ||
                            sendRequestState?.HasErrors == true))
                        {

                            requests.TryRemove(BinaryRequestMessage.RequestId, out _);

                            return sendRequestState;

                        }

                    }
                    catch (Exception e)
                    {
                        DebugX.Log(String.Concat(nameof(EEBusWebSocketAdapterIN), ".", nameof(SendJSONRequestAndWait), " exception occured: ", e.Message));
                    }

                }
                while (Timestamp.Now < BinaryRequestMessage.RequestTimeout);

                #endregion

                #region 3. When timed out...

                if (requests.TryGetValue(BinaryRequestMessage.RequestId, out var sendRequestState2) &&
                    sendRequestState2 is not null)
                {

                    sendRequestState2.JSONRequestErrorMessage =  new JSONRequestErrorMessage(

                                                                     Timestamp.Now,
                                                                     BinaryRequestMessage.EventTrackingId,
                                                                     NetworkingMode.Unknown,
                                                                     BinaryRequestMessage.NetworkPath.Source,
                                                                     NetworkPath.From(Id),
                                                                     BinaryRequestMessage.RequestId,

                                                                     ErrorCode: ResultCode.Timeout

                                                                 );

                    requests.TryRemove(BinaryRequestMessage.RequestId, out _);

                    return sendRequestState2;

                }

                #endregion

            }

            // Just in case...
            return SendRequestState.FromBinaryRequest(

                       RequestTimestamp:         BinaryRequestMessage.RequestTimestamp,
                       NetworkingNodeId:         BinaryRequestMessage.DestinationId,
                       Timeout:                  BinaryRequestMessage.RequestTimeout,
                       BinaryRequest:            BinaryRequestMessage,
                       ResponseTimestamp:        Timestamp.Now,

                       JSONRequestErrorMessage:  new JSONRequestErrorMessage(

                                                     Timestamp.Now,
                                                     BinaryRequestMessage.EventTrackingId,
                                                     NetworkingMode.Unknown,
                                                     BinaryRequestMessage.NetworkPath.Source,
                                                     NetworkPath.From(Id),
                                                     BinaryRequestMessage.RequestId,

                                                     ErrorCode: ResultCode.InternalError

                                                 )

                   );

        }

        #endregion

        #region SendBinaryResponse       (BinaryResponseMessage)

        public async Task<SendWebSocketMessageResult> SendBinaryResponse(BinaryResponseMessage BinaryResponseMessage)
        {

            if (LookupNetworkingNode(BinaryResponseMessage.DestinationId, out var reachability) &&
                reachability is not null)
            {

                if (reachability.EEBusWebSocketClient is not null)
                    return await reachability.EEBusWebSocketClient.SendBinaryResponse(BinaryResponseMessage);

                if (reachability.EEBusWebSocketServer is not null)
                    return await reachability.EEBusWebSocketServer.SendBinaryResponse(BinaryResponseMessage);

            }

            return SendWebSocketMessageResult.UnknownClient;

        }

        #endregion


        #region ReceiveJSONResponse      (JSONResponseMessage)

        public Boolean ReceiveJSONResponse(JSONResponseMessage JSONResponseMessage)
        {

            if (requests.TryGetValue(JSONResponseMessage.RequestId, out var sendRequestState) &&
                sendRequestState is not null)
            {

                sendRequestState.ResponseTimestamp  = Timestamp.Now;
                sendRequestState.JSONResponse       = JSONResponseMessage;

                return true;

            }

            DebugX.Log($"Received an unknown EEBus response with identificaiton '{JSONResponseMessage.RequestId}' within {Id}:{Environment.NewLine}'{JSONResponseMessage.Payload.ToString(Formatting.None)}'!");
            return false;

        }

        #endregion

        #region ReceiveBinaryResponse    (BinaryResponseMessage)

        public Boolean ReceiveBinaryResponse(BinaryResponseMessage BinaryResponseMessage)
        {

            if (requests.TryGetValue(BinaryResponseMessage.RequestId, out var sendRequestState) &&
                sendRequestState is not null)
            {

                sendRequestState.ResponseTimestamp  = Timestamp.Now;
                sendRequestState.BinaryResponse     = BinaryResponseMessage;

                return true;

            }

            DebugX.Log($"Received an unknown EEBus response with identificaiton '{BinaryResponseMessage.RequestId}' within {Id}:{Environment.NewLine}'{BinaryResponseMessage.Payload.ToBase64()}'!");
            return false;

        }

        #endregion

        #region ReceiveJSONRequestError  (JSONRequestErrorMessage)

        public Boolean ReceiveJSONRequestError(JSONRequestErrorMessage JSONRequestErrorMessage)
        {

            if (requests.TryGetValue(JSONRequestErrorMessage.RequestId, out var sendRequestState) &&
                sendRequestState is not null)
            {

                sendRequestState.JSONResponse             = null;
                sendRequestState.JSONRequestErrorMessage  = JSONRequestErrorMessage;

                return true;

            }

            DebugX.Log($"Received an unknown EEBus JSON request error message with identificaiton '{JSONRequestErrorMessage.RequestId}' within {Id}:{Environment.NewLine}'{JSONRequestErrorMessage.ToJSON().ToString(Formatting.None)}'!");
            return false;

        }

        #endregion

        #region ReceiveJSONResponseError (JSONResponseErrorMessage)

        public Boolean ReceiveJSONResponseError(JSONResponseErrorMessage JSONResponseErrorMessage)
        {

            if (requests.TryGetValue(JSONResponseErrorMessage.RequestId, out var sendRequestState) &&
                sendRequestState is not null)
            {

                sendRequestState.JSONResponse              = null;
                sendRequestState.JSONResponseErrorMessage  = JSONResponseErrorMessage;

                //ToDo: This has to be forwarded actively, as it is not expected (async)!

                return true;

            }

            DebugX.Log($"Received an unknown EEBus JSON response error message with identificaiton '{JSONResponseErrorMessage.RequestId}' within {Id}:{Environment.NewLine}'{JSONResponseErrorMessage.ToJSON().ToString(Formatting.None)}'!");
            return false;

        }

        #endregion



        #region LookupNetworkingNode (DestinationNodeId, out Reachability)

        public Boolean LookupNetworkingNode(NetworkingNode_Id DestinationNodeId, out Reachability? Reachability)
        {

            if (reachableNetworkingNodes.TryGetValue(DestinationNodeId, out var reachabilityList) &&
                reachabilityList is not null &&
                reachabilityList.Count > 0)
            {

                var reachability = reachabilityList.OrderBy(entry => entry.Priority).First();

                if (reachability.NetworkingHub.HasValue)
                {

                    var visitedIds = new HashSet<NetworkingNode_Id>();

                    do
                    {

                        if (reachability.NetworkingHub.HasValue)
                        {

                            visitedIds.Add(reachability.NetworkingHub.Value);

                            if (reachableNetworkingNodes.TryGetValue(reachability.NetworkingHub.Value, out var reachability2List) &&
                                reachability2List is not null &&
                                reachability2List.Count > 0)
                            {
                                reachability = reachability2List.OrderBy(entry => entry.Priority).First();
                            }

                            // Loop detected!
                            if (reachability.NetworkingHub.HasValue && visitedIds.Contains(reachability.NetworkingHub.Value))
                                break;

                        }

                    } while (reachability.EEBusWebSocketClient is null &&
                             reachability.EEBusWebSocketServer is null);

                }

                Reachability = reachability;
                return true;

            }

            Reachability = null;
            return false;

        }

        #endregion

        #region AddStaticRouting     (DestinationNodeId, WebSocketClient,        Priority = 0, Timestamp = null, Timeout = null)

        public void AddStaticRouting(NetworkingNode_Id     DestinationNodeId,
                                     IWebSocketClient  WebSocketClient,
                                     Byte?                 Priority    = 0,
                                     DateTime?             Timestamp   = null,
                                     DateTime?             Timeout     = null)
        {

            var reachability = new Reachability(
                                   DestinationNodeId,
                                   WebSocketClient,
                                   Priority,
                                   Timeout
                               );

            reachableNetworkingNodes.AddOrUpdate(

                DestinationNodeId,

                (id)                   => [reachability],

                (id, reachabilityList) => {

                    if (reachabilityList is null)
                        return [reachability];

                    else
                    {

                        // For thread-safety!
                        var updatedReachabilityList = new List<Reachability>();
                        updatedReachabilityList.AddRange(reachabilityList.Where(entry => entry.Priority != reachability.Priority));
                        updatedReachabilityList.Add     (reachability);

                        return updatedReachabilityList;

                    }

                }

            );

            //csmsChannel.Item1.AddStaticRouting(DestinationNodeId,
            //                                   NetworkingHubId);

        }

        #endregion

        #region AddStaticRouting     (DestinationNodeId, WebSocketServer,        Priority = 0, Timestamp = null, Timeout = null)

        public void AddStaticRouting(NetworkingNode_Id     DestinationNodeId,
                                     IWebSocketServer  WebSocketServer,
                                     Byte?                 Priority    = 0,
                                     DateTime?             Timestamp   = null,
                                     DateTime?             Timeout     = null)
        {

            var reachability = new Reachability(
                                   DestinationNodeId,
                                   WebSocketServer,
                                   Priority,
                                   Timeout
                               );

            reachableNetworkingNodes.AddOrUpdate(

                DestinationNodeId,

                (id)                   => [reachability],

                (id, reachabilityList) => {

                    if (reachabilityList is null)
                        return [reachability];

                    else
                    {

                        // For thread-safety!
                        var updatedReachabilityList = new List<Reachability>();
                        updatedReachabilityList.AddRange(reachabilityList.Where(entry => entry.Priority != reachability.Priority));
                        updatedReachabilityList.Add     (reachability);

                        return updatedReachabilityList;

                    }

                }

            );

            //csmsChannel.Item1.AddStaticRouting(DestinationNodeId,
            //                                   NetworkingHubId);

        }

        #endregion

        #region AddStaticRouting     (DestinationNodeId, NetworkingHubId,        Priority = 0, Timestamp = null, Timeout = null)

        public void AddStaticRouting(NetworkingNode_Id  DestinationNodeId,
                                     NetworkingNode_Id  NetworkingHubId,
                                     Byte?              Priority    = 0,
                                     DateTime?          Timestamp   = null,
                                     DateTime?          Timeout     = null)
        {

            var reachability = new Reachability(
                                   DestinationNodeId,
                                   NetworkingHubId,
                                   Priority,
                                   Timeout
                               );

            reachableNetworkingNodes.AddOrUpdate(

                DestinationNodeId,

                (id)                   => [reachability],

                (id, reachabilityList) => {

                    if (reachabilityList is null)
                        return [reachability];

                    else
                    {

                        // For thread-safety!
                        var updatedReachabilityList = new List<Reachability>();
                        updatedReachabilityList.AddRange(reachabilityList.Where(entry => entry.Priority != reachability.Priority));
                        updatedReachabilityList.Add     (reachability);

                        return updatedReachabilityList;

                    }

                }

            );

            //csmsChannel.Item1.AddStaticRouting(DestinationNodeId,
            //                                   NetworkingHubId);

        }

        #endregion

        #region RemoveStaticRouting  (DestinationNodeId, NetworkingHubId = null, Priority = 0)

        public void RemoveStaticRouting(NetworkingNode_Id   DestinationNodeId,
                                        NetworkingNode_Id?  NetworkingHubId   = null,
                                        Byte?               Priority          = 0)
        {

            if (!NetworkingHubId.HasValue)
            {
                reachableNetworkingNodes.TryRemove(DestinationNodeId, out _);
                return;
            }

            if (reachableNetworkingNodes.TryGetValue(DestinationNodeId, out var reachabilityList) &&
                reachabilityList is not null &&
                reachabilityList.Count > 0)
            {

                // For thread-safety!
                var updatedReachabilityList = new List<Reachability>(reachabilityList.Where(entry => entry.NetworkingHub == NetworkingHubId && (!Priority.HasValue || entry.Priority != (Priority ?? 0))));

                if (updatedReachabilityList.Count > 0)
                    reachableNetworkingNodes.TryUpdate(
                        DestinationNodeId,
                        updatedReachabilityList,
                        reachabilityList
                    );

                else
                    reachableNetworkingNodes.TryRemove(DestinationNodeId, out _);

            }

            //csmsChannel.Item1.RemoveStaticRouting(DestinationNodeId,
            //                                      NetworkingHubId);

        }

        #endregion


    }

}
