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

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBus
{


    /// <summary>
    /// A filtered JSON request message.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the request message.</param>
    /// <param name="Sender">The sender of the request message.</param>
    /// <param name="JSONRequestMessage">The JSON request message.</param>
    /// <param name="SendEEBusMessageResult">The result of the sending attempt.</param>
    public delegate Task

        OnJSONRequestMessageSentLoggingDelegate(DateTime                 Timestamp,
                                                IEventSender             Sender,
                                                JSONRequestMessage  JSONRequestMessage,
                                                SendWebSocketMessageResult    SendEEBusMessageResult);

    /// <summary>
    /// A filtered JSON response message.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the response message.</param>
    /// <param name="Sender">The sender of the response message.</param>
    /// <param name="JSONResponseMessage">The JSON response message.</param>
    /// <param name="SendEEBusMessageResult">The result of the sending attempt.</param>
    public delegate Task

        OnJSONResponseMessageSentLoggingDelegate(DateTime                  Timestamp,
                                                 IEventSender              Sender,
                                                 JSONResponseMessage  JSONResponseMessage,
                                                 SendWebSocketMessageResult     SendEEBusMessageResult);


    /// <summary>
    /// A filtered JSON request error message.
    /// </summary>
    /// <param name="Timestamp">The timestamp of the response message.</param>
    /// <param name="Sender">The sender of the response message.</param>
    /// <param name="JSONRequestErrorMessage">The JSON request error message.</param>
    /// <param name="SendEEBusMessageResult">The result of the sending attempt.</param>
    public delegate Task

        OnJSONRequestErrorMessageSentLoggingDelegate(DateTime                      Timestamp,
                                                     IEventSender                  Sender,
                                                     JSONRequestErrorMessage  JSONRequestErrorMessage,
                                                     SendWebSocketMessageResult         SendEEBusMessageResult);



    public interface IEEBusWebSocketAdapterFORWARD
    {

        ForwardingResult            DefaultResult        { get; set; }

        HashSet<NetworkingNode_Id>  AnycastIdsAllowed    { get; }

        HashSet<NetworkingNode_Id>  AnycastIdsDenied     { get; }

        #region Events


        #endregion


        NetworkingNode_Id? GetForwardedNodeId(Request_Id RequestId);


        Task ProcessBinaryRequestMessage   (BinaryRequestMessage     BinaryRequestMessage);
        Task ProcessBinaryResponseMessage  (BinaryResponseMessage    BinaryResponseMessage);


    }

}
