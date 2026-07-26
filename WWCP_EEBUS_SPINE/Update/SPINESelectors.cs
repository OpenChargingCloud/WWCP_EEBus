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

using System.Collections;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// Which entries of a list a filter selects (SPINE 1.3.0, 5.3.4.7).
    ///
    /// A "&lt;SELECTORS&gt;" instance names some child elements of a list entry
    /// together with the values they must have. An entry is selected when
    /// **every** named element matches - the specification calls this a logical
    /// AND, and a command may carry several selectors to get an OR.
    ///
    /// The elements a selector names need not be identifiers. That is why a
    /// match must not be read as "this is the entry": it is a filter, and it may
    /// well select more than one entry.
    /// </summary>
    public static class SPINESelectors
    {

        #region Matches(Selectors, Item)

        /// <summary>
        /// Whether the given list entry is selected.
        /// </summary>
        /// <param name="Selectors">A "selectors" instance of the function, or null when the command carries none.</param>
        /// <param name="Item">One entry of the list.</param>
        /// <returns>True when every element named by the selectors has the value it demands. A command without selectors selects everything.</returns>
        public static Boolean Matches(Object? Selectors, Object? Item)
        {

            if (Selectors is null)
                return true;

            if (Item is null)
                return false;

            var selectorInfo = SPINETypeInfo.Of(Selectors.GetType());
            var itemInfo     = SPINETypeInfo.Of(Item.     GetType());

            foreach (var selector in selectorInfo.Properties)
            {

                var demanded = selector.Get(Selectors);

                if (demanded is null)
                    continue;

                // A selectors instance may name something the data type does not
                // have at all. Nothing can match that, and answering "yes" would
                // quietly widen the selection instead of reporting the mismatch.
                var property = itemInfo.Find(selector.Name);

                if (property is null)
                    return false;

                if (!MatchesValue(demanded, property.Get(Item)))
                    return false;

            }

            return true;

        }

        #endregion

        #region (private static) MatchesValue(Demanded, Value)

        private static Boolean MatchesValue(Object Demanded, Object? Value)
        {

            if (Value is null)
                return false;

            // A list of numbers is the address of an entity, and there only an
            // exact match counts (SPINE 1.3.0, 5.3.4.7.2: the selector
            // "entity 4" matches the entity "4", but neither "1/4" nor its
            // children).
            if (Demanded is IList demandedList &&
                Value    is IList valueList)
            {

                var demandedEntries = demandedList.Cast<Object?>().ToList();
                var valueEntries    = valueList.   Cast<Object?>().ToList();

                if (demandedEntries.Count == 0)
                    return true;

                if (SPINETypeInfo.IsModelType(demandedEntries[0]!.GetType()))
                    // A nested list of its own data types: the surrounding entry
                    // is selected when every named entry is found within it.
                    return demandedEntries.All(demandedEntry => valueEntries.Any(valueEntry => Matches(demandedEntry, valueEntry)));

                return demandedEntries.Count == valueEntries.Count &&
                       demandedEntries.Zip(valueEntries).All(pair => Equals(pair.First, pair.Second));

            }

            if (SPINETypeInfo.IsModelType(Demanded.GetType()))
                return Matches(Demanded, Value);

            return Equals(Demanded, Value);

        }

        #endregion

        #region Select(Selectors, Entries)

        /// <summary>
        /// The entries of a list which the given selectors select.
        /// </summary>
        /// <param name="Selectors">A "selectors" instance of the function, or null when the command carries none.</param>
        /// <param name="Entries">The entries of the list.</param>
        public static IEnumerable<Object> Select(Object? Selectors, IEnumerable<Object> Entries)

            => Entries.Where(entry => Matches(Selectors, entry));

        #endregion

    }

}
