/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// Extension methods for SHIP identifications.
    /// </summary>
    public static class SHIPIdExtensions
    {

        /// <summary>
        /// Indicates whether this SHIP identification is null or empty.
        /// </summary>
        /// <param name="SHIPId">A SHIP identification.</param>
        public static Boolean IsNullOrEmpty(this SHIP_Id? SHIPId)
            => !SHIPId.HasValue || SHIPId.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this SHIP identification is null or empty.
        /// </summary>
        /// <param name="SHIPId">A SHIP identification.</param>
        public static Boolean IsNotNullOrEmpty(this SHIP_Id? SHIPId)
            => SHIPId.HasValue && SHIPId.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A SHIP identification.
    /// </summary>
    public readonly struct SHIP_Id : IId,
                                     IEquatable<SHIP_Id>,
                                     IComparable<SHIP_Id>
    {

        #region Data

        private readonly static Dictionary<String, SHIP_Id>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                    InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this SHIP identification is null or empty.
        /// </summary>
        public readonly  Boolean  IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this SHIP identification is NOT null or empty.
        /// </summary>
        public readonly  Boolean  IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the SHIP identification.
        /// </summary>
        public readonly  UInt64   Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SHIP identification based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a SHIP identification.</param>
        private SHIP_Id(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static SHIP_Id Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new SHIP_Id(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a SHIP identification.
        /// </summary>
        /// <param name="Text">A text representation of a SHIP identification.</param>
        public static SHIP_Id Parse(String Text)
        {

            if (TryParse(Text, out var shipId))
                return shipId;

            throw new ArgumentException("The given text representation of a SHIP identification is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as SHIP identification.
        /// </summary>
        /// <param name="Text">A text representation of a SHIP identification.</param>
        public static SHIP_Id? TryParse(String Text)
        {

            if (TryParse(Text, out var shipId))
                return shipId;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out SHIPId)

        /// <summary>
        /// Try to parse the given text as SHIP identification.
        /// </summary>
        /// <param name="Text">A text representation of a SHIP identification.</param>
        /// <param name="SHIPId">The parsed SHIP identification.</param>
        public static Boolean TryParse(String Text, out SHIP_Id SHIPId)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out SHIPId))
                    SHIPId = Register(Text);

                return true;

            }

            SHIPId = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this SHIP identification.
        /// </summary>
        public SHIP_Id Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// Pending
        /// </summary>
        public static SHIP_Id  Pending    { get; }
            = Register("pending");

        /// <summary>
        /// Ready
        /// </summary>
        public static SHIP_Id  Ready      { get; }
            = Register("ready");

        /// <summary>
        /// Aborted
        /// </summary>
        public static SHIP_Id  Aborted    { get; }
            = Register("aborted");

        #endregion


        #region Operator overloading

        #region Operator == (SHIPId1, SHIPId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SHIPId1">A SHIP identification.</param>
        /// <param name="SHIPId2">Another SHIP identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (SHIP_Id SHIPId1,
                                           SHIP_Id SHIPId2)

            => SHIPId1.Equals(SHIPId2);

        #endregion

        #region Operator != (SHIPId1, SHIPId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SHIPId1">A SHIP identification.</param>
        /// <param name="SHIPId2">Another SHIP identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (SHIP_Id SHIPId1,
                                           SHIP_Id SHIPId2)

            => !SHIPId1.Equals(SHIPId2);

        #endregion

        #region Operator <  (SHIPId1, SHIPId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SHIPId1">A SHIP identification.</param>
        /// <param name="SHIPId2">Another SHIP identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (SHIP_Id SHIPId1,
                                          SHIP_Id SHIPId2)

            => SHIPId1.CompareTo(SHIPId2) < 0;

        #endregion

        #region Operator <= (SHIPId1, SHIPId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SHIPId1">A SHIP identification.</param>
        /// <param name="SHIPId2">Another SHIP identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (SHIP_Id SHIPId1,
                                           SHIP_Id SHIPId2)

            => SHIPId1.CompareTo(SHIPId2) <= 0;

        #endregion

        #region Operator >  (SHIPId1, SHIPId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SHIPId1">A SHIP identification.</param>
        /// <param name="SHIPId2">Another SHIP identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (SHIP_Id SHIPId1,
                                          SHIP_Id SHIPId2)

            => SHIPId1.CompareTo(SHIPId2) > 0;

        #endregion

        #region Operator >= (SHIPId1, SHIPId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="SHIPId1">A SHIP identification.</param>
        /// <param name="SHIPId2">Another SHIP identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (SHIP_Id SHIPId1,
                                           SHIP_Id SHIPId2)

            => SHIPId1.CompareTo(SHIPId2) >= 0;

        #endregion

        #endregion

        #region IComparable<SHIPId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two SHIP identifications.
        /// </summary>
        /// <param name="Object">A SHIP identification to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is SHIP_Id shipId
                   ? CompareTo(shipId)
                   : throw new ArgumentException("The given object is not SHIP identification!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(SHIPId)

        /// <summary>
        /// Compares two SHIP identifications.
        /// </summary>
        /// <param name="SHIPId">A SHIP identification to compare with.</param>
        public Int32 CompareTo(SHIP_Id SHIPId)

            => String.Compare(InternalId,
                              SHIPId.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<SHIPId> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two SHIP identifications for equality.
        /// </summary>
        /// <param name="Object">A SHIP identification to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is SHIP_Id shipId &&
                   Equals(shipId);

        #endregion

        #region Equals(SHIPId)

        /// <summary>
        /// Compares two SHIP identifications for equality.
        /// </summary>
        /// <param name="SHIPId">A SHIP identification to compare with.</param>
        public Boolean Equals(SHIP_Id SHIPId)

            => String.Equals(InternalId,
                             SHIPId.InternalId,
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
