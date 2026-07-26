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

using System.Net;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// A SHIP node discovered on the local network, or announced by ourselves
    /// (SHIP TS 1.0.1, chapter 7).
    /// </summary>
    /// <param name="InstanceName">The mDNS service instance name.</param>
    /// <param name="TXT">The content of the mDNS TXT record.</param>
    /// <param name="Port">The TCP port of the SHIP WebSocket endpoint.</param>
    /// <param name="HostName">The host name announced within the SRV record.</param>
    /// <param name="Addresses">The IP addresses of the SHIP node.</param>
    /// <param name="DiscoveredAt">When the SHIP node was discovered.</param>
    public class SHIPServiceInstance(String                    InstanceName,
                                     SHIPServiceTXT            TXT,
                                     UInt16                    Port,
                                     String?                   HostName       = null,
                                     IEnumerable<IPAddress>?   Addresses      = null,
                                     DateTimeOffset?           DiscoveredAt   = null)
    {

        #region Data

        /// <summary>
        /// The DNS-SD service type of SHIP (SHIP TS 1.0.1, chapter 7.3).
        /// </summary>
        public const String  ServiceType    = "_ship._tcp";

        /// <summary>
        /// The DNS-SD domain of SHIP.
        /// </summary>
        public const String  ServiceDomain  = "local";

        /// <summary>
        /// The default TCP port of a SHIP WebSocket endpoint.
        /// </summary>
        public const UInt16  DefaultPort    = 4712;

        #endregion

        #region Properties

        /// <summary>
        /// The mDNS service instance name.
        /// </summary>
        public String                   InstanceName    { get; } = InstanceName;

        /// <summary>
        /// The content of the mDNS TXT record.
        /// </summary>
        public SHIPServiceTXT           TXT             { get; } = TXT;

        /// <summary>
        /// The TCP port of the SHIP WebSocket endpoint.
        /// </summary>
        public UInt16                   Port            { get; } = Port;

        /// <summary>
        /// The host name announced within the SRV record.
        /// </summary>
        public String?                  HostName        { get; } = HostName;

        /// <summary>
        /// The IP addresses of the SHIP node.
        /// </summary>
        public IEnumerable<IPAddress>   Addresses       { get; } = Addresses ?? [];

        /// <summary>
        /// When the SHIP node was discovered.
        /// </summary>
        public DateTimeOffset?          DiscoveredAt    { get; } = DiscoveredAt;

        /// <summary>
        /// The SKI of the SHIP node, its identity.
        /// </summary>
        public SKI                      SKI
            => TXT.SKI;

        #endregion


        #region ToURLs()

        /// <summary>
        /// Return the WebSocket URLs this SHIP node can be reached at.
        /// </summary>
        public IEnumerable<String> ToURLs()
        {

            var path = TXT.Path.StartsWith('/') ? TXT.Path : "/" + TXT.Path;

            foreach (var address in Addresses)
                yield return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                                 ? $"wss://[{address}]:{Port}{path}"
                                 : $"wss://{address}:{Port}{path}";

            if (!Addresses.Any() && HostName is not null)
                yield return $"wss://{HostName}:{Port}{path}";

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{InstanceName}.{ServiceType}.{ServiceDomain}: {TXT}";

        #endregion

    }

}
