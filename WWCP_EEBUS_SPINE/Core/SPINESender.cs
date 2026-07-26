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

using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json.Linq;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// Everything this device sends to one communication partner.
    ///
    /// Every datagram carries a message counter which is unique within the
    /// connection, and an answer refers back to it. That makes this class the
    /// place where three things belong together: handing out the counter,
    /// remembering what was sent under it, and not sending the same question
    /// twice while the first one is still unanswered.
    ///
    /// The counter is injectable. A test bench compares datagrams against
    /// recorded ones, and a counter which starts wherever the process happens to
    /// have got to would make that impossible.
    /// </summary>
    public class SPINESender
    {

        #region Data

        private readonly Func<UInt64>                       nextMsgCounter;

        private readonly Lock                               requestLock       = new ();

        /// <summary>
        /// Requests which are still unanswered, by message counter.
        /// </summary>
        private readonly Dictionary<UInt64, String>         openRequests      = [];

        /// <summary>
        /// What was sent under which message counter, so that a result which
        /// arrives later can be shown next to the message it is about.
        /// </summary>
        private readonly Dictionary<UInt64, DatagramType>   sentDatagrams     = [];

        private readonly Queue<UInt64>                      sentOrder         = new ();

        /// <summary>
        /// How many sent datagrams to keep. The Go reference implementation
        /// keeps the last 100 notifies for the same purpose.
        /// </summary>
        private const    Int32                              keepSent          = 100;

        /// <summary>
        /// How many unanswered requests to keep before forgetting the oldest.
        /// </summary>
        private const    Int32                              keepOpen          = 20;

        #endregion

        #region Properties

        /// <summary>
        /// Where the datagrams go.
        /// </summary>
        public ISPINEWriter  Writer    { get; }

        /// <summary>
        /// How many datagrams were sent.
        /// </summary>
        public UInt64        Sent      { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// A datagram was handed to the transport.
        /// </summary>
        public event Action<SPINESender, DatagramType>? OnDatagramSent;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new sender for one communication partner.
        /// </summary>
        /// <param name="Writer">Where the datagrams go.</param>
        /// <param name="MsgCounter">Where the message counters come from. Counts from 1 by default.</param>
        public SPINESender(ISPINEWriter   Writer,
                           Func<UInt64>?  MsgCounter   = null)
        {

            this.Writer          = Writer;

            var counter          = 0UL;
            this.nextMsgCounter  = MsgCounter ?? (() => Interlocked.Increment(ref counter));

        }

        #endregion


        #region Send(Datagram, CancellationToken = default)

        /// <summary>
        /// Send a datagram as it stands.
        /// </summary>
        /// <param name="Datagram">A SPINE datagram.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task Send(DatagramType       Datagram,
                               CancellationToken  CancellationToken   = default)
        {

            if (Datagram.Header?.MsgCounter is UInt64 msgCounter)
                Remember(msgCounter, Datagram);

            Sent++;

            OnDatagramSent?.Invoke(this, Datagram);

            await Writer.SendSPINEDatagram(
                      new JObject(
                          new JProperty("datagram", SPINEJSON.ToJObject(Datagram))
                      ),
                      CancellationToken
                  );

        }

        #endregion

        #region DatagramFor(MsgCounter)

        /// <summary>
        /// The datagram which was sent under the given message counter, or null
        /// when it is no longer remembered.
        /// </summary>
        /// <param name="MsgCounter">A message counter of one of our datagrams.</param>
        public DatagramType? DatagramFor(UInt64 MsgCounter)
        {
            lock (requestLock)
            {
                return sentDatagrams.GetValueOrDefault(MsgCounter);
            }
        }

        #endregion


        #region Request(CmdClassifier, Source, Destination, AckRequest, Cmds, CancellationToken = default)

        /// <summary>
        /// Send a request, and answer with the message counter its answer will
        /// refer to.
        ///
        /// A request which is already on its way and has not been answered is
        /// not sent again: the message counter of the first one is returned
        /// instead. Devices do ask twice - a use case which starts twice, a
        /// subscription which is requested while the first request is in flight -
        /// and two identical questions on one connection only make two answers
        /// to sort out.
        /// </summary>
        /// <param name="CmdClassifier">What kind of request this is.</param>
        /// <param name="Source">The address of the feature sending it.</param>
        /// <param name="Destination">The address of the feature it goes to.</param>
        /// <param name="AckRequest">Whether the partner should acknowledge it.</param>
        /// <param name="Cmds">The commands.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<UInt64> Request(CmdClassifierType   CmdClassifier,
                                          FeatureAddressType  Source,
                                          FeatureAddressType  Destination,
                                          Boolean             AckRequest,
                                          List<CmdType>       Cmds,
                                          CancellationToken   CancellationToken   = default)
        {

            var (msgCounter, datagram) = PrepareRequest(CmdClassifier,
                                                        Source,
                                                        Destination,
                                                        AckRequest,
                                                        Cmds);

            if (datagram is not null)
                await Send(datagram, CancellationToken);

            return msgCounter;

        }

        #endregion

        #region PrepareRequest(CmdClassifier, Source, Destination, AckRequest, Cmds)

        /// <summary>
        /// The message counter a request will carry, and the datagram to send.
        ///
        /// Two steps rather than one, because the answer can be faster than the
        /// caller: over a loopback - and over a fast link with a fast partner -
        /// the reply is processed while the send is still on the stack. Whoever
        /// waits for an answer therefore has to be able to start waiting before
        /// the question leaves.
        /// </summary>
        /// <param name="CmdClassifier">What kind of request this is.</param>
        /// <param name="Source">The address of the feature sending it.</param>
        /// <param name="Destination">The address of the feature it goes to.</param>
        /// <param name="AckRequest">Whether the partner should acknowledge it.</param>
        /// <param name="Cmds">The commands.</param>
        /// <returns>The message counter, and the datagram - or null for it when the very same request is still unanswered.</returns>
        public (UInt64 MsgCounter, DatagramType? Datagram) PrepareRequest(CmdClassifierType   CmdClassifier,
                                                                          FeatureAddressType  Source,
                                                                          FeatureAddressType  Destination,
                                                                          Boolean             AckRequest,
                                                                          List<CmdType>       Cmds)
        {

            var hash = HashOf(CmdClassifier, Destination, Cmds);

            lock (requestLock)
            {

                foreach (var open in openRequests)
                    if (open.Value == hash)
                        return (open.Key, null);

                var msgCounter  = nextMsgCounter();

                var datagram    = new DatagramType {
                                      Header   = new HeaderType {
                                                     SpecificationVersion  = Version.String,
                                                     AddressSource         = Source,
                                                     AddressDestination    = Destination,
                                                     MsgCounter            = msgCounter,
                                                     CmdClassifier         = CmdClassifier,
                                                     AckRequest            = AckRequest ? true : null
                                                 },
                                      Payload  = new PayloadType {
                                                     Cmd = Cmds
                                                 }
                                  };

                openRequests.Add(msgCounter, hash);

                while (openRequests.Count > keepOpen)
                    openRequests.Remove(openRequests.Keys.Min());

                return (msgCounter, datagram);

            }

        }

        #endregion

        #region ResponseReceived(MsgCounterReference)

        /// <summary>
        /// An answer to the given message counter arrived, so the question is no
        /// longer open and may be asked again.
        /// </summary>
        /// <param name="MsgCounterReference">The message counter the answer refers to.</param>
        public void ResponseReceived(UInt64? MsgCounterReference)
        {

            if (MsgCounterReference is null)
                return;

            lock (requestLock)
            {
                openRequests.Remove(MsgCounterReference.Value);
            }

        }

        #endregion


        #region Read  (Source, Destination, Cmd, CancellationToken = default)

        /// <summary>
        /// Ask a remote feature for the data of a function.
        /// </summary>
        public Task<UInt64> Read(FeatureAddressType  Source,
                                 FeatureAddressType  Destination,
                                 CmdType             Cmd,
                                 CancellationToken   CancellationToken   = default)

            => Request(CmdClassifierType.Read,
                       Source,
                       Destination,
                       false,
                       [ Cmd ],
                       CancellationToken);

        #endregion

        #region Write (Source, Destination, Cmd, CancellationToken = default)

        /// <summary>
        /// Ask a remote feature to change the data of a function.
        ///
        /// Always with an acknowledgement request: a write which is refused -
        /// for a missing binding, for data which may not be changed - is
        /// something the sender has to hear about.
        /// </summary>
        public Task<UInt64> Write(FeatureAddressType  Source,
                                  FeatureAddressType  Destination,
                                  CmdType             Cmd,
                                  CancellationToken   CancellationToken   = default)

            => Request(CmdClassifierType.Write,
                       Source,
                       Destination,
                       true,
                       [ Cmd ],
                       CancellationToken);

        #endregion

        #region Call  (Source, Destination, Cmd, CancellationToken = default)

        /// <summary>
        /// Send a call, which is what node management uses for subscriptions and
        /// bindings.
        /// </summary>
        public Task<UInt64> Call(FeatureAddressType  Source,
                                 FeatureAddressType  Destination,
                                 CmdType             Cmd,
                                 CancellationToken   CancellationToken   = default)

            => Request(CmdClassifierType.Call,
                       Source,
                       Destination,
                       true,
                       [ Cmd ],
                       CancellationToken);

        #endregion

        #region Notify(Source, Destination, Cmd, CancellationToken = default)

        /// <summary>
        /// Tell a remote feature what changed.
        ///
        /// A notify is not a request and is therefore never deduplicated: two
        /// changes which happen to look alike are two changes.
        /// </summary>
        public async Task<UInt64> Notify(FeatureAddressType  Source,
                                         FeatureAddressType  Destination,
                                         CmdType             Cmd,
                                         CancellationToken   CancellationToken   = default)
        {

            var msgCounter  = nextMsgCounter();

            await Send(
                      new DatagramType {
                          Header   = new HeaderType {
                                         SpecificationVersion  = Version.String,
                                         AddressSource         = Source,
                                         AddressDestination    = Destination,
                                         MsgCounter            = msgCounter,
                                         CmdClassifier         = CmdClassifierType.Notify
                                     },
                          Payload  = new PayloadType { Cmd = [ Cmd ] }
                      },
                      CancellationToken
                  );

            return msgCounter;

        }

        #endregion

        #region Reply (RequestHeader, Source, Cmd, CancellationToken = default)

        /// <summary>
        /// Answer a read.
        /// </summary>
        /// <param name="RequestHeader">The header of the message being answered.</param>
        /// <param name="Source">The address of the feature answering.</param>
        /// <param name="Cmd">The answer.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task Reply(HeaderType          RequestHeader,
                          FeatureAddressType  Source,
                          CmdType             Cmd,
                          CancellationToken   CancellationToken   = default)

            => Send(
                   Answer(RequestHeader, Source, CmdClassifierType.Reply, Cmd),
                   CancellationToken
               );

        #endregion

        #region Result(RequestHeader, Source, Error = null, CancellationToken = default)

        /// <summary>
        /// Acknowledge a message, or refuse it.
        /// </summary>
        /// <param name="RequestHeader">The header of the message being answered.</param>
        /// <param name="Source">The address of the feature answering.</param>
        /// <param name="Error">The reason for refusing it, or null to acknowledge it.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task Result(HeaderType          RequestHeader,
                           FeatureAddressType  Source,
                           ResultDataType?     Error               = null,
                           CancellationToken   CancellationToken   = default)

            => Send(
                   Answer(RequestHeader,
                          Source,
                          CmdClassifierType.Result,
                          new CmdType {
                              ResultData = Error ?? ResultDataType.Success()
                          }),
                   CancellationToken
               );

        #endregion


        #region (private) Answer(RequestHeader, Source, CmdClassifier, Cmd)

        /// <summary>
        /// The datagram of an answer.
        ///
        /// The address it comes from is the address it was sent to, with our own
        /// device name filled in: a request may address us by entity and feature
        /// alone, and the answer has to say who we are.
        /// </summary>
        private DatagramType Answer(HeaderType          RequestHeader,
                                    FeatureAddressType  Source,
                                    CmdClassifierType   CmdClassifier,
                                    CmdType             Cmd)
        {

            var source = RequestHeader.AddressDestination?.Clone() ?? Source.Clone();

            source.Device = Source.Device;

            return new DatagramType {
                       Header   = new HeaderType {
                                      SpecificationVersion  = Version.String,
                                      AddressSource         = source,
                                      AddressDestination    = RequestHeader.AddressSource,
                                      MsgCounter            = nextMsgCounter(),
                                      MsgCounterReference   = RequestHeader.MsgCounter,
                                      CmdClassifier         = CmdClassifier
                                  },
                       Payload  = new PayloadType { Cmd = [ Cmd ] }
                   };

        }

        #endregion

        #region (private) Remember(MsgCounter, Datagram)

        private void Remember(UInt64 MsgCounter, DatagramType Datagram)
        {
            lock (requestLock)
            {

                if (sentDatagrams.TryAdd(MsgCounter, Datagram))
                    sentOrder.Enqueue(MsgCounter);

                while (sentOrder.Count > keepSent)
                    sentDatagrams.Remove(sentOrder.Dequeue());

            }
        }

        #endregion

        #region (private static) HashOf(CmdClassifier, Destination, Cmds)

        /// <summary>
        /// What makes two requests the same request: the same kind of question,
        /// to the same feature, about the same thing.
        /// </summary>
        private static String HashOf(CmdClassifierType   CmdClassifier,
                                     FeatureAddressType  Destination,
                                     List<CmdType>       Cmds)

            => Convert.ToHexStringLower(
                   SHA256.HashData(
                       Encoding.UTF8.GetBytes(
                           $"{CmdClassifier}-{SPINEAddresses.KeyOf(Destination)}-{SPINEJSON.ToJSON(Cmds)}"
                       )
                   )
               );

        #endregion

    }

}
