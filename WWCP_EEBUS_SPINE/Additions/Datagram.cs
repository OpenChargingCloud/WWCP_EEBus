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
    /// A SPINE datagram (SPINE 1.3.0, Datagram): a header saying who talks to
    /// whom about what, and a payload of commands.
    /// </summary>
    public partial class DatagramType
    {

        #region Properties

        /// <summary>
        /// The commands of this datagram, never null.
        /// </summary>
        public IEnumerable<CmdType> Commands
            => Payload?.Cmd ?? [];

        /// <summary>
        /// The single command of this datagram, or null when it carries none or
        /// more than one. SPINE 1.3.0 allows several, but everything the use
        /// cases do sends exactly one, and a datagram with several is a case for
        /// the conformance tests rather than for the ordinary path.
        /// </summary>
        public CmdType? Command
        {
            get
            {

                var commands = Payload?.Cmd;

                return commands is not null && commands.Count == 1
                           ? commands[0]
                           : null;

            }
        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// One line describing this datagram: who, what, which message counter,
        /// which function - the line one wants in a test log when a handshake
        /// does not do what it should.
        ///
        /// The shape follows the overview of the Go reference implementation, so
        /// that a log of theirs and a log of ours can be read next to each other.
        /// </summary>
        public override String ToString()
        {

            var builder = new StringBuilder();

            builder.Append(Header?.CmdClassifier?.ToString() ?? "?");

            if (Header?.MsgCounter is not null)
                builder.Append(' ').
                        Append(Header.MsgCounter.Value.ToString(CultureInfo.InvariantCulture));

            if (Header?.MsgCounterReference is not null)
                builder.Append(" ref ").
                        Append(Header.MsgCounterReference.Value.ToString(CultureInfo.InvariantCulture));

            builder.Append(": ").
                    Append(Header?.AddressSource?.     ToString() ?? "?").
                    Append(" -> ").
                    Append(Header?.AddressDestination?.ToString() ?? "?");

            var command = Command;

            if (command is not null)
            {

                builder.Append(' ').
                        Append(command.ToString());

                // A result is only interesting together with its error number.
                if (command.ResultData is not null)
                    builder.Append(" (").
                            Append(command.ResultData.ToString()).
                            Append(')');

            }

            else if (Payload?.Cmd is not null && Payload.Cmd.Count > 1)
                builder.Append(' ').
                        Append(Payload.Cmd.Count.ToString(CultureInfo.InvariantCulture)).
                        Append(" commands");

            if (Header?.AckRequest == true)
                builder.Append(" [ack]");

            return builder.ToString();

        }

        #endregion

    }

}
