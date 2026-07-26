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
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

using Newtonsoft.Json;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// One property of a SPINE data type, as the update system needs to see it.
    /// </summary>
    public sealed class SPINEPropertyInfo
    {

        #region Properties

        /// <summary>
        /// The property itself.
        /// </summary>
        public PropertyInfo  Property        { get; }

        /// <summary>
        /// The name of the C# property, which is also the name the selectors and
        /// the elements of the same function use for it.
        /// </summary>
        public String        Name            { get; }

        /// <summary>
        /// The name of the XSD element, which is also the JSON property name.
        /// </summary>
        public String        JSONName        { get; }

        /// <summary>
        /// The position of the property within its data type.
        /// </summary>
        public Int32         Order           { get; }

        /// <summary>
        /// Whether this property is an identifier of its data type.
        /// </summary>
        public Boolean       IsKey           { get; }

        /// <summary>
        /// Whether this property is the primary identifier of its data type.
        /// </summary>
        public Boolean       IsPrimaryKey    { get; }

        /// <summary>
        /// Whether this property states that a remote peer may change its data type.
        /// </summary>
        public Boolean       IsWriteCheck    { get; }

        /// <summary>
        /// Whether this property holds a list.
        /// </summary>
        public Boolean       IsList          { get; }

        /// <summary>
        /// What this property holds: the entry type for a list, and the type
        /// without the nullable marker for everything else.
        /// </summary>
        public Type          ValueType       { get; }

        /// <summary>
        /// Whether what it holds is a data type of the SPINE model, rather than
        /// a number, a text or one of the ISO 8601 types.
        /// </summary>
        public Boolean       IsModelType     { get; }

        #endregion

        #region Constructor(s)

        internal SPINEPropertyInfo(PropertyInfo        Property,
                                   JsonPropertyAttribute  JSON)
        {

            this.Property      = Property;
            this.Name          = Property.Name;
            this.JSONName      = JSON.PropertyName ?? Property.Name;
            this.Order         = JSON.Order;

            var key            = Property.GetCustomAttribute<EEBUSKeyAttribute>();

            this.IsKey         = key is not null;
            this.IsPrimaryKey  = key?.IsPrimary ?? false;
            this.IsWriteCheck  = Property.GetCustomAttribute<EEBUSWriteCheckAttribute>() is not null;

            this.IsList        = Property.PropertyType.IsGenericType &&
                                 Property.PropertyType.GetGenericTypeDefinition() == typeof(List<>);

            this.ValueType     = IsList
                                     ? Property.PropertyType.GetGenericArguments()[0]
                                     : Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;

            this.IsModelType   = SPINETypeInfo.IsModelType(ValueType);

        }

        #endregion


        #region Get(Item) / Set(Item, Value)

        /// <summary>
        /// The value of this property of the given data type instance.
        /// </summary>
        /// <param name="Item">An instance of the data type this property belongs to.</param>
        public Object? Get(Object Item)

            => Property.GetValue(Item);


        /// <summary>
        /// Set this property of the given data type instance.
        /// </summary>
        /// <param name="Item">An instance of the data type this property belongs to.</param>
        /// <param name="Value">The new value, or null to remove it.</param>
        public void Set(Object Item, Object? Value)
        {
            Property.SetValue(Item, Value);
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this property.
        /// </summary>
        public override String ToString()

            => $"{Property.DeclaringType?.Name}.{JSONName}" +
               (IsPrimaryKey ? " (primary identifier)" : IsKey ? " (identifier)" : "") +
               (IsWriteCheck ? " (write mark)"         : "");

        #endregion

    }


    /// <summary>
    /// What the update system knows about a SPINE data type.
    ///
    /// The whole update system of SPINE - which entry of a list a partial notify
    /// means, which element a delete removes, whether a write from another
    /// device may be applied at all - is expressed in terms of the identifiers
    /// and the elements of a data type. Both are marks on the generated
    /// properties, and this is where they are read: once per type, at first use.
    ///
    /// Only properties carrying "JsonProperty" are considered. The generated
    /// types are partial and serialise opt-in, so what a hand-written addition
    /// adds next to them is convenience for us and no part of the protocol -
    /// and the update system has to see exactly what the wire sees.
    /// </summary>
    public sealed class SPINETypeInfo
    {

        #region Data

        private static readonly ConcurrentDictionary<Type, SPINETypeInfo>  cache          = new ();

        private static readonly String                                     modelNamespace = typeof(CmdType).Namespace!;

        private readonly        Dictionary<String, SPINEPropertyInfo>      byName;

        private readonly        Dictionary<String, SPINEPropertyInfo>      byJSONName;

        #endregion

        #region Properties

        /// <summary>
        /// The data type.
        /// </summary>
        public Type                 Type            { get; }

        /// <summary>
        /// All of its properties, in the order of the specification.
        /// </summary>
        public SPINEPropertyInfo[]  Properties      { get; }

        /// <summary>
        /// Its identifiers, in the order of the specification, which is the
        /// order of their hierarchy (SPINE 1.3.0, 5.3.4.6.1).
        /// </summary>
        public SPINEPropertyInfo[]  Keys            { get; }

        /// <summary>
        /// Its primary identifiers.
        /// </summary>
        public SPINEPropertyInfo[]  PrimaryKeys     { get; }

        /// <summary>
        /// The property which states whether a remote peer may change this data,
        /// where the data type has one.
        /// </summary>
        public SPINEPropertyInfo?   WriteCheck      { get; }

        /// <summary>
        /// The list of a list based function ("xListData" holds one list of
        /// "xData" and nothing else), or null when this is not one.
        ///
        /// Which functions are list based is not a question of their name: it is
        /// what their data type looks like.
        /// </summary>
        public SPINEPropertyInfo?   ListProperty    { get; }

        #endregion

        #region Constructor(s)

        private SPINETypeInfo(Type Type)
        {

            this.Type         = Type;

            this.Properties   = [.. Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).
                                         Select(property => (Property:  property,
                                                             JSON:      property.GetCustomAttribute<JsonPropertyAttribute>())).
                                         Where (entry    => entry.JSON is not null).
                                         Select(entry    => new SPINEPropertyInfo(entry.Property, entry.JSON!)).
                                         OrderBy(property => property.Order)];

            this.Keys         = [.. Properties.Where(property => property.IsKey)];
            this.PrimaryKeys  = [.. Properties.Where(property => property.IsPrimaryKey)];
            this.WriteCheck   =     Properties.FirstOrDefault(property => property.IsWriteCheck);

            this.ListProperty =     Properties.Length == 1 &&
                                    Properties[0].IsList   &&
                                    Properties[0].IsModelType
                                        ? Properties[0]
                                        : null;

            this.byName       = Properties.ToDictionary(property => property.Name,
                                                        StringComparer.Ordinal);

            this.byJSONName   = Properties.ToDictionary(property => property.JSONName,
                                                        StringComparer.Ordinal);

        }

        #endregion


        #region (static) Of(Type) / IsModelType(Type)

        /// <summary>
        /// What the update system knows about the given data type.
        /// </summary>
        /// <param name="Type">A data type of the SPINE model.</param>
        public static SPINETypeInfo Of(Type Type)

            => cache.GetOrAdd(Type, type => new SPINETypeInfo(type));


        /// <summary>
        /// Whether the given type is a data type of the generated SPINE model.
        /// </summary>
        /// <param name="Type">A type.</param>
        public static Boolean IsModelType(Type Type)

            => Type.IsClass &&
               Type.Namespace == modelNamespace;

        #endregion

        #region Find(Name)

        /// <summary>
        /// The property of the given name, or null when the data type has none.
        ///
        /// The selectors and the elements of a function name their properties
        /// exactly as the function data does, which is what makes it possible to
        /// apply them to a data type the update system has never seen.
        /// </summary>
        /// <param name="Name">The name of a C# property.</param>
        public SPINEPropertyInfo? Find(String Name)

            => byName.GetValueOrDefault(Name);

        #endregion

        #region FindJSON(JSONName)

        /// <summary>
        /// The property of the given element name of the specification, or null
        /// when the data type has none.
        /// </summary>
        /// <param name="JSONName">The name of an XSD element, which is also the JSON property name.</param>
        public SPINEPropertyInfo? FindJSON(String JSONName)

            => byJSONName.GetValueOrDefault(JSONName);

        #endregion


        #region HasIdentifiers(Item)

        /// <summary>
        /// Whether every identifier of the given entry has a value, so that it
        /// names exactly one entry of a list (SPINE 1.3.0, 5.3.4.6.1).
        ///
        /// A data type without identifiers answers false: its entries cannot be
        /// named one by one, and the specification allows only the exchange of
        /// the complete list for those.
        /// </summary>
        /// <param name="Item">An instance of this data type.</param>
        public Boolean HasIdentifiers(Object Item)

            => Keys.Length > 0 &&
               Keys.All(key => key.Get(Item) is not null);

        #endregion

        #region KeyOf(Item)

        /// <summary>
        /// The identifier of the given entry as a text, or null when it is
        /// incomplete.
        /// </summary>
        /// <param name="Item">An instance of this data type.</param>
        public String? KeyOf(Object Item)
        {

            if (Keys.Length == 0)
                return null;

            var parts = new List<String>(Keys.Length);

            foreach (var key in Keys)
            {

                var value = key.Get(Item);

                if (value is null)
                    return null;

                parts.Add(Text(value));

            }

            return String.Join('|', parts);

        }

        #endregion

        #region HasDataBeyondItsIdentifiers(Item)

        /// <summary>
        /// Whether the given entry says anything at all beyond naming itself.
        ///
        /// Devices are known to announce the structure of a list first, by
        /// sending its entries with nothing but their identifiers, and to send
        /// the data afterwards. Whether such an entry is worth keeping is a
        /// question this class does not answer - see
        /// <see cref="SPINEUpdateOptions.IgnoreEntriesWithoutData"/>.
        /// </summary>
        /// <param name="Item">An instance of this data type.</param>
        public Boolean HasDataBeyondItsIdentifiers(Object Item)

            => Properties.Any(property => !property.IsKey &&
                                           property.Get(Item) is not null);

        #endregion

        #region MayBeWrittenBy(Item, RemoteWrite)

        /// <summary>
        /// Whether the given entry may be changed.
        ///
        /// A local change is always allowed: the owner of the data may change
        /// it. A change which arrived from another device is allowed only where
        /// the data itself says so - "isLimitChangeable", "isValueChangeable",
        /// "isSetpointChangeable" - and a data type which says nothing about it
        /// leaves the answer to the feature, one layer above.
        /// </summary>
        /// <param name="Item">An instance of this data type.</param>
        /// <param name="RemoteWrite">Whether the change arrived from another device.</param>
        public Boolean MayBeWrittenBy(Object Item, Boolean RemoteWrite)

            => !RemoteWrite ||
                WriteCheck is null ||
                WriteCheck.Get(Item) is Boolean changeable && changeable;

        #endregion


        #region (internal static) Text(Value)

        /// <summary>
        /// The value of an identifier as a text, in a way which does not depend
        /// on the machine's locale.
        /// </summary>
        internal static String Text(Object Value)

            => Value switch {
                   String       text         => text,
                   IFormattable formattable  => formattable.ToString(null, CultureInfo.InvariantCulture),
                   _                         => IsModelType(Value.GetType())
                                                    ? SPINEJSON.ToJSON(Value)
                                                    : Value.ToString() ?? ""
               };

        #endregion

        #region (static) Clone(Value)

        /// <summary>
        /// A copy of the given value which shares nothing with it.
        ///
        /// The update system never changes what it was given: a partial write
        /// which turns out to be refused halfway through has to leave the data
        /// of the device exactly as it was (SPINE 1.3.0, 5.3.4.2: a server shall
        /// only execute a restricted write if it can execute it completely).
        ///
        /// Everything a generated property can hold is either a data type of the
        /// model, a list of those or of numbers, or an immutable value - so
        /// copying the first two and passing on the third is a complete answer.
        /// </summary>
        /// <param name="Value">A value of the SPINE model, or null.</param>
        public static Object? Clone(Object? Value)
        {

            if (Value is null)
                return null;

            var type = Value.GetType();

            if (IsModelType(type))
            {

                var copy = Activator.CreateInstance(type)!;

                foreach (var property in Of(type).Properties)
                    property.Set(copy, Clone(property.Get(Value)));

                return copy;

            }

            if (Value is IList list && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {

                var copy = (IList) Activator.CreateInstance(type)!;

                foreach (var entry in list)
                    copy.Add(Clone(entry));

                return copy;

            }

            // Numbers, texts, the extensible string types and the ISO 8601
            // types: all of them immutable, all of them safe to share.
            return Value;

        }


        /// <summary>
        /// A copy of the given value which shares nothing with it.
        /// </summary>
        /// <typeparam name="T">A data type of the SPINE model.</typeparam>
        /// <param name="Value">A value, or null.</param>
        public static T? Clone<T>(T? Value)
            where T : class

            => (T?) Clone((Object?) Value);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this data type.
        /// </summary>
        public override String ToString()

            => $"{Type.Name}: {Properties.Length} properties" +
               (Keys.Length       > 0    ? $", identified by {String.Join(", ", Keys.Select(key => key.JSONName))}" : "") +
               (ListProperty is not null ? $", a list of {ListProperty.ValueType.Name}"                             : "");

        #endregion

    }

}
