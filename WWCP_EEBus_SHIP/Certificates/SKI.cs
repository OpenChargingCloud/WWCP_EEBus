/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of WWCP EEBus <https://github.com/OpenChargingCloud/WWCP_EEBus>
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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// The Subject Key Identifier (SKI) of the SHIP node specific public key -
    /// the identity of a SHIP node (SHIP TS 1.0.1, chapter 12.2).
    ///
    /// It is generated as described in RFC 3280, chapter 4.2.1.2, method (1):
    /// the SHA-1 hash of the subjectPublicKey BIT STRING, and is therefore
    /// exactly 20 bytes long, presented to the user as 40 hexadecimal digits.
    ///
    /// Trust within SHIP is based on this value alone, not on a PKI.
    /// </summary>
    public readonly struct SKI : IEquatable<SKI>,
                                 IComparable<SKI>,
                                 IComparable
    {

        #region Data

        private readonly Byte[] bytes;

        /// <summary>
        /// The length of a SKI in bytes.
        /// </summary>
        public const Int32 Length = 20;

        /// <summary>
        /// The length of the hexadecimal text representation of a SKI.
        /// </summary>
        public const Int32 TextLength = 2 * Length;

        #endregion

        #region Properties

        /// <summary>
        /// Whether this SKI is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => bytes is null || bytes.Length == 0;

        /// <summary>
        /// Whether this SKI is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => bytes is not null && bytes.Length == Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SKI based on the given bytes.
        /// </summary>
        /// <param name="Bytes">The 20 bytes of the subject key identifier.</param>
        private SKI(Byte[] Bytes)
        {
            this.bytes = Bytes;
        }

        #endregion


        #region (static) Parse       (Text)

        /// <summary>
        /// Parse the given text as a SKI.
        /// </summary>
        /// <param name="Text">A text representation of a SKI.</param>
        public static SKI Parse(String Text)
        {

            if (TryParse(Text, out var ski, out var errorResponse))
                return ski;

            throw new ArgumentException("The given text representation of a SKI is invalid: " + errorResponse,
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse    (Text)

        /// <summary>
        /// Try to parse the given text as a SKI.
        /// </summary>
        /// <param name="Text">A text representation of a SKI.</param>
        public static SKI? TryParse(String Text)
        {

            if (TryParse(Text, out var ski, out _))
                return ski;

            return null;

        }

        #endregion

        #region (static) TryParse    (Text, out SKI, out ErrorResponse)

        /// <summary>
        /// Try to parse the given text as a SKI.
        ///
        /// Parsing is tolerant regarding the presentation: upper and lower case
        /// hexadecimal digits are accepted, as are the usual separators of the
        /// grouped notation printed on device labels ("1234 5678 90ab ...").
        /// </summary>
        /// <param name="Text">A text representation of a SKI.</param>
        /// <param name="SKI">The parsed SKI.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(String                            Text,
                                       out SKI                           SKI,
                                       [NotNullWhen(false)] out String?  ErrorResponse)
        {

            SKI            = default;
            ErrorResponse  = null;

            var normalized = Normalize(Text);

            if (normalized.Length != TextLength)
            {
                ErrorResponse = $"A SKI consists of {TextLength} hexadecimal digits, but the given text has {normalized.Length}!";
                return false;
            }

            var bytes = new Byte[Length];

            for (var i = 0; i < Length; i++)
            {

                var high = HexValue(normalized[2 * i]);
                var low  = HexValue(normalized[2 * i + 1]);

                if (high < 0 || low < 0)
                {
                    ErrorResponse = $"The given SKI contains the invalid hexadecimal digit '{normalized[high < 0 ? 2 * i : 2 * i + 1]}'!";
                    return false;
                }

                bytes[i] = (Byte) ((high << 4) | low);

            }

            SKI = new SKI(bytes);
            return true;

        }

        #endregion

        #region (static) TryParseBytes(Bytes, out SKI, out ErrorResponse)

        /// <summary>
        /// Try to use the given bytes as a SKI.
        /// </summary>
        /// <param name="Bytes">The 20 bytes of a subject key identifier.</param>
        /// <param name="SKI">The parsed SKI.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParseBytes(ReadOnlySpan<Byte>                Bytes,
                                            out SKI                           SKI,
                                            [NotNullWhen(false)] out String?  ErrorResponse)
        {

            SKI            = default;
            ErrorResponse  = null;

            if (Bytes.Length != Length)
            {
                ErrorResponse = $"A SKI is {Length} bytes long, but the given data is {Bytes.Length} bytes!";
                return false;
            }

            SKI = new SKI(Bytes.ToArray());
            return true;

        }

        #endregion

        #region (static) Normalize   (Text)

        /// <summary>
        /// Normalize the given text representation of a SKI: remove the usual
        /// separators and convert it to lower case.
        /// </summary>
        /// <param name="Text">A text representation of a SKI.</param>
        public static String Normalize(String Text)
        {

            if (Text is null)
                return "";

            var result = new System.Text.StringBuilder(Text.Length);

            foreach (var character in Text)
            {

                if (character is ' ' or '\t' or ':' or '-' or '.')
                    continue;

                result.Append(Char.ToLowerInvariant(character));

            }

            return result.ToString();

        }

        #endregion


        #region ToByteArray()

        /// <summary>
        /// Return the 20 bytes of this SKI.
        /// </summary>
        public Byte[] ToByteArray()

            => bytes is null
                   ? []
                   : (Byte[]) bytes.Clone();

        #endregion

        #region ToGroupedString(GroupSize = 4, Separator = " ")

        /// <summary>
        /// Return a grouped text representation of this SKI, as it is usually
        /// printed on device labels and shown to the user.
        /// </summary>
        /// <param name="GroupSize">The number of hexadecimal digits per group.</param>
        /// <param name="Separator">The separator between the groups.</param>
        public String ToGroupedString(UInt16  GroupSize   = 4,
                                      String  Separator   = " ")
        {

            var text = ToString();

            if (GroupSize == 0 || text.Length == 0)
                return text;

            var result = new System.Text.StringBuilder(text.Length + text.Length / GroupSize);

            for (var i = 0; i < text.Length; i++)
            {

                if (i > 0 && i % GroupSize == 0)
                    result.Append(Separator);

                result.Append(text[i]);

            }

            return result.ToString();

        }

        #endregion


        #region (private static) HexValue(Character)

        private static Int32 HexValue(Char Character)

            => Character switch {
                   >= '0' and <= '9'  => Character - '0',
                   >= 'a' and <= 'f'  => Character - 'a' + 10,
                   >= 'A' and <= 'F'  => Character - 'A' + 10,
                   _                  => -1
               };

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two SKIs for equality.
        /// </summary>
        public static Boolean operator == (SKI SKI1, SKI SKI2)
            => SKI1.Equals(SKI2);

        /// <summary>
        /// Compares two SKIs for inequality.
        /// </summary>
        public static Boolean operator != (SKI SKI1, SKI SKI2)
            => !SKI1.Equals(SKI2);

        /// <summary>
        /// Compares two SKIs.
        /// </summary>
        public static Boolean operator < (SKI SKI1, SKI SKI2)
            => SKI1.CompareTo(SKI2) < 0;

        /// <summary>
        /// Compares two SKIs.
        /// </summary>
        public static Boolean operator <= (SKI SKI1, SKI SKI2)
            => SKI1.CompareTo(SKI2) <= 0;

        /// <summary>
        /// Compares two SKIs.
        /// </summary>
        public static Boolean operator > (SKI SKI1, SKI SKI2)
            => SKI1.CompareTo(SKI2) > 0;

        /// <summary>
        /// Compares two SKIs.
        /// </summary>
        public static Boolean operator >= (SKI SKI1, SKI SKI2)
            => SKI1.CompareTo(SKI2) >= 0;

        #endregion

        #region IComparable<SKI> Members

        /// <summary>
        /// Compares two SKIs.
        /// </summary>
        /// <param name="Object">A SKI to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is SKI ski
                   ? CompareTo(ski)
                   : throw new ArgumentException("The given object is not a SKI!",
                                                 nameof(Object));

        /// <summary>
        /// Compares two SKIs.
        ///
        /// SHIP TS 1.0.1, chapter 12.2.2 resolves double connections by comparing
        /// the SKI values of both communication partners.
        /// </summary>
        /// <param name="SKI">A SKI to compare with.</param>
        public Int32 CompareTo(SKI SKI)

            => String.CompareOrdinal(ToString(),
                                     SKI.ToString());

        #endregion

        #region IEquatable<SKI> Members

        /// <summary>
        /// Compares two SKIs for equality.
        /// </summary>
        /// <param name="Object">A SKI to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is SKI ski &&
                   Equals(ski);

        /// <summary>
        /// Compares two SKIs for equality.
        /// </summary>
        /// <param name="SKI">A SKI to compare with.</param>
        public Boolean Equals(SKI SKI)
        {

            if (bytes is null || SKI.bytes is null)
                return bytes is null && SKI.bytes is null;

            return bytes.AsSpan().SequenceEqual(SKI.bytes);

        }

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {

            if (bytes is null)
                return 0;

            var hashCode = new HashCode();
            hashCode.AddBytes(bytes);

            return hashCode.ToHashCode();

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return the canonical text representation of this SKI:
        /// 40 lower case hexadecimal digits without separators.
        /// </summary>
        public override String ToString()

            => bytes is null
                   ? ""
                   : Convert.ToHexStringLower(bytes);

        #endregion

    }

}
