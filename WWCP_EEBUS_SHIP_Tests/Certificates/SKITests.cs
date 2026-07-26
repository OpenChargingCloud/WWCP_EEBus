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

namespace cloud.charging.open.protocols.EEBUS.SHIP.tests
{

    /// <summary>
    /// Tests for the Subject Key Identifier, the identity of a SHIP node
    /// (SHIP TS 1.0.1, chapter 12.2).
    /// </summary>
    [TestFixture]
    public class SKITests
    {

        #region Data

        private const String ExampleSKI = "6ff5e2d2b1a41c9e0b2d3f4a5b6c7d8e9f001122";

        #endregion


        #region Parse_40HexDigits_Succeeds()

        [Test]
        public void Parse_40HexDigits_Succeeds()
        {

            var ski = SKI.Parse(ExampleSKI);

            Assert.Multiple(() => {
                Assert.That(ski.ToString(),            Is.EqualTo(ExampleSKI));
                Assert.That(ski.ToByteArray().Length,  Is.EqualTo(20));
                Assert.That(ski.IsNotNullOrEmpty,      Is.True);
            });

        }

        #endregion

        #region Parse_ToleratesPresentationVariants()

        /// <summary>
        /// A SKI is shown to the user in groups and may be typed back in with
        /// spaces, colons or dashes, and in upper case.
        /// </summary>
        [Test]
        [TestCase("6FF5E2D2B1A41C9E0B2D3F4A5B6C7D8E9F001122")]
        [TestCase("6ff5 e2d2 b1a4 1c9e 0b2d 3f4a 5b6c 7d8e 9f00 1122")]
        [TestCase("6f:f5:e2:d2:b1:a4:1c:9e:0b:2d:3f:4a:5b:6c:7d:8e:9f:00:11:22")]
        [TestCase("6ff5-e2d2-b1a4-1c9e-0b2d-3f4a-5b6c-7d8e-9f00-1122")]
        public void Parse_ToleratesPresentationVariants(String Text)
        {

            Assert.That(SKI.Parse(Text).ToString(), Is.EqualTo(ExampleSKI));

        }

        #endregion

        #region TryParse_InvalidInput_Fails()

        [Test]
        [TestCase("",                                            TestName = "empty")]
        [TestCase("6ff5e2d2b1a41c9e0b2d3f4a5b6c7d8e9f0011",      TestName = "too short")]
        [TestCase("6ff5e2d2b1a41c9e0b2d3f4a5b6c7d8e9f00112233",  TestName = "too long")]
        [TestCase("6ff5e2d2b1a41c9e0b2d3f4a5b6c7d8e9f00112z",    TestName = "invalid hex digit")]
        public void TryParse_InvalidInput_Fails(String Text)
        {

            Assert.Multiple(() => {
                Assert.That(SKI.TryParse(Text, out _, out var errorResponse),  Is.False);
                Assert.That(errorResponse,                                     Is.Not.Null);
            });

        }

        #endregion

        #region ToGroupedString_MatchesDeviceLabelNotation()

        [Test]
        public void ToGroupedString_MatchesDeviceLabelNotation()
        {

            Assert.That(SKI.Parse(ExampleSKI).ToGroupedString(),
                        Is.EqualTo("6ff5 e2d2 b1a4 1c9e 0b2d 3f4a 5b6c 7d8e 9f00 1122"));

        }

        #endregion

        #region Comparison_IsUsedToResolveDoubleConnections()

        /// <summary>
        /// SHIP TS 1.0.1, chapter 12.2.2 resolves double connections by comparing
        /// the SKI values of both communication partners.
        /// </summary>
        [Test]
        public void Comparison_IsUsedToResolveDoubleConnections()
        {

            var lower  = SKI.Parse("0000000000000000000000000000000000000001");
            var higher = SKI.Parse("f000000000000000000000000000000000000000");

            Assert.Multiple(() => {
                Assert.That(lower  <  higher,                Is.True);
                Assert.That(higher >  lower,                 Is.True);
                Assert.That(lower.Equals(SKI.Parse(lower.ToString())),  Is.True);
                Assert.That(lower  == SKI.Parse("0000000000000000000000000000000000000001"), Is.True);
                Assert.That(lower  != higher,                Is.True);
            });

        }

        #endregion

        #region Equality_IsCaseInsensitiveOnInput()

        [Test]
        public void Equality_IsCaseInsensitiveOnInput()
        {

            Assert.Multiple(() => {
                Assert.That(SKI.Parse(ExampleSKI.ToUpper()), Is.EqualTo(SKI.Parse(ExampleSKI)));
                Assert.That(SKI.Parse(ExampleSKI.ToUpper()).GetHashCode(),
                            Is.EqualTo(SKI.Parse(ExampleSKI).GetHashCode()));
            });

        }

        #endregion

    }

}
