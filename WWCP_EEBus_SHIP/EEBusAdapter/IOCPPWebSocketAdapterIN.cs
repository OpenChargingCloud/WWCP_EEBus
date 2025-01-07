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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBus
{

    public delegate Task OnBinaryMessageRequestReceivedDelegate (DateTime                    Timestamp,
                                                                 IEEBusWebSocketAdapterIN     Server,
                                                                 BinaryRequestMessage   BinaryRequestMessage);

    public delegate Task OnBinaryMessageResponseReceivedDelegate(DateTime                    Timestamp,
                                                                 IEEBusWebSocketAdapterIN     Server,
                                                                 BinaryResponseMessage  BinaryResponseMessage);

    //public delegate Task OnBinaryErrorResponseReceivedDelegate  (DateTime                    Timestamp,
    //                                                             IEEBusWebSocketAdapterIN     Server,
    //                                                             EEBus_BinaryErrorMessage     BinaryErrorMessage);


    /// <summary>
    /// The common interface of all incoming EEBus WebSocket adapters.
    /// </summary>
    public interface IEEBusWebSocketAdapterIN
    {

        #region Properties

        HashSet<NetworkingNode_Id> AnycastIds { get; }

        #endregion

        #region Events

        #region Generic Binary Messages

        /// <summary>
        /// An event sent whenever a binary request was received.
        /// </summary>
        event OnBinaryMessageRequestReceivedDelegate?     OnBinaryMessageRequestReceived;

        /// <summary>
        /// An event sent whenever a binary response was received.
        /// </summary>
        event OnBinaryMessageResponseReceivedDelegate?    OnBinaryMessageResponseReceived;

        ///// <summary>
        ///// An event sent whenever a binary error response was received.
        ///// </summary>
        //event OnBinaryErrorResponseReceivedDelegate?      OnBinaryErrorResponseReceived;

        #endregion


        #region HTTP WebSocket connection management

        ///// <summary>
        ///// An event sent whenever the HTTP connection switched successfully to web socket.
        ///// </summary>
        //event OnNetworkingNodeNewWebSocketConnectionDelegate?    OnNetworkingNodeNewWebSocketConnection;

        ///// <summary>
        ///// An event sent whenever a web socket close frame was received.
        ///// </summary>
        //event OnNetworkingNodeCloseMessageReceivedDelegate?      OnNetworkingNodeCloseMessageReceived;

        ///// <summary>
        ///// An event sent whenever a TCP connection was closed.
        ///// </summary>
        //event OnNetworkingNodeTCPConnectionClosedDelegate?       OnNetworkingNodeTCPConnectionClosed;

        #endregion

        #endregion


        Task<WebSocketBinaryMessageResponse> ProcessBinaryMessage(DateTime              RequestTimestamp,
                                                                  IWebSocketConnection  WebSocketConnection,
                                                                  Byte[]                BinaryMessage,
                                                                  EventTracking_Id      EventTrackingId,
                                                                  CancellationToken     CancellationToken);


    }

}
