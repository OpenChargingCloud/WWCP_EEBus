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
using System.Text;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE.Model
{

    /// <summary>
    /// A number and the power of ten it has to be multiplied with
    /// (SPINE 1.3.0, CommonDataTypes).
    ///
    /// Every measured value, every limit and every price of SPINE is one of
    /// these, because the protocol transmits no floating point numbers at all:
    /// 1185 with the scale -1 is 118.5 W, and it is that exactly.
    /// </summary>
    public partial class ScaledNumberType
    {

        #region Properties

        /// <summary>
        /// The value, or null when no number was given.
        ///
        /// Decimal rather than Double: a scaled number is a decimal number by
        /// construction, and a price or an energy reading which changes in the
        /// last digit because it went through a binary floating point number is
        /// exactly the kind of finding a conformance test must not produce
        /// itself.
        /// </summary>
        public Decimal? Value
        {
            get
            {

                if (!Number.HasValue)
                    return null;

                var scale = Scale ?? 0;

                // Decimal covers 10^-28 .. 10^28; anything beyond that is not a
                // measurement any more, and silently returning something wrong
                // would be worse than saying nothing.
                if (scale < -28 || scale > 28)
                    return null;

                var value = (Decimal) Number.Value;

                for (var i = 0; i < scale; i++)
                    value *= 10;

                for (var i = 0; i > scale; i--)
                    value /= 10;

                return value;

            }
        }

        #endregion


        #region (static) FromValue(Value, MaximumDecimals = 4)

        /// <summary>
        /// The scaled number of the given value.
        ///
        /// The scale is chosen as the smallest one which represents the value
        /// exactly, so that 12 becomes {12, 0} and not {120000, -4}.
        /// </summary>
        /// <param name="Value">A value.</param>
        /// <param name="MaximumDecimals">How many decimals to keep at most.</param>
        public static ScaledNumberType FromValue(Decimal  Value,
                                                 Byte     MaximumDecimals   = 4)
        {

            var number = Value;
            var scale  = 0;

            while (number != Decimal.Truncate(number) && -scale < MaximumDecimals)
            {
                number *= 10;
                scale--;
            }

            // Still not a whole number: round to what was allowed.
            number = Decimal.Round(number, 0, MidpointRounding.AwayFromZero);

            // A value like 1200 does not need a scale of -2.
            while (scale < 0 && number % 10 == 0)
            {
                number /= 10;
                scale++;
            }

            return new ScaledNumberType {
                       Number  = (Int64) number,
                       Scale   = (Int16) scale
                   };

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this scaled number.
        /// </summary>
        public override String ToString()

            => Value?.ToString(CultureInfo.InvariantCulture)
                   ?? "-";

        #endregion

    }


    /// <summary>
    /// The address of a SPINE device (SPINE 1.3.0, CommonDataTypes).
    /// </summary>
    public partial class DeviceAddressType
    {

        /// <summary>
        /// Return a text representation of this address.
        /// The format is the one of the Go reference implementation, so that a
        /// log of ours and a log of theirs can be read next to each other.
        /// </summary>
        public override String ToString()

            => Device ?? "";

    }


    /// <summary>
    /// The address of a SPINE entity (SPINE 1.3.0, CommonDataTypes).
    /// </summary>
    public partial class EntityAddressType
    {

        /// <summary>
        /// Return a text representation of this address, e.g. "d:_i:19667_HEMS:[1,1]:".
        /// </summary>
        public override String ToString()

            => SPINEAddress.ToString(Device, Entity, null);

    }


    /// <summary>
    /// The address of a SPINE feature (SPINE 1.3.0, CommonDataTypes): device,
    /// entity and feature together are what a datagram is addressed to.
    /// </summary>
    public partial class FeatureAddressType
    {

        #region Properties

        /// <summary>
        /// Whether this address names all three parts.
        /// </summary>
        public Boolean IsComplete

            => Device is not null &&
               Entity is not null && Entity.Count > 0 &&
               Feature.HasValue;

        #endregion


        #region Matches(Address)

        /// <summary>
        /// Whether the given address is addressed by this one.
        ///
        /// A part which this address leaves out matches anything: SPINE 1.3.0
        /// addresses a datagram to a device, to an entity of it or to a single
        /// feature, and which of the three it is can be seen from what is
        /// missing. The device is compared without regard to case, because a
        /// device address is a name and not a byte string.
        /// </summary>
        /// <param name="Address">An address to compare with.</param>
        public Boolean Matches(FeatureAddressType? Address)
        {

            if (Address is null)
                return false;

            if (Device is not null &&
                !String.Equals(Device, Address.Device, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (Entity is not null && Entity.Count > 0)
            {

                if (Address.Entity is null || Address.Entity.Count != Entity.Count)
                    return false;

                for (var i = 0; i < Entity.Count; i++)
                    if (Entity[i] != Address.Entity[i])
                        return false;

            }

            if (Feature.HasValue && Feature != Address.Feature)
                return false;

            return true;

        }

        #endregion

        #region Clone()

        /// <summary>
        /// Return a copy of this address.
        ///
        /// The model is mutable - a generated data transfer object has to be, to
        /// be deserialised - so an address which is kept has to be copied.
        /// </summary>
        public FeatureAddressType Clone()

            => new () {
                   Device   = Device,
                   Entity   = Entity is not null ? [.. Entity] : null,
                   Feature  = Feature
               };

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this address, e.g. "d:_i:19667_HEMS:[1,1]:6".
        /// </summary>
        public override String ToString()

            => SPINEAddress.ToString(Device, Entity, Feature);

        #endregion

    }


    /// <summary>
    /// How an address of SPINE is written down.
    /// </summary>
    internal static class SPINEAddress
    {

        /// <summary>
        /// "device:[entity,entity]:feature", as the Go reference implementation
        /// writes it. Parts which are missing are left empty rather than
        /// omitted, so that the shape of the address stays readable.
        /// </summary>
        internal static String ToString(String?         Device,
                                        List<UInt32>?   Entity,
                                        UInt32?         Feature)
        {

            var builder = new StringBuilder();

            builder.Append(Device ?? "");
            builder.Append(":[");

            if (Entity is not null)
                for (var i = 0; i < Entity.Count; i++)
                {

                    if (i > 0)
                        builder.Append(',');

                    builder.Append(Entity[i].ToString(CultureInfo.InvariantCulture));

                }

            builder.Append("]:");

            if (Feature.HasValue)
                builder.Append(Feature.Value.ToString(CultureInfo.InvariantCulture));

            return builder.ToString();

        }

    }


    /// <summary>
    /// A period of time (SPINE 1.3.0, CommonDataTypes).
    /// </summary>
    public partial class TimePeriodType
    {

        #region Duration(TimeProvider)

        /// <summary>
        /// How long this period still lasts, or null when that cannot be told.
        ///
        /// A period without a start time and with a relative end time is a
        /// duration from now; one with an absolute end time is the time left
        /// until then. SPINE uses the first form for everything which is meant
        /// to expire, above all the limits of load control.
        ///
        /// Note that reading and writing do not change the value: the Go
        /// reference implementation rewrites a relative end time into an
        /// absolute one while reading and back while writing, so that the
        /// duration keeps decreasing. That is convenient for a stack and wrong
        /// for a test bench, which has to be able to say what actually stood in
        /// the datagram. Ask for the duration here instead, whenever it is
        /// needed.
        /// </summary>
        /// <param name="TimeProvider">The time provider deciding what "now" is.</param>
        public TimeSpan? Duration(TimeProvider TimeProvider)
        {

            if (StartTime is not null || !EndTime.HasValue)
                return null;

            var endTime = EndTime.Value;

            if (endTime.IsRelative)
                return endTime.AsTimeSpan;

            var timestamp = endTime.AsDateTimeOffset;

            if (!timestamp.HasValue)
                return null;

            var remaining = timestamp.Value - TimeProvider.GetUtcNow();

            return remaining < TimeSpan.Zero
                       ? TimeSpan.Zero
                       : remaining;

        }

        #endregion

        #region (static) FromDuration(Duration)

        /// <summary>
        /// A period which ends after the given duration.
        /// </summary>
        /// <param name="Duration">A duration.</param>
        public static TimePeriodType FromDuration(TimeSpan Duration)

            => new () {
                   EndTime = AbsoluteOrRelativeTimeType.Parse(Duration)
               };

        #endregion

    }


    /// <summary>
    /// Which operations a feature offers for one of its functions
    /// (SPINE 1.3.0, CommonDataTypes).
    /// </summary>
    public partial class PossibleOperationsType
    {

        /// <summary>
        /// Whether the function may be read.
        /// </summary>
        public Boolean CanRead
            => Read is not null;

        /// <summary>
        /// Whether the function may be written.
        /// </summary>
        public Boolean CanWrite
            => Write is not null;

        /// <summary>
        /// Whether the function may be read partially.
        /// </summary>
        public Boolean CanReadPartial
            => Read?.Partial is not null;

        /// <summary>
        /// Whether the function may be written partially.
        /// </summary>
        public Boolean CanWritePartial
            => Write?.Partial is not null;


        /// <summary>
        /// Return the possible operations of a function which can be read, and
        /// optionally written.
        /// </summary>
        /// <param name="Write">Whether the function may be written.</param>
        /// <param name="PartialRead">Whether the function may be read partially.</param>
        /// <param name="PartialWrite">Whether the function may be written partially.</param>
        public static PossibleOperationsType ReadAndMaybeWrite(Boolean  Write          = false,
                                                               Boolean  PartialRead    = false,
                                                               Boolean  PartialWrite   = false)

            => new () {

                   Read   = new PossibleOperationsReadType {
                                Partial = PartialRead ? new ElementTagType() : null
                            },

                   Write  = Write
                                ? new PossibleOperationsWriteType {
                                      Partial = PartialWrite ? new ElementTagType() : null
                                  }
                                : null

               };

    }


    /// <summary>
    /// An element tag (SPINE 1.3.0, CommonDataTypes): it names a field and
    /// carries nothing. On the wire it is the empty object.
    /// </summary>
    public partial class ElementTagType
    {

        /// <summary>
        /// A new element tag. Reads better than "new ElementTagType()" where a
        /// tag is only being set, e.g. "CmdControl = CmdControlType.Partial".
        /// </summary>
        public static ElementTagType Set
            => new ();

        /// <summary>
        /// Return a text representation of this element tag.
        /// </summary>
        public override String ToString()
            => "{}";

    }

}
