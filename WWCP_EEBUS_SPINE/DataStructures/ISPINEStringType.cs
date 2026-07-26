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
    /// A SPINE data type which is a string on the wire.
    ///
    /// Every enumeration of the SPINE data model is one of these: SPINE
    /// enumerations are extensible (CommonDataTypes "EnumExtendType"), so an
    /// unknown value is a legal value which has to survive being received,
    /// stored and sent again unchanged. The same holds for the ISO 8601 types,
    /// where the text is normative and re-formatting it would change the
    /// datagram.
    ///
    /// The generated model implements this interface on every such type, which
    /// is what allows a single <see cref="SPINEStringTypeConverter{T}"/> to
    /// serve all of them.
    /// </summary>
    /// <typeparam name="TSelf">The implementing type itself.</typeparam>
    public interface ISPINEStringType<TSelf>

        where TSelf : struct, ISPINEStringType<TSelf>

    {

        /// <summary>
        /// Parse the given text.
        ///
        /// Parsing is deliberately tolerant: which values are defined by the
        /// specification is a question for the conformance tests, not for the
        /// parser. Only null or empty is refused.
        /// </summary>
        /// <param name="Text">A text representation.</param>
        static abstract TSelf Parse(String Text);

        /// <summary>
        /// Try to parse the given text.
        /// </summary>
        /// <param name="Text">A text representation.</param>
        /// <param name="Value">The parsed value.</param>
        static abstract Boolean TryParse(String Text, out TSelf Value);

        /// <summary>
        /// The text representation, exactly as it appeared on the wire.
        /// </summary>
        String ToString();

    }

}
