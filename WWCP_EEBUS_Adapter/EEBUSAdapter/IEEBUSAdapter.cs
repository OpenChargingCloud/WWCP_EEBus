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

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBUS
{

    /// <summary>
    /// The common interface of all EEBUS adapters.
    /// </summary>
    public interface IEEBUSAdapter
    {

        #region Properties

        /// <summary>
        /// Incoming EEBUS messages.
        /// </summary>
        IEEBUSWebSocketAdapterIN       IN                             { get; }

        /// <summary>
        /// Outgoing EEBUS messages.
        /// </summary>
        IEEBUSWebSocketAdapterOUT      OUT                            { get; }

        /// <summary>
        /// Forwarded EEBUS messages.
        /// </summary>
        IEEBUSWebSocketAdapterFORWARD  FORWARD                        { get; }

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
