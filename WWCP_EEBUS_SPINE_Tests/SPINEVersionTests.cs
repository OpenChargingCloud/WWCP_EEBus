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

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// Tests for the announced SPINE specification version.
    /// </summary>
    [TestFixture]
    public class SPINEVersionTests
    {

        #region Version_AnnouncesSPINE130()

        /// <summary>
        /// Every SPINE datagram header carries this "specificationVersion".
        /// </summary>
        [Test]
        public void Version_AnnouncesSPINE130()
        {

            Assert.Multiple(() => {
                Assert.That(Version.String,        Is.EqualTo("1.3.0"));
                Assert.That(Version.Id.ToString(), Is.EqualTo("1.3.0"));
                Assert.That(Version.XMLNamespace,  Is.EqualTo("http://docs.eebus.org/spine/xsd/v1"));
            });

        }

        #endregion

    }

}
