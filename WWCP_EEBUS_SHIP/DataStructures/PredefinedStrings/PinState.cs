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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// Extension methods for PIN states.
    /// </summary>
    public static class PinStateExtensions
    {

        /// <summary>
        /// Indicates whether this PIN state is null or empty.
        /// </summary>
        /// <param name="PinState">A PIN state.</param>
        public static Boolean IsNullOrEmpty(this PinState? PinState)
            => !PinState.HasValue || PinState.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this PIN state is null or empty.
        /// </summary>
        /// <param name="PinState">A PIN state.</param>
        public static Boolean IsNotNullOrEmpty(this PinState? PinState)
            => PinState.HasValue && PinState.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The PIN state of a SHIP connection (SHIP TS 1.0.1, chapter 13.4.5).
    /// </summary>
    public readonly struct PinState : IId,
                                                  IEquatable<PinState>,
                                                  IComparable<PinState>
    {

        #region Data

        private readonly static Dictionary<String, PinState>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                    InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this PIN state is null or empty.
        /// </summary>
        public readonly  Boolean  IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this PIN state is NOT null or empty.
        /// </summary>
        public readonly  Boolean  IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the PIN state.
        /// </summary>
        public readonly  UInt64   Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new PIN state based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a PIN state.</param>
        private PinState(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static PinState Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new PinState(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a PIN state.
        /// </summary>
        /// <param name="Text">A text representation of a PIN state.</param>
        public static PinState Parse(String Text)
        {

            if (TryParse(Text, out var pinState))
                return pinState;

            throw new ArgumentException("The given text representation of a PIN state is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as PIN state.
        /// </summary>
        /// <param name="Text">A text representation of a PIN state.</param>
        public static PinState? TryParse(String Text)
        {

            if (TryParse(Text, out var pinState))
                return pinState;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out PinState)

        /// <summary>
        /// Try to parse the given text as PIN state.
        /// </summary>
        /// <param name="Text">A text representation of a PIN state.</param>
        /// <param name="PinState">The parsed PIN state.</param>
        public static Boolean TryParse(String Text, out PinState PinState)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out PinState))
                    PinState = Register(Text);

                return true;

            }

            PinState = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this PIN state.
        /// </summary>
        public PinState Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// A PIN is required.
        /// </summary>
        public static PinState  Required    { get; }
            = Register("required");

        /// <summary>
        /// A PIN may be provided.
        /// </summary>
        public static PinState  Optional    { get; }
            = Register("optional");

        /// <summary>
        /// The PIN has been accepted.
        /// </summary>
        public static PinState  PinOk    { get; }
            = Register("pinOk");

        /// <summary>
        /// No PIN is used.
        /// </summary>
        public static PinState  None    { get; }
            = Register("none");

        #endregion

        #region Validity

        /// <summary>
        /// All PIN states defined by SHIP TS 1.0.1, chapter 13.4.5.
        /// </summary>
        public static IEnumerable<PinState>  All    { get; }
            = [ Required, Optional, PinOk, None ];

        /// <summary>
        /// Whether this is one of the PIN states defined by SHIP TS 1.0.1.
        ///
        /// Parsing is deliberately tolerant, so that a value sent by a communication
        /// partner can be reported precisely instead of failing to parse. Validating
        /// it is the task of the state machines and the conformance tests.
        /// </summary>
        public Boolean IsDefined
            => All.Contains(this);

        #endregion



        #region Operator overloading

        #region Operator == (PinState1, PinState2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinState1">A PIN state.</param>
        /// <param name="PinState2">Another PIN state.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (PinState PinState1,
                                           PinState PinState2)

            => PinState1.Equals(PinState2);

        #endregion

        #region Operator != (PinState1, PinState2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinState1">A PIN state.</param>
        /// <param name="PinState2">Another PIN state.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (PinState PinState1,
                                           PinState PinState2)

            => !PinState1.Equals(PinState2);

        #endregion

        #region Operator <  (PinState1, PinState2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinState1">A PIN state.</param>
        /// <param name="PinState2">Another PIN state.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (PinState PinState1,
                                          PinState PinState2)

            => PinState1.CompareTo(PinState2) < 0;

        #endregion

        #region Operator <= (PinState1, PinState2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinState1">A PIN state.</param>
        /// <param name="PinState2">Another PIN state.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (PinState PinState1,
                                           PinState PinState2)

            => PinState1.CompareTo(PinState2) <= 0;

        #endregion

        #region Operator >  (PinState1, PinState2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinState1">A PIN state.</param>
        /// <param name="PinState2">Another PIN state.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (PinState PinState1,
                                          PinState PinState2)

            => PinState1.CompareTo(PinState2) > 0;

        #endregion

        #region Operator >= (PinState1, PinState2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="PinState1">A PIN state.</param>
        /// <param name="PinState2">Another PIN state.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (PinState PinState1,
                                           PinState PinState2)

            => PinState1.CompareTo(PinState2) >= 0;

        #endregion

        #endregion

        #region IComparable<PinState> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two PIN states.
        /// </summary>
        /// <param name="Object">A PIN state to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is PinState pinState
                   ? CompareTo(pinState)
                   : throw new ArgumentException("The given object is not PIN state!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(PinState)

        /// <summary>
        /// Compares two PIN states.
        /// </summary>
        /// <param name="PinState">A PIN state to compare with.</param>
        public Int32 CompareTo(PinState PinState)

            => String.Compare(InternalId,
                              PinState.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<PinState> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two PIN states for equality.
        /// </summary>
        /// <param name="Object">A PIN state to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is PinState pinState &&
                   Equals(pinState);

        #endregion

        #region Equals(PinState)

        /// <summary>
        /// Compares two PIN states for equality.
        /// </summary>
        /// <param name="PinState">A PIN state to compare with.</param>
        public Boolean Equals(PinState PinState)

            => String.Equals(InternalId,
                             PinState.InternalId,
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
