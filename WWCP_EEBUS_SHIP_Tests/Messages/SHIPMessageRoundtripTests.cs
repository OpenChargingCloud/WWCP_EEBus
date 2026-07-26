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

using System.Text;

using NUnit.Framework;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP.tests
{

    /// <summary>
    /// Every SHIP message has to survive the way to the wire and back:
    /// message -> EEBUS JSON -> bytes -> message.
    /// </summary>
    [TestFixture]
    public class SHIPMessageRoundtripTests
    {

        #region (private) Roundtrip(Message)

        private static ASHIPMessage Roundtrip(ASHIPMessage Message)
        {

            var bytes = Message.ToByteArray();

            Assert.That(ASHIPMessage.TryParse(bytes, out var parsedMessage, out var errorResponse), Is.True, errorResponse);
            Assert.That(parsedMessage!.MessageType, Is.EqualTo(Message.MessageType));

            return parsedMessage;

        }

        #endregion


        #region InitMessage_Roundtrip()

        [Test]
        public void InitMessage_Roundtrip()
        {

            var message = Roundtrip(new SHIPInitMessage());

            Assert.That(message, Is.InstanceOf<SHIPInitMessage>());

        }

        #endregion

        #region HelloMessage_Roundtrip()

        [Test]
        public void HelloMessage_Roundtrip()
        {

            var message = Roundtrip(
                              new SHIPHelloMessage(
                                  new ConnectionHello(
                                      ConnectionHelloPhase.Pending,
                                      Waiting:              60000,
                                      ProlongationRequest:  true
                                  )
                              )
                          ) as SHIPHelloMessage;

            Assert.That(message, Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(message!.ConnectionHello.Phase,                Is.EqualTo(ConnectionHelloPhase.Pending));
                Assert.That(message!.ConnectionHello.Waiting,              Is.EqualTo(60000));
                Assert.That(message!.ConnectionHello.ProlongationRequest,  Is.True);
            });

        }

        #endregion

        #region HandshakeMessage_Roundtrip()

        [Test]
        public void HandshakeMessage_Roundtrip()
        {

            var message = Roundtrip(
                              new SHIPHandshakeMessage(
                                  new MessageProtocolHandshake(
                                      ProtocolHandshakeTypeTypes.announceMax,
                                      new MessageProtocolHandshakeVersion(1, 0),
                                      [ MessageProtocolFormat.JSON_UTF8 ]
                                  )
                              )
                          ) as SHIPHandshakeMessage;

            Assert.That(message, Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(message!.MessageProtocolHandshake.HandshakeType,    Is.EqualTo(ProtocolHandshakeTypeTypes.announceMax));
                Assert.That(message!.MessageProtocolHandshake.Version.Major,    Is.EqualTo(1));
                Assert.That(message!.MessageProtocolHandshake.Version.Minor,    Is.EqualTo(0));
                Assert.That(message!.MessageProtocolHandshake.Formats,          Is.EqualTo(new[] { MessageProtocolFormat.JSON_UTF8 }));
            });

        }

        #endregion

        #region HandshakeMessage_UsesTheFormatsElementOfTheXSD()

        /// <summary>
        /// The XSD wraps the repeated "format" elements within a "formats"
        /// complex type - a plain array would not be understood by other stacks.
        /// </summary>
        [Test]
        public void HandshakeMessage_UsesTheFormatsElementOfTheXSD()
        {

            var bytes = new SHIPHandshakeMessage(
                            new MessageProtocolHandshake(
                                ProtocolHandshakeTypeTypes.select,
                                new MessageProtocolHandshakeVersion(1, 0),
                                [ MessageProtocolFormat.JSON_UTF8 ]
                            )
                        ).ToByteArray();

            Assert.That(Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1),
                        Is.EqualTo("""{"messageProtocolHandshake":[{"handshakeType":"select"},{"version":[{"major":1},{"minor":0}]},{"formats":[{"format":["JSON-UTF8"]}]}]}"""));

        }

        #endregion

        #region PinStateMessage_Roundtrip()

        [Test]
        public void PinStateMessage_Roundtrip()
        {

            var message = Roundtrip(
                              new SHIPPinStateMessage(
                                  new ConnectionPinState(
                                      PinState.Required,
                                      PinInputPermission.OK
                                  )
                              )
                          ) as SHIPPinStateMessage;

            Assert.That(message, Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(message!.ConnectionPinState.PinState,         Is.EqualTo(PinState.Required));
                Assert.That(message!.ConnectionPinState.InputPermission,  Is.EqualTo(PinInputPermission.OK));
            });

        }

        #endregion

        #region PinStateMessage_None_OmitsInputPermission()

        /// <summary>
        /// This implementation only supports the PIN state "none", which is what
        /// goes over the wire during a normal connection setup.
        /// </summary>
        [Test]
        public void PinStateMessage_None_OmitsInputPermission()
        {

            var bytes = new SHIPPinStateMessage(
                            new ConnectionPinState(PinState.None)
                        ).ToByteArray();

            Assert.Multiple(() => {
                Assert.That(bytes[0],                                             Is.EqualTo((Byte) SHIPMessageTypes.CONTROL));
                Assert.That(Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1),  Is.EqualTo("""{"connectionPinState":[{"pinState":"none"}]}"""));
            });

        }

        #endregion

        #region PinInputMessage_Roundtrip()

        [Test]
        public void PinInputMessage_Roundtrip()
        {

            var message = Roundtrip(new SHIPPinInputMessage(new ConnectionPinInput("123456"))) as SHIPPinInputMessage;

            Assert.That(message,                             Is.Not.Null);
            Assert.That(message!.ConnectionPinInput.Pin,      Is.EqualTo("123456"));

        }

        #endregion

        #region PinErrorMessage_Roundtrip()

        [Test]
        public void PinErrorMessage_Roundtrip()
        {

            var message = Roundtrip(new SHIPPinErrorMessage(new ConnectionPinError(1))) as SHIPPinErrorMessage;

            Assert.That(message,                          Is.Not.Null);
            Assert.That(message!.ConnectionPinError.Error, Is.EqualTo(1));

        }

        #endregion

        #region AccessMethodsRequestMessage_Roundtrip()

        [Test]
        public void AccessMethodsRequestMessage_Roundtrip()
        {

            var bytes    = new SHIPAccessMethodsRequestMessage().ToByteArray();
            var message  = Roundtrip(new SHIPAccessMethodsRequestMessage());

            Assert.Multiple(() => {
                Assert.That(message,                                              Is.InstanceOf<SHIPAccessMethodsRequestMessage>());
                Assert.That(Encoding.UTF8.GetString(bytes, 1, bytes.Length - 1),  Is.EqualTo("""{"accessMethodsRequest":[]}"""));
            });

        }

        #endregion

        #region CloseMessage_Roundtrip()

        [Test]
        public void CloseMessage_Roundtrip()
        {

            var message = Roundtrip(
                              new SHIPCloseMessage(
                                  new ConnectionClose(
                                      ConnectionClosePhases.Announce,
                                      MaxTime:  60000,
                                      Reason:   ConnectionCloseReasons.RemovedConnection
                                  )
                              )
                          ) as SHIPCloseMessage;

            Assert.That(message, Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(message!.MessageType,               Is.EqualTo(SHIPMessageTypes.END));
                Assert.That(message!.ConnectionClose.Phase,     Is.EqualTo(ConnectionClosePhases.Announce));
                Assert.That(message!.ConnectionClose.MaxTime,   Is.EqualTo(60000));
                Assert.That(message!.ConnectionClose.Reason,    Is.EqualTo(ConnectionCloseReasons.RemovedConnection));
            });

        }

        #endregion

        #region DataMessage_CarriesSpineDatagram()

        [Test]
        public void DataMessage_CarriesSpineDatagram()
        {

            var spineDatagram = JObject.Parse(EEBUSJSONTests.StandardJSON);

            var message = Roundtrip(
                              new SHIPDataMessage(
                                  new DataType(
                                      new HeaderType(Version.ProtocolId),
                                      spineDatagram
                                  )
                              )
                          ) as SHIPDataMessage;

            Assert.That(message, Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(message!.MessageType,                 Is.EqualTo(SHIPMessageTypes.DATA));
                Assert.That(message!.Data.Header.ProtocolId,      Is.EqualTo("ee1.0"));
                Assert.That(message!.Data.Payload.ToString(Newtonsoft.Json.Formatting.None),
                            Is.EqualTo(spineDatagram.ToString(Newtonsoft.Json.Formatting.None)));
            });

        }

        #endregion

        #region TryParse_MessageElementInWrongMessageType_Fails()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 13.4: a "connectionHello" is a control message;
        /// it must not arrive within a data message.
        /// </summary>
        [Test]
        public void TryParse_MessageElementInWrongMessageType_Fails()
        {

            var json   = Encoding.UTF8.GetBytes("""{"connectionHello":[{"phase":"ready"}]}""");
            var bytes  = new Byte[1 + json.Length];
            bytes[0]   = (Byte) SHIPMessageTypes.DATA;
            Array.Copy(json, 0, bytes, 1, json.Length);

            Assert.Multiple(() => {
                Assert.That(ASHIPMessage.TryParse(bytes, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                               Does.Contain("not allowed"));
            });

        }

        #endregion

    }

}
