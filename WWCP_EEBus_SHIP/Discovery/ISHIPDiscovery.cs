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

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// The discovery of SHIP nodes on the local network
    /// (SHIP TS 1.0.1, chapter 7: DNS-SD via mDNS, service type "_ship._tcp").
    ///
    /// Discovery is deliberately an interface: SHIP nodes have to be reachable
    /// without mDNS as well - by an explicitly configured address - because
    /// multicast is unavailable in many test rigs, containers and CI runners.
    /// </summary>
    public interface ISHIPDiscovery
    {

        /// <summary>
        /// All SHIP nodes currently visible on the network.
        /// </summary>
        IEnumerable<SHIPServiceInstance> DiscoveredServices { get; }

        /// <summary>
        /// A SHIP node appeared on the network, or its announcement changed.
        /// </summary>
        event Action<SHIPServiceInstance>? OnServiceDiscovered;

        /// <summary>
        /// A SHIP node disappeared from the network.
        /// </summary>
        event Action<SHIPServiceInstance>? OnServiceLost;


        /// <summary>
        /// Announce the local SHIP node on the network.
        /// </summary>
        /// <param name="Service">The local SHIP node.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task AnnounceAsync(SHIPServiceInstance  Service,
                           CancellationToken    CancellationToken   = default);

        /// <summary>
        /// Stop announcing the local SHIP node.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task UnannounceAsync(CancellationToken CancellationToken = default);

        /// <summary>
        /// Start searching for SHIP nodes.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task StartSearchAsync(CancellationToken CancellationToken = default);

        /// <summary>
        /// Stop searching for SHIP nodes.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        Task StopSearchAsync(CancellationToken CancellationToken = default);

    }

}
