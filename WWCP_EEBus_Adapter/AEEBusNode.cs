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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;

using cloud.charging.open.protocols.WWCP.OverlayNetworking;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    public abstract class AEEBusNode : AOverlayNetworkingNode,
                                       IEEBusNetworkingNode
    {

        #region Data

        #endregion

        #region Properties

        public IEEBusAdapter  EEBus    { get; }


        public IEnumerable<SHIPWebSocketClient> EEBusWebSocketClients
            => base.webSocketClients.Where(cl => cl is SHIPWebSocketClient).Cast<SHIPWebSocketClient>();

        public IEnumerable<SHIPWebSocketServer> EEBusWebSocketServers
            => base.webSocketServers.Where(cl => cl is SHIPWebSocketServer).Cast<SHIPWebSocketServer>();

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new networking node for testing.
        /// </summary>
        /// <param name="Id">The unique identification of this networking node.</param>
        public AEEBusNode(NetworkingNode_Id  Id,
                          I18NString?        Description                 = null,

                          SignaturePolicy?   SignaturePolicy             = null,
                          SignaturePolicy?   ForwardingSignaturePolicy   = null,

                          Boolean            DisableSendHeartbeats       = false,
                          TimeSpan?          SendHeartbeatsEvery         = null,
                          TimeSpan?          DefaultRequestTimeout       = null,

                          Boolean            DisableMaintenanceTasks     = false,
                          TimeSpan?          MaintenanceEvery            = null,
                          DNSClient?         DNSClient                   = null)

            : base(Id,
                   Description,
                   SignaturePolicy,
                   ForwardingSignaturePolicy,
                   DisableSendHeartbeats,
                   SendHeartbeatsEvery,
                   DefaultRequestTimeout,
                   DisableMaintenanceTasks,
                   MaintenanceEvery,
                   DNSClient)

        {

            this.EEBus  = new EEBusAdapter(
                              this,
                              DisableSendHeartbeats,
                              SendHeartbeatsEvery,
                              DefaultRequestTimeout,
                              SignaturePolicy,
                              ForwardingSignaturePolicy
                          );

        }

        #endregion



        public Byte[] GetEncryptionKey(NetworkingNode_Id  DestinationNodeId,
                                       UInt16?            KeyId   = null)
        {
            return "5a733d6660df00c447ff184ae971e1d5bba5de5784768795ee6535867130aa12".FromHEX();
        }

        public Byte[] GetDecryptionKey(NetworkingNode_Id  SourceNodeId,
                                       UInt16?            KeyId   = null)
        {
            return "5a733d6660df00c447ff184ae971e1d5bba5de5784768795ee6535867130aa12".FromHEX();
        }


        public UInt64 GetEncryptionNonce(NetworkingNode_Id  DestinationNodeId,
                                         UInt16?            KeyId   = null)
        {
            return 1;
        }

        public UInt64 GetEncryptionCounter(NetworkingNode_Id  DestinationNodeId,
                                           UInt16?            KeyId   = null)
        {
            return 1;
        }



        #region (virtual) HandleErrors(Module, Caller, ErrorResponse)

        public virtual Task HandleErrors(String  Module,
                                         String  Caller,
                                         String  ErrorResponse)
        {

            return Task.CompletedTask;

        }

        #endregion

        #region (virtual) HandleErrors(Module, Caller, ExceptionOccured)

        public virtual Task HandleErrors(String     Module,
                                         String     Caller,
                                         Exception  ExceptionOccured)
        {

            return Task.CompletedTask;

        }

        #endregion


    }

}
