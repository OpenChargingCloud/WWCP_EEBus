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

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// The answering half of a restricted read (SPINE 1.3.0, 5.3.4.4 and 5.3.4.5).
    ///
    /// A "read" with cmdControl "partial" carries an empty function and says
    /// through its filter which entries and which elements it wants back. This
    /// class produces exactly that answer out of the data a device holds -
    /// including the rule that the identifiers of every entry of the reply are
    /// complete even where the read did not ask for them.
    /// </summary>
    public static class SPINERead
    {

        #region Apply<T>(Data, Cmd)

        /// <summary>
        /// What a read asks for out of the data of a function.
        /// </summary>
        /// <typeparam name="T">The data type of the function.</typeparam>
        /// <param name="Data">The data as the device holds it, or null when it holds none.</param>
        /// <param name="Cmd">The read command.</param>
        public static T? Apply<T>(T? Data, CmdType Cmd)

            where T : class

            => (T?) Apply((Object?) Data, Cmd);

        #endregion

        #region Apply   (Data, Cmd)

        /// <summary>
        /// What a read asks for out of the data of a function.
        /// </summary>
        /// <param name="Data">The data as the device holds it, or null when it holds none.</param>
        /// <param name="Cmd">The read command.</param>
        /// <returns>The reply data. A read without a filter is a full read and answers everything.</returns>
        public static Object? Apply(Object? Data, CmdType Cmd)
        {

            if (Data is null)
                return null;

            var partial = Cmd.Filter?.FirstOrDefault(filter => filter.IsPartial);

            if (partial is null)
                return SPINETypeInfo.Clone(Data);

            var function = Cmd.Function?.ToString() ?? Cmd.DataFunction;

            if (function is null)
                return SPINETypeInfo.Clone(Data);

            var selectors  = partial.GetSelectors(function);
            var elements   = partial.GetElements (function);

            if (selectors is null && elements is null)
                return SPINETypeInfo.Clone(Data);

            var info = SPINETypeInfo.Of(Data.GetType());

            #region A function which is not a list: only the elements restrict it

            if (info.ListProperty is null)
                return elements is not null
                           ? SPINEElements.Keep(Data, elements)
                           : SPINETypeInfo.Clone(Data);

            #endregion

            #region A list based function

            var listProperty  = info.ListProperty;

            var entries       = listProperty.Get(Data) is IList list
                                    ? list.Cast<Object>().ToList()
                                    : [];

            var selected      = SPINESelectors.Select(selectors, entries);

            var answer        = elements is not null
                                    ? selected.Select(entry => SPINEElements.Keep(entry, elements))
                                    : selected.Select(entry => SPINETypeInfo.Clone(entry)!);

            var result        = Activator.CreateInstance(info.Type)!;
            var answerList    = (IList) Activator.CreateInstance(listProperty.Property.PropertyType)!;

            foreach (var entry in answer)
                answerList.Add(entry);

            listProperty.Set(result, answerList);

            return result;

            #endregion

        }

        #endregion

    }

}
