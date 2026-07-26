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
    /// An ISO 8601 duration ("xs:duration"), for example "PT2M" or "P1DT30M".
    ///
    /// The text is kept exactly as it was received. "PT2M" and "PT120S" are the
    /// same duration but not the same datagram, and a test bench which silently
    /// re-formats what it forwards cannot tell anybody what actually went over
    /// the wire.
    /// </summary>
    [JsonConverter(typeof(SPINEStringTypeConverter<DurationType>))]
    public readonly struct DurationType : IId,
                                          ISPINEStringType<DurationType>,
                                          IEquatable<DurationType>,
                                          IComparable<DurationType>
    {

        #region Data

        private readonly String InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this duration is null or empty.
        /// </summary>
        public readonly Boolean    IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this duration is NOT null or empty.
        /// </summary>
        public readonly Boolean    IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the duration.
        /// </summary>
        public readonly UInt64     Length
            => (UInt64) (InternalId?.Length ?? 0);

        /// <summary>
        /// This duration as a TimeSpan, or null when the text is not a valid
        /// ISO 8601 duration.
        /// </summary>
        public readonly TimeSpan?  AsTimeSpan
        {
            get
            {

                if (InternalId.IsNullOrEmpty())
                    return null;

                try
                {
                    return XmlConvert.ToTimeSpan(InternalId);
                }
                catch (FormatException)
                {
                    return null;
                }

            }
        }

        #endregion

        #region Constructor(s)

        private DurationType(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given text as an ISO 8601 duration.
        /// </summary>
        /// <param name="Text">A text representation of a duration.</param>
        public static DurationType Parse(String Text)
        {

            if (TryParse(Text, out var duration))
                return duration;

            throw new ArgumentException("The given text representation of a duration is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) Parse   (TimeSpan)

        /// <summary>
        /// Create an ISO 8601 duration from the given time span.
        /// </summary>
        /// <param name="TimeSpan">A time span.</param>
        public static DurationType Parse(TimeSpan TimeSpan)

            => new (XmlConvert.ToString(TimeSpan));

        #endregion

        #region (static) TryParse(Text, out Duration)

        /// <summary>
        /// Try to parse the given text as an ISO 8601 duration.
        /// </summary>
        /// <param name="Text">A text representation of a duration.</param>
        /// <param name="Duration">The parsed duration.</param>
        public static Boolean TryParse(String Text, out DurationType Duration)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {
                Duration = new DurationType(Text);
                return true;
            }

            Duration = default;
            return false;

        }

        #endregion


        #region Operator overloading

        /// <summary>
        /// Compares two durations for equality.
        /// </summary>
        public static Boolean operator == (DurationType Duration1, DurationType Duration2)
            => Duration1.Equals(Duration2);

        /// <summary>
        /// Compares two durations for inequality.
        /// </summary>
        public static Boolean operator != (DurationType Duration1, DurationType Duration2)
            => !Duration1.Equals(Duration2);

        /// <summary>
        /// Compares two durations.
        /// </summary>
        public static Boolean operator <  (DurationType Duration1, DurationType Duration2)
            => Duration1.CompareTo(Duration2) <  0;

        /// <summary>
        /// Compares two durations.
        /// </summary>
        public static Boolean operator <= (DurationType Duration1, DurationType Duration2)
            => Duration1.CompareTo(Duration2) <= 0;

        /// <summary>
        /// Compares two durations.
        /// </summary>
        public static Boolean operator >  (DurationType Duration1, DurationType Duration2)
            => Duration1.CompareTo(Duration2) >  0;

        /// <summary>
        /// Compares two durations.
        /// </summary>
        public static Boolean operator >= (DurationType Duration1, DurationType Duration2)
            => Duration1.CompareTo(Duration2) >= 0;

        #endregion

        #region IComparable<DurationType> Members

        /// <summary>
        /// Compares two durations.
        /// </summary>
        /// <param name="Object">A duration to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is DurationType duration
                   ? CompareTo(duration)
                   : throw new ArgumentException("The given object is not a duration!",
                                                 nameof(Object));

        /// <summary>
        /// Compares two durations.
        ///
        /// Durations which can be understood are compared by their length, so
        /// that "PT2M" and "PT120S" are equivalent. Text which is not a valid
        /// duration is compared as text.
        /// </summary>
        /// <param name="Duration">A duration to compare with.</param>
        public Int32 CompareTo(DurationType Duration)
        {

            var a = AsTimeSpan;
            var b = Duration.AsTimeSpan;

            if (a.HasValue && b.HasValue)
                return a.Value.CompareTo(b.Value);

            return String.Compare(InternalId,
                                  Duration.InternalId,
                                  StringComparison.Ordinal);

        }

        #endregion

        #region IEquatable<DurationType> Members

        /// <summary>
        /// Compares two durations for equality.
        /// </summary>
        /// <param name="Object">A duration to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is DurationType duration &&
                   Equals(duration);

        /// <summary>
        /// Compares two durations for equality.
        ///
        /// This is equality of the text, not of the length: a test bench has to
        /// be able to see that a partner answered "PT120S" where it had been
        /// sent "PT2M". Use <see cref="CompareTo(DurationType)"/> for the
        /// semantic comparison.
        /// </summary>
        /// <param name="Duration">A duration to compare with.</param>
        public Boolean Equals(DurationType Duration)

            => String.Equals(InternalId,
                             Duration.InternalId,
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
        /// Return the text representation of this duration.
        /// </summary>
        public override String ToString()

            => InternalId ?? "";

        #endregion

    }

}
