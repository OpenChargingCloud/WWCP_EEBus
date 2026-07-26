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

namespace cloud.charging.open.protocols.EEBUS.UseCases
{

    /// <summary>
    /// Picking the identifiers a use case puts into a list function.
    ///
    /// SPINE allows at most one feature of a given feature type and role per
    /// entity (1.3.0, Table 21), so a use case which adds an entry to a list
    /// function is regularly adding it next to entries somebody else put there:
    /// an electric vehicle is the server of the EV commissioning, the electricity
    /// measurement and the state of charge use cases at once, and all three write
    /// to the same electrical connection feature.
    ///
    /// Which means no use case may assume its identifiers start at one. It has to
    /// look at what is already there - see
    /// <see href="../../docs/adr/0006-one-feature-many-use-cases.md">ADR 0006</see>.
    /// </summary>
    public static class UseCaseIds
    {

        #region NextFree(Used, StartingAt = 1)

        /// <summary>
        /// The lowest identifier which nobody on this feature is using yet.
        /// </summary>
        /// <param name="Used">The identifiers already taken, if any.</param>
        /// <param name="StartingAt">The lowest identifier worth having.</param>
        public static UInt32 NextFree(IEnumerable<UInt32?>  Used,
                                      UInt32                StartingAt   = 1)
        {

            var used = Used.Where (identifier => identifier.HasValue).
                            Select(identifier => identifier!.Value).
                            ToHashSet();

            var next = StartingAt;

            while (used.Contains(next))
                next++;

            return next;

        }

        #endregion

        #region NextFree(Used, Count, StartingAt = 1)

        /// <summary>
        /// The given number of identifiers which nobody on this feature is using
        /// yet, lowest first.
        /// </summary>
        /// <param name="Used">The identifiers already taken, if any.</param>
        /// <param name="Count">How many are needed.</param>
        /// <param name="StartingAt">The lowest identifier worth having.</param>
        public static IEnumerable<UInt32> NextFree(IEnumerable<UInt32?>  Used,
                                                   UInt32                Count,
                                                   UInt32                StartingAt   = 1)
        {

            var used  = Used.Where (identifier => identifier.HasValue).
                             Select(identifier => identifier!.Value).
                             ToHashSet();

            var free  = new List<UInt32>();
            var next  = StartingAt;

            while (free.Count < Count)
            {

                if (!used.Contains(next))
                    free.Add(next);

                next++;

            }

            return free;

        }

        #endregion

    }

}
