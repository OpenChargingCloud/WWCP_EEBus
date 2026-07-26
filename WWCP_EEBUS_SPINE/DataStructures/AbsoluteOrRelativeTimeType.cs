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

using Newtonsoft.Json;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// A point in time which is either absolute or relative (SPINE 1.3.0,
    /// CommonDataTypes): the XSD declares it as the union of "xs:duration" and
    /// "xs:dateTime", so "2023-08-31T14:00:00Z" and "PT2M" are both valid.
    ///
    /// Which of the two a value is can be read from <see cref="IsRelative"/>.
    /// The text itself is kept verbatim, as for every other ISO 8601 type of
    /// this model.
    /// </summary>
    [JsonConverter(typeof(SPINEStringTypeConverter<AbsoluteOrRelativeTimeType>))]
    public readonly struct AbsoluteOrRelativeTimeType : IId,
                                                        ISPINEStringType<AbsoluteOrRelativeTimeType>,
                                                        IEquatable<AbsoluteOrRelativeTimeType>,
                                                        IComparable<AbsoluteOrRelativeTimeType>
    {

        #region Data

        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this point in time is null or empty.
        /// </summary>
        public readonly Boolean          IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this point in time is NOT null or empty.
        /// </summary>
        public readonly Boolean          IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the point in time.
        /// </summary>
        public readonly UInt64           Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// Whether this is a relative time, i.e. an ISO 8601 duration.
        /// Every ISO 8601 duration starts with "P", optionally preceded by a
        /// sign; a timestamp always starts with its year.
        /// </summary>
        public readonly Boolean          IsRelative

            => InternalId.IsNotNullOrEmpty() &&
               (InternalId[0] == 'P' ||
               ((InternalId[0] == '-' || InternalId[0] == '+') && InternalId.Length > 1 && InternalId[1] == 'P'));

        /// <summary>
        /// Whether this is an absolute point in time.
        /// </summary>
        public readonly Boolean          IsAbsolute
            => IsNotNullOrEmpty && !IsRelative;

        /// <summary>
        /// This value as a duration, or null when it is not a relative time.
        /// </summary>
        public readonly TimeSpan?        AsTimeSpan

            => IsRelative && DurationType.TryParse(InternalId, out var duration)
                   ? duration.AsTimeSpan
                   : null;

        /// <summary>
        /// This value as a point in time, or null when it is not an absolute time.
        /// </summary>
        public readonly DateTimeOffset?  AsDateTimeOffset

            => IsAbsolute && DateTimeType.TryParse(InternalId, out var timestamp)
                   ? timestamp.AsDateTimeOffset
                   : null;

        #endregion

        #region Constructor(s)

        private AbsoluteOrRelativeTimeType(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text as an absolute or relative point in time.
        /// </summary>
        /// <param name="Text">A text representation.</param>
        public static AbsoluteOrRelativeTimeType Parse(String Text)
        {

            if (TryParse(Text, out var time))
                return time;

            throw new ArgumentException("The given text representation of an absolute or relative time is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) Parse   (Timestamp/TimeSpan)

        /// <summary>
        /// Create an absolute point in time.
        /// </summary>
        /// <param name="Timestamp">A point in time.</param>
        public static AbsoluteOrRelativeTimeType Parse(DateTimeOffset Timestamp)

            => new (DateTimeType.Parse(Timestamp).ToString());

        /// <summary>
        /// Create a relative point in time.
        /// </summary>
        /// <param name="TimeSpan">A duration.</param>
        public static AbsoluteOrRelativeTimeType Parse(TimeSpan TimeSpan)

            => new (DurationType.Parse(TimeSpan).ToString());

        #endregion

        #region (static) TryParse(Text, out Time)

        /// <summary>
        /// Try to parse the given text as an absolute or relative point in time.
        /// </summary>
        /// <param name="Text">A text representation.</param>
        /// <param name="Time">The parsed point in time.</param>
        public static Boolean TryParse(String Text, out AbsoluteOrRelativeTimeType Time)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                Time = new AbsoluteOrRelativeTimeType(Text);
                return true;
            }

            Time = default;
            return false;

        }

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two points in time for equality.
        /// </summary>
        public static Boolean operator == (AbsoluteOrRelativeTimeType Time1, AbsoluteOrRelativeTimeType Time2)
            => Time1.Equals(Time2);

        /// <summary>
        /// Compares two points in time for inequality.
        /// </summary>
        public static Boolean operator != (AbsoluteOrRelativeTimeType Time1, AbsoluteOrRelativeTimeType Time2)
            => !Time1.Equals(Time2);

        /// <summary>
        /// Compares two points in time.
        /// </summary>
        public static Boolean operator <  (AbsoluteOrRelativeTimeType Time1, AbsoluteOrRelativeTimeType Time2)
            => Time1.CompareTo(Time2) <  0;

        /// <summary>
        /// Compares two points in time.
        /// </summary>
        public static Boolean operator <= (AbsoluteOrRelativeTimeType Time1, AbsoluteOrRelativeTimeType Time2)
            => Time1.CompareTo(Time2) <= 0;

        /// <summary>
        /// Compares two points in time.
        /// </summary>
        public static Boolean operator >  (AbsoluteOrRelativeTimeType Time1, AbsoluteOrRelativeTimeType Time2)
            => Time1.CompareTo(Time2) >  0;

        /// <summary>
        /// Compares two points in time.
        /// </summary>
        public static Boolean operator >= (AbsoluteOrRelativeTimeType Time1, AbsoluteOrRelativeTimeType Time2)
            => Time1.CompareTo(Time2) >= 0;

        #endregion

        #region IComparable<AbsoluteOrRelativeTimeType> Members

        /// <summary>
        /// Compares two points in time.
        /// </summary>
        /// <param name="Object">A point in time to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is AbsoluteOrRelativeTimeType time
                   ? CompareTo(time)
                   : throw new ArgumentException("The given object is not an absolute or relative time!",
                                                 nameof(Object));

        /// <summary>
        /// Compares two points in time.
        ///
        /// An absolute and a relative time cannot be compared without knowing
        /// what the relative one refers to, so those are compared as text.
        /// </summary>
        /// <param name="Time">A point in time to compare with.</param>
        public Int32 CompareTo(AbsoluteOrRelativeTimeType Time)
        {

            if (IsRelative == Time.IsRelative)
            {

                if (IsRelative)
                {

                    var durationA = AsTimeSpan;
                    var durationB = Time.AsTimeSpan;

                    if (durationA.HasValue && durationB.HasValue)
                        return durationA.Value.CompareTo(durationB.Value);

                }

                else
                {

                    var timestampA = AsDateTimeOffset;
                    var timestampB = Time.AsDateTimeOffset;

                    if (timestampA.HasValue && timestampB.HasValue)
                        return timestampA.Value.CompareTo(timestampB.Value);

                }

            }

            return String.Compare(InternalId,
                                  Time.InternalId,
                                  StringComparison.Ordinal);

        }

        #endregion

        #region IEquatable<AbsoluteOrRelativeTimeType> Members

        /// <summary>
        /// Compares two points in time for equality.
        /// </summary>
        /// <param name="Object">A point in time to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is AbsoluteOrRelativeTimeType time &&
                   Equals(time);

        /// <summary>
        /// Compares two points in time for equality of their text representation.
        /// </summary>
        /// <param name="Time">A point in time to compare with.</param>
        public Boolean Equals(AbsoluteOrRelativeTimeType Time)

            => String.Equals(InternalId,
                             Time.InternalId,
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
        /// Return the text representation of this point in time.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
