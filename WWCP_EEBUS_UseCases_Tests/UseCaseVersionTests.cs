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

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// The use case discovery version rules.
    ///
    /// Every use case specification repeats the same section 3.1.2, so the rules
    /// are tested once, here, rather than in every use case.
    /// </summary>
    [TestFixture]
    public class UseCaseVersionTests
    {

        #region AVersionIsThreeNumbers()

        [Test]
        [TestCase("1.0.0",  1u, 0u, 0u)]
        [TestCase("1.2.3",  1u, 2u, 3u)]
        [TestCase(" 2.0.0", 2u, 0u, 0u)]
        [TestCase("1.1",    1u, 1u, 0u)]
        [TestCase("2",      2u, 0u, 0u)]
        public void AVersionIsThreeNumbers(String Text, UInt32 Major, UInt32 Minor, UInt32 Patch)
        {

            Assert.That(UseCaseVersion.TryParse(Text, out var version), Is.True);

            Assert.Multiple(() => {
                Assert.That(version.Major, Is.EqualTo(Major));
                Assert.That(version.Minor, Is.EqualTo(Minor));
                Assert.That(version.Patch, Is.EqualTo(Patch));
            });

        }

        #endregion

        #region SomethingWhichIsNotAVersionIsRefused()

        [Test]
        [TestCase("")]
        [TestCase("1.0.0.0")]
        [TestCase("1.x.0")]
        [TestCase("-1.0.0")]
        [TestCase("v1.0.0")]
        public void SomethingWhichIsNotAVersionIsRefused(String Text)
        {
            Assert.That(UseCaseVersion.TryParse(Text, out _), Is.False);
        }

        #endregion

        #region VersionsAreComparedNumberByNumber()

        [Test]
        public void VersionsAreComparedNumberByNumber()
        {

            Assert.Multiple(() => {

                Assert.That(UseCaseVersion.Parse("1.0.0") < UseCaseVersion.Parse("1.0.1"), Is.True);
                Assert.That(UseCaseVersion.Parse("1.9.0") < UseCaseVersion.Parse("1.10.0"), Is.True,
                            "The numbers are numbers, not text.");
                Assert.That(UseCaseVersion.Parse("2.0.0") > UseCaseVersion.Parse("1.99.99"), Is.True);
                Assert.That(UseCaseVersion.Parse("1.2.3") == UseCaseVersion.Parse("1.2.3"), Is.True);

                Assert.That(UseCaseVersion.Parse("1.2.3").ToString(), Is.EqualTo("1.2.3"));

            });

        }

        #endregion


        #region OnlyTheHighestOfEachMajorVersionIsAnnounced()

        /// <summary>
        /// "If an Actor A supports multiple versions of this Use Case with the
        /// same major version number, only the highest one SHOULD be set within
        /// the Use Case discovery. [...] If an Actor A supports multiple
        /// versions with different major version numbers, for each major version
        /// number only the highest version number SHOULD be set."
        /// </summary>
        [Test]
        public void OnlyTheHighestOfEachMajorVersionIsAnnounced()
        {

            var announced = UseCaseVersion.Announced([
                                UseCaseVersion.Parse("1.0.0"),
                                UseCaseVersion.Parse("1.2.0"),
                                UseCaseVersion.Parse("1.1.5"),
                                UseCaseVersion.Parse("2.0.0"),
                                UseCaseVersion.Parse("2.0.1")
                            ]);

            Assert.That(announced.Select(version => version.ToString()),
                        Is.EqualTo(new[] { "1.2.0", "2.0.1" }));

        }

        #endregion

        #region TheHighestVersionOfOurMajorNumberIsChosen()

        /// <summary>
        /// "If an Actor A finds a proper counterpart Actor B [...] that supports
        /// multiple versions of this Use Case with the same major version number
        /// as supported by Actor A, the Actor A SHOULD evaluate from these
        /// versions of Actor B only the highest version number."
        /// </summary>
        [Test]
        public void TheHighestVersionOfOurMajorNumberIsChosen()
        {

            var ours = UseCaseVersion.Parse("1.1.0");

            Assert.That(UseCaseVersion.Best(ours,
                                            [ "1.0.0", "1.3.0", "1.2.0", "2.0.0" ],
                                            out var chosen,
                                            out var sameMajor),
                        Is.True);

            Assert.Multiple(() => {
                Assert.That(chosen.ToString(), Is.EqualTo("1.3.0"),
                            "The highest version of our own major number was not chosen.");
                Assert.That(sameMajor,         Is.True);
            });

        }

        #endregion

        #region ADifferentMajorVersionIsStillWorthTrying()

        /// <summary>
        /// "If an Actor A finds a proper counterpart Actor B for this Use Case
        /// that supports only versions with a major version number not
        /// implemented by Actor A, it still might be possible to run the Use
        /// Case or parts of the Use Case. Therefore, the Actor A should try to
        /// evaluate the Actor B as a valid partner for this Use Case."
        ///
        /// This is the rule which is easy to get wrong: a major version we do
        /// not implement is a reason to be careful, not a reason to refuse to
        /// talk.
        /// </summary>
        [Test]
        public void ADifferentMajorVersionIsStillWorthTrying()
        {

            var ours = UseCaseVersion.Parse("1.0.0");

            Assert.That(UseCaseVersion.Best(ours,
                                            [ "2.0.0", "3.1.0" ],
                                            out var chosen,
                                            out var sameMajor),
                        Is.True,
                        "A partner on another major version was refused outright.");

            Assert.Multiple(() => {
                Assert.That(chosen.ToString(), Is.EqualTo("3.1.0"));
                Assert.That(sameMajor,         Is.False,
                            "The caller is not told that this is another major version.");
            });

        }

        #endregion

        #region APartnerWhichAnnouncesNoVersionIsNoPartner()

        /// <summary>
        /// Something which is not a version at all is not one; a partner which
        /// announces nothing else cannot be evaluated.
        /// </summary>
        [Test]
        public void APartnerWhichAnnouncesNoVersionIsNoPartner()
        {

            Assert.Multiple(() => {

                Assert.That(UseCaseVersion.Best(UseCaseVersion.Parse("1.0.0"), Array.Empty<String?>(), out _, out _),
                            Is.False);

                Assert.That(UseCaseVersion.Best(UseCaseVersion.Parse("1.0.0"), [ null, "", "later" ], out _, out _),
                            Is.False);

                // ... but one good version among the rubbish is enough.
                Assert.That(UseCaseVersion.Best(UseCaseVersion.Parse("1.0.0"), [ "later", "1.0.0" ], out var chosen, out _),
                            Is.True);
                Assert.That(chosen.ToString(), Is.EqualTo("1.0.0"));

            });

        }

        #endregion

    }

}
