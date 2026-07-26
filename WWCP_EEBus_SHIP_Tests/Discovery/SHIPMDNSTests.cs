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

using NUnit.Framework;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP.tests
{

    /// <summary>
    /// Tests for the multicast DNS messages a SHIP node exchanges
    /// (SHIP TS 1.0.1, chapter 7; RFC 6762, RFC 6763).
    /// </summary>
    [TestFixture]
    public class SHIPMDNSTests
    {

        #region Data

        private static readonly SKI  exampleSKI = SKI.Parse("6ff5e2d2b1a41c9e0b2d3f4a5b6c7d8e9f001122");

        private static SHIPServiceInstance ExampleService()

            => new (
                   "GraphDefined-Wallbox",
                   new SHIPServiceTXT(
                       SHIP_Id.Parse("EVSE-12345678"),
                       exampleSKI,
                       DeviceBrand:  "GraphDefined",
                       DeviceModel:  "Wallbox",
                       DeviceType:   "EVSE",
                       Register:     true
                   ),
                   4712
               );

        private static IEnumerable<DNSRecord> AnnouncementOf(SHIPServiceInstance Service)
        {

            var instanceName = $"{Service.InstanceName}.{SHIPMDNSDiscovery.ServiceName}";

            return [
                       new DNSRecord(SHIPMDNSDiscovery.ServiceName, DNSRecordTypes.PTR, 120) { Target     = instanceName },
                       new DNSRecord(instanceName,                  DNSRecordTypes.SRV, 120) { Target     = "wallbox.local", Port = Service.Port },
                       new DNSRecord(instanceName,                  DNSRecordTypes.TXT, 120) { TXTStrings = Service.TXT.ToTXTStrings() },
                       new DNSRecord("wallbox.local",               DNSRecordTypes.A,   120) { Address    = IPAddress.Parse("192.168.1.42") }
                   ];

        }

        #endregion


        #region Query_ForShipServices_IsWellFormed()

        /// <summary>
        /// A SHIP node searches for the DNS-SD service type "_ship._tcp.local".
        /// </summary>
        [Test]
        public void Query_ForShipServices_IsWellFormed()
        {

            var query = SHIPMDNSMessage.CreateQuery(SHIPMDNSDiscovery.ServiceName, DNSRecordTypes.PTR);

            Assert.That(SHIPMDNSMessage.TryParse(query, out var questions, out var records), Is.True);

            Assert.Multiple(() => {

                Assert.That(questions!,          Has.Count.EqualTo(1));
                Assert.That(questions![0].Name,  Is.EqualTo("_ship._tcp.local"));
                Assert.That(questions![0].Type,  Is.EqualTo(DNSRecordTypes.PTR));
                Assert.That(records!,            Is.Empty);

                // mDNS messages carry the transaction id 0 (RFC 6762, chapter 18.1).
                Assert.That(query[0],            Is.EqualTo(0));
                Assert.That(query[1],            Is.EqualTo(0));

            });

        }

        #endregion

        #region Announcement_Roundtrip_PreservesEverything()

        /// <summary>
        /// The announcement of a SHIP node - PTR, SRV, TXT and A record - has to
        /// survive the way over the wire.
        /// </summary>
        [Test]
        public void Announcement_Roundtrip_PreservesEverything()
        {

            var service   = ExampleService();
            var response  = SHIPMDNSMessage.CreateResponse(AnnouncementOf(service));

            Assert.That(SHIPMDNSMessage.TryParse(response, out var questions, out var records), Is.True);

            var ptr = records!.First(record => record.Type == DNSRecordTypes.PTR);
            var srv = records!.First(record => record.Type == DNSRecordTypes.SRV);
            var txt = records!.First(record => record.Type == DNSRecordTypes.TXT);
            var a   = records!.First(record => record.Type == DNSRecordTypes.A);

            Assert.Multiple(() => {

                Assert.That(questions!,   Is.Empty);
                Assert.That(records!,     Has.Count.EqualTo(4));

                Assert.That(ptr.Name,     Is.EqualTo("_ship._tcp.local"));
                Assert.That(ptr.Target,   Is.EqualTo("GraphDefined-Wallbox._ship._tcp.local"));

                Assert.That(srv.Port,     Is.EqualTo(4712));
                Assert.That(srv.Target,   Is.EqualTo("wallbox.local"));

                Assert.That(a.Address,    Is.EqualTo(IPAddress.Parse("192.168.1.42")));

            });

            // The decisive part: every key/value pair is its own character string.
            Assert.That(SHIPServiceTXT.TryParse(txt.TXTStrings, out var parsedTXT, out var errorResponse), Is.True, errorResponse);

            Assert.Multiple(() => {
                Assert.That(txt.TXTStrings.Count(),  Is.EqualTo(8));
                Assert.That(parsedTXT!.SKI,          Is.EqualTo(exampleSKI));
                Assert.That(parsedTXT!.Id,           Is.EqualTo(SHIP_Id.Parse("EVSE-12345678")));
                Assert.That(parsedTXT!.Register,     Is.True);
                Assert.That(parsedTXT!.DeviceBrand,  Is.EqualTo("GraphDefined"));
            });

        }

        #endregion

        #region TXTRecord_KeepsEveryKeyValuePairSeparate()

        /// <summary>
        /// RFC 6763, chapter 6.1: a TXT record consists of one character string
        /// per key/value pair - not of one long string.
        /// </summary>
        [Test]
        public void TXTRecord_KeepsEveryKeyValuePairSeparate()
        {

            var response = SHIPMDNSMessage.CreateResponse([
                               new DNSRecord("test._ship._tcp.local", DNSRecordTypes.TXT, 120) {
                                   TXTStrings = [ "txtvers=1", "ski=abc", "brand=GraphDefined" ]
                               }
                           ]);

            Assert.That(SHIPMDNSMessage.TryParse(response, out _, out var records), Is.True);

            Assert.That(records![0].TXTStrings,
                        Is.EqualTo(new[] { "txtvers=1", "ski=abc", "brand=GraphDefined" }));

        }

        #endregion

        #region Goodbye_IsAnnouncedWithTimeToLiveZero()

        /// <summary>
        /// RFC 6762, chapter 10.1: a service which goes away announces its
        /// records with a time to live of zero.
        /// </summary>
        [Test]
        public void Goodbye_IsAnnouncedWithTimeToLiveZero()
        {

            var goodbye = SHIPMDNSMessage.CreateResponse(
                              AnnouncementOf(ExampleService()).
                                  Select(record => { record.TimeToLive = 0; return record; })
                          );

            Assert.That(SHIPMDNSMessage.TryParse(goodbye, out _, out var records), Is.True);

            Assert.That(records!.All(record => record.TimeToLive == 0), Is.True);

        }

        #endregion

        #region TryParse_GarbageDatagram_DoesNotThrow()

        /// <summary>
        /// A shared network delivers all kinds of packets; none of them may take
        /// the discovery down.
        /// </summary>
        [Test]
        public void TryParse_GarbageDatagram_DoesNotThrow()
        {

            Assert.Multiple(() => {

                Assert.That(SHIPMDNSMessage.TryParse([],                   out _, out _),  Is.False);
                Assert.That(SHIPMDNSMessage.TryParse([ 0x01, 0x02 ],       out _, out _),  Is.False);

                // A header announcing more records than the datagram contains.
                Assert.That(() => SHIPMDNSMessage.TryParse(
                                      [ 0, 0, 0x84, 0, 0, 0, 0, 99, 0, 0, 0, 0 ],
                                      out _, out _
                                  ),
                            Throws.Nothing);

            });

        }

        #endregion

        #region InMemoryDiscovery_AnnouncesAndFinds()

        /// <summary>
        /// Where multicast is unavailable - containers, continuous integration -
        /// the SHIP nodes find each other through the in-memory discovery.
        /// </summary>
        [Test]
        public async Task InMemoryDiscovery_AnnouncesAndFinds()
        {

            var network      = new InMemorySHIPDiscovery.Network();

            var wallbox      = new InMemorySHIPDiscovery(network);
            var energyManager= new InMemorySHIPDiscovery(network);

            SHIPServiceInstance? found = null;
            energyManager.OnServiceDiscovered += service => found = service;

            await energyManager.StartSearchAsync();
            await wallbox.AnnounceAsync(ExampleService());

            Assert.Multiple(() => {
                Assert.That(found,                                     Is.Not.Null);
                Assert.That(found!.SKI,                                Is.EqualTo(exampleSKI));
                Assert.That(energyManager.DiscoveredServices.Count(),  Is.EqualTo(1));
            });

            SHIPServiceInstance? lost = null;
            energyManager.OnServiceLost += service => lost = service;

            await wallbox.UnannounceAsync();

            Assert.Multiple(() => {
                Assert.That(lost,                                      Is.Not.Null);
                Assert.That(energyManager.DiscoveredServices.Count(),  Is.EqualTo(0));
            });

        }

        #endregion

        #region InMemoryDiscovery_DoesNotFindItself()

        [Test]
        public async Task InMemoryDiscovery_DoesNotFindItself()
        {

            var network    = new InMemorySHIPDiscovery.Network();
            var discovery  = new InMemorySHIPDiscovery(network);

            await discovery.StartSearchAsync();
            await discovery.AnnounceAsync(ExampleService());

            Assert.That(discovery.DiscoveredServices, Is.Empty);

        }

        #endregion

    }

}
