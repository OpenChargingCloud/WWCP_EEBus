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
    /// Extension methods for message protocol formats.
    /// </summary>
    public static class MessageProtocolFormatExtensions
    {

        /// <summary>
        /// Indicates whether this message protocol format is null or empty.
        /// </summary>
        /// <param name="MessageProtocolFormat">A message protocol format.</param>
        public static Boolean IsNullOrEmpty(this MessageProtocolFormat? MessageProtocolFormat)
            => !MessageProtocolFormat.HasValue || MessageProtocolFormat.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this message protocol format is null or empty.
        /// </summary>
        /// <param name="MessageProtocolFormat">A message protocol format.</param>
        public static Boolean IsNotNullOrEmpty(this MessageProtocolFormat? MessageProtocolFormat)
            => MessageProtocolFormat.HasValue && MessageProtocolFormat.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// A message protocol format negotiated within the SHIP protocol handshake (SHIP TS 1.0.1, chapter 13.4.4.2).
    /// </summary>
    public readonly struct MessageProtocolFormat : IId,
                                                  IEquatable<MessageProtocolFormat>,
                                                  IComparable<MessageProtocolFormat>
    {

        #region Data

        private readonly static Dictionary<String, MessageProtocolFormat>  lookup = new (StringComparer.OrdinalIgnoreCase);
        private readonly        String                                    InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this message protocol format is null or empty.
        /// </summary>
        public readonly  Boolean  IsNullOrEmpty
            => InternalId.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this message protocol format is NOT null or empty.
        /// </summary>
        public readonly  Boolean  IsNotNullOrEmpty
            => InternalId.IsNotNullOrEmpty();

        /// <summary>
        /// The length of the message protocol format.
        /// </summary>
        public readonly  UInt64   Length
            => (UInt64) (InternalId?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new message protocol format based on the given text.
        /// </summary>
        /// <param name="Text">The text representation of a message protocol format.</param>
        private MessageProtocolFormat(String Text)
        {
            this.InternalId = Text;
        }

        #endregion


        #region (private static) Register(Text)

        private static MessageProtocolFormat Register(String Text)

            => lookup.AddAndReturnValue(
                   Text,
                   new MessageProtocolFormat(Text)
               );

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a message protocol format.
        /// </summary>
        /// <param name="Text">A text representation of a message protocol format.</param>
        public static MessageProtocolFormat Parse(String Text)
        {

            if (TryParse(Text, out var messageProtocolFormat))
                return messageProtocolFormat;

            throw new ArgumentException("The given text representation of a message protocol format is invalid!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given text as message protocol format.
        /// </summary>
        /// <param name="Text">A text representation of a message protocol format.</param>
        public static MessageProtocolFormat? TryParse(String Text)
        {

            if (TryParse(Text, out var messageProtocolFormat))
                return messageProtocolFormat;

            return null;

        }

        #endregion

        #region (static) TryParse(Text, out MessageProtocolFormat)

        /// <summary>
        /// Try to parse the given text as message protocol format.
        /// </summary>
        /// <param name="Text">A text representation of a message protocol format.</param>
        /// <param name="MessageProtocolFormat">The parsed message protocol format.</param>
        public static Boolean TryParse(String Text, out MessageProtocolFormat MessageProtocolFormat)
        {

            Text = Text.Trim();

            if (Text.IsNotNullOrEmpty())
            {

                if (!lookup.TryGetValue(Text, out MessageProtocolFormat))
                    MessageProtocolFormat = Register(Text);

                return true;

            }

            MessageProtocolFormat = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this message protocol format.
        /// </summary>
        public MessageProtocolFormat Clone

            => new (
                   new String(InternalId?.ToCharArray())
               );

        #endregion


        // Note: MessageProtocolFormatType is an unrestricted xs:string in
        // EEBus_SHIP_TS_TransferProtocol.xsd, so these are conventions, not a
        // closed set of values.
        #region Static definitions

        /// <summary>
        /// JSON encoded as UTF-8. The only format required by SHIP.
        /// </summary>
        public static MessageProtocolFormat  JSON_UTF8    { get; }
            = Register("JSON-UTF8");

        /// <summary>
        /// JSON encoded as UTF-16. Optional; not supported by this implementation.
        /// </summary>
        public static MessageProtocolFormat  JSON_UTF16    { get; }
            = Register("JSON-UTF16");

        #endregion


        #region Operator overloading

        #region Operator == (MessageProtocolFormat1, MessageProtocolFormat2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageProtocolFormat1">A message protocol format.</param>
        /// <param name="MessageProtocolFormat2">Another message protocol format.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (MessageProtocolFormat MessageProtocolFormat1,
                                           MessageProtocolFormat MessageProtocolFormat2)

            => MessageProtocolFormat1.Equals(MessageProtocolFormat2);

        #endregion

        #region Operator != (MessageProtocolFormat1, MessageProtocolFormat2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageProtocolFormat1">A message protocol format.</param>
        /// <param name="MessageProtocolFormat2">Another message protocol format.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (MessageProtocolFormat MessageProtocolFormat1,
                                           MessageProtocolFormat MessageProtocolFormat2)

            => !MessageProtocolFormat1.Equals(MessageProtocolFormat2);

        #endregion

        #region Operator <  (MessageProtocolFormat1, MessageProtocolFormat2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageProtocolFormat1">A message protocol format.</param>
        /// <param name="MessageProtocolFormat2">Another message protocol format.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (MessageProtocolFormat MessageProtocolFormat1,
                                          MessageProtocolFormat MessageProtocolFormat2)

            => MessageProtocolFormat1.CompareTo(MessageProtocolFormat2) < 0;

        #endregion

        #region Operator <= (MessageProtocolFormat1, MessageProtocolFormat2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageProtocolFormat1">A message protocol format.</param>
        /// <param name="MessageProtocolFormat2">Another message protocol format.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (MessageProtocolFormat MessageProtocolFormat1,
                                           MessageProtocolFormat MessageProtocolFormat2)

            => MessageProtocolFormat1.CompareTo(MessageProtocolFormat2) <= 0;

        #endregion

        #region Operator >  (MessageProtocolFormat1, MessageProtocolFormat2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageProtocolFormat1">A message protocol format.</param>
        /// <param name="MessageProtocolFormat2">Another message protocol format.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (MessageProtocolFormat MessageProtocolFormat1,
                                          MessageProtocolFormat MessageProtocolFormat2)

            => MessageProtocolFormat1.CompareTo(MessageProtocolFormat2) > 0;

        #endregion

        #region Operator >= (MessageProtocolFormat1, MessageProtocolFormat2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MessageProtocolFormat1">A message protocol format.</param>
        /// <param name="MessageProtocolFormat2">Another message protocol format.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (MessageProtocolFormat MessageProtocolFormat1,
                                           MessageProtocolFormat MessageProtocolFormat2)

            => MessageProtocolFormat1.CompareTo(MessageProtocolFormat2) >= 0;

        #endregion

        #endregion

        #region IComparable<MessageProtocolFormat> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two message protocol formats.
        /// </summary>
        /// <param name="Object">A message protocol format to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is MessageProtocolFormat messageProtocolFormat
                   ? CompareTo(messageProtocolFormat)
                   : throw new ArgumentException("The given object is not message protocol format!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(MessageProtocolFormat)

        /// <summary>
        /// Compares two message protocol formats.
        /// </summary>
        /// <param name="MessageProtocolFormat">A message protocol format to compare with.</param>
        public Int32 CompareTo(MessageProtocolFormat MessageProtocolFormat)

            => String.Compare(InternalId,
                              MessageProtocolFormat.InternalId,
                              StringComparison.OrdinalIgnoreCase);

        #endregion

        #endregion

        #region IEquatable<MessageProtocolFormat> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two message protocol formats for equality.
        /// </summary>
        /// <param name="Object">A message protocol format to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is MessageProtocolFormat messageProtocolFormat &&
                   Equals(messageProtocolFormat);

        #endregion

        #region Equals(MessageProtocolFormat)

        /// <summary>
        /// Compares two message protocol formats for equality.
        /// </summary>
        /// <param name="MessageProtocolFormat">A message protocol format to compare with.</param>
        public Boolean Equals(MessageProtocolFormat MessageProtocolFormat)

            => String.Equals(InternalId,
                             MessageProtocolFormat.InternalId,
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
