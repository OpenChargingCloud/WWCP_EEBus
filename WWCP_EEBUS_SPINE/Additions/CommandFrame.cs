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

using System.Reflection;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.Model
{

    /// <summary>
    /// Which property of a command frame carries which function.
    ///
    /// "CmdType" has one property per function and "FilterType" has two, and a
    /// stack which had to know all 142 of them by name would have to be changed
    /// for every function ever added. The generated model marks each of those
    /// properties with <see cref="EEBUSFunctionAttribute"/>, and this is where
    /// that becomes usable: the reflection happens once, at startup, and
    /// everything afterwards is a dictionary lookup.
    /// </summary>
    internal static class CommandFrameMetadata
    {

        #region (class) FunctionProperty

        /// <summary>
        /// One property of a command frame which carries a function.
        /// </summary>
        /// <param name="Property">The property.</param>
        /// <param name="Function">The name of the function.</param>
        /// <param name="Part">Which face of the function it carries.</param>
        internal sealed record FunctionProperty(PropertyInfo       Property,
                                                String             Function,
                                                EEBUSFunctionPart  Part);

        #endregion

        #region Data

        internal static readonly FunctionProperty[]                               CmdProperties;
        internal static readonly FunctionProperty[]                               FilterProperties;

        private  static readonly Dictionary<String, FunctionProperty>             cmdByFunction;
        private  static readonly Dictionary<(String, EEBUSFunctionPart),
                                            FunctionProperty>                     filterByFunction;

        #endregion

        #region Constructor(s)

        static CommandFrameMetadata()
        {

            CmdProperties     = Read(typeof(CmdType));
            FilterProperties  = Read(typeof(FilterType));

            cmdByFunction     = CmdProperties.   ToDictionary(entry => entry.Function,
                                                              StringComparer.Ordinal);

            filterByFunction  = FilterProperties.ToDictionary(entry => (entry.Function, entry.Part));

        }

        #endregion


        #region (private static) Read(Type)

        private static FunctionProperty[] Read(Type Type)

            => [.. Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).
                       Select(property => (Property:   property,
                                           Attribute:  property.GetCustomAttribute<EEBUSFunctionAttribute>())).
                       Where (entry     => entry.Attribute is not null).
                       Select(entry     => new FunctionProperty(entry.Property,
                                                                entry.Attribute!.Function,
                                                                entry.Attribute.Part))];

        #endregion

        #region (internal static) CmdPropertyOf(Function) / FilterPropertyOf(Function, Part)

        internal static FunctionProperty? CmdPropertyOf(String Function)

            => cmdByFunction.GetValueOrDefault(Function);


        internal static FunctionProperty? FilterPropertyOf(String Function, EEBUSFunctionPart Part)

            => filterByFunction.GetValueOrDefault((Function, Part));

        #endregion

    }


    /// <summary>
    /// One command of a SPINE datagram (SPINE 1.3.0, CommandFrame).
    ///
    /// The XSD declares the payload as a choice of all functions, so exactly one
    /// of the many properties is set - which one is the actual content of the
    /// message.
    /// </summary>
    public partial class CmdType
    {

        #region Properties

        /// <summary>
        /// The name of the function this command carries, taken from whichever
        /// payload property is set, or null when it carries none.
        ///
        /// This is not the same as <see cref="Function"/>: that one is the
        /// function the sender declared, which is mandatory for a read and
        /// absent from most everything else.
        /// </summary>
        public String? DataFunction
        {
            get
            {

                foreach (var entry in CommandFrameMetadata.CmdProperties)
                    if (entry.Property.GetValue(this) is not null)
                        return entry.Function;

                return null;

            }
        }

        /// <summary>
        /// The payload of this command, or null when it carries none.
        /// </summary>
        public Object? Data
        {
            get
            {

                foreach (var entry in CommandFrameMetadata.CmdProperties)
                {

                    var value = entry.Property.GetValue(this);

                    if (value is not null)
                        return value;

                }

                return null;

            }
        }

        /// <summary>
        /// How many payloads this command carries. The XSD allows one; anything
        /// else is a datagram to report rather than to process.
        /// </summary>
        public UInt32 DataCount
        {
            get
            {

                var count = 0U;

                foreach (var entry in CommandFrameMetadata.CmdProperties)
                    if (entry.Property.GetValue(this) is not null)
                        count++;

                return count;

            }
        }

        #endregion


        #region GetData(Function) / SetData(Function, Data)

        /// <summary>
        /// The payload of the given function, or null when this command does not
        /// carry it.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        public Object? GetData(String Function)

            => CommandFrameMetadata.CmdPropertyOf(Function)?.Property.GetValue(this);


        /// <summary>
        /// Put the payload of the given function into this command.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Data">Its data, or null to remove it.</param>
        /// <returns>False when the function is unknown, or when the data is not of its type.</returns>
        public Boolean SetData(String Function, Object? Data)
        {

            var entry = CommandFrameMetadata.CmdPropertyOf(Function);

            if (entry is null)
                return false;

            if (Data is not null && !entry.Property.PropertyType.IsInstanceOfType(Data))
                return false;

            entry.Property.SetValue(this, Data);

            return true;

        }

        #endregion

        #region (static) For(Function, Data)

        /// <summary>
        /// A command carrying the given function.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Data">Its data.</param>
        public static CmdType? For(String Function, Object Data)
        {

            var cmd = new CmdType();

            return cmd.SetData(Function, Data)
                       ? cmd
                       : null;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this command.
        /// </summary>
        public override String ToString()

            => DataFunction
                   ?? Function?.ToString()
                   ?? "(empty)";

        #endregion

    }


    /// <summary>
    /// A filter of a command (SPINE 1.3.0, CommandFrame): which part of a
    /// function a partial read, write or notify is about.
    /// </summary>
    public partial class FilterType
    {

        #region Properties

        /// <summary>
        /// The name of the function this filter is about, taken from whichever
        /// selectors or elements property is set, or null when it names none.
        ///
        /// A partial filter without selectors and without elements is legal and
        /// means "everything"; the function is then the one of the command.
        /// </summary>
        public String? FilterFunction
        {
            get
            {

                foreach (var entry in CommandFrameMetadata.FilterProperties)
                    if (entry.Property.GetValue(this) is not null)
                        return entry.Function;

                return null;

            }
        }

        /// <summary>
        /// Whether this filter marks a partial operation.
        /// </summary>
        public Boolean IsPartial
            => CmdControl?.Partial is not null;

        /// <summary>
        /// Whether this filter marks a delete.
        /// </summary>
        public Boolean IsDelete
            => CmdControl?.Delete is not null;

        #endregion


        #region GetSelectors(Function) / GetElements(Function)

        /// <summary>
        /// The selectors of the given function, or null when this filter does
        /// not carry them.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        public Object? GetSelectors(String Function)

            => CommandFrameMetadata.FilterPropertyOf(Function, EEBUSFunctionPart.Selectors)?.
                   Property.GetValue(this);


        /// <summary>
        /// The elements of the given function, or null when this filter does not
        /// carry them.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        public Object? GetElements(String Function)

            => CommandFrameMetadata.FilterPropertyOf(Function, EEBUSFunctionPart.Elements)?.
                   Property.GetValue(this);

        #endregion

        #region SetSelectors(Function, Selectors) / SetElements(Function, Elements)

        /// <summary>
        /// Put the selectors of the given function into this filter.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Selectors">Its selectors, or null to remove them.</param>
        /// <returns>False when the function is unknown, or when the selectors are not of its type.</returns>
        public Boolean SetSelectors(String Function, Object? Selectors)

            => Set(EEBUSFunctionPart.Selectors, Function, Selectors);


        /// <summary>
        /// Put the elements of the given function into this filter.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Elements">Its elements, or null to remove them.</param>
        /// <returns>False when the function is unknown, or when the elements are not of its type.</returns>
        public Boolean SetElements(String Function, Object? Elements)

            => Set(EEBUSFunctionPart.Elements, Function, Elements);


        private Boolean Set(EEBUSFunctionPart Part, String Function, Object? Value)
        {

            var entry = CommandFrameMetadata.FilterPropertyOf(Function, Part);

            if (entry is null)
                return false;

            if (Value is not null && !entry.Property.PropertyType.IsInstanceOfType(Value))
                return false;

            entry.Property.SetValue(this, Value);

            return true;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this filter.
        /// </summary>
        public override String ToString()
        {

            var what = FilterFunction ?? "all";

            if (IsDelete)
                return $"delete {what}";

            return IsPartial
                       ? $"partial {what}"
                       : what;

        }

        #endregion

    }


    /// <summary>
    /// What a command does with the data it carries (SPINE 1.3.0, CommandFrame).
    /// </summary>
    public partial class CmdControlType
    {

        // "ForPartial" and not "Partial": the generated class already has the
        // instance property "Partial", which is the element tag itself.

        /// <summary>
        /// A command control marking a partial operation.
        /// </summary>
        public static CmdControlType ForPartial
            => new () { Partial = ElementTagType.Set };

        /// <summary>
        /// A command control marking a delete.
        /// </summary>
        public static CmdControlType ForDelete
            => new () { Delete = ElementTagType.Set };


        /// <summary>
        /// Return a text representation of this command control.
        /// </summary>
        public override String ToString()

            => Delete is not null
                   ? "delete"
                   : Partial is not null
                         ? "partial"
                         : "";

    }

}
