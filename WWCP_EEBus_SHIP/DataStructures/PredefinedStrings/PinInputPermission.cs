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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// Extension methods for PIN input permissions.
    /// </summary>
    public static class PinInputPermissionExtensions
    {

        /// <summary>
        /// Indicates whether this PIN input permission is null or empty.
        /// </summary>
        /// <param name="PinInputPermission">A PIN input permission.</param>
        public static Boolean IsNullOrEmpty(this PinInputPermission? PinInputPermission)
            => !PinInputPermission.HasValue || PinInputPermission.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this PIN input permission is null or empty.
        /// </summary>
        /// <param name="PinInputPermission">A PIN input permission.</param>
        public static Boolean IsNotNullOrEmpty(this PinInputPermission? PinInputPermission)
            => PinInputPermission.HasValue && PinInputPermission.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// Whether the communication partner currently accepts a PIN input (SHIP TS 1.0.1, chapter 13.4.5).
    /// </summary>
    public readonly struct PinInputPermission : IId,
                                                  IEquatable<PinInputPermission>,
                                                  IComparable<PinInputPermission>
    {

        #region Data

        private readonly static Dictionary<String, PinInputPermission>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                    InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this PIN input permission is null or empty.
        /// </summary>
        public readonly  Boolean  IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this PIN input permission is NOT null or empty.
        /// </summary>
        public readonly  Boolean  IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the PIN input permission.
        /// </summary>
        public readonly  UInt64   Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new PIN input permission based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a PIN input permission.</param>
        private PinInputPermission(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static PinInputPermission Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new PinInputPermission(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a PIN input permission.
        /// </summary>
        /// <param name="Text">A text representation of a PIN input permission.</param>
        public static PinInputPermission Parse(String Text)
        {

            if (TryParse(Text, out var pinInputPermission))
                return pinInputPermission;

            throw new ArgumentException("The given text representation of a PIN input permission is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as PIN input permission.
        /// </summary>
        /// <param name="Text">A text representation of a PIN input permission.</param>
        public static PinInputPermission? TryParse(String Text)
        {

            if (TryParse(Text, out var pinInputPermission))
                return pinInputPermission;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out PinInputPermission)

        /// <summary>
        /// Try to parse the given text as PIN input permission.
        /// </summary>
        /// <param name="Text">A text representation of a PIN input permission.</param>
        /// <param name="PinInputPermission">The parsed PIN input permission.</param>
        public static Boolean TryParse(String Text, out PinInputPermission PinInputPermission)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out PinInputPermission))
                    PinInputPermission = Register(Text);

                return true;

            }

            PinInputPermission = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this PIN input permission.
        /// </summary>
        public PinInputPermission Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// The communication partner is busy and does not accept a PIN input.
        /// </summary>
        public static PinInputPermission  Busy    { get; }
            = Register("busy");

        /// <summary>
        /// The communication partner accepts a PIN input.
        /// </summary>
        public static PinInputPermission  OK    { get; }
            = Register("ok");

        #endregion

        #region Validity

        /// <summary>
        /// All PIN input permissions defined by SHIP TS 1.0.1, chapter 13.4.5.
        /// </summary>
        public static IEnumerable<PinInputPermission>  All    { get; }
            = [ Busy, OK ];

        /// <summary>
        /// Whether this is one of the PIN input permissions defined by SHIP TS 1.0.1.
        ///
        /// Parsing is deliberately tolerant, so that a value sent by a communication
        /// partner can be reported precisely instead of failing to parse. Validating
        /// it is the task of the state machines and the conformance tests.
        /// </summary>
        public Boolean IsDefined
            => All.Contains(this);

        #endregion



        #region Operator overloading

        #region Operator == (PinInputPermission1, PinInputPermission2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinInputPermission1">A PIN input permission.</param>
        /// <param name="PinInputPermission2">Another PIN input permission.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (PinInputPermission PinInputPermission1,
                                           PinInputPermission PinInputPermission2)

            => PinInputPermission1.Equals(PinInputPermission2);

        #endregion

        #region Operator != (PinInputPermission1, PinInputPermission2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinInputPermission1">A PIN input permission.</param>
        /// <param name="PinInputPermission2">Another PIN input permission.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (PinInputPermission PinInputPermission1,
                                           PinInputPermission PinInputPermission2)

            => !PinInputPermission1.Equals(PinInputPermission2);

        #endregion

        #region Operator <  (PinInputPermission1, PinInputPermission2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinInputPermission1">A PIN input permission.</param>
        /// <param name="PinInputPermission2">Another PIN input permission.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (PinInputPermission PinInputPermission1,
                                          PinInputPermission PinInputPermission2)

            => PinInputPermission1.CompareTo(PinInputPermission2) < 0;

        #endregion

        #region Operator <= (PinInputPermission1, PinInputPermission2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinInputPermission1">A PIN input permission.</param>
        /// <param name="PinInputPermission2">Another PIN input permission.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (PinInputPermission PinInputPermission1,
                                           PinInputPermission PinInputPermission2)

            => PinInputPermission1.CompareTo(PinInputPermission2) <= 0;

        #endregion

        #region Operator >  (PinInputPermission1, PinInputPermission2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinInputPermission1">A PIN input permission.</param>
        /// <param name="PinInputPermission2">Another PIN input permission.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (PinInputPermission PinInputPermission1,
                                          PinInputPermission PinInputPermission2)

            => PinInputPermission1.CompareTo(PinInputPermission2) > 0;

        #endregion

        #region Operator >= (PinInputPermission1, PinInputPermission2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinInputPermission1">A PIN input permission.</param>
        /// <param name="PinInputPermission2">Another PIN input permission.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (PinInputPermission PinInputPermission1,
                                           PinInputPermission PinInputPermission2)

            => PinInputPermission1.CompareTo(PinInputPermission2) >= 0;

        #endregion

        #endregion

        #region IComparable<PinInputPermission> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two PIN input permissions.
        /// </summary>
        /// <param name="Object">A PIN input permission to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is PinInputPermission pinInputPermission
                   ? CompareTo(pinInputPermission)
                   : throw new ArgumentException("The given object is not PIN input permission!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(PinInputPermission)

        /// <summary>
        /// Compares two PIN input permissions.
        /// </summary>
        /// <param name="PinInputPermission">A PIN input permission to compare with.</param>
        public Int32 CompareTo(PinInputPermission PinInputPermission)

            => String.Compare(InternalId,
                              PinInputPermission.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<PinInputPermission> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two PIN input permissions for equality.
        /// </summary>
        /// <param name="Object">A PIN input permission to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is PinInputPermission pinInputPermission &&
                   Equals(pinInputPermission);

        #endregion

        #region Equals(PinInputPermission)

        /// <summary>
        /// Compares two PIN input permissions for equality.
        /// </summary>
        /// <param name="PinInputPermission">A PIN input permission to compare with.</param>
        public Boolean Equals(PinInputPermission PinInputPermission)

            => String.Equals(InternalId,
                             PinInputPermission.InternalId,
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
