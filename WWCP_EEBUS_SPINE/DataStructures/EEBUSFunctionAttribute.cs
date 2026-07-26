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

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// Which of the three faces of a SPINE function a type or property is.
    ///
    /// A function appears three times in a command frame: as the data itself,
    /// as the selectors which address a part of it, and as the elements which
    /// name the fields a partial operation is about.
    /// </summary>
    public enum EEBUSFunctionPart
    {

        /// <summary>
        /// The function data, e.g. "loadControlLimitListData".
        /// </summary>
        Data,

        /// <summary>
        /// The selectors of the function, e.g. "loadControlLimitListDataSelectors".
        /// </summary>
        Selectors,

        /// <summary>
        /// The elements of the function, e.g. "loadControlLimitListDataElements".
        /// </summary>
        Elements

    }


    /// <summary>
    /// Marks a property of a command frame as belonging to a SPINE function.
    ///
    /// This is the C# counterpart of the "eebus:&quot;fct:...,typ:...&quot;" struct
    /// tags of the Go reference implementation, and it is what allows the update
    /// system to work on any function without knowing it: given a command, the
    /// function name, its data, its selectors and its elements can all be found
    /// by reflection.
    /// </summary>
    /// <param name="Function">The name of the SPINE function.</param>
    /// <param name="Part">Which face of the function this property carries.</param>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EEBUSFunctionAttribute(String             Function,
                                               EEBUSFunctionPart  Part = EEBUSFunctionPart.Data) : Attribute
    {

        /// <summary>
        /// The name of the SPINE function, as it appears within "FunctionEnumType".
        /// </summary>
        public String             Function    { get; } = Function;

        /// <summary>
        /// Which face of the function this property carries.
        /// </summary>
        public EEBUSFunctionPart  Part        { get; } = Part;

    }

}
