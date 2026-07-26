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
    /// Marks a property as an identifier of its data type.
    ///
    /// The update system of SPINE merges the entries of a list by their
    /// identifiers rather than by their position, so knowing which properties
    /// those are is a precondition for every partial write and every partial
    /// notify.
    ///
    /// Where a data type has several identifiers, exactly one of them is the
    /// PRIMARY IDENTIFIER of the specification and carries
    /// <see cref="IsPrimary"/>; the others are sub or foreign identifiers.
    /// The XSD does not express any of this - it comes from the specification
    /// text and is kept in agreement with the Go reference implementation, whose
    /// "eebus:&quot;key,primarykey&quot;" tags are checked by the generator tests.
    /// </summary>
    /// <param name="IsPrimary">Whether this is the primary identifier of a composite key.</param>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EEBUSKeyAttribute(Boolean IsPrimary = false) : Attribute
    {

        /// <summary>
        /// Whether this is the primary identifier of a composite key.
        /// </summary>
        public Boolean IsPrimary    { get; } = IsPrimary;

    }

}
