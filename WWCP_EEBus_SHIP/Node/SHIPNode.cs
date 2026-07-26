/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    #region (enum) SHIPPairingStates

    /// <summary>
    /// How far the pairing with a communication partner has come
    /// (SHIP TS 1.0.1, chapter 12.3).
    /// </summary>
    public enum SHIPPairingStates
    {

        /// <summary>
        /// The communication partner is unknown.
        /// </summary>
        None,

        /// <summary>
        /// A connection to the communication partner is being established.
        /// </summary>
        Initiated,

        /// <summary>
        /// The communication partner is waiting for our trust decision.
        /// </summary>
        ReceivedPairingRequest,

        /// <summary>
        /// The communication partner is trusted.
        /// </summary>
        Trusted,

        /// <summary>
        /// The communication partner was rejected.
        /// </summary>
        Rejected

    }

    #endregion


    /// <summary>
    /// A SHIP node: it accepts connections of other SHIP nodes, opens
    /// connections to them, and keeps exactly one connection per communication
    /// partner (SHIP TS 1.0.1, chapters 4 and 12.2.2).
    ///
    /// The node is transport agnostic: the WebSocket layer hands an established
    /// transport - together with the SKI authenticated during the TLS handshake -
    /// over to <see cref="AcceptAsync"/> or <see cref="ConnectAsync"/>.
    /// </summary>
    public class SHIPNode
    {

        #region Data

        private readonly ConcurrentDictionary<SKI, SHIPConnection>     connections   = new ();
        private readonly ConcurrentDictionary<SKI, SHIPPairingStates>  pairingStates = new ();
        private readonly ISHIPTrustStore                               trustStore;
        private readonly TimeProvider                                  timeProvider;

        #endregion

        #region Properties

        /// <summary>
        /// The SKI of this SHIP node, taken from its certificate.
        /// </summary>
        public SKI              SKI            { get; }

        /// <summary>
        /// The SHIP identifier of this node, announced within the access methods.
        /// </summary>
        public SHIP_Id          SHIPId         { get; }

        /// <summary>
        /// Whether this node accepts every communication partner without asking
        /// the user - which is what a device does while it is in its pairing mode.
        /// </summary>
        public Boolean          AutoAccept     { get; set; }

        /// <summary>
        /// The timeouts used for all connections of this node.
        /// </summary>
        public SHIPTimeouts     Timeouts       { get; }

        /// <summary>
        /// The SKIs this node trusts.
        /// </summary>
        public ISHIPTrustStore  TrustStore
            => trustStore;

        /// <summary>
        /// All connections of this node, one per communication partner.
        /// </summary>
        public IEnumerable<SHIPConnection> Connections
            => connections.Values;

        #endregion

        #region Events

        /// <summary>
        /// The SHIP handshake with a communication partner completed.
        /// </summary>
        public event Action<SHIPNode, SHIPConnection>?          OnConnected;

        /// <summary>
        /// The connection to a communication partner ended.
        /// </summary>
        public event Action<SHIPNode, SKI, String?>?            OnDisconnected;

        /// <summary>
        /// A communication partner is waiting for a trust decision of the user.
        /// </summary>
        public event Action<SHIPNode, SKI>?                     OnPairingRequest;

        /// <summary>
        /// A SPINE datagram was received from a communication partner.
        /// </summary>
        public event Action<SHIPNode, SKI, JObject>?            OnSPINEDataReceived;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SHIP node.
        /// </summary>
        /// <param name="SKI">The SKI of this SHIP node.</param>
        /// <param name="SHIPId">The SHIP identifier of this node.</param>
        /// <param name="TrustStore">The SKIs this node trusts.</param>
        /// <param name="AutoAccept">Whether to accept every communication partner without asking.</param>
        /// <param name="Timeouts">The timeouts used for all connections.</param>
        /// <param name="TimeProvider">The time provider driving all protocol timers.</param>
        public SHIPNode(SKI               SKI,
                        SHIP_Id           SHIPId,
                        ISHIPTrustStore?  TrustStore     = null,
                        Boolean           AutoAccept     = false,
                        SHIPTimeouts?     Timeouts       = null,
                        TimeProvider?     TimeProvider   = null)
        {

            this.SKI           = SKI;
            this.SHIPId        = SHIPId;
            this.trustStore    = TrustStore   ?? new InMemoryTrustStore();
            this.AutoAccept    = AutoAccept;
            this.Timeouts      = Timeouts     ?? SHIPTimeouts.Default;
            this.timeProvider  = TimeProvider ?? System.TimeProvider.System;

        }

        #endregion


        #region ConnectAsync(RemoteSKI, Transport, CancellationToken = default)

        /// <summary>
        /// Use the given outgoing transport for a connection to the communication
        /// partner and start the SHIP handshake.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of the communication partner, as authenticated during the TLS handshake.</param>
        /// <param name="Transport">The established transport.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SHIPConnection?> ConnectAsync(SKI                RemoteSKI,
                                                  ISHIPTransport     Transport,
                                                  CancellationToken  CancellationToken   = default)

            => StartConnectionAsync(SHIPRoles.Client, RemoteSKI, Transport, CancellationToken);

        #endregion

        #region AcceptAsync (RemoteSKI, Transport, CancellationToken = default)

        /// <summary>
        /// Use the given incoming transport for a connection of the communication
        /// partner and start the SHIP handshake.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of the communication partner, as authenticated during the TLS handshake.</param>
        /// <param name="Transport">The established transport.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SHIPConnection?> AcceptAsync(SKI                RemoteSKI,
                                                 ISHIPTransport     Transport,
                                                 CancellationToken  CancellationToken   = default)

            => StartConnectionAsync(SHIPRoles.Server, RemoteSKI, Transport, CancellationToken);

        #endregion

        #region KeepThisConnection(RemoteSKI, Incoming)

        /// <summary>
        /// Decide whether a further connection to a communication partner we are
        /// already connected to may be kept (SHIP TS 1.0.1, chapter 12.2.2).
        ///
        /// The specification resolves such a double connection by comparing the
        /// SKI values. Following the Go reference implementation - and therefore
        /// every device tested against it - the connection *initiated by the
        /// higher SKI* wins: an incoming connection is kept if the communication
        /// partner has the higher SKI, an outgoing one if we have.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of the communication partner.</param>
        /// <param name="Incoming">Whether the new connection was opened by the communication partner.</param>
        public Boolean KeepThisConnection(SKI      RemoteSKI,
                                          Boolean  Incoming)
        {

            // No double connection at all.
            if (!connections.ContainsKey(RemoteSKI))
                return true;

            return Incoming
                       ? RemoteSKI > SKI
                       : SKI       > RemoteSKI;

        }

        #endregion


        #region PairingStateOf   (RemoteSKI)

        /// <summary>
        /// The pairing state of the given communication partner.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of a communication partner.</param>
        public SHIPPairingStates PairingStateOf(SKI RemoteSKI)

            => trustStore.IsTrusted(RemoteSKI)
                   ? SHIPPairingStates.Trusted
                   : pairingStates.TryGetValue(RemoteSKI, out var state)
                         ? state
                         : SHIPPairingStates.None;

        #endregion

        #region ApprovePairingAsync(RemoteSKI, CancellationToken = default)

        /// <summary>
        /// The user accepted the given communication partner: remember the
        /// decision and continue a connection which is waiting for it.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of a communication partner.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task ApprovePairingAsync(SKI                RemoteSKI,
                                              CancellationToken  CancellationToken   = default)
        {

            await trustStore.TrustAsync(RemoteSKI);

            pairingStates[RemoteSKI] = SHIPPairingStates.Trusted;

            if (connections.TryGetValue(RemoteSKI, out var connection))
                await connection.ApproveTrustAsync(CancellationToken);

        }

        #endregion

        #region RejectPairingAsync (RemoteSKI, CancellationToken = default)

        /// <summary>
        /// The user rejected the given communication partner: forget the trust
        /// and close a running connection.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of a communication partner.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task RejectPairingAsync(SKI                RemoteSKI,
                                             CancellationToken  CancellationToken   = default)
        {

            await trustStore.DistrustAsync(RemoteSKI);

            pairingStates[RemoteSKI] = SHIPPairingStates.Rejected;

            if (connections.TryRemove(RemoteSKI, out var connection))
                await connection.CloseAsync(ConnectionCloseReasons.RemovedConnection, CancellationToken);

        }

        #endregion

        #region DisconnectAsync    (RemoteSKI, Reason = null, CancellationToken = default)

        /// <summary>
        /// Close the connection to the given communication partner.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of a communication partner.</param>
        /// <param name="Reason">An optional reason.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task DisconnectAsync(SKI                      RemoteSKI,
                                          ConnectionCloseReasons?  Reason              = null,
                                          CancellationToken        CancellationToken   = default)
        {

            if (connections.TryRemove(RemoteSKI, out var connection))
                await connection.CloseAsync(Reason, CancellationToken);

        }

        #endregion

        #region SendSPINEDataAsync (RemoteSKI, Datagram, CancellationToken = default)

        /// <summary>
        /// Send the given SPINE datagram to the given communication partner.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of a communication partner.</param>
        /// <param name="Datagram">A SPINE datagram.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task SendSPINEDataAsync(SKI                RemoteSKI,
                                             JObject            Datagram,
                                             CancellationToken  CancellationToken   = default)
        {

            if (!connections.TryGetValue(RemoteSKI, out var connection))
                throw new InvalidOperationException($"There is no connection to the SHIP node '{RemoteSKI}'!");

            await connection.SendSPINEDataAsync(Datagram, CancellationToken);

        }

        #endregion


        #region (private) StartConnectionAsync(Role, RemoteSKI, Transport, CancellationToken)

        private async Task<SHIPConnection?> StartConnectionAsync(SHIPRoles          Role,
                                                                 SKI                RemoteSKI,
                                                                 ISHIPTransport     Transport,
                                                                 CancellationToken  CancellationToken)
        {

            // A SHIP node must never talk to itself.
            if (RemoteSKI == SKI)
            {
                await Transport.CloseAsync("A SHIP node cannot connect to itself!", CancellationToken);
                return null;
            }

            #region Double connections, chapter 12.2.2

            if (connections.TryGetValue(RemoteSKI, out var existingConnection))
            {

                if (!KeepThisConnection(RemoteSKI, Role == SHIPRoles.Server))
                {
                    await Transport.CloseAsync("An existing connection to this SHIP node is kept.", CancellationToken);
                    return null;
                }

                // The new connection wins, so the older one has to go.
                connections.TryRemove(RemoteSKI, out _);

                await existingConnection.CloseAsync(ConnectionCloseReasons.RemovedConnection, CancellationToken);

            }

            #endregion

            var connection = new SHIPConnection(
                                 Role,
                                 RemoteSKI,
                                 SHIPId,
                                 Transport,
                                 new NodeTrustProvider(this, RemoteSKI),
                                 Timeouts,
                                 timeProvider
                             );

            connection.OnCompleted          += completedConnection => {
                pairingStates[RemoteSKI] = SHIPPairingStates.Trusted;
                OnConnected?.Invoke(this, completedConnection);
            };

            connection.OnSPINEDataReceived  += (_, datagram) => OnSPINEDataReceived?.Invoke(this, RemoteSKI, datagram);

            connection.OnClosed             += (_, reason) => {
                connections.TryRemove(RemoteSKI, out SHIPConnection? _);
                OnDisconnected?.Invoke(this, RemoteSKI, reason);
            };

            connections[RemoteSKI] = connection;

            if (PairingStateOf(RemoteSKI) == SHIPPairingStates.None)
                pairingStates[RemoteSKI] = SHIPPairingStates.Initiated;

            await connection.StartAsync(CancellationToken);

            return connection;

        }

        #endregion

        #region (private class) NodeTrustProvider

        /// <summary>
        /// Answers the trust questions of a connection out of the trust store,
        /// the auto accept mode and the pairing state of the node.
        /// </summary>
        private class NodeTrustProvider(SHIPNode  Node,
                                        SKI       RemoteSKI) : ISHIPTrustProvider
        {

            public Boolean IsTrusted(SKI SKI)
            {

                if (Node.trustStore.IsTrusted(SKI))
                    return true;

                // A device in its pairing mode accepts everybody - which is the
                // only way an initial pairing can happen without a user interface.
                if (Node.AutoAccept)
                {
                    _ = Node.trustStore.TrustAsync(SKI);
                    return true;
                }

                return false;

            }

            public Boolean AllowWaitingForTrust(SKI SKI)
            {

                if (Node.PairingStateOf(SKI) == SHIPPairingStates.Rejected)
                    return false;

                // Tell the application that somebody is waiting; without anybody
                // listening there is no point in waiting at all.
                var onPairingRequest = Node.OnPairingRequest;

                if (onPairingRequest is null)
                    return false;

                if (Node.PairingStateOf(SKI) != SHIPPairingStates.ReceivedPairingRequest)
                {
                    Node.pairingStates[RemoteSKI] = SHIPPairingStates.ReceivedPairingRequest;
                    onPairingRequest.Invoke(Node, SKI);
                }

                return true;

            }

        }

        #endregion

    }

}
