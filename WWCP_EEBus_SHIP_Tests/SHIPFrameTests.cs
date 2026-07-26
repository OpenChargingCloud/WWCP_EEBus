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

using System.Text;

using NUnit.Framework;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP.tests
{

    /// <summary>
    /// Tests for the SHIP framing: a single message type byte followed by the
    /// message in EEBus JSON (SHIP TS 1.0.1, chapter 13.3).
    /// </summary>
    [TestFixture]
    public class SHIPFrameTests
    {

        #region (private) Frame(MessageType, JSON)

        private static Byte[] Frame(SHIPMessageTypes MessageType, String JSON)
        {

            var json   = Encoding.UTF8.GetBytes(JSON);
            var bytes  = new Byte[1 + json.Length];

            bytes[0] = (Byte) MessageType;
            Array.Copy(json, 0, bytes, 1, json.Length);

            return bytes;

        }

        #endregion


        #region InitFrame_IsTwoZeroBytes()

        /// <summary>
        /// The "Connection Mode Initialisation" message consists of the message
        /// type byte 0 followed by the value byte 0 (SHIP TS 1.0.1, chapter 13.4.3).
        /// </summary>
        [Test]
        public void InitFrame_IsTwoZeroBytes()
        {

            Assert.That(SHIPFrame.Init.ToByteArray(), Is.EqualTo(new Byte[] { 0x00, 0x00 }));

        }

        #endregion

        #region TryParse_InitFrame_Succeeds()

        [Test]
        public void TryParse_InitFrame_Succeeds()
        {

            Assert.That(SHIPFrame.TryParse(new Byte[] { 0x00, 0x00 }, out var frame, out var errorResponse), Is.True, errorResponse);

            Assert.Multiple(() => {
                Assert.That(frame!.MessageType,  Is.EqualTo(SHIPMessageTypes.INIT));
                Assert.That(frame!.Payload,      Is.Null);
            });

        }

        #endregion

        #region TryParse_InvalidInitValue_Fails()

        [Test]
        [TestCase(new Byte[] { 0x00 },              TestName = "init without value byte")]
        [TestCase(new Byte[] { 0x00, 0x01 },        TestName = "init with wrong value byte")]
        [TestCase(new Byte[] { 0x00, 0x00, 0x00 },  TestName = "init with too many value bytes")]
        public void TryParse_InvalidInitValue_Fails(Byte[] ByteArray)
        {

            Assert.Multiple(() => {
                Assert.That(SHIPFrame.TryParse(ByteArray, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                Is.Not.Null);
            });

        }

        #endregion

        #region TryParse_EmptyFrame_Fails()

        [Test]
        public void TryParse_EmptyFrame_Fails()
        {

            Assert.Multiple(() => {
                Assert.That(SHIPFrame.TryParse(Array.Empty<Byte>(), out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                          Does.Contain("message type"));
            });

        }

        #endregion

        #region TryParse_UnknownMessageType_Fails()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 13.3: the message types 4..255 are reserved.
        /// </summary>
        [Test]
        public void TryParse_UnknownMessageType_Fails()
        {

            Assert.Multiple(() => {
                Assert.That(SHIPFrame.TryParse(Frame((SHIPMessageTypes) 4, "{}"), out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                                        Does.Contain("Unknown SHIP message type"));
            });

        }

        #endregion

        #region TryParse_ControlFrameWithoutValue_Fails()

        [Test]
        public void TryParse_ControlFrameWithoutValue_Fails()
        {

            Assert.Multiple(() => {
                Assert.That(SHIPFrame.TryParse(new Byte[] { (Byte) SHIPMessageTypes.CONTROL }, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                                                     Does.Contain("message value"));
            });

        }

        #endregion

        #region TryParse_OversizedFrame_Fails()

        [Test]
        public void TryParse_OversizedFrame_Fails()
        {

            var bytes = new Byte[SHIPFrame.DefaultMaxFrameLength + 1];
            bytes[0] = (Byte) SHIPMessageTypes.DATA;

            Assert.Multiple(() => {
                Assert.That(SHIPFrame.TryParse(bytes, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                            Does.Contain("too large"));
            });

        }

        #endregion

        #region TryParse_TrailingNullBytes_AreTolerated()

        /// <summary>
        /// The Porsche Mobile Charger appends NUL bytes to many of its messages.
        /// </summary>
        [Test]
        public void TryParse_TrailingNullBytes_AreTolerated()
        {

            var bytes = Frame(SHIPMessageTypes.CONTROL, """{"connectionHello":[{"phase":"ready"}]}""" + "\0\0");

            Assert.That(SHIPFrame.TryParse(bytes, out var frame, out var errorResponse), Is.True, errorResponse);
            Assert.That(frame!.Payload?["connectionHello"]?["phase"]?.Value<String>(), Is.EqualTo("ready"));

        }

        #endregion

        #region TryParse_WhitespaceFormattedJSON_IsAccepted()

        /// <summary>
        /// Corresponds to TC_SHIP_MSG_003 of the official SHIP test specification.
        /// </summary>
        [Test]
        public void TryParse_WhitespaceFormattedJSON_IsAccepted()
        {

            var bytes = Frame(SHIPMessageTypes.CONTROL,
                              "  {\n  \"connectionHello\" : [\n    { \"phase\" : \"ready\" }\n  ]\n}  ");

            Assert.That(SHIPFrame.TryParse(bytes, out var frame, out var errorResponse), Is.True, errorResponse);
            Assert.That(frame!.Payload?["connectionHello"]?["phase"]?.Value<String>(), Is.EqualTo("ready"));

        }

        #endregion

        #region TryParse_InvalidJSON_Fails()

        [Test]
        public void TryParse_InvalidJSON_Fails()
        {

            Assert.Multiple(() => {
                Assert.That(SHIPFrame.TryParse(Frame(SHIPMessageTypes.CONTROL, "{not json"), out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                                                                   Is.Not.Null);
            });

        }

        #endregion

        #region TryParse_MultipleMessageElements_Fails()

        /// <summary>
        /// A SHIP message consists of exactly one message element.
        /// </summary>
        [Test]
        public void TryParse_MultipleMessageElements_Fails()
        {

            var bytes = Frame(SHIPMessageTypes.CONTROL,
                              """{"connectionHello":[{"phase":"ready"}],"connectionPinState":[{"pinState":"none"}]}""");

            Assert.Multiple(() => {
                Assert.That(SHIPFrame.TryParse(bytes, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                            Does.Contain("exactly one message element"));
            });

        }

        #endregion

        #region ToByteArray_ControlFrame_UsesEEBusJSON()

        [Test]
        public void ToByteArray_ControlFrame_UsesEEBusJSON()
        {

            var frame = new SHIPFrame(
                            SHIPMessageTypes.CONTROL,
                            JObject.Parse("""{"connectionHello":{"phase":"ready","waiting":60000}}""")
                        );

            var bytes = frame.ToByteArray();

            Assert.Multiple(() => {
                Assert.That(bytes[0],                            Is.EqualTo((Byte) SHIPMessageTypes.CONTROL));
                Assert.That(Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1),
                            Is.EqualTo("""{"connectionHello":[{"phase":"ready"},{"waiting":60000}]}"""));
            });

        }

        #endregion

    }

}
