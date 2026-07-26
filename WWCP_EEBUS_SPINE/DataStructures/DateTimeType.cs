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

using System.Xml;

using Newtonsoft.Json;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// An ISO 8601 point in time ("xs:dateTime"), for example
    /// "2023-08-31T14:00:00Z".
    ///
    /// As with <see cref="DurationType"/> the received text is kept verbatim:
    /// the offset notation, the number of fractional digits and the "Z" are all
    /// part of what a conformance test has to be able to report.
    /// </summary>
    [JsonConverter(typeof(SPINEStringTypeConverter<DateTimeType>))]
    public readonly struct DateTimeType : IId,
                                          ISPINEStringType<DateTimeType>,
                                          IEquatable<DateTimeType>,
                                          IComparable<DateTimeType>
    {

        #region Data

        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this timestamp is null or empty.
        /// </summary>
        public readonly Boolean          IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this timestamp is NOT null or empty.
        /// </summary>
        public readonly Boolean          IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the timestamp.
        /// </summary>
        public readonly UInt64           Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// This timestamp as a DateTimeOffset, or null when the text is not a
        /// valid ISO 8601 timestamp.
        /// </summary>
        public readonly DateTimeOffset?  AsDateTimeOffset
        {
            get
            {

                if (InternalId.IsNullOrEmpty())
                    return null;

                try
                {
                    return XmlConvert.ToDateTimeOffset(InternalId);
                }
                catch (FormatException)
                {
                    return null;
                }

            }
        }

        #endregion

        #region Constructor(s)

        private DateTimeType(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text as an ISO 8601 timestamp.
        /// </summary>
        /// <param name="Text">A text representation of a timestamp.</param>
        public static DateTimeType Parse(String Text)
        {

            if (TryParse(Text, out var timestamp))
                return timestamp;

            throw new ArgumentException("The given text representation of a timestamp is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) Parse   (Timestamp)

        /// <summary>
        /// Create an ISO 8601 timestamp from the given point in time.
        /// </summary>
        /// <param name="Timestamp">A point in time.</param>
        public static DateTimeType Parse(DateTimeOffset Timestamp)

            => new (XmlConvert.ToString(Timestamp));

        #endregion

        #region (static) TryParse(Text, out Timestamp)

        /// <summary>
        /// Try to parse the given text as an ISO 8601 timestamp.
        /// </summary>
        /// <param name="Text">A text representation of a timestamp.</param>
        /// <param name="Timestamp">The parsed timestamp.</param>
        public static Boolean TryParse(String Text, out DateTimeType Timestamp)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                Timestamp = new DateTimeType(Text);
                return true;
            }

            Timestamp = default;
            return false;

        }

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two timestamps for equality.
        /// </summary>
        public static Boolean operator == (DateTimeType Timestamp1, DateTimeType Timestamp2)
            => Timestamp1.Equals(Timestamp2);

        /// <summary>
        /// Compares two timestamps for inequality.
        /// </summary>
        public static Boolean operator != (DateTimeType Timestamp1, DateTimeType Timestamp2)
            => !Timestamp1.Equals(Timestamp2);

        /// <summary>
        /// Compares two timestamps.
        /// </summary>
        public static Boolean operator <  (DateTimeType Timestamp1, DateTimeType Timestamp2)
            => Timestamp1.CompareTo(Timestamp2) <  0;

        /// <summary>
        /// Compares two timestamps.
        /// </summary>
        public static Boolean operator <= (DateTimeType Timestamp1, DateTimeType Timestamp2)
            => Timestamp1.CompareTo(Timestamp2) <= 0;

        /// <summary>
        /// Compares two timestamps.
        /// </summary>
        public static Boolean operator >  (DateTimeType Timestamp1, DateTimeType Timestamp2)
            => Timestamp1.CompareTo(Timestamp2) >  0;

        /// <summary>
        /// Compares two timestamps.
        /// </summary>
        public static Boolean operator >= (DateTimeType Timestamp1, DateTimeType Timestamp2)
            => Timestamp1.CompareTo(Timestamp2) >= 0;

        #endregion

        #region IComparable<DateTimeType> Members

        /// <summary>
        /// Compares two timestamps.
        /// </summary>
        /// <param name="Object">A timestamp to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DateTimeType timestamp
                   ? CompareTo(timestamp)
                   : throw new ArgumentException("The given object is not a timestamp!",
                                                 nameof(Object));

        /// <summary>
        /// Compares two timestamps: those which can be understood by their
        /// point in time, everything else as text.
        /// </summary>
        /// <param name="Timestamp">A timestamp to compare with.</param>
        public Int32 CompareTo(DateTimeType Timestamp)
        {

            var a = AsDateTimeOffset;
            var b = Timestamp.AsDateTimeOffset;

            if (a.HasValue && b.HasValue)
                return a.Value.CompareTo(b.Value);

            return String.Compare(InternalId,
                                  Timestamp.InternalId,
                                  StringComparison.Ordinal);

        }

        #endregion

        #region IEquatable<DateTimeType> Members

        /// <summary>
        /// Compares two timestamps for equality.
        /// </summary>
        /// <param name="Object">A timestamp to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DateTimeType timestamp &&
                   Equals(timestamp);

        /// <summary>
        /// Compares two timestamps for equality of their text representation.
        /// </summary>
        /// <param name="Timestamp">A timestamp to compare with.</param>
        public Boolean Equals(DateTimeType Timestamp)

            => String.Equals(InternalId,
                             Timestamp.InternalId,
                             StringComparison.Ordinal);

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        public override Int32 GetHashCode()

            => InternalId?.GetHashCode() ?? 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return the text representation of this timestamp.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
