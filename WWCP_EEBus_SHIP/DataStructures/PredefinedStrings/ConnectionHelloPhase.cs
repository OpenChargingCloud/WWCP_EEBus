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
    /// Extension methods for Connection Hello phases.
    /// </summary>
    public static class ConnectionHelloPhaseTypeExtensions
    {

        /// <summary>
        /// Indicates whether this Connection Hello phase is null or empty.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType">A Connection Hello phase.</param>
        public static Boolean IsNullOrEmpty(this ConnectionHelloPhase? ConnectionHelloPhaseType)
            => !ConnectionHelloPhaseType.HasValue || ConnectionHelloPhaseType.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this Connection Hello phase is null or empty.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType">A Connection Hello phase.</param>
        public static Boolean IsNotNullOrEmpty(this ConnectionHelloPhase? ConnectionHelloPhaseType)
            => ConnectionHelloPhaseType.HasValue && ConnectionHelloPhaseType.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A Connection Hello phase.
    /// </summary>
    public readonly struct ConnectionHelloPhase : IId,
                                                  IEquatable<ConnectionHelloPhase>,
                                                  IComparable<ConnectionHelloPhase>
    {

        #region Data

        private readonly static Dictionary<String, ConnectionHelloPhase>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                    InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this Connection Hello phase is null or empty.
        /// </summary>
        public readonly  Boolean  IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this Connection Hello phase is NOT null or empty.
        /// </summary>
        public readonly  Boolean  IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the Connection Hello phase.
        /// </summary>
        public readonly  UInt64   Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new Connection Hello phase based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a Connection Hello phase.</param>
        private ConnectionHelloPhase(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static ConnectionHelloPhase Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new ConnectionHelloPhase(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a Connection Hello phase.
        /// </summary>
        /// <param name="Text">A text representation of a Connection Hello phase.</param>
        public static ConnectionHelloPhase Parse(String Text)
        {

            if (TryParse(Text, out var connectionHelloPhase))
                return connectionHelloPhase;

            throw new ArgumentException("The given text representation of a Connection Hello phase is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as Connection Hello phase.
        /// </summary>
        /// <param name="Text">A text representation of a Connection Hello phase.</param>
        public static ConnectionHelloPhase? TryParse(String Text)
        {

            if (TryParse(Text, out var connectionHelloPhase))
                return connectionHelloPhase;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out ConnectionHelloPhaseType)

        /// <summary>
        /// Try to parse the given text as Connection Hello phase.
        /// </summary>
        /// <param name="Text">A text representation of a Connection Hello phase.</param>
        /// <param name="ConnectionHelloPhaseType">The parsed Connection Hello phase.</param>
        public static Boolean TryParse(String Text, out ConnectionHelloPhase ConnectionHelloPhaseType)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out ConnectionHelloPhaseType))
                    ConnectionHelloPhaseType = Register(Text);

                return true;

            }

            ConnectionHelloPhaseType = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this Connection Hello phase.
        /// </summary>
        public ConnectionHelloPhase Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        #region Static definitions

        /// <summary>
        /// Pending
        /// </summary>
        public static ConnectionHelloPhase  Pending    { get; }
            = Register("pending");

        /// <summary>
        /// Ready
        /// </summary>
        public static ConnectionHelloPhase  Ready      { get; }
            = Register("ready");

        /// <summary>
        /// Aborted
        /// </summary>
        public static ConnectionHelloPhase  Aborted    { get; }
            = Register("aborted");

        #endregion


        #region Operator overloading

        #region Operator == (ConnectionHelloPhaseType1, ConnectionHelloPhaseType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType1">A Connection Hello phase.</param>
        /// <param name="ConnectionHelloPhaseType2">Another Connection Hello phase.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (ConnectionHelloPhase ConnectionHelloPhaseType1,
                                           ConnectionHelloPhase ConnectionHelloPhaseType2)

            => ConnectionHelloPhaseType1.Equals(ConnectionHelloPhaseType2);

        #endregion

        #region Operator != (ConnectionHelloPhaseType1, ConnectionHelloPhaseType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType1">A Connection Hello phase.</param>
        /// <param name="ConnectionHelloPhaseType2">Another Connection Hello phase.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (ConnectionHelloPhase ConnectionHelloPhaseType1,
                                           ConnectionHelloPhase ConnectionHelloPhaseType2)

            => !ConnectionHelloPhaseType1.Equals(ConnectionHelloPhaseType2);

        #endregion

        #region Operator <  (ConnectionHelloPhaseType1, ConnectionHelloPhaseType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType1">A Connection Hello phase.</param>
        /// <param name="ConnectionHelloPhaseType2">Another Connection Hello phase.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (ConnectionHelloPhase ConnectionHelloPhaseType1,
                                          ConnectionHelloPhase ConnectionHelloPhaseType2)

            => ConnectionHelloPhaseType1.CompareTo(ConnectionHelloPhaseType2) < 0;

        #endregion

        #region Operator <= (ConnectionHelloPhaseType1, ConnectionHelloPhaseType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType1">A Connection Hello phase.</param>
        /// <param name="ConnectionHelloPhaseType2">Another Connection Hello phase.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (ConnectionHelloPhase ConnectionHelloPhaseType1,
                                           ConnectionHelloPhase ConnectionHelloPhaseType2)

            => ConnectionHelloPhaseType1.CompareTo(ConnectionHelloPhaseType2) <= 0;

        #endregion

        #region Operator >  (ConnectionHelloPhaseType1, ConnectionHelloPhaseType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType1">A Connection Hello phase.</param>
        /// <param name="ConnectionHelloPhaseType2">Another Connection Hello phase.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (ConnectionHelloPhase ConnectionHelloPhaseType1,
                                          ConnectionHelloPhase ConnectionHelloPhaseType2)

            => ConnectionHelloPhaseType1.CompareTo(ConnectionHelloPhaseType2) > 0;

        #endregion

        #region Operator >= (ConnectionHelloPhaseType1, ConnectionHelloPhaseType2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType1">A Connection Hello phase.</param>
        /// <param name="ConnectionHelloPhaseType2">Another Connection Hello phase.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (ConnectionHelloPhase ConnectionHelloPhaseType1,
                                           ConnectionHelloPhase ConnectionHelloPhaseType2)

            => ConnectionHelloPhaseType1.CompareTo(ConnectionHelloPhaseType2) >= 0;

        #endregion

        #endregion

        #region IComparable<ConnectionHelloPhaseType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two Connection Hello phases.
        /// </summary>
        /// <param name="Object">A Connection Hello phase to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is ConnectionHelloPhase connectionHelloPhase
                   ? CompareTo(connectionHelloPhase)
                   : throw new ArgumentException("The given object is not Connection Hello phase!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(ConnectionHelloPhaseType)

        /// <summary>
        /// Compares two Connection Hello phases.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType">A Connection Hello phase to compare with.</param>
        public Int32 CompareTo(ConnectionHelloPhase ConnectionHelloPhaseType)

            => String.Compare(InternalId,
                              ConnectionHelloPhaseType.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<ConnectionHelloPhaseType> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two Connection Hello phases for equality.
        /// </summary>
        /// <param name="Object">A Connection Hello phase to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is ConnectionHelloPhase connectionHelloPhase &&
                   Equals(connectionHelloPhase);

        #endregion

        #region Equals(ConnectionHelloPhaseType)

        /// <summary>
        /// Compares two Connection Hello phases for equality.
        /// </summary>
        /// <param name="ConnectionHelloPhaseType">A Connection Hello phase to compare with.</param>
        public Boolean Equals(ConnectionHelloPhase ConnectionHelloPhaseType)

            => String.Equals(InternalId,
                             ConnectionHelloPhaseType.InternalId,
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
