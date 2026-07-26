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
    /// Tests for the SHIP message framing constants
    /// (SHIP TS 1.0.1, chapter 13.3).
    /// </summary>
    [TestFixture]
    public class SHIPMessageTypeTests
    {

        #region MessageTypes_MatchSpecifiedByteValues()

        [Test]
        public void MessageTypes_MatchSpecifiedByteValues()
        {

            Assert.Multiple(() => {
                Assert.That((Byte) SHIPMessageTypes.INIT,     Is.EqualTo(0));
                Assert.That((Byte) SHIPMessageTypes.CONTROL,  Is.EqualTo(1));
                Assert.That((Byte) SHIPMessageTypes.DATA,     Is.EqualTo(2));
                Assert.That((Byte) SHIPMessageTypes.END,      Is.EqualTo(3));
                Assert.That(SHIPMessageValue.CMI_HEAD,        Is.EqualTo(0));
            });

        }

        #endregion

        #region Messages_CarryTheirSpecifiedMessageType()

        [Test]
        public void Messages_CarryTheirSpecifiedMessageType()
        {

            var helloMessage = new SHIPHelloMessage(new ConnectionHello(ConnectionHelloPhase.Ready));
            var closeMessage = new SHIPCloseMessage(new ConnectionClose (ConnectionClosePhases.Announce));

            Assert.Multiple(() => {
                Assert.That(helloMessage.MessageType,  Is.EqualTo(SHIPMessageTypes.CONTROL));
                Assert.That(closeMessage.MessageType,  Is.EqualTo(SHIPMessageTypes.END));
            });

        }

        #endregion

        #region Version_AnnouncesSHIP101AndWellKnownProtocolId()

        [Test]
        public void Version_AnnouncesSHIP101AndWellKnownProtocolId()
        {

            Assert.Multiple(() => {
                Assert.That(Version.String,      Is.EqualTo("1.0.1"));
                Assert.That(Version.Major,       Is.EqualTo(1));
                Assert.That(Version.Minor,       Is.EqualTo(0));
                Assert.That(Version.ProtocolId,  Is.EqualTo("ee1.0"));
            });

        }

        #endregion

    }

}
