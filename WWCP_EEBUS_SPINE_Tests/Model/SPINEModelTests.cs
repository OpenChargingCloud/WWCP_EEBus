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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NUnit.Framework;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// Tests for the generated SPINE data model.
    ///
    /// The model is generated from the official XSDs, so these tests are not
    /// about the individual data type - they are about the generator: whether
    /// what came out still says the same as the specification and as the Go
    /// reference implementation, which is the stack proven in certification.
    /// </summary>
    [TestFixture]
    public class SPINEModelTests
    {

        #region Data

        /// <summary>
        /// The namespace of the generated model.
        /// </summary>
        private const String ModelNamespace = "cloud.charging.open.protocols.EEBUS.SPINE.Model";

        private static readonly Assembly modelAssembly = typeof(DatagramType).Assembly;

        /// <summary>
        /// The data types where the generated model deliberately says something
        /// else than the Go reference implementation, with the reason.
        ///
        /// Every entry here is checked by
        /// <see cref="KnownDeviations_FromTheGoReferenceImplementation_StillExist"/>,
        /// so that an entry which spine-go has meanwhile fixed does not quietly
        /// keep a real difference hidden.
        /// </summary>
        private static readonly Dictionary<String, String> knownDeviations = new (StringComparer.Ordinal) {

            [ "SupplyConditionThresholdRelationListDataType" ]
                = "spine-go serialises the entries as \"SupplyConditionThresholdRelationDataType\" - the name of the " +
                  "type instead of the name of the element. The XSD says \"supplyConditionThresholdRelationData\", " +
                  "and so do we. See docs/spec-deviations.md.",

            [ "Datagram" ]
                = "A wrapper of spine-go around the outer {\"datagram\": ...} object. Our SHIP layer builds that " +
                  "envelope, so the model does not need a type for it."

        };

        /// <summary>
        /// Single properties which spine-go has and the generated model
        /// deliberately does not, with the reason.
        /// </summary>
        private static readonly (String Type, String Property, String Reason)[] knownGoOnlyProperties = [

            ( "CmdType",
              "electricalConnectionCharacteristicData",
              "\"DataChoiceGroup\" does not list it, and \"FunctionEnumType\" knows only " +
              "\"electricalConnectionCharacteristicListData\": the element is the entry of that list, " +
              "not a payload of its own. See docs/spec-deviations.md." )

        ];

        /// <summary>
        /// The two types of the command frame which the XSD declares as a choice:
        /// at most one of their many properties is ever set, so the order in
        /// which they are declared cannot influence any datagram. We follow the
        /// order of the XSD, spine-go follows its own; comparing them as sets is
        /// therefore the honest comparison.
        /// </summary>
        private static readonly HashSet<String> choiceTypes = new (StringComparer.Ordinal) {
            "CmdType",
            "FilterType"
        };

        #endregion

        #region (private static) ModelTypes / JSONNames / GoModel

        /// <summary>
        /// All classes of the generated model, by their name.
        /// </summary>
        private static Dictionary<String, Type> ModelTypes()

            => modelAssembly.GetTypes().
                   Where       (type => type.Namespace == ModelNamespace &&
                                        type.IsClass                     &&
                                        !type.IsAbstract).
                   ToDictionary(type => type.Name,
                                type => type,
                                StringComparer.Ordinal);


        /// <summary>
        /// The JSON property names of a generated class, in the order in which
        /// they are serialised.
        /// </summary>
        private static List<String> JSONNames(Type Type)

            => [.. Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).
                       Select   (property => property.GetCustomAttribute<JsonPropertyAttribute>()).
                       Where    (attribute => attribute is not null).
                       OrderBy  (attribute => attribute!.Order).
                       Select   (attribute => attribute!.PropertyName ?? "")];


        /// <summary>
        /// The data types of the Go reference implementation, from the fixture
        /// generated by Apps/EEBUSModelGen.
        /// </summary>
        private static JObject GoModel()
        {

            var file = Path.Combine(TestContext.CurrentContext.TestDirectory,
                                    "TestData",
                                    "spine-go-model.json");

            if (!File.Exists(file))
                Assert.Fail($"The Go model fixture was not found at '{file}'.");

            return JObject.Parse(File.ReadAllText(file))["types"] as JObject
                       ?? throw new InvalidOperationException("The Go model fixture has no 'types'.");

        }

        #endregion


        #region PropertyNames_AreThoseOfTheGoReferenceImplementation()

        /// <summary>
        /// Every data type which both implementations know has to have the same
        /// JSON properties, in the same order.
        ///
        /// This is the check which makes the generator trustworthy: the XSDs and
        /// spine-go are two independent readings of the same specification, and
        /// where they agree, a difference in our model is our mistake.
        /// </summary>
        [Test]
        public void PropertyNames_AreThoseOfTheGoReferenceImplementation()
        {

            var modelTypes  = ModelTypes();
            var goModel     = GoModel();

            var compared    = 0;
            var differences = new List<String>();

            foreach (var (typeName, entry) in goModel)
            {

                if (!modelTypes.TryGetValue(typeName, out var type) ||
                    knownDeviations.ContainsKey(typeName))
                {
                    continue;
                }

                var ours   = JSONNames(type);
                var theirs = (entry?["fields"] as JArray)?.
                                 Select(field => field.Value<String>() ?? "").
                                 Where (field => !knownGoOnlyProperties.Any(known => known.Type     == typeName &&
                                                                                     known.Property == field)).
                                 ToList() ?? [];

                compared++;

                // A choice: the order of the declarations means nothing, because
                // at most one of them is ever set.
                var equal  = choiceTypes.Contains(typeName)
                                 ? !ours.Except(theirs, StringComparer.Ordinal).Any() &&
                                   !theirs.Except(ours, StringComparer.Ordinal).Any()
                                 : ours.SequenceEqual(theirs, StringComparer.Ordinal);

                if (!equal)
                    differences.Add($"{typeName}{Environment.NewLine}" +
                                    $"    only ours:     {String.Join(", ", ours.Except(theirs, StringComparer.Ordinal))}{Environment.NewLine}" +
                                    $"    only spine-go: {String.Join(", ", theirs.Except(ours, StringComparer.Ordinal))}{Environment.NewLine}" +
                                    $"    ours:          {String.Join(", ", ours)}{Environment.NewLine}" +
                                    $"    spine-go:      {String.Join(", ", theirs)}");

            }

            Assert.Multiple(() => {

                Assert.That(compared, Is.GreaterThan(500),
                            "Too few data types were compared - is the fixture still the right one?");

                Assert.That(differences, Is.Empty,
                            $"{differences.Count} data type(s) differ from the Go reference implementation:{Environment.NewLine}" +
                            String.Join(Environment.NewLine, differences));

            });

        }

        #endregion

        #region DataTypes_OfTheGoModel_AllExist()

        /// <summary>
        /// A data type which spine-go knows and we do not is a hole in the model.
        /// </summary>
        [Test]
        public void DataTypes_OfTheGoModel_AllExist()
        {

            var modelTypes = ModelTypes();

            var missing    = GoModel().
                                 Properties().
                                 Select(property => property.Name).
                                 Where (name     => !modelTypes.ContainsKey(name) &&
                                                    !knownDeviations.ContainsKey(name)).
                                 ToList();

            Assert.That(missing, Is.Empty,
                        $"spine-go knows {missing.Count} data type(s) which the generated model does not: " +
                        String.Join(", ", missing));

        }

        #endregion

        #region KnownDeviations_FromTheGoReferenceImplementation_StillExist()

        /// <summary>
        /// Every documented deviation has to still be a deviation.
        ///
        /// A list of accepted differences is only worth something as long as
        /// somebody notices when an entry becomes obsolete - otherwise it turns
        /// into a place where real differences hide.
        /// </summary>
        [Test]
        public void KnownDeviations_FromTheGoReferenceImplementation_StillExist()
        {

            var modelTypes = ModelTypes();
            var goModel    = GoModel();
            var obsolete   = new List<String>();

            foreach (var (typeName, reason) in knownDeviations)
            {

                var theirs = (goModel[typeName]?["fields"] as JArray)?.
                                 Select(field => field.Value<String>() ?? "").ToList();

                if (theirs is null)
                {
                    obsolete.Add($"{typeName}: spine-go does not know this data type any more.");
                    continue;
                }

                if (!modelTypes.TryGetValue(typeName, out var type))
                    // A type which spine-go has and we deliberately do not.
                    continue;

                if (JSONNames(type).SequenceEqual(theirs, StringComparer.Ordinal))
                    obsolete.Add($"{typeName}: the model and spine-go agree again, so the entry can go. " +
                                 $"It was accepted because: {reason}");

            }

            foreach (var (typeName, property, reason) in knownGoOnlyProperties)
            {

                var theirs = (goModel[typeName]?["fields"] as JArray)?.
                                 Select(field => field.Value<String>() ?? "").ToList() ?? [];

                if (!theirs.Contains(property, StringComparer.Ordinal))
                    obsolete.Add($"{typeName}.{property}: spine-go does not have it any more, so the entry can go. " +
                                 $"It was accepted because: {reason}");

            }

            Assert.That(obsolete, Is.Empty,
                        String.Join(Environment.NewLine, obsolete));

        }

        #endregion

        #region Keys_AreThoseOfTheGoReferenceImplementation()

        /// <summary>
        /// The identifiers of a data type decide how a partial update merges a
        /// list, so a missing one is a silently wrong merge later on.
        /// </summary>
        [Test]
        public void Keys_AreThoseOfTheGoReferenceImplementation()
        {

            var modelTypes = ModelTypes();
            var goModel    = GoModel();

            var found      = 0;
            var problems   = new List<String>();

            foreach (var (typeName, entry) in goModel)
            {

                if (entry?["keys"] is not JObject keys ||
                    !modelTypes.TryGetValue(typeName, out var type))
                {
                    continue;
                }

                foreach (var (jsonName, kind) in keys)
                {

                    found++;

                    var property = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).
                                        FirstOrDefault(p => p.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName == jsonName);

                    var key      = property?.GetCustomAttribute<EEBUSKeyAttribute>();

                    if (key is null)
                        problems.Add($"{typeName}.{jsonName} is an identifier in spine-go, but is not marked as one.");

                    else if (key.IsPrimary != (kind?.Value<String>() == "primary"))
                        problems.Add($"{typeName}.{jsonName}: spine-go says '{kind}', the model says " +
                                     $"'{(key.IsPrimary ? "primary" : "key")}'.");

                }

            }

            Assert.Multiple(() => {

                Assert.That(found,    Is.GreaterThan(50),
                            "Too few identifiers were checked - is the fixture still the right one?");

                Assert.That(problems, Is.Empty,
                            String.Join(Environment.NewLine, problems));

            });

        }

        #endregion


        #region FunctionRegistry_IsComplete()

        /// <summary>
        /// The registry is built from the choice groups of the command frame,
        /// "FunctionType" is a separate enumeration of the same XSDs. Where the
        /// two disagree, the specification contradicts itself and we want to
        /// know about it rather than to find out during a certification run.
        /// </summary>
        [Test]
        public void FunctionRegistry_IsComplete()
        {

            var registered = SPINEFunctions.All.Select(function => function.Name).ToHashSet(StringComparer.Ordinal);
            var declared   = FunctionType.All.Select(function => function.ToString()).ToHashSet(StringComparer.Ordinal);

            Assert.Multiple(() => {

                Assert.That(registered, Is.Not.Empty);

                Assert.That(registered.Except(declared), Is.Empty,
                            "Functions of the command frame which 'FunctionEnumType' does not declare: " +
                            String.Join(", ", registered.Except(declared)));

                // The other direction is not an error: "FunctionEnumType" lists
                // a few functions which no data element of the command frame
                // carries. Those are named here so that the list stays visible.
                TestContext.Out.WriteLine("Declared but not carried by the command frame: " +
                                          String.Join(", ", declared.Except(registered).Order(StringComparer.Ordinal)));

            });

        }

        #endregion

        #region CommandFrame_CarriesEveryFunction()

        /// <summary>
        /// Every function of the registry has to be reachable through the command
        /// frame: its data through "CmdType", its selectors and elements through
        /// "FilterType". That is what lets the update system work on a function
        /// it does not know.
        /// </summary>
        [Test]
        public void CommandFrame_CarriesEveryFunction()
        {

            static Dictionary<String, EEBUSFunctionPart> PartsOf(Type Type)

                => Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).
                        Select   (property  => property.GetCustomAttribute<EEBUSFunctionAttribute>()).
                        Where    (attribute => attribute is not null).
                        ToDictionary(attribute => $"{attribute!.Function}/{attribute.Part}",
                                     attribute => attribute!.Part,
                                     StringComparer.Ordinal);

            var cmd      = PartsOf(typeof(CmdType));
            var filter   = PartsOf(typeof(FilterType));

            var problems = new List<String>();

            foreach (var function in SPINEFunctions.All)
            {

                if (!cmd.ContainsKey($"{function.Name}/{EEBUSFunctionPart.Data}"))
                    problems.Add($"CmdType does not carry the data of '{function.Name}'.");

                if (function.SelectorsType is not null &&
                    !filter.ContainsKey($"{function.Name}/{EEBUSFunctionPart.Selectors}"))
                {
                    problems.Add($"FilterType does not carry the selectors of '{function.Name}'.");
                }

                if (function.ElementsType is not null &&
                    !filter.ContainsKey($"{function.Name}/{EEBUSFunctionPart.Elements}"))
                {
                    problems.Add($"FilterType does not carry the elements of '{function.Name}'.");
                }

            }

            Assert.That(problems, Is.Empty,
                        String.Join(Environment.NewLine, problems));

        }

        #endregion

        #region FunctionRegistry_KnowsLoadControlLimits()

        /// <summary>
        /// One entry read in full, as a check that the registry says something
        /// and not just anything: the limit list of load control is the function
        /// the whole grid family (LPC, LPP) is about.
        /// </summary>
        [Test]
        public void FunctionRegistry_KnowsLoadControlLimits()
        {

            var function = SPINEFunctions.Get("loadControlLimitListData");

            Assert.That(function, Is.Not.Null);

            Assert.Multiple(() => {
                Assert.That(function!.Resource,      Is.EqualTo("LoadControl"));
                Assert.That(function!.DataType,      Is.EqualTo(typeof(LoadControlLimitListDataType)));
                Assert.That(function!.SelectorsType,  Is.EqualTo(typeof(LoadControlLimitListDataSelectorsType)));
                Assert.That(function!.ElementsType,   Is.EqualTo(typeof(LoadControlLimitDataElementsType)));
            });

        }

        #endregion


        #region EveryProperty_IsOptional()

        /// <summary>
        /// Every element of the SPINE data model is optional - "minOccurs" is 0
        /// throughout - because a partial notify sends exactly the fields which
        /// changed. A property which cannot be left out could not take part in
        /// that.
        /// </summary>
        [Test]
        public void EveryProperty_IsOptional()
        {

            var problems = new List<String>();

            foreach (var (typeName, type) in ModelTypes())
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {

                    if (property.GetCustomAttribute<JsonPropertyAttribute>() is null)
                        continue;

                    var isNullable = !property.PropertyType.IsValueType ||
                                      Nullable.GetUnderlyingType(property.PropertyType) is not null;

                    if (!isNullable)
                        problems.Add($"{typeName}.{property.Name} is not nullable.");

                }

            Assert.That(problems, Is.Empty,
                        String.Join(Environment.NewLine, problems));

        }

        #endregion

        #region EveryProperty_IgnoresNullWhenWriting()

        /// <summary>
        /// A property which is not set must not appear in the datagram at all.
        /// Writing "null" instead of leaving it out is a different message.
        /// </summary>
        [Test]
        public void EveryProperty_IgnoresNullWhenWriting()
        {

            var problems = new List<String>();

            foreach (var (typeName, type) in ModelTypes())
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {

                    var attribute = property.GetCustomAttribute<JsonPropertyAttribute>();

                    if (attribute is null)
                        continue;

                    if (attribute.NullValueHandling != NullValueHandling.Ignore)
                        problems.Add($"{typeName}.{property.Name} does not ignore null.");

                }

            Assert.That(problems, Is.Empty,
                        String.Join(Environment.NewLine, problems));

        }

        #endregion

    }

}
