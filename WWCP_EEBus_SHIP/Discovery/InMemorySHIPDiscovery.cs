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

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// A discovery which does not use the network: all SHIP nodes sharing one
    /// instance of this class see each other.
    ///
    /// This is what conformance and interoperability tests use where multicast
    /// is unavailable - and what a SHIP node falls back to when discovery is
    /// switched off and the communication partners are configured explicitly.
    /// </summary>
    /// <param name="SharedNetwork">An optional network shared with other discoveries.</param>
    public class InMemorySHIPDiscovery(InMemorySHIPDiscovery.Network? SharedNetwork = null) : ISHIPDiscovery
    {

        #region (class) Network

        /// <summary>
        /// The "network" all connected discoveries announce into.
        /// </summary>
        public class Network
        {

            private readonly ConcurrentDictionary<SKI, SHIPServiceInstance>  services     = new ();
            private readonly List<InMemorySHIPDiscovery>                     discoveries  = [];

            internal void Register(InMemorySHIPDiscovery Discovery)
            {
                lock (discoveries)
                    discoveries.Add(Discovery);
            }

            internal IEnumerable<SHIPServiceInstance> Services
                => services.Values;

            internal void Announce(SHIPServiceInstance Service)
            {

                services[Service.SKI] = Service;

                lock (discoveries)
                {
                    foreach (var discovery in discoveries)
                        discovery.Discovered(Service);
                }

            }

            internal void Unannounce(SHIPServiceInstance Service)
            {

                services.TryRemove(Service.SKI, out _);

                lock (discoveries)
                {
                    foreach (var discovery in discoveries)
                        discovery.Lost(Service);
                }

            }

        }

        #endregion

        #region Data

        private readonly Network              network   = SharedNetwork ?? new Network();
        private          SHIPServiceInstance? announcedService;
        private          Boolean              searching;

        #endregion

        #region Properties

        /// <summary>
        /// All SHIP nodes currently visible - except the local one.
        /// </summary>
        public IEnumerable<SHIPServiceInstance> DiscoveredServices

            => searching
                   ? network.Services.Where(service => announcedService is null || service.SKI != announcedService.SKI)
                   : [];

        /// <summary>
        /// The network this discovery announces into.
        /// </summary>
        public Network                          SharedNetwork
            => network;

        #endregion

        #region Events

        /// <summary>
        /// A SHIP node appeared.
        /// </summary>
        public event Action<SHIPServiceInstance>? OnServiceDiscovered;

        /// <summary>
        /// A SHIP node disappeared.
        /// </summary>
        public event Action<SHIPServiceInstance>? OnServiceLost;

        #endregion


        #region AnnounceAsync   (Service, CancellationToken = default)

        public Task AnnounceAsync(SHIPServiceInstance  Service,
                                  CancellationToken    CancellationToken   = default)
        {

            announcedService = Service;
            network.Announce(Service);

            return Task.CompletedTask;

        }

        #endregion

        #region UnannounceAsync (CancellationToken = default)

        public Task UnannounceAsync(CancellationToken CancellationToken = default)
        {

            if (announcedService is not null)
            {
                network.Unannounce(announcedService);
                announcedService = null;
            }

            return Task.CompletedTask;

        }

        #endregion

        #region StartSearchAsync(CancellationToken = default)

        public Task StartSearchAsync(CancellationToken CancellationToken = default)
        {

            searching = true;
            network.Register(this);

            // Everything already announced is visible right away.
            foreach (var service in DiscoveredServices)
                OnServiceDiscovered?.Invoke(service);

            return Task.CompletedTask;

        }

        #endregion

        #region StopSearchAsync (CancellationToken = default)

        public Task StopSearchAsync(CancellationToken CancellationToken = default)
        {

            searching = false;

            return Task.CompletedTask;

        }

        #endregion


        #region (internal) Discovered(Service) / Lost(Service)

        internal void Discovered(SHIPServiceInstance Service)
        {

            if (searching && (announcedService is null || Service.SKI != announcedService.SKI))
                OnServiceDiscovered?.Invoke(Service);

        }

        internal void Lost(SHIPServiceInstance Service)
        {

            if (searching && (announcedService is null || Service.SKI != announcedService.SKI))
                OnServiceLost?.Invoke(Service);

        }

        #endregion

    }

}
