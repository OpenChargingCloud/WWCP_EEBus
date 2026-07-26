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

namespace cloud.charging.open.protocols.EEBUS.SPINE.Model
{

    /// <summary>
    /// Which use cases an actor of a device supports
    /// (SPINE 1.3.0, UseCaseInformation).
    ///
    /// This is what a partner reads to find out what we can do, and what we read
    /// to find out what it can do, so it is written and searched constantly.
    ///
    /// The names of the actors and of the use cases are plain strings here: the
    /// XSD restricts them by a pattern and lists no values, because which use
    /// cases exist is decided by the use case specifications and not by SPINE.
    /// The constants belong to the use case layer (WP08).
    /// </summary>
    public partial class UseCaseInformationDataType
    {

        #region Find(UseCaseName)

        /// <summary>
        /// The support of the given use case, or null when it is not listed.
        /// </summary>
        /// <param name="UseCaseName">The name of a use case.</param>
        public UseCaseSupportType? Find(String UseCaseName)

            => UseCaseSupport?.FirstOrDefault(useCase => String.Equals(useCase.UseCaseName,
                                                                       UseCaseName,
                                                                       StringComparison.Ordinal));

        #endregion

        #region Supports(UseCaseName, Scenario = null)

        /// <summary>
        /// Whether the given use case is supported and available, optionally in
        /// a given scenario.
        ///
        /// "Listed" and "available" are not the same thing: a device announces
        /// the use cases it implements and switches "useCaseAvailable" off while
        /// it cannot serve them, which is what a partner has to respect before
        /// sending anything.
        /// </summary>
        /// <param name="UseCaseName">The name of a use case.</param>
        /// <param name="Scenario">An optional scenario of it.</param>
        public Boolean Supports(String   UseCaseName,
                                UInt32?  Scenario   = null)
        {

            var useCase = Find(UseCaseName);

            if (useCase is null)
                return false;

            // "useCaseAvailable" is optional; a use case which does not say
            // otherwise is available.
            if (useCase.UseCaseAvailable == false)
                return false;

            return !Scenario.HasValue ||
                    useCase.ScenarioSupport?.Contains(Scenario.Value) == true;

        }

        #endregion

        #region Set(UseCase)

        /// <summary>
        /// Add the given use case support, or replace the one of the same name.
        /// </summary>
        /// <param name="UseCase">A use case support.</param>
        public void Set(UseCaseSupportType UseCase)
        {

            if (UseCase.UseCaseName is null)
                return;

            UseCaseSupport ??= [];

            var index = UseCaseSupport.FindIndex(entry => String.Equals(entry.UseCaseName,
                                                                        UseCase.UseCaseName,
                                                                        StringComparison.Ordinal));

            if (index >= 0)
                UseCaseSupport[index] = UseCase;
            else
                UseCaseSupport.Add(UseCase);

        }

        #endregion

        #region Remove(UseCaseName)

        /// <summary>
        /// Remove the support of the given use case.
        /// </summary>
        /// <param name="UseCaseName">The name of a use case.</param>
        /// <returns>Whether it was listed at all.</returns>
        public Boolean Remove(String UseCaseName)

            => UseCaseSupport?.RemoveAll(useCase => String.Equals(useCase.UseCaseName,
                                                                  UseCaseName,
                                                                  StringComparison.Ordinal)) > 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this use case information.
        /// </summary>
        public override String ToString()

            => $"{Actor ?? "?"} @ {Address?.ToString() ?? "?"}: " +
               (UseCaseSupport is null || UseCaseSupport.Count == 0
                    ? "no use cases"
                    : String.Join(", ", UseCaseSupport.Select(useCase => useCase.ToString())));

        #endregion

    }


    /// <summary>
    /// The support of one use case (SPINE 1.3.0, UseCaseInformation).
    /// </summary>
    public partial class UseCaseSupportType
    {

        /// <summary>
        /// Whether the given scenario is supported.
        /// </summary>
        /// <param name="Scenario">A scenario of this use case.</param>
        public Boolean SupportsScenario(UInt32 Scenario)

            => ScenarioSupport?.Contains(Scenario) == true;


        /// <summary>
        /// Return a text representation of this use case support.
        /// </summary>
        public override String ToString()
        {

            var name = $"{UseCaseName ?? "?"} {UseCaseVersion ?? "?"}";

            if (UseCaseAvailable == false)
                name += " (not available)";

            return ScenarioSupport is not null && ScenarioSupport.Count > 0
                       ? $"{name} [{String.Join(",", ScenarioSupport)}]"
                       : name;

        }

    }

}
