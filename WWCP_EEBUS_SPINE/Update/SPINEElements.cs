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
    /// Which elements of a data type a filter names (SPINE 1.3.0, 5.3.4.8).
    ///
    /// An "&lt;ELEMENTS&gt;" instance has the same shape as the function data,
    /// but carries no values: every element is either present, which names it,
    /// or absent. The specification allows it in exactly two places - a deletion
    /// and a partial read - and this class is both of them:
    /// <see cref="Remove"/> deletes what is named, <see cref="Keep"/> answers
    /// with what is named and nothing else.
    ///
    /// An element which itself names elements reaches one level deeper: an
    /// elements instance which says nothing more than "timePeriod" removes the
    /// whole time period, one which says "timePeriod.startTime" removes only its
    /// start time.
    /// </summary>
    public static class SPINEElements
    {

        #region Names(Elements)

        /// <summary>
        /// The names of the elements the given instance names.
        /// </summary>
        /// <param name="Elements">An "elements" instance of the function.</param>
        public static IEnumerable<String> Names(Object Elements)

            => SPINETypeInfo.Of(Elements.GetType()).
                   Properties.
                   Where (property => property.Get(Elements) is not null).
                   Select(property => property.Name);

        #endregion

        #region NamesAnything(Elements)

        /// <summary>
        /// Whether the given instance names any element at all. An element tag
        /// names nothing, because it has nothing to name - that is what makes it
        /// the end of the road: the element it stands for is meant as a whole.
        /// </summary>
        /// <param name="Elements">An "elements" instance of the function, or null.</param>
        public static Boolean NamesAnything(Object? Elements)

            => Elements is not null &&
               SPINETypeInfo.Of(Elements.GetType()).
                   Properties.Any(property => property.Get(Elements) is not null);

        #endregion


        #region Remove(Item, Elements)

        /// <summary>
        /// Delete the named elements from the given data.
        /// </summary>
        /// <param name="Item">An instance of a data type of the model, which is changed in place.</param>
        /// <param name="Elements">An "elements" instance naming what to delete.</param>
        public static void Remove(Object Item, Object Elements)
        {

            var itemInfo = SPINETypeInfo.Of(Item.GetType());

            foreach (var element in SPINETypeInfo.Of(Elements.GetType()).Properties)
            {

                var named = element.Get(Elements);

                if (named is null)
                    continue;

                var property = itemInfo.Find(element.Name);

                if (property is null)
                    continue;

                var value = property.Get(Item);

                if (value is null)
                    continue;

                // The element names elements of its own: reach into it rather
                // than deleting it as a whole.
                if (named is IList namedList && value is IList valueList)
                {

                    var template = namedList.Cast<Object?>().FirstOrDefault(entry => NamesAnything(entry));

                    if (template is not null)
                    {
                        foreach (var entry in valueList)
                            if (entry is not null)
                                Remove(entry, template);
                        continue;
                    }

                }

                else if (NamesAnything(named))
                {
                    Remove(value, named);
                    continue;
                }

                property.Set(Item, null);

            }

        }

        #endregion

        #region Keep  (Item, Elements, KeepIdentifiers = true)

        /// <summary>
        /// A copy of the given data holding the named elements and nothing else.
        ///
        /// This is the answering half of a partial read. The identifiers come
        /// along whether they were asked for or not: SPINE 1.3.0, 5.3.4.5
        /// requires the identifiers of every list entry of a reply to be
        /// complete, even where the read did not name them.
        /// </summary>
        /// <param name="Item">An instance of a data type of the model.</param>
        /// <param name="Elements">An "elements" instance naming what to answer with.</param>
        /// <param name="KeepIdentifiers">Whether to keep the identifiers of the data type as well.</param>
        public static Object Keep(Object   Item,
                                  Object   Elements,
                                  Boolean  KeepIdentifiers = true)
        {

            var itemInfo   = SPINETypeInfo.Of(Item.GetType());
            var result     = Activator.CreateInstance(Item.GetType())!;
            var elementsBy = SPINETypeInfo.Of(Elements.GetType());

            foreach (var property in itemInfo.Properties)
            {

                var value = property.Get(Item);

                if (value is null)
                    continue;

                if (KeepIdentifiers && property.IsKey)
                {
                    property.Set(result, SPINETypeInfo.Clone(value));
                    continue;
                }

                var named = elementsBy.Find(property.Name)?.Get(Elements);

                if (named is null)
                    continue;

                if (named is IList namedList && value is IList valueList)
                {

                    var template = namedList.Cast<Object?>().FirstOrDefault(entry => NamesAnything(entry));

                    if (template is not null)
                    {

                        var copy = (IList) Activator.CreateInstance(property.Property.PropertyType)!;

                        foreach (var entry in valueList)
                            copy.Add(entry is not null
                                         ? Keep(entry, template, KeepIdentifiers)
                                         : null);

                        property.Set(result, copy);
                        continue;

                    }

                }

                else if (NamesAnything(named))
                {
                    property.Set(result, Keep(value, named, KeepIdentifiers));
                    continue;
                }

                property.Set(result, SPINETypeInfo.Clone(value));

            }

            return result;

        }

        #endregion

    }

}
