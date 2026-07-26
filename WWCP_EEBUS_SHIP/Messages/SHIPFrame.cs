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

using System.Text;
using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// A SHIP frame: the payload of a binary WebSocket frame, consisting of a
    /// single message type byte followed by the message itself in EEBUS JSON
    /// (SHIP TS 1.0.1, chapter 13.3).
    ///
    /// Messages of type "init" carry a single value byte instead of JSON.
    /// </summary>
    /// <param name="MessageType">The SHIP message type.</param>
    /// <param name="Payload">The message, in ordinary JSON; null for "init" messages.</param>
    public class SHIPFrame(SHIPMessageTypes  MessageType,
                           JObject?          Payload   = null)
    {

        #region Data

        /// <summary>
        /// The default maximum size of an incoming SHIP frame.
        ///
        /// SHIP does not define a limit; this one protects against memory exhaustion
        /// and matches the limit of the ship-go reference implementation.
        /// </summary>
        public const UInt32 DefaultMaxFrameLength = 100 * 1024;

        #endregion

        #region Properties

        /// <summary>
        /// The SHIP message type.
        /// </summary>
        public SHIPMessageTypes  MessageType    { get; } = MessageType;

        /// <summary>
        /// The message in ordinary JSON, or null for "init" messages.
        /// </summary>
        public JObject?          Payload        { get; } = Payload;

        #endregion


        #region (static) Init

        /// <summary>
        /// The "Connection Mode Initialisation" (CMI) frame, which both
        /// communication partners send first (SHIP TS 1.0.1, chapter 13.4.3).
        /// </summary>
        public static SHIPFrame Init

            => new (SHIPMessageTypes.INIT);

        #endregion


        #region (static) TryParse(ByteArray, out Frame, out ErrorResponse, MaxFrameLength = DefaultMaxFrameLength)

        /// <summary>
        /// Try to parse the given binary representation of a SHIP frame.
        /// </summary>
        /// <param name="ByteArray">The payload of a binary WebSocket frame.</param>
        /// <param name="Frame">The parsed SHIP frame.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="MaxFrameLength">The maximum accepted frame length.</param>
        public static Boolean TryParse(ReadOnlyMemory<Byte>              ByteArray,
                                       [NotNullWhen(true)]  out SHIPFrame?  Frame,
                                       [NotNullWhen(false)] out String?     ErrorResponse,
                                       UInt32                              MaxFrameLength = DefaultMaxFrameLength)
        {

            Frame          = null;
            ErrorResponse  = null;

            #region Length

            if (ByteArray.Length == 0)
            {
                ErrorResponse = "A SHIP frame must at least contain its message type byte!";
                return false;
            }

            if (ByteArray.Length > MaxFrameLength)
            {
                ErrorResponse = $"The given SHIP frame is too large: {ByteArray.Length} > {MaxFrameLength} bytes!";
                return false;
            }

            #endregion

            #region Message type

            var messageTypeByte = ByteArray.Span[0];

            if (!Enum.IsDefined(typeof(SHIPMessageTypes), messageTypeByte))
            {
                ErrorResponse = $"Unknown SHIP message type '{messageTypeByte}'!";
                return false;
            }

            var messageType = (SHIPMessageTypes) messageTypeByte;
            var payload     = ByteArray[1..];

            #endregion

            #region Message type "init": a single value byte, no JSON

            if (messageType == SHIPMessageTypes.INIT)
            {

                if (payload.Length != 1)
                {
                    ErrorResponse = $"A SHIP init message must contain exactly one value byte, but contains {payload.Length}!";
                    return false;
                }

                if (payload.Span[0] != SHIPMessageValue.CMI_HEAD)
                {
                    ErrorResponse = $"A SHIP init message must contain the value {SHIPMessageValue.CMI_HEAD}, but contains {payload.Span[0]}!";
                    return false;
                }

                Frame = Init;
                return true;

            }

            #endregion

            #region All other message types: EEBUS JSON

            if (payload.Length == 0)
            {
                ErrorResponse = $"A SHIP {messageType} message must contain a message value!";
                return false;
            }

            String text;

            try
            {
                // Some devices (e.g. the Porsche Mobile Charger) append NUL bytes.
                text = Encoding.UTF8.GetString(payload.Span).TrimEnd('\0').Trim();
            }
            catch (Exception e)
            {
                ErrorResponse = "The given SHIP frame is not valid UTF-8: " + e.Message;
                return false;
            }

            if (text.Length == 0)
            {
                ErrorResponse = $"A SHIP {messageType} message must contain a message value!";
                return false;
            }

            JObject eebusJSON;

            try
            {
                // Whitespace formatting of the JSON text is explicitly allowed.
                eebusJSON = JObject.Parse(text);
            }
            catch (JsonException e)
            {
                ErrorResponse = "The given SHIP frame does not contain a valid JSON object: " + e.Message;
                return false;
            }

            if (!EEBUSJSON.TryToStandardJSON(eebusJSON, out var standardJSON, out ErrorResponse))
                return false;

            if (standardJSON.Count != 1)
            {
                ErrorResponse = $"A SHIP message must consist of exactly one message element, but contains {standardJSON.Count}!";
                return false;
            }

            Frame = new SHIPFrame(messageType, standardJSON);
            return true;

            #endregion

        }

        #endregion

        #region ToByteArray()

        /// <summary>
        /// Return the binary representation of this SHIP frame.
        /// </summary>
        public Byte[] ToByteArray()
        {

            if (MessageType == SHIPMessageTypes.INIT)
                return [ (Byte) SHIPMessageTypes.INIT, SHIPMessageValue.CMI_HEAD ];

            if (Payload is null)
                throw new InvalidOperationException($"A SHIP {MessageType} message requires a message value!");

            var json  = EEBUSJSON.ToEEBUSJSON(Payload).ToString(Formatting.None);
            var bytes = new Byte[1 + Encoding.UTF8.GetByteCount(json)];

            bytes[0] = (Byte) MessageType;
            Encoding.UTF8.GetBytes(json, 0, json.Length, bytes, 1);

            return bytes;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => MessageType == SHIPMessageTypes.INIT
                   ? "init"
                   : $"{MessageType.ToString().ToLower()}: {Payload?.ToString(Formatting.None) ?? "-"}";

        #endregion

    }

}
