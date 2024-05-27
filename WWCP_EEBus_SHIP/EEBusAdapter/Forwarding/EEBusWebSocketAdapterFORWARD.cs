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

using System.Reflection;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;
using cloud.charging.open.protocols.WWCP.OverlayNetworking.WebSockets;

#endregion

namespace cloud.charging.open.protocols.EEBus
{

    /// <summary>
    /// The EEBus adapter for forwarding messages.
    /// </summary>
    public partial class EEBusWebSocketAdapterFORWARD : IEEBusWebSocketAdapterFORWARD
    {

        #region Data

        private   readonly  IEEBusNetworkingNode                            parentNetworkingNode;

        protected readonly  Dictionary<String, MethodInfo>                  forwardingMessageProcessorsLookup   = [];

        protected readonly  ConcurrentDictionary<Request_Id, ResponseInfo>  expectedResponses                   = [];

        #endregion

        #region Properties

        public ForwardingResult            DefaultResult        { get; set; } = ForwardingResult.DROP;

        public HashSet<NetworkingNode_Id>  AnycastIdsAllowed    { get; }      = [];

        public HashSet<NetworkingNode_Id>  AnycastIdsDenied     { get; }      = [];

        #endregion

        #region Events

        public event OnJSONRequestMessageSentLoggingDelegate?       OnJSONRequestMessageSent;
        public event OnJSONResponseMessageSentLoggingDelegate?      OnJSONResponseMessageSent;
        public event OnJSONRequestErrorMessageSentLoggingDelegate?  OnJSONRequestErrorMessageSent;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EEBus adapter for forwarding messages.
        /// </summary>
        /// <param name="ParentNetworkingNode">The parent networking node.</param>
        /// <param name="DefaultResult">The default forwarding result.</param>
        public EEBusWebSocketAdapterFORWARD(IEEBusNetworkingNode  ParentNetworkingNode,
                                            ForwardingResult      DefaultResult = ForwardingResult.DROP)
        {

            this.parentNetworkingNode  = ParentNetworkingNode;
            this.DefaultResult         = DefaultResult;

            #region Reflect "Forward_XXX" messages and wire them...

            foreach (var method in typeof(EEBusWebSocketAdapterFORWARD).
                                       GetMethods(BindingFlags.Public | BindingFlags.Instance).
                                            Where(method => method.Name.StartsWith("Forward_")))
            {

                var processorName = method.Name[8..];

                if (forwardingMessageProcessorsLookup.ContainsKey(processorName))
                    throw new ArgumentException("Duplicate processor name: " + processorName);

                forwardingMessageProcessorsLookup.Add(processorName,
                                                      method);

            }

            #endregion

        }

        #endregion


        public NetworkingNode_Id? GetForwardedNodeId(Request_Id RequestId)
        {

            if (expectedResponses.TryGetValue(RequestId, out var responseInfo))
                return responseInfo.SourceNodeId;

            return null;

        }


        #region ProcessBinaryRequestMessage (BinaryRequestMessage)

        public async Task ProcessBinaryRequestMessage(BinaryRequestMessage BinaryRequestMessage)
        {

            if (AnycastIdsAllowed.Count > 0 && !AnycastIdsAllowed.Contains(BinaryRequestMessage.DestinationId))
                return;

            if (AnycastIdsDenied. Count > 0 &&  AnycastIdsDenied. Contains(BinaryRequestMessage.DestinationId))
                return;

            await parentNetworkingNode.EEBus.SendBinaryRequest(BinaryRequestMessage);

        }

        #endregion

        #region ProcessBinaryResponseMessage(BinaryResponseMessage)

        public async Task ProcessBinaryResponseMessage(BinaryResponseMessage BinaryResponseMessage)
        {

            if (expectedResponses.TryRemove(BinaryResponseMessage.RequestId, out var responseInfo))
            {

                if (responseInfo.Timeout <= Timestamp.Now)
                //responseInfo.Context == JSONResponseMessage.Context)
                {

                    await parentNetworkingNode.EEBus.SendBinaryResponse(BinaryResponseMessage);

                }
                else
                    DebugX.Log($"Received a binary response message too late for request identification: {BinaryResponseMessage.RequestId}!");

            }
            else
                DebugX.Log($"Received a binary response message for an unknown request identification: {BinaryResponseMessage.RequestId}!");

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
