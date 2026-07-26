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

using System.Net;
using System.Text;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    #region (enum) DNSRecordTypes

    /// <summary>
    /// The DNS resource record types needed for DNS-SD (RFC 6763).
    /// </summary>
    public enum DNSRecordTypes : UInt16
    {

        /// <summary>
        /// An IPv4 address.
        /// </summary>
        A     =  1,

        /// <summary>
        /// A pointer to a service instance.
        /// </summary>
        PTR   = 12,

        /// <summary>
        /// The key/value pairs of a service instance.
        /// </summary>
        TXT   = 16,

        /// <summary>
        /// An IPv6 address.
        /// </summary>
        AAAA  = 28,

        /// <summary>
        /// The host and port of a service instance.
        /// </summary>
        SRV   = 33,

        /// <summary>
        /// Any record type.
        /// </summary>
        ANY   = 255

    }

    #endregion

    #region (class) DNSRecord

    /// <summary>
    /// A DNS resource record as needed for DNS-SD.
    /// </summary>
    /// <param name="Name">The name of the record.</param>
    /// <param name="Type">The type of the record.</param>
    /// <param name="TimeToLive">How long the record may be cached.</param>
    public class DNSRecord(String          Name,
                           DNSRecordTypes  Type,
                           UInt32          TimeToLive)
    {

        /// <summary>
        /// The name of the record.
        /// </summary>
        public String                Name          { get; }      = Name;

        /// <summary>
        /// The type of the record.
        /// </summary>
        public DNSRecordTypes        Type          { get; }      = Type;

        /// <summary>
        /// How long the record may be cached; zero means the record is going away.
        /// </summary>
        public UInt32                TimeToLive    { get; set; } = TimeToLive;

        /// <summary>
        /// The target of a PTR or SRV record.
        /// </summary>
        public String?               Target        { get; set; }

        /// <summary>
        /// The port of an SRV record.
        /// </summary>
        public UInt16                Port          { get; set; }

        /// <summary>
        /// The key/value strings of a TXT record.
        /// </summary>
        public IEnumerable<String>   TXTStrings    { get; set; } = [];

        /// <summary>
        /// The address of an A or AAAA record.
        /// </summary>
        public IPAddress?            Address       { get; set; }

    }

    #endregion


    /// <summary>
    /// The encoding and decoding of the multicast DNS messages needed for the
    /// discovery of SHIP nodes (RFC 1035, RFC 6762, RFC 6763).
    ///
    /// Only what DNS-SD requires is implemented - most importantly a TXT record
    /// consisting of *several* character strings, one per key/value pair, which
    /// is exactly what a SHIP node announces (SHIP TS 1.0.1, chapter 7.3.2).
    /// </summary>
    public static class SHIPMDNSMessage
    {

        #region Data

        /// <summary>
        /// The multicast group of mDNS (RFC 6762).
        /// </summary>
        public static readonly IPAddress  MulticastGroup       = IPAddress.Parse("224.0.0.251");

        /// <summary>
        /// The UDP port of mDNS.
        /// </summary>
        public const           UInt16     MulticastPort        = 5353;

        /// <summary>
        /// The DNS class "Internet".
        /// </summary>
        private const          UInt16     ClassInternet        = 0x0001;

        /// <summary>
        /// The mDNS cache flush bit within the class field of a response record.
        /// </summary>
        private const          UInt16     CacheFlush           = 0x8000;

        /// <summary>
        /// The mDNS unicast response bit within the class field of a question.
        /// </summary>
        private const          UInt16     UnicastResponse      = 0x8000;

        #endregion


        #region CreateQuery   (Name, Type)

        /// <summary>
        /// Create a multicast DNS query.
        /// </summary>
        /// <param name="Name">The name to ask for.</param>
        /// <param name="Type">The record type to ask for.</param>
        public static Byte[] CreateQuery(String          Name,
                                         DNSRecordTypes  Type)
        {

            var bytes = new List<Byte>();

            WriteHeader(bytes, IsResponse: false, Questions: 1, Answers: 0);
            WriteName  (bytes, Name);
            WriteUInt16(bytes, (UInt16) Type);
            WriteUInt16(bytes, ClassInternet);

            return [.. bytes];

        }

        #endregion

        #region CreateResponse(Records)

        /// <summary>
        /// Create a multicast DNS response containing the given records.
        /// </summary>
        /// <param name="Records">The records to announce.</param>
        public static Byte[] CreateResponse(IEnumerable<DNSRecord> Records)
        {

            var records  = Records.ToArray();
            var bytes    = new List<Byte>();

            WriteHeader(bytes, IsResponse: true, Questions: 0, Answers: (UInt16) records.Length);

            foreach (var record in records)
            {

                WriteName  (bytes, record.Name);
                WriteUInt16(bytes, (UInt16) record.Type);

                // A responder is the only owner of its records (RFC 6762, chapter 10.2).
                WriteUInt16(bytes, ClassInternet | CacheFlush);
                WriteUInt32(bytes, record.TimeToLive);

                var rdata = new List<Byte>();

                switch (record.Type)
                {

                    case DNSRecordTypes.PTR:
                        WriteName(rdata, record.Target ?? "");
                        break;

                    case DNSRecordTypes.SRV:
                        WriteUInt16(rdata, 0);              // priority
                        WriteUInt16(rdata, 0);              // weight
                        WriteUInt16(rdata, record.Port);
                        WriteName  (rdata, record.Target ?? "");
                        break;

                    case DNSRecordTypes.TXT:
                        {

                            var texts = record.TXTStrings.ToArray();

                            // An empty TXT record still needs one empty string
                            // (RFC 6763, chapter 6.1).
                            if (texts.Length == 0)
                                rdata.Add(0);

                            foreach (var text in texts)
                            {

                                var textBytes = Encoding.UTF8.GetBytes(text);

                                if (textBytes.Length > 255)
                                    throw new ArgumentException($"The TXT record string '{text}' is longer than 255 bytes!");

                                rdata.Add((Byte) textBytes.Length);
                                rdata.AddRange(textBytes);

                            }

                        }
                        break;

                    case DNSRecordTypes.A:
                    case DNSRecordTypes.AAAA:
                        rdata.AddRange((record.Address ?? IPAddress.None).GetAddressBytes());
                        break;

                }

                WriteUInt16(bytes, (UInt16) rdata.Count);
                bytes.AddRange(rdata);

            }

            return [.. bytes];

        }

        #endregion


        #region TryParse(ByteArray, out Questions, out Records)

        /// <summary>
        /// Try to parse the given multicast DNS message.
        /// </summary>
        /// <param name="ByteArray">A received UDP datagram.</param>
        /// <param name="Questions">The questions of the message.</param>
        /// <param name="Records">The resource records of the message.</param>
        public static Boolean TryParse(Byte[]                                                       ByteArray,
                                       [NotNullWhen(true)] out List<(String Name, DNSRecordTypes Type)>?  Questions,
                                       [NotNullWhen(true)] out List<DNSRecord>?                          Records)
        {

            Questions  = null;
            Records    = null;

            try
            {

                if (ByteArray.Length < 12)
                    return false;

                var position       = 0;

                position          += 2;                                            // transaction id
                var flags          = ReadUInt16(ByteArray, ref position);
                var questionCount  = ReadUInt16(ByteArray, ref position);
                var answerCount    = ReadUInt16(ByteArray, ref position);
                var authorityCount = ReadUInt16(ByteArray, ref position);
                var additionalCount= ReadUInt16(ByteArray, ref position);

                Questions          = [];
                Records            = [];

                for (var i = 0; i < questionCount; i++)
                {

                    var name  = ReadName  (ByteArray, ref position);
                    var type  = (DNSRecordTypes) ReadUInt16(ByteArray, ref position);
                    position += 2;                                                 // class

                    Questions.Add((name, type));

                }

                var recordCount = answerCount + authorityCount + additionalCount;

                for (var i = 0; i < recordCount && position < ByteArray.Length; i++)
                {

                    var name        = ReadName  (ByteArray, ref position);
                    var type        = (DNSRecordTypes) ReadUInt16(ByteArray, ref position);
                    position       += 2;                                           // class
                    var timeToLive  = ReadUInt32(ByteArray, ref position);
                    var rdLength    = ReadUInt16(ByteArray, ref position);
                    var rdataStart  = position;

                    var record      = new DNSRecord(name, type, timeToLive);

                    switch (type)
                    {

                        case DNSRecordTypes.PTR:
                            record.Target = ReadName(ByteArray, ref position);
                            break;

                        case DNSRecordTypes.SRV:
                            position     += 4;                                     // priority + weight
                            record.Port   = ReadUInt16(ByteArray, ref position);
                            record.Target = ReadName  (ByteArray, ref position);
                            break;

                        case DNSRecordTypes.TXT:
                            {

                                var texts = new List<String>();

                                while (position < rdataStart + rdLength)
                                {

                                    var length = ByteArray[position++];

                                    if (position + length > ByteArray.Length)
                                        break;

                                    texts.Add(Encoding.UTF8.GetString(ByteArray, position, length));
                                    position += length;

                                }

                                record.TXTStrings = texts;

                            }
                            break;

                        case DNSRecordTypes.A:
                            if (rdLength == 4)
                                record.Address = new IPAddress(ByteArray[position..(position + 4)]);
                            break;

                        case DNSRecordTypes.AAAA:
                            if (rdLength == 16)
                                record.Address = new IPAddress(ByteArray[position..(position + 16)]);
                            break;

                    }

                    // Skip whatever of the RDATA was not understood.
                    position = rdataStart + rdLength;

                    Records.Add(record);

                }

                return true;

            }
            catch (Exception)
            {
                // A malformed multicast packet is a fact of life on a shared
                // network and must never take the discovery down.
                Questions = null;
                Records   = null;
                return false;
            }

        }

        #endregion


        #region (private) Writing

        private static void WriteHeader(List<Byte>  Bytes,
                                        Boolean     IsResponse,
                                        UInt16      Questions,
                                        UInt16      Answers)
        {

            // mDNS messages carry the transaction id 0 (RFC 6762, chapter 18.1).
            WriteUInt16(Bytes, 0);

            // Responses are authoritative answers (RFC 6762, chapter 18.4).
            WriteUInt16(Bytes, IsResponse ? (UInt16) 0x8400 : (UInt16) 0x0000);

            WriteUInt16(Bytes, Questions);
            WriteUInt16(Bytes, Answers);
            WriteUInt16(Bytes, 0);
            WriteUInt16(Bytes, 0);

        }

        private static void WriteName(List<Byte> Bytes, String Name)
        {

            foreach (var label in Name.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {

                var labelBytes = Encoding.UTF8.GetBytes(label);

                if (labelBytes.Length > 63)
                    throw new ArgumentException($"The DNS label '{label}' is longer than 63 bytes!");

                Bytes.Add((Byte) labelBytes.Length);
                Bytes.AddRange(labelBytes);

            }

            Bytes.Add(0);

        }

        private static void WriteUInt16(List<Byte> Bytes, UInt16 Value)
        {
            Bytes.Add((Byte) (Value >> 8));
            Bytes.Add((Byte) (Value & 0xFF));
        }

        private static void WriteUInt32(List<Byte> Bytes, UInt32 Value)
        {
            Bytes.Add((Byte) (Value >> 24));
            Bytes.Add((Byte) (Value >> 16));
            Bytes.Add((Byte) (Value >>  8));
            Bytes.Add((Byte) (Value & 0xFF));
        }

        #endregion

        #region (private) Reading

        private static UInt16 ReadUInt16(Byte[] Bytes, ref Int32 Position)
        {
            var value  = (UInt16) ((Bytes[Position] << 8) | Bytes[Position + 1]);
            Position  += 2;
            return value;
        }

        private static UInt32 ReadUInt32(Byte[] Bytes, ref Int32 Position)
        {
            var value  = (UInt32) ((Bytes[Position] << 24) | (Bytes[Position + 1] << 16) |
                                   (Bytes[Position + 2] << 8) | Bytes[Position + 3]);
            Position  += 4;
            return value;
        }

        private static String ReadName(Byte[] Bytes, ref Int32 Position)
        {

            var labels     = new List<String>();
            var position   = Position;
            var jumped     = false;
            var safetyStop = 0;

            while (position < Bytes.Length && ++safetyStop < 128)
            {

                var length = Bytes[position];

                if (length == 0)
                {
                    position++;
                    break;
                }

                // A compression pointer (RFC 1035, chapter 4.1.4).
                if ((length & 0xC0) == 0xC0)
                {

                    var pointer = ((length & 0x3F) << 8) | Bytes[position + 1];

                    if (!jumped)
                    {
                        Position  = position + 2;
                        jumped    = true;
                    }

                    position = pointer;
                    continue;

                }

                position++;
                labels.Add(Encoding.UTF8.GetString(Bytes, position, length));
                position += length;

            }

            if (!jumped)
                Position = position;

            return String.Join(".", labels);

        }

        #endregion

    }

}
