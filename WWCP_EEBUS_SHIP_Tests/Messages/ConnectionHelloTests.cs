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

using NUnit.Framework;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP.tests
{

    /// <summary>
    /// Tests for the SHIP "connectionHello" message
    /// (SHIP TS 1.0.1, chapter 13.4.4.1).
    /// </summary>
    [TestFixture]
    public class ConnectionHelloTests
    {

        #region ToJSON_ReadyPhaseWithWaiting_MatchesSpecExample()

        [Test]
        public void ToJSON_ReadyPhaseWithWaiting_MatchesSpecExample()
        {

            var json = new ConnectionHello(
                           ConnectionHelloPhase.Ready,
                           Waiting: 60000
                       ).ToJSON();

            Assert.Multiple(() => {
                Assert.That(json["phase"]?.  Value<String>(), Is.EqualTo("ready"));
                Assert.That(json["waiting"]?.Value<UInt32>(), Is.EqualTo(60000));
                Assert.That(json.ContainsKey("prolongationRequest"), Is.False);
            });

        }

        #endregion

        #region ToJSON_OmitsOptionalProperties()

        [Test]
        public void ToJSON_OmitsOptionalProperties()
        {

            var json = new ConnectionHello(ConnectionHelloPhase.Aborted).ToJSON();

            Assert.Multiple(() => {
                Assert.That(json.Properties().Count(),  Is.EqualTo(1));
                Assert.That(json["phase"]?.Value<String>(), Is.EqualTo("aborted"));
            });

        }

        #endregion

        #region ToJSON_NeverEmitsCustomData()

        /// <summary>
        /// SHIP messages have a fixed schema (EEBus_SHIP_TS_TransferProtocol.xsd).
        /// A "customData" property - inherited from the OCPP stack - would break
        /// wire conformance and must not appear.
        /// </summary>
        [Test]
        public void ToJSON_NeverEmitsCustomData()
        {

            var json = new ConnectionHello(
                           ConnectionHelloPhase.Pending,
                           Waiting:              60000,
                           ProlongationRequest:  true
                       ).ToJSON();

            Assert.That(json.ContainsKey("customData"), Is.False);

        }

        #endregion

        #region TryParse_ValidJSON_Succeeds()

        [Test]
        public void TryParse_ValidJSON_Succeeds()
        {

            var json = JObject.Parse("""
                                     {
                                         "phase":                "pending",
                                         "waiting":              60000,
                                         "prolongationRequest":  true
                                     }
                                     """);

            Assert.That(ConnectionHello.TryParse(json, out var connectionHello, out var errorResponse), Is.True, errorResponse);
            Assert.That(connectionHello, Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(connectionHello!.Phase,                Is.EqualTo(ConnectionHelloPhase.Pending));
                Assert.That(connectionHello!.Waiting,              Is.EqualTo(60000));
                Assert.That(connectionHello!.ProlongationRequest,  Is.True);
            });

        }

        #endregion

        #region TryParse_MissingPhase_Fails()

        [Test]
        public void TryParse_MissingPhase_Fails()
        {

            var json = JObject.Parse("""{ "waiting": 60000 }""");

            Assert.Multiple(() => {
                Assert.That(ConnectionHello.TryParse(json, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                 Is.Not.Null);
            });

        }

        #endregion

        #region Roundtrip_AllPhases_PreservesValues()

        [Test]
        [TestCase("pending")]
        [TestCase("ready")]
        [TestCase("aborted")]
        public void Roundtrip_AllPhases_PreservesValues(String Phase)
        {

            var json = JObject.Parse($$"""{ "phase": "{{Phase}}", "waiting": 42 }""");

            Assert.That(ConnectionHello.TryParse(json, out var parsed, out var errorResponse), Is.True, errorResponse);

            var json2 = parsed!.ToJSON();

            Assert.Multiple(() => {
                Assert.That(json2["phase"]?.  Value<String>(),  Is.EqualTo(Phase));
                Assert.That(json2["waiting"]?.Value<UInt32>(),  Is.EqualTo(42));
            });

        }

        #endregion

    }

}
