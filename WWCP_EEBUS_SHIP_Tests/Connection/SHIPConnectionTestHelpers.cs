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

namespace cloud.charging.open.protocols.EEBUS.SHIP.tests
{

    /// <summary>
    /// A SHIP transport which records everything that was sent and hands the
    /// frames over to a wire, so that the state machines can be driven and
    /// inspected without any networking.
    /// </summary>
    /// <param name="Wire">The wire the sent frames are handed over to.</param>
    public class RecordingTransport(SHIPWire? Wire = null) : ISHIPTransport
    {

        private readonly List<Byte[]> sentFrames = [];

        /// <summary>
        /// All frames sent through this transport.
        /// </summary>
        public IReadOnlyList<Byte[]>  SentFrames
            => sentFrames;

        /// <summary>
        /// Whether the transport was closed.
        /// </summary>
        public Boolean                IsClosed        { get; private set; }

        /// <summary>
        /// The reason the transport was closed with.
        /// </summary>
        public String?                CloseReason     { get; private set; }

        /// <summary>
        /// The connection the sent frames are delivered to.
        /// </summary>
        public SHIPConnection?        Peer            { get; set; }


        public Task SendAsync(Byte[] Frame, CancellationToken CancellationToken = default)
        {

            sentFrames.Add(Frame);

            // A real network never delivers a message while the sender is still
            // busy sending it: the frame is queued and delivered by the wire.
            if (Peer is not null)
                Wire?.Enqueue(Peer, Frame);

            return Task.CompletedTask;

        }

        public Task CloseAsync(String? Reason = null, CancellationToken CancellationToken = default)
        {

            IsClosed     = true;
            CloseReason  = Reason;

            return Task.CompletedTask;

        }


        /// <summary>
        /// Parse the sent frames as SHIP messages.
        /// </summary>
        public IEnumerable<ASHIPMessage> SentMessages()
        {
            foreach (var frame in sentFrames)
            {
                if (ASHIPMessage.TryParse(frame, out var message, out _))
                    yield return message;
            }
        }

        /// <summary>
        /// The last sent message of the given type.
        /// </summary>
        public T? LastSent<T>() where T : ASHIPMessage

            => SentMessages().OfType<T>().LastOrDefault();

    }


    /// <summary>
    /// The wire between two SHIP connections: it holds the frames in flight and
    /// delivers them one after another, so that no connection is called back
    /// while it is still processing.
    /// </summary>
    public class SHIPWire
    {

        private readonly Queue<(SHIPConnection Target, Byte[] Frame)> framesInFlight = new ();

        /// <summary>
        /// Hand a frame over to the wire.
        /// </summary>
        public void Enqueue(SHIPConnection Target, Byte[] Frame)
        {
            framesInFlight.Enqueue((Target, Frame));
        }

        /// <summary>
        /// Deliver all frames in flight, including those which are sent while
        /// delivering, until both communication partners fall silent.
        /// </summary>
        /// <param name="MaxFrames">An upper bound protecting against a message ping-pong.</param>
        public async Task DeliverAsync(UInt32 MaxFrames = 100)
        {

            var delivered  = 0;
            var trace      = new List<String>();

            while (framesInFlight.Count > 0)
            {

                var (target, frame) = framesInFlight.Dequeue();

                trace.Add(ASHIPMessage.TryParse(frame, out var message, out _)
                              ? $"-> {target.Role}: {message.GetType().Name}"
                              : $"-> {target.Role}: <unparsable>");

                if (++delivered > MaxFrames)
                    throw new InvalidOperationException(
                              $"More than {MaxFrames} frames were exchanged - the communication partners do not fall silent!" +
                              Environment.NewLine +
                              String.Join(Environment.NewLine, trace.TakeLast(12))
                          );

                await target.ReceiveAsync(frame);

            }

        }

    }


    /// <summary>
    /// A trust provider with fixed answers.
    /// </summary>
    /// <param name="Trusted">Whether the communication partner is already trusted.</param>
    /// <param name="WaitForTrust">Whether this node waits for a trust decision.</param>
    public class StaticTrustProvider(Boolean  Trusted        = true,
                                     Boolean  WaitForTrust   = true) : ISHIPTrustProvider
    {

        /// <summary>
        /// Whether the communication partner is already trusted.
        /// </summary>
        public Boolean Trusted       { get; set; } = Trusted;

        /// <summary>
        /// Whether this node waits for a trust decision.
        /// </summary>
        public Boolean WaitForTrust  { get; set; } = WaitForTrust;

        public Boolean IsTrusted           (SKI RemoteSKI) => Trusted;

        public Boolean AllowWaitingForTrust(SKI RemoteSKI) => WaitForTrust;

    }

}
