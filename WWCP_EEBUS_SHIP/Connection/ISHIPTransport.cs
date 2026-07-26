/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// The role a SHIP node has taken within a connection
    /// (SHIP TS 1.0.1, chapter 13.4.2).
    ///
    /// A SHIP node can act in both roles at the same time - towards different
    /// communication partners, or even towards the same one while a double
    /// connection is being resolved.
    /// </summary>
    public enum SHIPRoles
    {

        /// <summary>
        /// This node opened the connection.
        /// </summary>
        Client,

        /// <summary>
        /// This node accepted the connection.
        /// </summary>
        Server

    }


    /// <summary>
    /// The transport a SHIP connection runs on: on a real device a binary
    /// WebSocket over TLS, within tests an in-memory pipe.
    /// </summary>
    public interface ISHIPTransport
    {

        /// <summary>
        /// Whether the transport has been closed.
        /// </summary>
        Boolean IsClosed { get; }

        /// <summary>
        /// Send the given SHIP frame as a single binary message.
        /// </summary>
        /// <param name="Frame">The binary representation of a SHIP frame.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task SendAsync(Byte[]             Frame,
                       CancellationToken  CancellationToken   = default);

        /// <summary>
        /// Close the underlying transport.
        /// </summary>
        /// <param name="Reason">An optional reason.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task CloseAsync(String?            Reason              = null,
                        CancellationToken  CancellationToken   = default);

    }


    /// <summary>
    /// What the application has to tell a SHIP connection about the trust
    /// relationship with a communication partner (SHIP TS 1.0.1, chapter 13.4.4.1).
    /// </summary>
    public interface ISHIPTrustProvider
    {

        /// <summary>
        /// Whether the given communication partner is already trusted, i.e. the
        /// user has accepted its SKI before. Only then a connection may announce
        /// itself as "ready" right away.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of the communication partner.</param>
        Boolean IsTrusted(SKI RemoteSKI);

        /// <summary>
        /// Whether this node is currently willing to wait for a trust decision of
        /// the user - e.g. because a pairing dialog is open or the node is in an
        /// auto accept mode. If not, a pending connection is aborted instead of
        /// being prolonged.
        /// </summary>
        /// <param name="RemoteSKI">The SKI of the communication partner.</param>
        Boolean AllowWaitingForTrust(SKI RemoteSKI);

    }

}
