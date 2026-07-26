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

using System.Globalization;

using Newtonsoft.Json;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// An ISO 8601 time of day ("xs:time"), for example "14:00:00" or
    /// "14:00:00+02:00". The received text is kept verbatim.
    /// </summary>
    [JsonConverter(typeof(SPINEStringTypeConverter<TimeType>))]
    public readonly struct TimeType : IId,
                                      ISPINEStringType<TimeType>,
                                      IEquatable<TimeType>,
                                      IComparable<TimeType>
    {

        #region Data

        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this time of day is null or empty.
        /// </summary>
        public readonly Boolean    IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this time of day is NOT null or empty.
        /// </summary>
        public readonly Boolean    IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the time of day.
        /// </summary>
        public readonly UInt64     Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// This time of day as a TimeOnly, or null when the text cannot be
        /// understood. Any time zone offset is dropped here - it is still part
        /// of <see cref="ToString"/>.
        /// </summary>
        public readonly TimeOnly?  AsTimeOnly
        {
            get
            {

                if (InternalId.IsNullOrEmpty())
                    return null;

                // "14:00:00+02:00" and "14:00:00Z" carry an offset which TimeOnly
                // does not know about; the time of day itself is the part in front.
                var text  = InternalId;
                var index = text.IndexOfAny([ 'Z', 'z', '+' ]);

                if (index < 0)
                    index = text.LastIndexOf('-');

                if (index > 0)
                    text = text[..index];

                return TimeOnly.TryParse(text,
                                         CultureInfo.InvariantCulture,
                                         out var timeOnly)
                           ? timeOnly
                           : null;

            }
        }

        #endregion

        #region Constructor(s)

        private TimeType(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text as an ISO 8601 time of day.
        /// </summary>
        /// <param name="Text">A text representation of a time of day.</param>
        public static TimeType Parse(String Text)
        {

            if (TryParse(Text, out var time))
                return time;

            throw new ArgumentException("The given text representation of a time of day is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text, out Time)

        /// <summary>
        /// Try to parse the given text as an ISO 8601 time of day.
        /// </summary>
        /// <param name="Text">A text representation of a time of day.</param>
        /// <param name="Time">The parsed time of day.</param>
        public static Boolean TryParse(String Text, out TimeType Time)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                Time = new TimeType(Text);
                return true;
            }

            Time = default;
            return false;

        }

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two times of day for equality.
        /// </summary>
        public static Boolean operator == (TimeType Time1, TimeType Time2)
            => Time1.Equals(Time2);

        /// <summary>
        /// Compares two times of day for inequality.
        /// </summary>
        public static Boolean operator != (TimeType Time1, TimeType Time2)
            => !Time1.Equals(Time2);

        /// <summary>
        /// Compares two times of day.
        /// </summary>
        public static Boolean operator <  (TimeType Time1, TimeType Time2)
            => Time1.CompareTo(Time2) <  0;

        /// <summary>
        /// Compares two times of day.
        /// </summary>
        public static Boolean operator <= (TimeType Time1, TimeType Time2)
            => Time1.CompareTo(Time2) <= 0;

        /// <summary>
        /// Compares two times of day.
        /// </summary>
        public static Boolean operator >  (TimeType Time1, TimeType Time2)
            => Time1.CompareTo(Time2) >  0;

        /// <summary>
        /// Compares two times of day.
        /// </summary>
        public static Boolean operator >= (TimeType Time1, TimeType Time2)
            => Time1.CompareTo(Time2) >= 0;

        #endregion

        #region IComparable<TimeType> Members

        /// <summary>
        /// Compares two times of day.
        /// </summary>
        /// <param name="Object">A time of day to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is TimeType time
                   ? CompareTo(time)
                   : throw new ArgumentException("The given object is not a time of day!",
                                                 nameof(Object));

        /// <summary>
        /// Compares two times of day.
        /// </summary>
        /// <param name="Time">A time of day to compare with.</param>
        public Int32 CompareTo(TimeType Time)
        {

            var a = AsTimeOnly;
            var b = Time.AsTimeOnly;

            if (a.HasValue && b.HasValue)
                return a.Value.CompareTo(b.Value);

            return String.Compare(InternalId,
                                  Time.InternalId,
                                  StringComparison.Ordinal);

        }

        #endregion

        #region IEquatable<TimeType> Members

        /// <summary>
        /// Compares two times of day for equality.
        /// </summary>
        /// <param name="Object">A time of day to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is TimeType time &&
                   Equals(time);

        /// <summary>
        /// Compares two times of day for equality of their text representation.
        /// </summary>
        /// <param name="Time">A time of day to compare with.</param>
        public Boolean Equals(TimeType Time)

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
        /// Return the text representation of this time of day.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
