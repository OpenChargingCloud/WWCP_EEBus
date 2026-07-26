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
    /// How an update is to be applied.
    /// </summary>
    /// <param name="RemoteWrite">
    /// Whether the data arrived as a "write" from another device. Only then is
    /// the write mark of a data type consulted, and only then is a refusal a
    /// refusal: a "notify" is the owner of the data telling us what changed, and
    /// there is nothing to permit.
    /// </param>
    /// <param name="IgnoreEntriesWithoutData">
    /// Whether to drop incoming list entries which carry nothing but their
    /// identifiers. Devices are known to announce the structure of a list that
    /// way before they send the data, and the Go reference implementation drops
    /// those entries to keep them from becoming empty rows.
    /// <br />
    /// This is off by default. A test bench which quietly drops what a device
    /// sent cannot report what the device sent - and per SPINE 1.3.0, Annex A a
    /// list entry added by a "write" has to be complete, so an entry which is
    /// not is a finding rather than noise.
    /// </param>
    /// <param name="Sort">
    /// Whether to order the result by the identifiers of its entries. SPINE
    /// lists are identified rather than ordered, so this changes no meaning, and
    /// it makes two runs of the same exchange comparable.
    /// </param>
    public sealed record SPINEUpdateOptions(Boolean  RemoteWrite               = false,
                                            Boolean  IgnoreEntriesWithoutData  = false,
                                            Boolean  Sort                      = true)
    {

        /// <summary>
        /// A change of our own data.
        /// </summary>
        public static readonly SPINEUpdateOptions Local   = new ();

        /// <summary>
        /// A change which another device notified us of.
        /// </summary>
        public static readonly SPINEUpdateOptions Notify  = new ();

        /// <summary>
        /// A change another device asked us to make.
        /// </summary>
        public static readonly SPINEUpdateOptions Write   = new (RemoteWrite: true);

    }


    /// <summary>
    /// What became of an update.
    ///
    /// There are two ways not to succeed, and they are told apart by the data
    /// rather than by a second flag: a **refused** write answers with the data
    /// as it was, because SPINE 1.3.0, 5.3.4.2 allows a server to execute a
    /// restricted write only completely or not at all; a command which is
    /// **out of spec** but unambiguous is carried out, and answers with the new
    /// data and the reason why it should not have looked like that. A stack may
    /// ignore the second kind. A test bench is here for it.
    /// </summary>
    /// <typeparam name="T">The data type of the function.</typeparam>
    /// <param name="Data">The function data after the update. On a refused write this is the data as it was.</param>
    /// <param name="Success">Whether the command was a proper restricted function exchange and was carried out completely.</param>
    /// <param name="Problem">What was wrong with the command, or why it was refused.</param>
    public sealed record SPINEUpdateResult<T>(T?       Data,
                                              Boolean  Success,
                                              String?  Problem = null)

        where T : class

    {

        /// <summary>
        /// Return a text representation of this result.
        /// </summary>
        public override String ToString()

            => Success
                   ? "ok"
                   : $"refused: {Problem ?? "unknown"}";

    }


    /// <summary>
    /// The restricted function exchange of SPINE (SPINE 1.3.0, 5.3.4).
    ///
    /// A SPINE command may carry the whole of a function, or a filter saying
    /// that only a part of it is meant: entries of a list named by their
    /// identifiers or selected by their content, single elements of an entry, a
    /// deletion, or all of that within one message. This class applies such a
    /// command to the data a device holds.
    ///
    /// None of it knows a single function by name. Which properties identify an
    /// entry, which one says whether a remote peer may change it, and which
    /// property of a filter belongs to which function are marks the generator
    /// put on the model, so a function nobody has thought of yet is updated by
    /// the same code as "loadControlLimitListData".
    ///
    /// The order of the work is the one the specification prescribes: the
    /// deletion first, the partial afterwards.
    /// </summary>
    public static class SPINEUpdate
    {

        #region Apply<T>(Existing, Incoming, Cmd, Options = null)

        /// <summary>
        /// Apply a command to the data of a function.
        /// </summary>
        /// <typeparam name="T">The data type of the function.</typeparam>
        /// <param name="Existing">The data as the device holds it, or null when it holds none.</param>
        /// <param name="Incoming">The data the command carries, or null when it carries none.</param>
        /// <param name="Cmd">The command, whose filters say what is meant.</param>
        /// <param name="Options">How the update is to be applied. Local changes by default.</param>
        public static SPINEUpdateResult<T> Apply<T>(T?                   Existing,
                                                    T?                   Incoming,
                                                    CmdType              Cmd,
                                                    SPINEUpdateOptions?  Options   = null)

            where T : class

        {

            var result = Apply((Object?) Existing,
                               (Object?) Incoming,
                               Cmd,
                               Options);

            return new SPINEUpdateResult<T>((T?) result.Data,
                                            result.Success,
                                            result.Problem);

        }

        #endregion

        #region Apply   (Existing, Incoming, Cmd, Options = null)

        /// <summary>
        /// Apply a command to the data of a function.
        /// </summary>
        /// <param name="Existing">The data as the device holds it, or null when it holds none.</param>
        /// <param name="Incoming">The data the command carries, or null when it carries none.</param>
        /// <param name="Cmd">The command, whose filters say what is meant.</param>
        /// <param name="Options">How the update is to be applied. Local changes by default.</param>
        public static SPINEUpdateResult<Object> Apply(Object?              Existing,
                                                      Object?              Incoming,
                                                      CmdType              Cmd,
                                                      SPINEUpdateOptions?  Options   = null)
        {

            var options = Options ?? SPINEUpdateOptions.Local;
            var type    = Existing?.GetType() ?? Incoming?.GetType();

            if (type is null)
                return new (null, true);

            if (Existing is not null && Incoming is not null && Existing.GetType() != Incoming.GetType())
                return new (Existing,
                            false,
                            $"The command carries '{Incoming.GetType().Name}', but the device holds '{Existing.GetType().Name}'.");

            #region Which filters the command carries

            var partialFilters  = Cmd.Filter?.Where(filter => filter.IsPartial).ToArray() ?? [];
            var deleteFilters   = Cmd.Filter?.Where(filter => filter.IsDelete). ToArray() ?? [];

            // SPINE 1.3.0, 5.3.4.2: at maximum one "delete" filter and at
            // maximum one "partial" filter shall be used in one command.
            if (partialFilters.Length > 1 || deleteFilters.Length > 1)
                return new (Existing,
                            false,
                            $"The command carries {deleteFilters.Length} delete and {partialFilters.Length} partial filters; " +
                             "SPINE 1.3.0, 5.3.4.2 allows at most one of each.");

            var partial         = partialFilters.FirstOrDefault();
            var delete          = deleteFilters. FirstOrDefault();

            // No filter at all is a full function exchange: what arrives is what
            // the function is from now on.
            if (partial is null && delete is null)
                return new (SPINETypeInfo.Clone(Incoming), true);

            #endregion

            #region Which function the filters are about

            // SPINE 1.3.0, 5.3.4.1: "datagram.payload.cmd.function" shall be
            // used and include the correct function name if and only if any of
            // the other cmdOptions is used. Where a command leaves it out, the
            // payload and the filter still name their function - so the command
            // can be carried out, and is reported for what it is.
            var function      = Cmd.Function?.ToString()
                                    ?? Cmd.DataFunction
                                    ?? partial?.FilterFunction
                                    ?? delete?. FilterFunction;

            var unnamed       = Cmd.Function is null
                                    ? "The command uses a filter, but does not state the name of its function " +
                                      "(SPINE 1.3.0, 5.3.4.1)."
                                    : null;

            if (function is null)
                return new (Existing,
                            false,
                            "The command uses a filter, but nothing within it says which function it is about " +
                            "(SPINE 1.3.0, 5.3.4.1).");

            var deleteSelectors   = delete?. GetSelectors(function);
            var deleteElements    = delete?. GetElements (function);
            var partialSelectors  = partial?.GetSelectors(function);
            var partialElements   = partial?.GetElements (function);

            #endregion

            var info = SPINETypeInfo.Of(type);

            var result = info.ListProperty is not null

                       ? UpdateList  (info,
                                      Existing,
                                      Incoming,
                                      deleteSelectors,
                                      deleteElements,
                                      partial is not null,
                                      partialSelectors,
                                      partialElements,
                                      delete  is not null,
                                      options)

                       : UpdateSingle(info,
                                      Existing,
                                      Incoming,
                                      deleteElements,
                                      deleteSelectors,
                                      partial is not null,
                                      partialElements,
                                      delete  is not null,
                                      options);

            return unnamed is null
                       ? result
                       : result with { Success  = false,
                                       Problem  = result.Problem ?? unnamed };

        }

        #endregion


        #region (private static) UpdateList  (...)

        /// <summary>
        /// The update of a list based function ("xListData"), which is where the
        /// restricted function exchange has all of its cases.
        /// </summary>
        private static SPINEUpdateResult<Object> UpdateList(SPINETypeInfo       Info,
                                                            Object?             Existing,
                                                            Object?             Incoming,
                                                            Object?             DeleteSelectors,
                                                            Object?             DeleteElements,
                                                            Boolean             HasPartialFilter,
                                                            Object?             PartialSelectors,
                                                            Object?             PartialElements,
                                                            Boolean             HasDeleteFilter,
                                                            SPINEUpdateOptions  Options)
        {

            var listProperty  = Info.ListProperty!;
            var entryInfo     = SPINETypeInfo.Of(listProperty.ValueType);

            var problem       = (String?) null;
            var refused       = false;

            var entries       = Existing is not null && listProperty.Get(Existing) is IList existingList
                                    ? existingList.Cast<Object>().Select(entry => SPINETypeInfo.Clone(entry)!).ToList()
                                    : [];

            var incoming      = Incoming is not null && listProperty.Get(Incoming) is IList incomingList
                                    ? incomingList.Cast<Object>().ToList()
                                    : [];

            #region The deletion, first (SPINE 1.3.0, 5.3.4.2 and 5.3.4.3)

            if (HasDeleteFilter)
            {

                if (DeleteSelectors is null && DeleteElements is null)
                    problem = "The delete filter names neither entries nor elements.";

                else
                {

                    var selected = SPINESelectors.Select(DeleteSelectors, entries).ToList();

                    foreach (var entry in selected)
                        if (!entryInfo.MayBeWrittenBy(entry, Options.RemoteWrite))
                        {
                            refused  = true;
                            problem ??= $"The device does not allow another device to change the entry '{entryInfo.KeyOf(entry) ?? "?"}'.";
                        }

                    if (!refused)
                    {

                        // Selectors alone remove the whole entry, elements
                        // remove what they name - within the selected entries
                        // where both are given, within all of them otherwise
                        // (SPINE 1.3.0, 5.3.4.8, rules 1 and 2).
                        if (DeleteElements is null)
                            entries.RemoveAll(selected.Contains);

                        else
                            foreach (var entry in selected)
                                SPINEElements.Remove(entry, DeleteElements);

                    }

                }

            }

            #endregion

            #region The partial, afterwards

            // A pure deletion carries the function, but empty: "must be present
            // but can be ignored" (SPINE 1.3.0, Table 6, "dc").
            if (HasPartialFilter && incoming.Count > 0 && !refused)
            {

                if (PartialElements is not null)
                    problem ??= "The partial filter names elements, which SPINE 1.3.0, 5.3.4.8 allows " +
                                "only for a deletion and for a partial read; they are ignored.";

                if (Options.IgnoreEntriesWithoutData)
                    incoming = [.. incoming.Where(entry => entryInfo.HasDataBeyondItsIdentifiers(entry))];

                if (PartialSelectors is not null)
                {

                    // SPINE 1.3.0, Table 6: the selectors say where the data
                    // belongs, the function itself carries no identifiers, and
                    // no entry can be added this way.
                    var selected = SPINESelectors.Select(PartialSelectors, entries).ToList();

                    foreach (var entry in selected)
                        if (!entryInfo.MayBeWrittenBy(entry, Options.RemoteWrite))
                        {
                            refused  = true;
                            problem ??= $"The device does not allow another device to change the entry '{entryInfo.KeyOf(entry) ?? "?"}'.";
                        }

                    if (!refused)
                        foreach (var source in incoming)
                            foreach (var entry in selected)
                                CopyStatedElements(source, entry);

                }

                else if (entryInfo.Keys.Length == 0)
                {

                    // SPINE 1.3.0, 5.3.4.1: a list whose entries cannot be
                    // identified can only be exchanged as a whole. Saying so and
                    // then exchanging it as a whole is the most useful answer we
                    // can give: the data stays readable, and the command is
                    // reported for what it is.
                    problem ??= $"The entries of '{Info.Type.Name}' have no identifiers, so SPINE 1.3.0, 5.3.4.1 " +
                                 "allows only the exchange of the complete list; it was replaced as a whole.";

                    entries = [.. incoming.Select(entry => SPINETypeInfo.Clone(entry)!)];

                }

                else
                    foreach (var source in incoming)
                    {

                        if (entryInfo.HasIdentifiers(source))
                        {

                            var key    = entryInfo.KeyOf(source);
                            var target = entries.FirstOrDefault(entry => entryInfo.KeyOf(entry) == key);

                            if (target is null)
                                entries.Add(SPINETypeInfo.Clone(source)!);

                            else if (!entryInfo.MayBeWrittenBy(target, Options.RemoteWrite))
                            {
                                refused  = true;
                                problem ??= $"The device does not allow another device to change the entry '{key}'.";
                            }

                            else
                                CopyStatedElements(source, target);

                        }

                        else
                        {

                            // SPINE 1.3.0, Table 6 and Table 7, note *1: a list
                            // item without identifier is applied to all entries.
                            foreach (var entry in entries)
                                if (!entryInfo.MayBeWrittenBy(entry, Options.RemoteWrite))
                                {
                                    refused  = true;
                                    problem ??= $"The device does not allow another device to change the entry '{entryInfo.KeyOf(entry) ?? "?"}'.";
                                }

                            if (!refused)
                                foreach (var entry in entries)
                                    CopyStatedElements(source, entry);

                        }

                    }

            }

            #endregion

            // SPINE 1.3.0, 5.3.4.2: a write with restricted function exchange
            // shall only be executed by a server if it can be executed
            // completely. Half of it applied is worse than none of it.
            if (refused)
                return new (Existing, false, problem);

            if (Options.Sort)
                entries = Sort(entryInfo, entries);

            var result = Activator.CreateInstance(Info.Type)!;
            var list   = (IList) Activator.CreateInstance(listProperty.Property.PropertyType)!;

            foreach (var entry in entries)
                list.Add(entry);

            listProperty.Set(result, list);

            return new (result, problem is null, problem);

        }

        #endregion

        #region (private static) UpdateSingle(...)

        /// <summary>
        /// The update of a function which is not a list, where there is nothing
        /// to select and the elements are the whole of the restriction.
        /// </summary>
        private static SPINEUpdateResult<Object> UpdateSingle(SPINETypeInfo       Info,
                                                              Object?             Existing,
                                                              Object?             Incoming,
                                                              Object?             DeleteElements,
                                                              Object?             DeleteSelectors,
                                                              Boolean             HasPartialFilter,
                                                              Object?             PartialElements,
                                                              Boolean             HasDeleteFilter,
                                                              SPINEUpdateOptions  Options)
        {

            var problem = (String?) null;
            var result  = SPINETypeInfo.Clone(Existing) ?? Activator.CreateInstance(Info.Type)!;

            if (DeleteSelectors is not null)
                problem = "The command selects entries of a list, but the function is not a list.";

            if (HasPartialFilter && PartialElements is not null)
                problem ??= "The partial filter names elements, which SPINE 1.3.0, 5.3.4.8 allows " +
                            "only for a deletion and for a partial read; they are ignored.";

            if (!Info.MayBeWrittenBy(result, Options.RemoteWrite))
                return new (Existing,
                            false,
                            "The device does not allow another device to change this function.");

            if (HasDeleteFilter)
            {

                if (DeleteElements is null)
                    problem ??= "The delete filter names no elements.";

                else
                    SPINEElements.Remove(result, DeleteElements);

            }

            if (HasPartialFilter && Incoming is not null)
                CopyStatedElements(Incoming, result);

            return new (result, problem is null, problem);

        }

        #endregion

        #region (private static) CopyStatedElements(Source, Destination)

        /// <summary>
        /// Copy every element the source states onto the destination.
        ///
        /// An element which is stated replaces the one which was there, as a
        /// whole rather than element by element: SPINE 1.3.0, 5.3.4.7.1 lets a
        /// child which is left out fall back to its default value, so a scaled
        /// number arriving as "&lt;number&gt;14&lt;/number&gt;" is 14 - and not
        /// 14 with whatever scale happened to stand there before.
        /// </summary>
        private static void CopyStatedElements(Object Source, Object Destination)
        {

            foreach (var property in SPINETypeInfo.Of(Source.GetType()).Properties)
            {

                var value = property.Get(Source);

                if (value is not null)
                    property.Set(Destination, SPINETypeInfo.Clone(value));

            }

        }

        #endregion

        #region (private static) Sort(EntryInfo, Entries)

        /// <summary>
        /// Order the entries of a list by their identifiers, in the hierarchy
        /// the specification gives them (SPINE 1.3.0, 5.3.4.6.1). Entries whose
        /// identifiers are incomplete keep their order, at the end.
        /// </summary>
        private static List<Object> Sort(SPINETypeInfo EntryInfo, List<Object> Entries)
        {

            if (EntryInfo.Keys.Length == 0)
                return Entries;

            return [.. Entries.OrderBy(entry => entry, Comparer<Object>.Create((left, right) => {

                foreach (var key in EntryInfo.Keys)
                {

                    var a = key.Get(left);
                    var b = key.Get(right);

                    if (a is null && b is null)  continue;
                    if (a is null)               return  1;
                    if (b is null)               return -1;

                    var order = a is IComparable comparable && a.GetType() == b.GetType()
                                    ? comparable.CompareTo(b)
                                    : String.CompareOrdinal(SPINETypeInfo.Text(a), SPINETypeInfo.Text(b));

                    if (order != 0)
                        return order;

                }

                return 0;

            }))];

        }

        #endregion

    }

}
