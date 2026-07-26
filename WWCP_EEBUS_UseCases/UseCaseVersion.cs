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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases
{

    /// <summary>
    /// The version of a use case, and the rules for comparing two of them.
    ///
    /// Every use case specification repeats the same section (3.1.2, "Use Case
    /// discovery rules"), and the rules in it are the same for all of them -
    /// which is why they belong here rather than in each use case:
    ///
    /// * an actor which supports several versions with the **same major number**
    ///   SHOULD announce only the highest one;
    /// * an actor which finds a partner announcing several versions with its own
    ///   major number SHOULD evaluate only the highest of them;
    /// * an actor which supports several **different** major numbers SHOULD
    ///   announce the highest version of each;
    /// * an actor which finds a partner announcing **only** major numbers it
    ///   does not implement "should try to evaluate the Actor B as a valid
    ///   partner" anyway - it might still be possible to run the use case, or
    ///   parts of it.
    ///
    /// That last rule is the one which is easy to get wrong, and it is a SHOULD
    /// rather than a SHALL: a different major version is a reason to be careful,
    /// not a reason to refuse to talk.
    /// </summary>
    /// <param name="Major">The major number.</param>
    /// <param name="Minor">The minor number.</param>
    /// <param name="Patch">The patch number.</param>
    public readonly record struct UseCaseVersion(UInt32 Major,
                                                 UInt32 Minor,
                                                 UInt32 Patch) : IComparable<UseCaseVersion>
    {

        #region Parse(Text) / TryParse(Text, out Version)

        /// <summary>
        /// Parse a version of the form "1.0.0".
        /// </summary>
        /// <param name="Text">The text of a version.</param>
        public static UseCaseVersion Parse(String Text)

            => TryParse(Text, out var version)
                   ? version
                   : throw new ArgumentException($"'{Text}' is not a use case version.", nameof(Text));


        /// <summary>
        /// Try to parse a version.
        ///
        /// A version with fewer than three numbers is read as if the missing
        /// ones were zero: devices do announce "1.0" and "1".
        /// </summary>
        /// <param name="Text">The text of a version, or null.</param>
        /// <param name="Version">The version.</param>
        public static Boolean TryParse(String? Text, out UseCaseVersion Version)
        {

            Version = default;

            if (Text is null)
                return false;

            var parts    = Text.Trim().Split('.');

            if (parts.Length is 0 or > 3)
                return false;

            var numbers  = new UInt32[3];

            for (var i = 0; i < parts.Length; i++)
                if (!UInt32.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                    return false;

            Version = new UseCaseVersion(numbers[0], numbers[1], numbers[2]);

            return true;

        }

        #endregion

        #region CompareTo(Other) / operators

        /// <summary>
        /// Compare two versions, number by number.
        /// </summary>
        /// <param name="Other">Another version.</param>
        public Int32 CompareTo(UseCaseVersion Other)
        {

            if (Major != Other.Major)  return Major.CompareTo(Other.Major);
            if (Minor != Other.Minor)  return Minor.CompareTo(Other.Minor);

            return Patch.CompareTo(Other.Patch);

        }

        /// <summary>Whether the one version is below the other.</summary>
        public static Boolean operator < (UseCaseVersion A, UseCaseVersion B) => A.CompareTo(B) <  0;

        /// <summary>Whether the one version is above the other.</summary>
        public static Boolean operator > (UseCaseVersion A, UseCaseVersion B) => A.CompareTo(B) >  0;

        /// <summary>Whether the one version is not above the other.</summary>
        public static Boolean operator <=(UseCaseVersion A, UseCaseVersion B) => A.CompareTo(B) <= 0;

        /// <summary>Whether the one version is not below the other.</summary>
        public static Boolean operator >=(UseCaseVersion A, UseCaseVersion B) => A.CompareTo(B) >= 0;

        #endregion


        #region HasSameMajorAs(Other)

        /// <summary>
        /// Whether two versions belong to the same major version of the use
        /// case, which is what decides whether they are meant to work together.
        /// </summary>
        /// <param name="Other">Another version.</param>
        public Boolean HasSameMajorAs(UseCaseVersion Other)

            => Major == Other.Major;

        #endregion

        #region (static) Best(Ours, Theirs, out Chosen, out SameMajor)

        /// <summary>
        /// Which of the versions a partner announces to work with, following
        /// section 3.1.2 of the use case specifications.
        ///
        /// The highest version sharing our major number, where there is one.
        /// Otherwise the highest they announce at all - the specification asks
        /// us to try rather than to give up - and <paramref name="SameMajor"/>
        /// then says that the partner is on a major version we do not implement,
        /// which is something a test bench reports and an application may decide
        /// about.
        /// </summary>
        /// <param name="Ours">The version we implement.</param>
        /// <param name="Theirs">The versions the partner announces.</param>
        /// <param name="Chosen">The version to work with.</param>
        /// <param name="SameMajor">Whether it shares our major number.</param>
        /// <returns>False when the partner announces no version at all.</returns>
        public static Boolean Best(UseCaseVersion               Ours,
                                   IEnumerable<UseCaseVersion>  Theirs,
                                   out UseCaseVersion           Chosen,
                                   out Boolean                  SameMajor)
        {

            Chosen     = default;
            SameMajor  = false;

            var theirs = Theirs.ToList();

            if (theirs.Count == 0)
                return false;

            var matching = theirs.Where(version => version.HasSameMajorAs(Ours)).ToList();

            if (matching.Count > 0)
            {
                Chosen     = matching.Max();
                SameMajor  = true;
                return true;
            }

            Chosen = theirs.Max();

            return true;

        }


        /// <summary>
        /// The same, over the versions as they arrive on the wire.
        /// </summary>
        /// <param name="Ours">The version we implement.</param>
        /// <param name="Theirs">The versions the partner announces, as text.</param>
        /// <param name="Chosen">The version to work with.</param>
        /// <param name="SameMajor">Whether it shares our major number.</param>
        public static Boolean Best(UseCaseVersion       Ours,
                                   IEnumerable<String?>  Theirs,
                                   out UseCaseVersion   Chosen,
                                   out Boolean          SameMajor)
        {

            var versions = new List<UseCaseVersion>();

            foreach (var text in Theirs)
                if (TryParse(text, out var version))
                    versions.Add(version);

            return Best(Ours, versions, out Chosen, out SameMajor);

        }

        #endregion

        #region (static) Announced(Supported)

        /// <summary>
        /// Which of the versions we support to announce: the highest of each
        /// major version, and nothing else (section 3.1.2).
        /// </summary>
        /// <param name="Supported">Every version we implement.</param>
        public static IEnumerable<UseCaseVersion> Announced(IEnumerable<UseCaseVersion> Supported)

            => [.. Supported.
                     GroupBy(version => version.Major).
                     Select (group   => group.Max()).
                     Order()];

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return the text of this version, as it goes over the wire.
        /// </summary>
        public override String ToString()

            => $"{Major}.{Minor}.{Patch}";

        #endregion

    }

}
