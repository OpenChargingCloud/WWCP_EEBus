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

using System.Diagnostics.CodeAnalysis;

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    /// <summary>
    /// The common base class of all SHIP messages.
    ///
    /// On the wire every SHIP message is a binary WebSocket frame consisting of
    /// a single message type byte followed by its JSON representation
    /// (SHIP TS 1.0.1, chapter 13.3).
    /// </summary>
    /// <param name="MessageType">The SHIP message type determining the leading byte of the binary frame.</param>
    public abstract class ASHIPMessage(SHIPMessageTypes MessageType)
    {

        #region Properties

        /// <summary>
        /// The SHIP message type, transmitted as the leading byte of the binary frame.
        /// </summary>
        public SHIPMessageTypes  MessageType    { get; } = MessageType;

        #endregion


        #region ToMessageJSON()

        /// <summary>
        /// Return the JSON representation of this message, including its message
        /// element, e.g. { "connectionHello": { ... } }.
        ///
        /// Null for "init" messages, which carry a single value byte instead of JSON.
        /// </summary>
        public abstract JObject? ToMessageJSON();

        #endregion

        #region ToFrame()

        /// <summary>
        /// Return this message as a SHIP frame.
        /// </summary>
        public SHIPFrame ToFrame()

            => new (MessageType,
                    ToMessageJSON());

        #endregion

        #region ToByteArray()

        /// <summary>
        /// Return the binary representation of this message, ready to be sent
        /// within a binary WebSocket frame.
        /// </summary>
        public Byte[] ToByteArray()

            => ToFrame().ToByteArray();

        #endregion


        #region (static) TryParse(Frame,     out Message, out ErrorResponse)

        /// <summary>
        /// Try to parse the given SHIP frame as a SHIP message.
        /// </summary>
        /// <param name="Frame">A SHIP frame.</param>
        /// <param name="Message">The parsed SHIP message.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        public static Boolean TryParse(SHIPFrame                            Frame,
                                       [NotNullWhen(true)]  out ASHIPMessage?  Message,
                                       [NotNullWhen(false)] out String?        ErrorResponse)
        {

            Message        = null;
            ErrorResponse  = null;

            if (Frame.MessageType == SHIPMessageTypes.INIT)
            {
                Message = new SHIPInitMessage();
                return true;
            }

            if (Frame.Payload is null)
            {
                ErrorResponse = $"A SHIP {Frame.MessageType} message requires a message value!";
                return false;
            }

            var messageElement  = Frame.Payload.Properties().First();
            var messageName     = messageElement.Name;

            if (messageElement.Value is not JObject)
            {
                ErrorResponse = $"The message element '{messageName}' must be a JSON object!";
                return false;
            }

            // SHIP TS 1.0.1, chapter 13.4: which message elements are allowed
            // within which message type.
            switch (Frame.MessageType, messageName)
            {

                case (SHIPMessageTypes.CONTROL, "connectionHello"):
                    if (!SHIPHelloMessage.         TryParse(Frame.Payload, out var helloMessage,          out ErrorResponse))
                        return false;
                    Message = helloMessage;
                    return true;

                case (SHIPMessageTypes.CONTROL, "messageProtocolHandshake"):
                    if (!SHIPHandshakeMessage.     TryParse(Frame.Payload, out var handshakeMessage,      out ErrorResponse))
                        return false;
                    Message = handshakeMessage;
                    return true;

                case (SHIPMessageTypes.CONTROL, "messageProtocolHandshakeError"):
                    if (!SHIPHandshakeErrorMessage.TryParse(Frame.Payload, out var handshakeErrorMessage, out ErrorResponse))
                        return false;
                    Message = handshakeErrorMessage;
                    return true;

                case (SHIPMessageTypes.CONTROL, "connectionPinState"):
                    if (!SHIPPinStateMessage.      TryParse(Frame.Payload, out var pinStateMessage,       out ErrorResponse))
                        return false;
                    Message = pinStateMessage;
                    return true;

                case (SHIPMessageTypes.CONTROL, "connectionPinInput"):
                    if (!SHIPPinInputMessage.      TryParse(Frame.Payload, out var pinInputMessage,       out ErrorResponse))
                        return false;
                    Message = pinInputMessage;
                    return true;

                case (SHIPMessageTypes.CONTROL, "connectionPinError"):
                    if (!SHIPPinErrorMessage.      TryParse(Frame.Payload, out var pinErrorMessage,       out ErrorResponse))
                        return false;
                    Message = pinErrorMessage;
                    return true;

                case (SHIPMessageTypes.CONTROL, "accessMethodsRequest"):
                    Message = new SHIPAccessMethodsRequestMessage();
                    return true;

                case (SHIPMessageTypes.CONTROL, "accessMethods"):
                    if (!SHIPAccessMethodsMessage. TryParse(Frame.Payload, out var accessMethodsMessage,  out ErrorResponse))
                        return false;
                    Message = accessMethodsMessage;
                    return true;

                case (SHIPMessageTypes.DATA,    "data"):
                    if (!SHIPDataMessage.          TryParse(Frame.Payload, out var dataMessage,           out ErrorResponse))
                        return false;
                    Message = dataMessage;
                    return true;

                case (SHIPMessageTypes.END,     "connectionClose"):
                    if (!SHIPCloseMessage.         TryParse(Frame.Payload, out var closeMessage,          out ErrorResponse))
                        return false;
                    Message = closeMessage;
                    return true;

                default:
                    ErrorResponse = $"The message element '{messageName}' is not allowed within a SHIP {Frame.MessageType} message!";
                    return false;

            }

        }

        #endregion

        #region (static) TryParse(ByteArray, out Message, out ErrorResponse, MaxFrameLength = SHIPFrame.DefaultMaxFrameLength)

        /// <summary>
        /// Try to parse the given binary representation of a SHIP message.
        /// </summary>
        /// <param name="ByteArray">The payload of a binary WebSocket frame.</param>
        /// <param name="Message">The parsed SHIP message.</param>
        /// <param name="ErrorResponse">An optional error response.</param>
        /// <param name="MaxFrameLength">The maximum accepted frame length.</param>
        public static Boolean TryParse(ReadOnlyMemory<Byte>                 ByteArray,
                                       [NotNullWhen(true)]  out ASHIPMessage?  Message,
                                       [NotNullWhen(false)] out String?        ErrorResponse,
                                       UInt32                                 MaxFrameLength = SHIPFrame.DefaultMaxFrameLength)
        {

            Message = null;

            if (!SHIPFrame.TryParse(ByteArray, out var frame, out ErrorResponse, MaxFrameLength))
                return false;

            return TryParse(frame, out Message, out ErrorResponse);

        }

        #endregion

    }

}
