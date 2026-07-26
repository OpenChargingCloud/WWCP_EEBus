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
using System.Net.Sockets;
using System.Collections.Concurrent;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// The discovery of SHIP nodes via multicast DNS
    /// (SHIP TS 1.0.1, chapter 7: DNS-SD, service type "_ship._tcp").
    ///
    /// The implementation answers queries for its own service, announces it
    /// unsolicited, says goodbye when it goes away, and collects the
    /// announcements of the other SHIP nodes on the network.
    /// </summary>
    public class SHIPMDNSDiscovery : ISHIPDiscovery, IAsyncDisposable
    {

        #region Data

        /// <summary>
        /// The DNS-SD name of the SHIP service.
        /// </summary>
        public const String  ServiceName          = $"{SHIPServiceInstance.ServiceType}.{SHIPServiceInstance.ServiceDomain}";

        /// <summary>
        /// How long an announcement may be cached (RFC 6763, chapter 6.1).
        /// </summary>
        public const UInt32  DefaultTimeToLive    = 120;

        private readonly ConcurrentDictionary<String, SHIPServiceInstance>  discoveredServices  = new ();
        private readonly TimeProvider                                       timeProvider;
        private readonly IPAddress                                          announcedAddress;

        private UdpClient?            udpClient;
        private CancellationTokenSource?  cancellationTokenSource;
        private Task?                 listenerTask;
        private SHIPServiceInstance?  announcedService;
        private Boolean               searching;

        #endregion

        #region Properties

        /// <summary>
        /// All SHIP nodes currently visible on the network.
        /// </summary>
        public IEnumerable<SHIPServiceInstance> DiscoveredServices
            => discoveredServices.Values;

        /// <summary>
        /// The host name this SHIP node announces itself with.
        /// </summary>
        public String                           HostName    { get; }

        #endregion

        #region Events

        /// <summary>
        /// A SHIP node appeared on the network, or its announcement changed.
        /// </summary>
        public event Action<SHIPServiceInstance>? OnServiceDiscovered;

        /// <summary>
        /// A SHIP node disappeared from the network.
        /// </summary>
        public event Action<SHIPServiceInstance>? OnServiceLost;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new multicast DNS discovery for SHIP nodes.
        /// </summary>
        /// <param name="HostName">The host name to announce, without the ".local" suffix.</param>
        /// <param name="AnnouncedAddress">The IPv4 address to announce; by default the first non-loopback address of this machine.</param>
        /// <param name="TimeProvider">An optional time provider.</param>
        public SHIPMDNSDiscovery(String?        HostName           = null,
                                 IPAddress?     AnnouncedAddress   = null,
                                 TimeProvider?  TimeProvider       = null)
        {

            this.HostName          = HostName ?? Dns.GetHostName();
            this.announcedAddress  = AnnouncedAddress ?? LocalAddress();
            this.timeProvider      = TimeProvider ?? System.TimeProvider.System;

        }

        #endregion


        #region AnnounceAsync   (Service, CancellationToken = default)

        /// <summary>
        /// Announce the local SHIP node on the network.
        /// </summary>
        /// <param name="Service">The local SHIP node.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task AnnounceAsync(SHIPServiceInstance  Service,
                                        CancellationToken    CancellationToken   = default)
        {

            announcedService = Service;

            await EnsureSocketAsync(CancellationToken);

            await SendAsync(
                      SHIPMDNSMessage.CreateResponse(AnnouncementRecords(Service, DefaultTimeToLive)),
                      CancellationToken
                  );

        }

        #endregion

        #region UnannounceAsync (CancellationToken = default)

        /// <summary>
        /// Stop announcing the local SHIP node: a "goodbye" with a time to live
        /// of zero (RFC 6762, chapter 10.1).
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task UnannounceAsync(CancellationToken CancellationToken = default)
        {

            if (announcedService is null || udpClient is null)
                return;

            await SendAsync(
                      SHIPMDNSMessage.CreateResponse(AnnouncementRecords(announcedService, 0)),
                      CancellationToken
                  );

            announcedService = null;

        }

        #endregion

        #region StartSearchAsync(CancellationToken = default)

        /// <summary>
        /// Start searching for SHIP nodes.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task StartSearchAsync(CancellationToken CancellationToken = default)
        {

            searching = true;

            await EnsureSocketAsync(CancellationToken);

            await SendAsync(
                      SHIPMDNSMessage.CreateQuery(ServiceName, DNSRecordTypes.PTR),
                      CancellationToken
                  );

        }

        #endregion

        #region StopSearchAsync (CancellationToken = default)

        /// <summary>
        /// Stop searching for SHIP nodes.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task StopSearchAsync(CancellationToken CancellationToken = default)
        {

            searching = false;

            return Task.CompletedTask;

        }

        #endregion

        #region DisposeAsync()

        /// <summary>
        /// Say goodbye and close the socket.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            try
            {
                await UnannounceAsync();
            }
            catch (Exception)
            { }

            cancellationTokenSource?.Cancel();
            udpClient?.Dispose();
            udpClient = null;

            GC.SuppressFinalize(this);

        }

        #endregion


        #region (private) EnsureSocketAsync(CancellationToken)

        private Task EnsureSocketAsync(CancellationToken CancellationToken)
        {

            if (udpClient is not null)
                return Task.CompletedTask;

            var client = new UdpClient {
                             ExclusiveAddressUse = false
                         };

            // Several mDNS responders share port 5353 on one machine - on Windows
            // the operating system runs one itself.
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, SHIPMDNSMessage.MulticastPort));
            client.JoinMulticastGroup(SHIPMDNSMessage.MulticastGroup);
            client.MulticastLoopback = true;

            udpClient                = client;
            cancellationTokenSource  = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            listenerTask             = Task.Run(() => ListenAsync(cancellationTokenSource.Token));

            return Task.CompletedTask;

        }

        #endregion

        #region (private) ListenAsync(CancellationToken)

        private async Task ListenAsync(CancellationToken CancellationToken)
        {

            while (!CancellationToken.IsCancellationRequested && udpClient is not null)
            {

                try
                {

                    var received = await udpClient.ReceiveAsync(CancellationToken);

                    if (!SHIPMDNSMessage.TryParse(received.Buffer, out var questions, out var records))
                        continue;

                    // Somebody is looking for SHIP nodes.
                    foreach (var (name, type) in questions)
                    {
                        if (announcedService is not null &&
                            (type is DNSRecordTypes.PTR or DNSRecordTypes.ANY) &&
                            name.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
                        {
                            await SendAsync(
                                      SHIPMDNSMessage.CreateResponse(AnnouncementRecords(announcedService, DefaultTimeToLive)),
                                      CancellationToken
                                  );
                        }
                    }

                    if (searching)
                        ProcessAnnouncement(records);

                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // A single broken datagram must not end the discovery.
                }

            }

        }

        #endregion

        #region (private) ProcessAnnouncement(Records)

        private void ProcessAnnouncement(List<DNSRecord> Records)
        {

            foreach (var srv in Records.Where(record => record.Type == DNSRecordTypes.SRV))
            {

                var instanceName = srv.Name;

                if (!instanceName.EndsWith($".{ServiceName}", StringComparison.OrdinalIgnoreCase))
                    continue;

                #region A goodbye: the SHIP node is going away

                if (srv.TimeToLive == 0)
                {

                    if (discoveredServices.TryRemove(instanceName, out var lostService))
                        OnServiceLost?.Invoke(lostService);

                    continue;

                }

                #endregion

                var txt = Records.FirstOrDefault(record => record.Type == DNSRecordTypes.TXT &&
                                                           record.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));

                if (txt is null ||
                    !SHIPServiceTXT.TryParse(txt.TXTStrings, out var serviceTXT, out _))
                {
                    continue;
                }

                var addresses = Records.Where  (record => record.Type is DNSRecordTypes.A or DNSRecordTypes.AAAA &&
                                                          record.Address is not null &&
                                                          (srv.Target is null || record.Name.Equals(srv.Target, StringComparison.OrdinalIgnoreCase))).
                                        Select (record => record.Address!).
                                        ToArray();

                var service   = new SHIPServiceInstance(
                                    instanceName[..^(ServiceName.Length + 1)],
                                    serviceTXT,
                                    srv.Port,
                                    srv.Target,
                                    addresses,
                                    timeProvider.GetUtcNow()
                                );

                discoveredServices[instanceName] = service;

                OnServiceDiscovered?.Invoke(service);

            }

        }

        #endregion

        #region (private) AnnouncementRecords(Service, TimeToLive)

        private IEnumerable<DNSRecord> AnnouncementRecords(SHIPServiceInstance  Service,
                                                           UInt32               TimeToLive)
        {

            var instanceName = $"{Service.InstanceName}.{ServiceName}";
            var hostName     = $"{HostName}.{SHIPServiceInstance.ServiceDomain}";

            return [

                       new DNSRecord(ServiceName,  DNSRecordTypes.PTR, TimeToLive) {
                           Target      = instanceName
                       },

                       new DNSRecord(instanceName, DNSRecordTypes.SRV, TimeToLive) {
                           Port        = Service.Port,
                           Target      = hostName
                       },

                       new DNSRecord(instanceName, DNSRecordTypes.TXT, TimeToLive) {
                           TXTStrings  = Service.TXT.ToTXTStrings()
                       },

                       new DNSRecord(hostName,     DNSRecordTypes.A,   TimeToLive) {
                           Address     = announcedAddress
                       }

                   ];

        }

        #endregion

        #region (private) SendAsync(Message, CancellationToken) / LocalAddress()

        private async Task SendAsync(Byte[]             Message,
                                     CancellationToken  CancellationToken)
        {

            if (udpClient is null)
                return;

            await udpClient.SendAsync(
                      Message,
                      new IPEndPoint(SHIPMDNSMessage.MulticastGroup, SHIPMDNSMessage.MulticastPort),
                      CancellationToken
                  );

        }

        private static IPAddress LocalAddress()
        {
            try
            {

                return Dns.GetHostAddresses(Dns.GetHostName()).
                           FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork &&
                                                     !IPAddress.IsLoopback(address))
                       ?? IPAddress.Loopback;

            }
            catch (Exception)
            {
                return IPAddress.Loopback;
            }
        }

        #endregion

    }

}
