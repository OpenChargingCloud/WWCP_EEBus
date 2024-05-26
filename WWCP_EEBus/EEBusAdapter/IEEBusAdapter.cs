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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBus
{

    /// <summary>
    /// The common interface of all EEBus adapters.
    /// </summary>
    public interface IEEBusAdapter
    {

        #region Properties

        /// <summary>
        /// Incoming EEBus messages.
        /// </summary>
        IEEBusWebSocketAdapterIN       IN                             { get; }

        /// <summary>
        /// Outgoing EEBus messages.
        /// </summary>
        IEEBusWebSocketAdapterOUT      OUT                            { get; }

        /// <summary>
        /// Forwarded EEBus messages.
        /// </summary>
        IEEBusWebSocketAdapterFORWARD  FORWARD                        { get; }

        /// <summary>
        /// Disable all heartbeats.
        /// </summary>
        Boolean                        DisableSendHeartbeats          { get; set; }

        /// <summary>
        /// The time span between heartbeat requests.
        /// </summary>
        TimeSpan                       SendHeartbeatsEvery            { get; set; }

        /// <summary>
        /// The default request timeout for all requests.
        /// </summary>
        TimeSpan                       DefaultRequestTimeout          { get; }


        /// <summary>
        /// Return a new unique request identification.
        /// </summary>
        Request_Id                     NextRequestId                  { get; }

        #endregion

        #region Custom JSON serializer delegates

        #endregion

        #region Custom JSON parser delegates

        #endregion



        Task<SendWebSocketMessageResult> SendBinaryRequest       (BinaryRequestMessage     BinaryRequestMessage);
        Task<SendRequestState>           SendBinaryRequestAndWait(BinaryRequestMessage     BinaryRequestMessage);
        Task<SendWebSocketMessageResult> SendBinaryResponse      (BinaryResponseMessage    BinaryResponseMessage);

        Boolean ReceiveJSONResponse  (JSONResponseMessage    JSONResponseMessage);
        Boolean ReceiveBinaryResponse(BinaryResponseMessage  BinaryResponseMessage);
        Boolean ReceiveJSONRequestError         (JSONRequestErrorMessage       JSONErrorMessage);


    }

}
