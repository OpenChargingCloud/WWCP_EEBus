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

using System.Xml.Linq;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.tests
{

    /// <summary>
    /// The example datagrams which come with the SPINE specification.
    ///
    /// The specification ships its examples as XML - SPINE is defined in XSDs,
    /// and XML is the shape the definition is written in - while EEBUS devices
    /// exchange them as JSON. This turns the one into the other, so that the
    /// 29 official datagrams of "ExampleXMLs/RestrictedFunctionExchange" can be
    /// used as what they are: the specification's own answer to what a
    /// restricted function exchange looks like.
    ///
    /// The transformation needs the data model, and there is no way around it:
    /// an XML element which occurs once is indistinguishable from a list of one
    /// entry, and only the schema knows which of the two it is. The model
    /// carries that knowledge, so the conversion walks the model and the XML
    /// side by side - which makes an element the model does not know an error
    /// rather than a silently dropped value.
    ///
    /// The specifications are licensed material and are not part of this
    /// repository; where they are absent, the tests using them report
    /// "inconclusive".
    /// </summary>
    public static class SPINEExampleXML
    {

        #region Data

        /// <summary>
        /// Where the examples live below the extracted specification.
        /// </summary>
        private static readonly String[] exampleDirectory = [
            "docs", "specs", "SHIP SPINE", "Technical Specifications",
            "EEBus_SPINE_V1.3.0", "EEBus_SPINE_V1.3.0_Final_hp",
            "ExampleXMLs", "RestrictedFunctionExchange"
        ];

        #endregion


        #region Directory()

        /// <summary>
        /// The directory of the official restricted function exchange examples,
        /// or null where the specifications are not checked out.
        /// </summary>
        public static String? Directory()
        {

            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (directory is not null)
            {

                var candidate = Path.Combine([directory.FullName, .. exampleDirectory]);

                if (System.IO.Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;

            }

            return null;

        }

        #endregion

        #region Files()

        /// <summary>
        /// All official restricted function exchange examples.
        /// </summary>
        public static List<String> Files()
        {

            var directory = Directory();

            return directory is null
                       ? []
                       : [.. System.IO.Directory.GetFiles(directory, "*.xml").Order(StringComparer.Ordinal)];

        }

        #endregion

        #region Load(Name)

        /// <summary>
        /// One official example, as JSON.
        /// </summary>
        /// <param name="Name">The name of the example, without the common prefix and without the extension, i.e. "W-A-Y_1-1-01".</param>
        public static JObject Load(String Name)
        {

            var directory = Directory()
                                ?? throw new InvalidOperationException("The SPINE specifications are not checked out below docs/specs.");

            return ToJSON(
                       XDocument.Load(
                           Path.Combine(directory, $"EEBus_SPINE_Spec_Example_RFE_{Name}.xml")
                       ).Root!
                   );

        }

        #endregion

        #region ToJSON(Datagram)

        /// <summary>
        /// The given example datagram as the JSON an EEBUS device would send.
        /// </summary>
        /// <param name="Datagram">The root element of an example.</param>
        public static JObject ToJSON(XElement Datagram)

            => (JObject) Convert(Datagram, typeof(Model.DatagramType));

        #endregion


        #region (private static) Convert(Element, Type)

        private static JToken Convert(XElement Element, Type Type)
        {

            var info      = SPINETypeInfo.Of(Type);
            var result    = new JObject();
            var children  = Element.Elements().ToList();

            for (var i = 0; i < children.Count; i++)
            {

                var name      = children[i].Name.LocalName;
                var property  = info.FindJSON(name)
                                    ?? throw new InvalidOperationException(
                                           $"The data type '{Type.Name}' has no element '{name}', " +
                                           $"but '{Element.Name.LocalName}' of the specification example does.");

                // Elements which repeat are one list. They stand next to each
                // other within all examples, and within the XSDs they have to.
                var repeated  = new List<XElement>();

                while (i < children.Count && children[i].Name.LocalName == name)
                    repeated.Add(children[i++]);

                i--;

                if (property.IsList)
                    result.Add(name, new JArray(repeated.Select(child => Value(child, property))));

                else if (repeated.Count > 1)
                    throw new InvalidOperationException(
                              $"'{Element.Name.LocalName}' of the specification example states '{name}' " +
                              $"{repeated.Count} times, but '{Type.Name}' holds it once.");

                else
                    result.Add(name, Value(repeated[0], property));

            }

            return result;

        }

        #endregion

        #region (private static) Value(Element, Property)

        private static JToken Value(XElement Element, SPINEPropertyInfo Property)

            => Property.IsModelType

                   // An element of the model, whose children are its elements.
                   // One without children is the empty object, which is how a
                   // deletion and a partial read name an element without giving
                   // it a value.
                   ? Convert(Element, Property.ValueType)

                   // Everything else is text. Which text is which number is a
                   // question for the model, not for the reader: leaving the
                   // values as they stand and letting the deserialisation of the
                   // data type read them is the same path a datagram takes.
                   : new JValue(Element.Value);

        #endregion

    }

}
