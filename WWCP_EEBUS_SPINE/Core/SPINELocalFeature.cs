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

using System.Collections.Concurrent;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// A feature of this device.
    ///
    /// It owns the data of its functions, answers what other devices ask of it,
    /// and asks other devices in turn. Which of those is allowed is decided
    /// before a message ever reaches this class - by the possible operations of
    /// the function and, for a write, by a binding.
    /// </summary>
    public class SPINELocalFeature : SPINEFeature
    {

        #region Data

        /// <summary>
        /// Requests of ours which are still waiting for an answer.
        /// </summary>
        private readonly ConcurrentDictionary<UInt64, TaskCompletionSource<SPINEResponse>> pending = new ();

        #endregion

        #region Properties

        /// <summary>
        /// The entity this feature belongs to.
        /// </summary>
        public SPINELocalEntity  Entity    { get; }

        /// <summary>
        /// The device this feature belongs to.
        /// </summary>
        public SPINELocalDevice  Device
            => Entity.Device;

        /// <summary>
        /// Asked before a write from another device is applied, where something
        /// above this layer wants a say.
        ///
        /// SPINE decides whether a device **may** write - the possible
        /// operations and the binding do that, before the message gets here.
        /// This is the other question: whether this particular write can be
        /// carried out. A use case answers it - an active power consumption
        /// limit below zero is refused by the "Limitation of Power Consumption"
        /// use case, not by SPINE.
        ///
        /// Answer null to let the write through, or the result to send back
        /// instead. It is asked before anything is changed, so a refusal leaves
        /// the data untouched.
        /// </summary>
        public Func<SPINEMessage, CancellationToken, Task<ResultDataType?>>? WriteApproval { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a feature of this device.
        /// </summary>
        /// <param name="Id">The number of this feature within its entity.</param>
        /// <param name="Entity">The entity it belongs to.</param>
        /// <param name="FeatureType">Which kind of feature it is.</param>
        /// <param name="Role">Whether it offers its data or asks for it.</param>
        public SPINELocalFeature(UInt32            Id,
                                 SPINELocalEntity  Entity,
                                 FeatureTypeType   FeatureType,
                                 RoleType          Role)

            : base(Id,
                   Entity.Address,
                   FeatureType,
                   Role)

        {
            this.Entity = Entity;
        }

        #endregion


        #region AddFunction(Function, Read = true, Write = false, PartialRead = false, PartialWrite = false)

        /// <summary>
        /// Offer a function on this feature.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Read">Whether it may be read.</param>
        /// <param name="Write">Whether it may be written.</param>
        /// <param name="PartialRead">Whether a part of it may be read.</param>
        /// <param name="PartialWrite">Whether a part of it may be written.</param>
        public SPINEFunctionData AddFunction(String   Function,
                                             Boolean  Read           = true,
                                             Boolean  Write          = false,
                                             Boolean  PartialRead    = false,
                                             Boolean  PartialWrite   = false)
        {

            var functionData = new SPINEFunctionData(
                                   Function,
                                   PossibleOperationsType.ReadAndMaybeWrite(Write, PartialRead, PartialWrite)
                               );

            functions[Function] = functionData;

            return functionData;

        }

        #endregion

        #region SetData(Function, Data, NotifySubscribers = true)

        /// <summary>
        /// Change the data of one of our functions, and tell whoever subscribed
        /// to it.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Data">The new data.</param>
        /// <param name="NotifySubscribers">Whether to notify the subscribers of this feature.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task SetData(String             Function,
                                  Object?            Data,
                                  Boolean            NotifySubscribers   = true,
                                  CancellationToken  CancellationToken   = default)
        {

            var functionData = FunctionData(Function)
                                   ?? throw new ArgumentException($"The feature {Address} does not have the function '{Function}'.",
                                                                  nameof(Function));

            functionData.SetData(Data);

            if (NotifySubscribers)
                await Device.NotifySubscribers(this,
                                               functionData.ToCmd(),
                                               CancellationToken);

        }

        #endregion


        #region Read (Function, RemoteFeature, Selectors = null, Elements = null, CancellationToken = default)

        /// <summary>
        /// Ask a remote feature for the data of a function, and wait for what it
        /// answers.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="RemoteFeature">The feature to ask.</param>
        /// <param name="Selectors">Which entries of a list are wanted, for a partial read.</param>
        /// <param name="Elements">Which elements are wanted, for a partial read.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> Read(String              Function,
                                              SPINERemoteFeature  RemoteFeature,
                                              Object?             Selectors           = null,
                                              Object?             Elements            = null,
                                              CancellationToken   CancellationToken   = default)
        {

            var info = SPINEFunctions.Get(Function)
                           ?? throw new ArgumentException($"'{Function}' is not a function of SPINE {Version.String}.",
                                                          nameof(Function));

            var cmd = new CmdType();

            // A read carries the function as an empty payload; which parts are
            // wanted is said by the filter (SPINE 1.3.0, 5.3.4.4).
            cmd.SetData(Function, Activator.CreateInstance(info.DataType));

            if (Selectors is not null || Elements is not null)
            {

                var filter = new FilterType { CmdControl = CmdControlType.ForPartial };

                filter.SetSelectors(Function, Selectors);
                filter.SetElements (Function, Elements);

                cmd.Function  = FunctionType.Parse(Function);
                cmd.Filter    = [ filter ];

            }

            return await Ask(CmdClassifierType.Read,
                             false,
                             cmd,
                             RemoteFeature,
                             CancellationToken);

        }

        #endregion

        #region Write(Function, Data, RemoteFeature, Partial = false, CancellationToken = default)

        /// <summary>
        /// Ask a remote feature to change the data of a function, and wait for
        /// its answer.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Data">The data to write.</param>
        /// <param name="RemoteFeature">The feature to ask.</param>
        /// <param name="Partial">Whether only the stated parts are meant.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> Write(String              Function,
                                               Object              Data,
                                               SPINERemoteFeature  RemoteFeature,
                                               Boolean             Partial             = false,
                                               CancellationToken   CancellationToken   = default)
        {

            var cmd = new CmdType();

            if (!cmd.SetData(Function, Data))
                throw new ArgumentException($"'{Function}' does not carry a '{Data.GetType().Name}'.",
                                            nameof(Data));

            if (Partial)
            {
                cmd.Function  = FunctionType.Parse(Function);
                cmd.Filter    = [ new FilterType { CmdControl = CmdControlType.ForPartial } ];
            }

            return await Ask(CmdClassifierType.Write,
                             true,
                             cmd,
                             RemoteFeature,
                             CancellationToken);

        }

        #endregion

        #region Notify(Function, RemoteFeature, Partial = false, CancellationToken = default)

        /// <summary>
        /// Tell a remote feature what the data of one of our functions is now.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="RemoteFeature">The feature to tell.</param>
        /// <param name="Partial">Whether only the stated parts changed.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<UInt64> Notify(String              Function,
                                         SPINERemoteFeature  RemoteFeature,
                                         Boolean             Partial             = false,
                                         CancellationToken   CancellationToken   = default)
        {

            var functionData = FunctionData(Function)
                                   ?? throw new ArgumentException($"The feature {Address} does not have the function '{Function}'.",
                                                                  nameof(Function));

            return await RemoteFeature.Device.Sender.Notify(Address,
                                                            RemoteFeature.Address,
                                                            functionData.ToCmd(Partial),
                                                            CancellationToken);

        }

        #endregion


        #region (internal) HandleMessage(Message, CancellationToken)

        /// <summary>
        /// Act on one incoming command.
        /// </summary>
        /// <returns>Null when it was handled, the reason otherwise.</returns>
        protected internal virtual async Task<ResultDataType?> HandleMessage(SPINEMessage       Message,
                                                                             CancellationToken  CancellationToken)
        {

            if (Message.CmdClassifier == CmdClassifierType.Result)
                return HandleResult(Message);

            if (Message.Function is null)
                return ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported,
                                            "The command carries no function.");

            if (Message.CmdClassifier == CmdClassifierType.Read)
                return await HandleRead  (Message, CancellationToken);

            if (Message.CmdClassifier == CmdClassifierType.Reply)
                return HandleReply (Message);

            if (Message.CmdClassifier == CmdClassifierType.Notify)
                return HandleNotify(Message);

            if (Message.CmdClassifier == CmdClassifierType.Write)
                return await HandleWrite (Message, CancellationToken);

            return ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported,
                                        $"A command classifier '{Message.CmdClassifier}' is not handled.");

        }

        #endregion

        #region (private) HandleResult(Message)

        /// <summary>
        /// An acknowledgement or a refusal of something we sent.
        /// </summary>
        private ResultDataType? HandleResult(SPINEMessage Message)
        {

            if (Message.RequestHeader.MsgCounterReference is not UInt64 reference)
                // A result which refers to nothing cannot be matched to
                // anything; there is no point in answering it either, as that
                // would be a result about a result.
                return null;

            Message.RemoteDevice.Sender.ResponseReceived(reference);

            Answered(new SPINEResponse(reference,
                                       null,
                                       null,
                                       Message.Cmd.ResultData,
                                       Message.RemoteFeature));

            return null;

        }

        #endregion

        #region (private) HandleRead(Message, CancellationToken)

        /// <summary>
        /// Somebody wants to know what one of our functions holds.
        /// </summary>
        private async Task<ResultDataType?> HandleRead(SPINEMessage       Message,
                                                       CancellationToken  CancellationToken)
        {

            // SPINE 1.3.0, 2.1.3: a client asks, a server answers. A read of a
            // client feature has nobody to answer it.
            if (Role == RoleType.Client)
                return ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                            "This feature is a client and holds no data of its own.");

            var functionData = FunctionData(Message.Function!);

            if (functionData is null)
                return ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported,
                                            $"This feature does not have the function '{Message.Function}'.");

            if (!functionData.Operations.CanRead)
                return ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported,
                                            $"The function '{Message.Function}' may not be read.");

            // A function which holds nothing is answered with an empty instance
            // of it, not with nothing at all: the reply has to name the function
            // it answers - the XML of the specification writes it as
            // "<setpointListData/>" - or the client cannot tell what it is an
            // answer to.
            var data = functionData.DataCopy()
                           ?? Activator.CreateInstance(functionData.DataType);

            // A partial read is answered with the part which was asked for -
            // but only where this feature announced that it can do that. SPINE
            // 1.3.0, 5.3.4.5 explicitly allows a server to ignore a restriction
            // it does not support and answer with more than was asked for, and
            // announcing something we then do not do would be the worse of the
            // two.
            var answerPartially = Message.PartialFilter is not null &&
                                  functionData.Operations.CanReadPartial;

            if (answerPartially)
                data = SPINERead.Apply(data, Message.Cmd);

            var cmd = new CmdType();

            cmd.SetData(Message.Function!, data);

            if (answerPartially)
            {
                cmd.Function  = FunctionType.Parse(Message.Function!);
                cmd.Filter    = [ new FilterType { CmdControl = CmdControlType.ForPartial } ];
            }

            await Message.RemoteDevice.Sender.Reply(Message.RequestHeader,
                                                    Address,
                                                    cmd,
                                                    CancellationToken);

            return null;

        }

        #endregion

        #region (private) HandleReply(Message) / HandleNotify(Message)

        /// <summary>
        /// The answer to a read of ours.
        /// </summary>
        private ResultDataType? HandleReply(SPINEMessage Message)
        {

            var result = Message.RemoteFeature.UpdateData(Message.Function!,
                                                          Message.Data,
                                                          Message.Cmd);

            if (!result.Success)
                return ResultDataType.Error(SPINEErrorNumbers.GeneralError, result.Problem);

            Device.Published(new SPINEDataChange(this,
                                                 Message.RemoteFeature,
                                                 CmdClassifierType.Reply,
                                                 Message.Function!,
                                                 Message.Data));

            if (Message.RequestHeader.MsgCounterReference is UInt64 reference)
            {

                Message.RemoteDevice.Sender.ResponseReceived(reference);

                Answered(new SPINEResponse(reference,
                                           Message.Function,
                                           Message.RemoteFeature.DataCopy(Message.Function!),
                                           null,
                                           Message.RemoteFeature));

            }

            return null;

        }


        /// <summary>
        /// A remote feature says what changed.
        /// </summary>
        private ResultDataType? HandleNotify(SPINEMessage Message)
        {

            var result = Message.RemoteFeature.UpdateData(Message.Function!,
                                                          Message.Data,
                                                          Message.Cmd);

            if (!result.Success)
                return ResultDataType.Error(SPINEErrorNumbers.GeneralError, result.Problem);

            Device.Published(new SPINEDataChange(this,
                                                 Message.RemoteFeature,
                                                 CmdClassifierType.Notify,
                                                 Message.Function!,
                                                 Message.Data));

            return null;

        }

        #endregion

        #region (private) HandleWrite(Message, CancellationToken)

        /// <summary>
        /// A remote feature asks us to change our data.
        ///
        /// That it is allowed to ask at all was decided before this - the
        /// function has to offer a write, and there has to be a binding.
        /// Whether the data itself may be changed is decided here, by the data.
        /// </summary>
        private async Task<ResultDataType?> HandleWrite(SPINEMessage       Message,
                                                        CancellationToken  CancellationToken)
        {

            var functionData = FunctionData(Message.Function!);

            if (functionData is null)
                return ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported,
                                            $"This feature does not have the function '{Message.Function}'.");

            // Whoever is above this layer gets to refuse it before anything is
            // changed.
            if (WriteApproval is not null &&
                await WriteApproval(Message, CancellationToken) is ResultDataType refusal)
                return refusal;

            var result = functionData.UpdateData(Message.Data,
                                                 Message.Cmd,
                                                 SPINEUpdateOptions.Write);

            if (!result.Success)
                return ResultDataType.Error(SPINEErrorNumbers.CommandRejected, result.Problem);

            await Device.NotifySubscribers(this,
                                           functionData.ToCmd(),
                                           CancellationToken);

            Device.Published(new SPINEDataChange(this,
                                                 Message.RemoteFeature,
                                                 CmdClassifierType.Write,
                                                 Message.Function!,
                                                 Message.Data));

            return null;

        }

        #endregion


        #region (protected) Ask(...) / Answered(Response)

        /// <summary>
        /// Send a request and wait for what comes back.
        ///
        /// The waiting is set up before the datagram leaves. Over a loopback the
        /// answer is processed while the send is still on the stack, and a
        /// partner on a fast link is not much slower - so registering
        /// afterwards would be a race which is lost every time in the one case
        /// and now and then in the other.
        /// </summary>
        protected async Task<SPINEResponse> Ask(CmdClassifierType   CmdClassifier,
                                                Boolean             AckRequest,
                                                CmdType             Cmd,
                                                SPINERemoteFeature  RemoteFeature,
                                                CancellationToken   CancellationToken)
        {

            var sender                  = RemoteFeature.Device.Sender;

            var (msgCounter, datagram)  = sender.PrepareRequest(CmdClassifier,
                                                                Address,
                                                                RemoteFeature.Address,
                                                                AckRequest,
                                                                [ Cmd ]);

            var waiting                 = pending.GetOrAdd(msgCounter,
                                                           _ => new TaskCompletionSource<SPINEResponse>(
                                                                    TaskCreationOptions.RunContinuationsAsynchronously));

            // How long to wait: what the feature announced it may take, or the
            // patience of this device. Waiting forever is not an option - a
            // partner which never answers would otherwise stop this one, and a
            // test bench has to be able to report "no answer" as a result.
            var timeout                 = RemoteFeature.MaxResponseDelay ?? Device.ResponseTimeout;

            try
            {

                // Null when the very same request is still unanswered: then this
                // caller waits for the answer to the first one.
                if (datagram is not null)
                    await sender.Send(datagram, CancellationToken);

                return await waiting.Task.WaitAsync(timeout,
                                                    Device.TimeProvider,
                                                    CancellationToken);

            }
            catch (TimeoutException)
            {

                return new SPINEResponse(msgCounter,
                                         null,
                                         null,
                                         ResultDataType.Error(SPINEErrorNumbers.Timeout,
                                                              $"{RemoteFeature.Address} did not answer within {timeout}."),
                                         RemoteFeature);

            }
            finally
            {
                pending.TryRemove(msgCounter, out _);
            }

        }


        /// <summary>
        /// An answer arrived for one of our requests.
        /// </summary>
        protected void Answered(SPINEResponse Response)
        {

            if (pending.TryGetValue(Response.MsgCounterReference, out var waiting))
                waiting.TrySetResult(Response);

        }


        /// <summary>
        /// An answer arrived which could not be handled.
        ///
        /// Whoever is waiting for it has to be told anyway. A message we cannot
        /// make sense of is a reason to report an error, never a reason to leave
        /// a caller waiting for something which has already arrived.
        /// </summary>
        internal void Failed(UInt64          MsgCounterReference,
                             ResultDataType  Error,
                             SPINEMessage    Message)
        {

            Answered(new SPINEResponse(MsgCounterReference,
                                       Message.Function,
                                       null,
                                       Error,
                                       Message.RemoteFeature));

        }

        #endregion

        #region Information()

        /// <summary>
        /// This feature as the detailed discovery states it.
        /// </summary>
        public NodeManagementDetailedDiscoveryFeatureInformationType Information()

            => new () {
                   Description = new NetworkManagementFeatureDescriptionDataType {
                                     FeatureAddress      = Address.Clone(),
                                     FeatureType         = FeatureType,
                                     Role                = Role,
                                     Description         = Description,
                                     SupportedFunction   = [.. functions.Values.
                                                                 OrderBy(function => function.Function, StringComparer.Ordinal).
                                                                 Select (function => function.ToProperty())]
                                 }
               };

        #endregion

    }

}
