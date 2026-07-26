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

#region Usings

using System.Net;

using NUnit.Framework;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP.tests
{

    /// <summary>
    /// The discovery of SHIP nodes over real multicast DNS.
    ///
    /// This needs a network interface which carries multicast, so it is excluded
    /// from the ordinary test runs - continuous integration runners usually do
    /// not provide one.
    /// </summary>
    [TestFixture]
    [Category("LocalNetwork")]
    public class SHIPMDNSLiveTests
    {

        #region AnnouncedShipNode_IsFoundOnTheNetwork()

        [Test]
        public async Task AnnouncedShipNode_IsFoundOnTheNetwork()
        {

            var ski        = SKI.Parse("6ff5e2d2b1a41c9e0b2d3f4a5b6c7d8e9f001122");

            var wallbox    = new SHIPMDNSDiscovery("wallbox-test",       IPAddress.Loopback);
            var manager    = new SHIPMDNSDiscovery("energymanager-test", IPAddress.Loopback);

            try
            {

                SHIPServiceInstance? found = null;

                manager.OnServiceDiscovered += service => {
                    if (service.SKI == ski)
                        found = service;
                };

                await manager.StartSearchAsync();

                await wallbox.AnnounceAsync(
                          new SHIPServiceInstance(
                              "GraphDefined-Wallbox-Test",
                              new SHIPServiceTXT(
                                  SHIP_Id.Parse("EVSE-12345678"),
                                  ski,
                                  DeviceBrand:  "GraphDefined",
                                  DeviceModel:  "Wallbox",
                                  DeviceType:   "EVSE"
                              ),
                              4712
                          )
                      );

                var until = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);

                while (found is null && DateTimeOffset.UtcNow < until)
                    await Task.Delay(100);

                if (found is null)
                    Assert.Inconclusive("No multicast DNS answer was received. This machine or network apparently does not carry multicast on the loopback interface.");

                Assert.Multiple(() => {
                    Assert.That(found!.SKI,                   Is.EqualTo(ski));
                    Assert.That(found!.Port,                  Is.EqualTo(4712));
                    Assert.That(found!.TXT.DeviceBrand,       Is.EqualTo("GraphDefined"));
                    Assert.That(found!.InstanceName,          Is.EqualTo("GraphDefined-Wallbox-Test"));
                    Assert.That(found!.ToURLs().First(),      Does.StartWith("wss://"));
                });

            }
            finally
            {
                await wallbox.DisposeAsync();
                await manager.DisposeAsync();
            }

        }

        #endregion

    }

}
