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

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBus
{

    public delegate Task OnBinaryMessageRequestSentDelegate   (DateTimeOffset             Timestamp,
                                                               IEEBusWebSocketAdapterOUT  Server,
                                                               BinaryRequestMessage       BinaryRequestMessage);

    public delegate Task OnBinaryMessageResponseSentDelegate  (DateTimeOffset             Timestamp,
                                                               IEEBusWebSocketAdapterOUT  Server,
                                                               BinaryResponseMessage      BinaryResponseMessage);

    //public delegate Task OnBinaryErrorMessageSentDelegate     (DateTime                    Timestamp,
    //                                                           IEEBusWebSocketAdapterOUT    Server,
    //                                                           EEBus_BinaryErrorMessage     BinaryErrorMessage);


    /// <summary>
    /// The common interface of all outgoing EEBus WebSocket adapters.
    /// </summary>
    public interface IEEBusWebSocketAdapterOUT
    {

        #region Generic Binary Messages

        /// <summary>
        /// An event sent whenever a binary request was sent.
        /// </summary>
        event OnBinaryMessageRequestSentDelegate?        OnBinaryMessageRequestSent;

        /// <summary>
        /// An event sent whenever a binary response was sent.
        /// </summary>
        event OnBinaryMessageResponseSentDelegate?       OnBinaryMessageResponseSent;

        ///// <summary>
        ///// An event sent whenever a binary error response was sent.
        ///// </summary>
        //event OnWebSocketBinaryErrorResponseDelegate?    OnBinaryErrorResponseSent;

        #endregion


        Task NotifyBinaryMessageResponseSent(BinaryResponseMessage BinaryResponseMessage);


    }

}
