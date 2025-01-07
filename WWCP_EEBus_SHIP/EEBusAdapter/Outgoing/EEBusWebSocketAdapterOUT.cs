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

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBus
{

    /// <summary>
    /// The EEBus adapter for sending messages.
    /// </summary>
    public partial class EEBusWebSocketAdapterOUT : IEEBusWebSocketAdapterOUT
    {

        #region Data

        private readonly IEEBusNetworkingNode parentNetworkingNode;

        #endregion

        #region Events

        #region Generic Binary Messages

        /// <summary>
        /// An event sent whenever a binary request was sent.
        /// </summary>
        public event OnBinaryMessageRequestSentDelegate?        OnBinaryMessageRequestSent;

        /// <summary>
        /// An event sent whenever a binary response was sent.
        /// </summary>
        public event OnBinaryMessageResponseSentDelegate?       OnBinaryMessageResponseSent;

        ///// <summary>
        ///// An event sent whenever a binary error response was sent.
        ///// </summary>
        //public event OnWebSocketBinaryErrorResponseDelegate?    OnBinaryErrorResponseSent;

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EEBus adapter for sending outgoing messages.
        /// </summary>
        /// <param name="NetworkingNode">The parent networking node.</param>
        public EEBusWebSocketAdapterOUT(IEEBusNetworkingNode NetworkingNode)
        {

            this.parentNetworkingNode = NetworkingNode;

        }

        #endregion


        // Send requests...

        #region SendBinaryRequest       (BinaryRequestMessage)

        /// <summary>
        /// Send (and forget) the given Binary.
        /// </summary>
        /// <param name="BinaryRequestMessage">A EEBus Binary request.</param>
        public async Task<SendWebSocketMessageResult> SendBinaryRequest(BinaryRequestMessage BinaryRequestMessage)
        {

            #region OnBinaryMessageRequestSent

            var logger = OnBinaryMessageRequestSent;
            if (logger is not null)
            {
                try
                {

                    await Task.WhenAll(logger.GetInvocationList().
                                           OfType<OnBinaryMessageRequestSentDelegate>().
                                           Select(loggingDelegate => loggingDelegate.Invoke(
                                                                         Timestamp.Now,
                                                                         this,
                                                                         BinaryRequestMessage
                                                                     )).
                                           ToArray());

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(EEBusWebSocketAdapterOUT) + "." + nameof(OnBinaryMessageRequestSent));
                }
            }

            #endregion

            return await parentNetworkingNode.EEBus.SendBinaryRequest(BinaryRequestMessage);

        }

        #endregion

        #region SendBinaryRequestAndWait(BinaryRequestMessage)

        /// <summary>
        /// Send (and forget) the given Binary.
        /// </summary>
        /// <param name="BinaryRequestMessage">A EEBus Binary request.</param>
        public async Task<SendRequestState> SendBinaryRequestAndWait(BinaryRequestMessage BinaryRequestMessage)
        {

            #region OnBinaryMessageRequestSent

            var logger = OnBinaryMessageRequestSent;
            if (logger is not null)
            {
                try
                {

                    await Task.WhenAll(logger.GetInvocationList().
                                           OfType<OnBinaryMessageRequestSentDelegate>().
                                           Select(loggingDelegate => loggingDelegate.Invoke(
                                                                         Timestamp.Now,
                                                                         this,
                                                                         BinaryRequestMessage
                                                                     )).
                                           ToArray());

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(EEBusWebSocketAdapterOUT) + "." + nameof(OnBinaryMessageRequestSent));
                }
            }

            #endregion

            return await parentNetworkingNode.EEBus.SendBinaryRequestAndWait(BinaryRequestMessage);

        }

        #endregion


        // Response events...

        #region NotifyBinaryMessageResponseSent(BinaryResponseMessage)


        public async Task NotifyBinaryMessageResponseSent(BinaryResponseMessage BinaryResponseMessage)
        {

            var logger = OnBinaryMessageResponseSent;
            if (logger is not null)
            {
                try
                {

                    await Task.WhenAll(logger.GetInvocationList().
                                           OfType<OnBinaryMessageResponseSentDelegate>().
                                           Select(loggingDelegate => loggingDelegate.Invoke(
                                                                         Timestamp.Now,
                                                                         this,
                                                                         BinaryResponseMessage
                                                                     )).
                                           ToArray());

                }
                catch (Exception e)
                {
                    DebugX.Log(e, nameof(EEBusWebSocketAdapterOUT) + "." + nameof(OnBinaryMessageRequestSent));
                }
            }

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
