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

using NUnit.Framework;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP.tests
{

    /// <summary>
    /// Tests for the mDNS TXT record of a SHIP node
    /// (SHIP TS 1.0.1, chapter 7.3.2).
    /// </summary>
    [TestFixture]
    public class SHIPServiceTXTTests
    {

        #region Data

        private static readonly SKI  exampleSKI = SKI.Parse("6ff5e2d2b1a41c9e0b2d3f4a5b6c7d8e9f001122");

        #endregion


        #region ToTXTStrings_ContainsTheMandatoryKeys()

        /// <summary>
        /// Corresponds to TC_SHIP_MDNS_001 of the official SHIP test specification.
        /// </summary>
        [Test]
        public void ToTXTStrings_ContainsTheMandatoryKeys()
        {

            var strings = new SHIPServiceTXT(
                              SHIP_Id.Parse("EVSE-12345678"),
                              exampleSKI,
                              DeviceBrand:  "GraphDefined",
                              DeviceModel:  "Wallbox",
                              DeviceType:   "EVSE"
                          ).ToTXTStrings().ToArray();

            Assert.Multiple(() => {
                Assert.That(strings[0],  Is.EqualTo("txtvers=1"));
                Assert.That(strings[1],  Is.EqualTo("path=/ship/"));
                Assert.That(strings[2],  Is.EqualTo("id=EVSE-12345678"));
                Assert.That(strings[3],  Is.EqualTo($"ski={exampleSKI}"));
                Assert.That(strings[4],  Is.EqualTo("brand=GraphDefined"));
                Assert.That(strings[5],  Is.EqualTo("model=Wallbox"));
                Assert.That(strings[6],  Is.EqualTo("type=EVSE"));
                Assert.That(strings[7],  Is.EqualTo("register=false"));
                Assert.That(strings,     Has.Length.EqualTo(8));
            });

        }

        #endregion

        #region ToTXTStrings_IncludesSerialAndCategories()

        /// <summary>
        /// The serial number and the device categories come from the
        /// "SHIP Requirements For Installation Process".
        /// </summary>
        [Test]
        public void ToTXTStrings_IncludesSerialAndCategories()
        {

            var strings = new SHIPServiceTXT(
                              SHIP_Id.Parse("EVSE-12345678"),
                              exampleSKI,
                              DeviceSerialNumber:  "SN-4711",
                              DeviceCategories:    [ "1", "3" ]
                          ).ToTXTStrings().ToArray();

            Assert.Multiple(() => {
                Assert.That(strings, Does.Contain("serial=SN-4711"));
                Assert.That(strings, Does.Contain("cat=1,3"));
            });

        }

        #endregion

        #region TryParse_Roundtrip_PreservesEverything()

        [Test]
        public void TryParse_Roundtrip_PreservesEverything()
        {

            var original = new SHIPServiceTXT(
                               SHIP_Id.Parse("EVSE-12345678"),
                               exampleSKI,
                               "/ship/",
                               "GraphDefined",
                               "Wallbox",
                               "EVSE",
                               Register:            true,
                               DeviceSerialNumber:  "SN-4711",
                               DeviceCategories:    [ "1", "3" ]
                           );

            Assert.That(SHIPServiceTXT.TryParse(original.ToTXTStrings(), out var parsed, out var errorResponse), Is.True, errorResponse);

            Assert.Multiple(() => {
                Assert.That(parsed!.Id,                  Is.EqualTo(original.Id));
                Assert.That(parsed!.SKI,                 Is.EqualTo(original.SKI));
                Assert.That(parsed!.Path,                Is.EqualTo("/ship/"));
                Assert.That(parsed!.DeviceBrand,         Is.EqualTo("GraphDefined"));
                Assert.That(parsed!.DeviceModel,         Is.EqualTo("Wallbox"));
                Assert.That(parsed!.DeviceType,          Is.EqualTo("EVSE"));
                Assert.That(parsed!.Register,            Is.True);
                Assert.That(parsed!.DeviceSerialNumber,  Is.EqualTo("SN-4711"));
                Assert.That(parsed!.DeviceCategories,    Is.EqualTo(new[] { "1", "3" }));
            });

        }

        #endregion

        #region TryParse_RealDeviceAnnouncement_IsUnderstood()

        /// <summary>
        /// An announcement as sent by the Go reference implementation, including
        /// its key order.
        /// </summary>
        [Test]
        public void TryParse_RealDeviceAnnouncement_IsUnderstood()
        {

            String[] announcement = [
                "txtvers=1",
                "path=/ship/",
                "id=Elli-Wallbox-2345678901",
                "ski=6ff5e2d2b1a41c9e0b2d3f4a5b6c7d8e9f001122",
                "brand=Elli",
                "model=Wallbox",
                "type=EVSE",
                "register=true"
            ];

            Assert.That(SHIPServiceTXT.TryParse(announcement, out var txt, out var errorResponse), Is.True, errorResponse);

            Assert.Multiple(() => {
                Assert.That(txt!.SKI,       Is.EqualTo(exampleSKI));
                Assert.That(txt!.Register,  Is.True, "The device announces that it accepts a registration.");
            });

        }

        #endregion

        #region TryParse_WithoutSKI_Fails()

        /// <summary>
        /// Without a SKI an announcement is worthless: the SKI is the identity
        /// of a SHIP node.
        /// </summary>
        [Test]
        public void TryParse_WithoutSKI_Fails()
        {

            Assert.Multiple(() => {
                Assert.That(SHIPServiceTXT.TryParse([ "txtvers=1", "id=EVSE-1" ], out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                                        Does.Contain("SKI"));
            });

        }

        #endregion

        #region TryParse_UnknownKeys_ArePreserved()

        /// <summary>
        /// A manufacturer specific key must neither break the parser nor get lost.
        /// </summary>
        [Test]
        public void TryParse_UnknownKeys_ArePreserved()
        {

            Assert.That(SHIPServiceTXT.TryParse(
                            [ "txtvers=1", "id=EVSE-1", $"ski={exampleSKI}", "vendorExtension=42" ],
                            out var txt,
                            out var errorResponse
                        ), Is.True, errorResponse);

            Assert.That(txt!.AdditionalKeyValues.Any(keyValue => keyValue.Key   == "vendorExtension" &&
                                                                 keyValue.Value == "42"), Is.True);

        }

        #endregion

        #region ServiceInstance_BuildsTheWebSocketURL()

        [Test]
        public void ServiceInstance_BuildsTheWebSocketURL()
        {

            var instance = new SHIPServiceInstance(
                               "Elli-Wallbox",
                               new SHIPServiceTXT(SHIP_Id.Parse("EVSE-1"), exampleSKI),
                               4712,
                               "wallbox.local",
                               [ System.Net.IPAddress.Parse("192.168.1.42") ]
                           );

            Assert.Multiple(() => {
                Assert.That(instance.ToURLs().First(),  Is.EqualTo("wss://192.168.1.42:4712/ship/"));
                Assert.That(instance.SKI,               Is.EqualTo(exampleSKI));
                Assert.That(SHIPServiceInstance.ServiceType, Is.EqualTo("_ship._tcp"));
            });

        }

        #endregion

    }

}
