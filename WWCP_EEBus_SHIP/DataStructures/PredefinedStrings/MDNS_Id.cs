/*
 * Copyright (c) 2014-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// Extension methods for MDNS identifications.
    /// </summary>
    public static class MDNSIdExtensions
    {

        /// <summary>
        /// Indicates whether this MDNS identification is null or empty.
        /// </summary>
        /// <param name="MDNSId">A MDNS identification.</param>
        public static Boolean IsNullOrEmpty(this MDNS_Id? MDNSId)
            => !MDNSId.HasValue || MDNSId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this MDNS identification is null or empty.
        /// </summary>
        /// <param name="MDNSId">A MDNS identification.</param>
        public static Boolean IsNotNullOrEmpty(this MDNS_Id? MDNSId)
            => MDNSId.HasValue && MDNSId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A MDNS identification.
    /// </summary>
    public readonly struct MDNS_Id : IId,
                                     IEquatable<MDNS_Id>,
                                     IComparable<MDNS_Id>
    {

        #region Data

        private readonly static Dictionary<String, MDNS_Id>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                    InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this MDNS identification is null or empty.
        /// </summary>
        public readonly  Boolean  IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this MDNS identification is NOT null or empty.
        /// </summary>
        public readonly  Boolean  IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the MDNS identification.
        /// </summary>
        public readonly  UInt64   Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new MDNS identification based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a MDNS identification.</param>
        private MDNS_Id(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static MDNS_Id Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new MDNS_Id(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a MDNS identification.
        /// </summary>
        /// <param name="Text">A text representation of a MDNS identification.</param>
        public static MDNS_Id Parse(String Text)
        {

            if (TryParse(Text, out var mdnsId))
                return mdnsId;

            throw new ArgumentException("The given text representation of a MDNS identification is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as MDNS identification.
        /// </summary>
        /// <param name="Text">A text representation of a MDNS identification.</param>
        public static MDNS_Id? TryParse(String Text)
        {

            if (TryParse(Text, out var mdnsId))
                return mdnsId;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out MDNSId)

        /// <summary>
        /// Try to parse the given text as MDNS identification.
        /// </summary>
        /// <param name="Text">A text representation of a MDNS identification.</param>
        /// <param name="MDNSId">The parsed MDNS identification.</param>
        public static Boolean TryParse(String Text, out MDNS_Id MDNSId)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out MDNSId))
                    MDNSId = Register(Text);

                return true;

            }

            MDNSId = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this MDNS identification.
        /// </summary>
        public MDNS_Id Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// Pending
        /// </summary>
        public static MDNS_Id  Pending    { get; }
            = Register("pending");

        /// <summary>
        /// Ready
        /// </summary>
        public static MDNS_Id  Ready      { get; }
            = Register("ready");

        /// <summary>
        /// Aborted
        /// </summary>
        public static MDNS_Id  Aborted    { get; }
            = Register("aborted");

        #endregion


        #region Operator overloading

        #region Operator == (MDNSId1, MDNSId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MDNSId1">A MDNS identification.</param>
        /// <param name="MDNSId2">Another MDNS identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (MDNS_Id MDNSId1,
                                           MDNS_Id MDNSId2)

            => MDNSId1.Equals(MDNSId2);

        #endregion

        #region Operator != (MDNSId1, MDNSId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MDNSId1">A MDNS identification.</param>
        /// <param name="MDNSId2">Another MDNS identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (MDNS_Id MDNSId1,
                                           MDNS_Id MDNSId2)

            => !MDNSId1.Equals(MDNSId2);

        #endregion

        #region Operator <  (MDNSId1, MDNSId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MDNSId1">A MDNS identification.</param>
        /// <param name="MDNSId2">Another MDNS identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (MDNS_Id MDNSId1,
                                          MDNS_Id MDNSId2)

            => MDNSId1.CompareTo(MDNSId2) < 0;

        #endregion

        #region Operator <= (MDNSId1, MDNSId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MDNSId1">A MDNS identification.</param>
        /// <param name="MDNSId2">Another MDNS identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (MDNS_Id MDNSId1,
                                           MDNS_Id MDNSId2)

            => MDNSId1.CompareTo(MDNSId2) <= 0;

        #endregion

        #region Operator >  (MDNSId1, MDNSId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MDNSId1">A MDNS identification.</param>
        /// <param name="MDNSId2">Another MDNS identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (MDNS_Id MDNSId1,
                                          MDNS_Id MDNSId2)

            => MDNSId1.CompareTo(MDNSId2) > 0;

        #endregion

        #region Operator >= (MDNSId1, MDNSId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MDNSId1">A MDNS identification.</param>
        /// <param name="MDNSId2">Another MDNS identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (MDNS_Id MDNSId1,
                                           MDNS_Id MDNSId2)

            => MDNSId1.CompareTo(MDNSId2) >= 0;

        #endregion

        #endregion

        #region IComparable<MDNSId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two MDNS identifications.
        /// </summary>
        /// <param name="Object">A MDNS identification to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is MDNS_Id mdnsId
                   ? CompareTo(mdnsId)
                   : throw new ArgumentException("The given object is not MDNS identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(MDNSId)

        /// <summary>
        /// Compares two MDNS identifications.
        /// </summary>
        /// <param name="MDNSId">A MDNS identification to compare with.</param>
        public Int32 CompareTo(MDNS_Id MDNSId)

            => String.Compare(InternalId,
                              MDNSId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<MDNSId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two MDNS identifications for equality.
        /// </summary>
        /// <param name="Object">A MDNS identification to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is MDNS_Id mdnsId &&
                   Equals(mdnsId);

        #endregion

        #region Equals(MDNSId)

        /// <summary>
        /// Compares two MDNS identifications for equality.
        /// </summary>
        /// <param name="MDNSId">A MDNS identification to compare with.</param>
        public Boolean Equals(MDNS_Id MDNSId)

            => String.Equals(InternalId,
                             MDNSId.InternalId,
                             StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => InternalId?.ToLower().GetHashCode() ?? 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
