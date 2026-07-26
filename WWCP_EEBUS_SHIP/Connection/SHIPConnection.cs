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

using Newtonsoft.Json.Linq;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    #region (enum) SHIPHandshakeTimers

    /// <summary>
    /// Which timer of the SHIP message exchange is currently running
    /// (SHIP TS 1.0.1, chapter 13.4.4.1.3).
    /// </summary>
    public enum SHIPHandshakeTimers
    {

        /// <summary>
        /// No timer is running.
        /// </summary>
        None,

        /// <summary>
        /// The communication partner has to announce its readiness - or request a
        /// prolongation - before this timer expires.
        /// </summary>
        WaitForReady,

        /// <summary>
        /// Local timer to request a prolongation at the communication partner in
        /// time, i.e. before its Wait-For-Ready timer expires.
        /// </summary>
        SendProlongationRequest,

        /// <summary>
        /// Detection of a response timeout on a prolongation request.
        /// </summary>
        ProlongationRequestReply

    }

    #endregion


    /// <summary>
    /// A SHIP connection and its message exchange state machines
    /// (SHIP TS 1.0.1, chapter 13.4).
    ///
    /// The connection drives the whole handshake - connection mode initialisation,
    /// connection data preparation ("Hello"), protocol handshake, PIN verification
    /// and access methods - and afterwards carries SPINE data.
    ///
    /// Everything time dependent runs on the given TimeProvider, so that the
    /// protocol timing can be tested without waiting.
    /// </summary>
    public class SHIPConnection
    {

        #region Data

        private readonly ISHIPTransport      transport;
        private readonly ISHIPTrustProvider  trustProvider;
        private readonly TimeProvider        timeProvider;
        private readonly SemaphoreSlim       processingLock         = new (1, 1);

        private ITimer?                      timer;
        private SHIPHandshakeTimers          runningTimer           = SHIPHandshakeTimers.None;
        private TimeSpan                     lastReceivedWaiting    = TimeSpan.Zero;
        private Boolean                      remoteAnnouncedReady;
        private Boolean                      accessMethodsAnswered;
        private Boolean                      accessMethodsReceived;

        #endregion

        #region Properties

        /// <summary>
        /// The role of the local SHIP node within this connection.
        /// </summary>
        public SHIPRoles                  Role                 { get; }

        /// <summary>
        /// The SKI of the communication partner, as authenticated during the TLS handshake.
        /// </summary>
        public SKI                        RemoteSKI            { get; }

        /// <summary>
        /// The SHIP identifier of the local SHIP node, announced within the access methods.
        /// </summary>
        public SHIP_Id                    LocalSHIPId          { get; }

        /// <summary>
        /// The SHIP identifier of the communication partner, learned from its access methods.
        /// </summary>
        public SHIP_Id?                   RemoteSHIPId         { get; private set; }

        /// <summary>
        /// The current state of the message exchange.
        /// </summary>
        public SHIPMessageExchangeStates  State                { get; private set; }
            = SHIPMessageExchangeStates.CmiStateInitStart;

        /// <summary>
        /// The error which ended the handshake, if any.
        /// </summary>
        public String?                    Error                { get; private set; }

        /// <summary>
        /// The timeouts of this connection.
        /// </summary>
        public SHIPTimeouts               Timeouts             { get; }

        /// <summary>
        /// Whether the handshake completed and SPINE data may be exchanged.
        /// </summary>
        public Boolean                    IsCompleted
            => State == SHIPMessageExchangeStates.SmeStateComplete;

        #endregion

        #region Events

        /// <summary>
        /// The state of the message exchange changed.
        /// </summary>
        public event Action<SHIPConnection, SHIPMessageExchangeStates>?  OnStateChanged;

        /// <summary>
        /// The handshake completed; SPINE data may now be exchanged.
        /// </summary>
        public event Action<SHIPConnection>?                             OnCompleted;

        /// <summary>
        /// SPINE data was received.
        /// </summary>
        public event Action<SHIPConnection, JObject>?                    OnSPINEDataReceived;

        /// <summary>
        /// The connection was closed, optionally with an error.
        /// </summary>
        public event Action<SHIPConnection, String?>?                    OnClosed;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SHIP connection.
        /// </summary>
        /// <param name="Role">The role of the local SHIP node within this connection.</param>
        /// <param name="RemoteSKI">The SKI of the communication partner.</param>
        /// <param name="LocalSHIPId">The SHIP identifier of the local SHIP node.</param>
        /// <param name="Transport">The underlying transport.</param>
        /// <param name="TrustProvider">Whether and how long to wait for a trust decision.</param>
        /// <param name="Timeouts">The timeouts of the message exchange.</param>
        /// <param name="TimeProvider">The time provider driving all protocol timers.</param>
        public SHIPConnection(SHIPRoles           Role,
                              SKI                 RemoteSKI,
                              SHIP_Id             LocalSHIPId,
                              ISHIPTransport      Transport,
                              ISHIPTrustProvider  TrustProvider,
                              SHIPTimeouts?       Timeouts       = null,
                              TimeProvider?       TimeProvider   = null)
        {

            this.Role           = Role;
            this.RemoteSKI      = RemoteSKI;
            this.LocalSHIPId    = LocalSHIPId;
            this.transport      = Transport;
            this.trustProvider  = TrustProvider;
            this.Timeouts       = Timeouts     ?? SHIPTimeouts.Default;
            this.timeProvider   = TimeProvider ?? System.TimeProvider.System;

        }

        #endregion


        #region StartAsync  (CancellationToken = default)

        /// <summary>
        /// Start the connection mode initialisation (SHIP TS 1.0.1, chapter 13.4.3).
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task StartAsync(CancellationToken CancellationToken = default)
        {

            await processingLock.WaitAsync(CancellationToken);

            try
            {

                switch (Role)
                {

                    case SHIPRoles.Client:
                        SetState(SHIPMessageExchangeStates.CmiStateClientSend);
                        await SendAsync(new SHIPInitMessage(), CancellationToken);
                        SetState(SHIPMessageExchangeStates.CmiStateClientWait);
                        break;

                    case SHIPRoles.Server:
                        SetState(SHIPMessageExchangeStates.CmiStateServerWait);
                        break;

                }

                StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.CMI);

            }
            finally
            {
                processingLock.Release();
            }

        }

        #endregion

        #region ReceiveAsync(Frame, CancellationToken = default)

        /// <summary>
        /// Process an incoming SHIP frame.
        /// </summary>
        /// <param name="Frame">The payload of a received binary WebSocket frame.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task ReceiveAsync(ReadOnlyMemory<Byte>  Frame,
                                       CancellationToken     CancellationToken   = default)
        {

            await processingLock.WaitAsync(CancellationToken);

            try
            {

                if (State is SHIPMessageExchangeStates.SmeStateError)
                    return;

                if (!ASHIPMessage.TryParse(Frame, out var message, out var errorResponse))
                {
                    await FailAsync(errorResponse, CancellationToken);
                    return;
                }

                // A communication partner may close the connection at any time
                // (SHIP TS 1.0.1, chapter 13.4.7).
                if (message is SHIPCloseMessage closeMessage)
                {
                    await ProcessCloseAsync(closeMessage, CancellationToken);
                    return;
                }

                switch (State)
                {

                    #region Connection Mode Initialisation, chapter 13.4.3

                    case SHIPMessageExchangeStates.CmiStateClientWait:
                    case SHIPMessageExchangeStates.CmiStateServerWait:
                        {

                            SetState(Role == SHIPRoles.Client
                                         ? SHIPMessageExchangeStates.CmiStateClientEvaluate
                                         : SHIPMessageExchangeStates.CmiStateServerEvaluate);

                            if (message is not SHIPInitMessage)
                            {
                                await FailAsync($"Expected a SHIP init message, but received '{message.MessageType}'!", CancellationToken);
                                return;
                            }

                            StopTimer();

                            // The server answers the initialisation of the client.
                            if (Role == SHIPRoles.Server)
                                await SendAsync(new SHIPInitMessage(), CancellationToken);

                            await StartHelloAsync(CancellationToken);

                        }
                        return;

                    #endregion

                    #region Connection Data Preparation ("Hello"), chapter 13.4.4.1

                    case SHIPMessageExchangeStates.SmeHelloStateReadyListen:
                    case SHIPMessageExchangeStates.SmeHelloStatePendingListen:
                        {

                            if (message is not SHIPHelloMessage helloMessage)
                            {
                                await AbortHelloAsync($"Expected a connection hello, but received a different message!", CancellationToken);
                                return;
                            }

                            await ProcessHelloAsync(helloMessage.ConnectionHello, CancellationToken);

                        }
                        return;

                    #endregion

                    #region Protocol Handshake, chapter 13.4.4.2

                    case SHIPMessageExchangeStates.SmeProtHStateServerListenProposal:
                    case SHIPMessageExchangeStates.SmeProtHStateServerListenConfirm:
                    case SHIPMessageExchangeStates.SmeProtHStateClientListenChoice:
                        {
                            await ProcessProtocolHandshakeAsync(message, CancellationToken);
                        }
                        return;

                    #endregion

                    #region PIN verification, chapter 13.4.5

                    case SHIPMessageExchangeStates.SmePinStateCheckListen:
                        {

                            if (message is not SHIPPinStateMessage pinStateMessage)
                            {
                                await FailAsync("Expected a connection PIN state!", CancellationToken);
                                return;
                            }

                            await ProcessPinStateAsync(pinStateMessage.ConnectionPinState, CancellationToken);

                        }
                        return;

                    #endregion

                    #region Access Methods, chapter 13.4.6

                    case SHIPMessageExchangeStates.SmeAccessMethodsRequest:
                        {
                            await ProcessAccessMethodsAsync(message, CancellationToken);
                        }
                        return;

                    #endregion

                    #region Data exchange, chapter 13.4.7

                    case SHIPMessageExchangeStates.SmeStateComplete:
                        {

                            if (message is not SHIPDataMessage dataMessage)
                            {
                                await FailAsync($"Expected SHIP data, but received a different message!", CancellationToken);
                                return;
                            }

                            if (dataMessage.Data.Header.ProtocolId != Version.ProtocolId)
                            {
                                await FailAsync($"Unknown protocol identifier '{dataMessage.Data.Header.ProtocolId}'!", CancellationToken);
                                return;
                            }

                            if (dataMessage.Data.Payload is JObject spineDatagram)
                                OnSPINEDataReceived?.Invoke(this, spineDatagram);

                        }
                        return;

                    #endregion

                    default:
                        await FailAsync($"Received a message while being in state '{State}'!", CancellationToken);
                        return;

                }

            }
            finally
            {
                processingLock.Release();
            }

        }

        #endregion

        #region TimeoutAsync(CancellationToken = default)

        /// <summary>
        /// The currently running timer expired.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task TimeoutAsync(CancellationToken CancellationToken = default)
        {

            await processingLock.WaitAsync(CancellationToken);

            try
            {

                var expiredTimer = runningTimer;
                runningTimer     = SHIPHandshakeTimers.None;

                switch (State)
                {

                    case SHIPMessageExchangeStates.CmiStateClientWait:
                    case SHIPMessageExchangeStates.CmiStateServerWait:
                        await FailAsync("The communication partner did not answer the connection mode initialisation in time!", CancellationToken);
                        return;

                    case SHIPMessageExchangeStates.SmeHelloStateReadyListen:
                        SetState(SHIPMessageExchangeStates.SmeHelloStateReadyTimeout);
                        await AbortHelloAsync("The communication partner did not become ready in time!", CancellationToken);
                        return;

                    case SHIPMessageExchangeStates.SmeHelloStatePendingListen:
                        await HelloPendingTimeoutAsync(expiredTimer, CancellationToken);
                        return;

                    case SHIPMessageExchangeStates.SmeProtHStateServerListenProposal:
                    case SHIPMessageExchangeStates.SmeProtHStateServerListenConfirm:
                    case SHIPMessageExchangeStates.SmeProtHStateClientListenChoice:
                        SetState(SHIPMessageExchangeStates.SmeProtHStateTimeout);
                        await AbortProtocolHandshakeAsync(MessageProtocolHandshakeErrors.Timeout, CancellationToken);
                        return;

                    case SHIPMessageExchangeStates.SmeAccessMethodsRequest:
                        await FailAsync("The communication partner did not answer the access methods request in time!", CancellationToken);
                        return;

                    default:
                        return;

                }

            }
            finally
            {
                processingLock.Release();
            }

        }

        #endregion


        #region ApproveTrustAsync(CancellationToken = default)

        /// <summary>
        /// The user accepted the communication partner while this connection was
        /// waiting for a trust decision: announce "ready" and continue the handshake
        /// (SHIP TS 1.0.1, chapter 13.4.4.1).
        ///
        /// A trust decision is an event of the application - the state machine
        /// cannot poll for it, it has to be told.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task ApproveTrustAsync(CancellationToken CancellationToken = default)
        {

            await processingLock.WaitAsync(CancellationToken);

            try
            {

                if (State != SHIPMessageExchangeStates.SmeHelloStatePendingListen &&
                    State != SHIPMessageExchangeStates.SmeHelloStatePendingInit)
                {
                    return;
                }

                await SendHelloAsync(ConnectionHelloPhase.Ready, Timeouts.HelloInit, false, CancellationToken);

                // The communication partner announced its readiness while we were
                // still waiting, so the connection data preparation is done.
                if (remoteAnnouncedReady)
                {

                    StopTimer();
                    SetState(SHIPMessageExchangeStates.SmeHelloStateOk);

                    await StartProtocolHandshakeAsync(CancellationToken);

                }

                else
                {
                    SetState(SHIPMessageExchangeStates.SmeHelloStateReadyListen);
                    StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.HelloInit);
                }

            }
            finally
            {
                processingLock.Release();
            }

        }

        #endregion

        #region SendSPINEDataAsync(Datagram, CancellationToken = default)

        /// <summary>
        /// Send the given SPINE datagram to the communication partner.
        /// </summary>
        /// <param name="Datagram">A SPINE datagram.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task SendSPINEDataAsync(JObject            Datagram,
                                             CancellationToken  CancellationToken   = default)
        {

            if (!IsCompleted)
                throw new InvalidOperationException($"SPINE data can only be sent after the SHIP handshake completed, but the connection is in state '{State}'!");

            await SendAsync(
                      new SHIPDataMessage(
                          new DataType(
                              new HeaderType(Version.ProtocolId),
                              Datagram
                          )
                      ),
                      CancellationToken
                  );

        }

        #endregion

        #region CloseAsync(Reason = null, CancellationToken = default)

        /// <summary>
        /// Announce the closing of this connection to the communication partner
        /// (SHIP TS 1.0.1, chapter 13.4.7).
        /// </summary>
        /// <param name="Reason">An optional reason.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task CloseAsync(ConnectionCloseReasons?  Reason              = null,
                                     CancellationToken        CancellationToken   = default)
        {

            await processingLock.WaitAsync(CancellationToken);

            try
            {

                StopTimer();

                await SendAsync(
                          new SHIPCloseMessage(
                              new ConnectionClose(
                                  ConnectionClosePhases.Announce,
                                  (UInt32) Timeouts.CloseConfirm.TotalMilliseconds,
                                  Reason
                              )
                          ),
                          CancellationToken
                      );

                await transport.CloseAsync(Reason?.AsText(), CancellationToken);

                OnClosed?.Invoke(this, null);

            }
            finally
            {
                processingLock.Release();
            }

        }

        #endregion


        #region (private) Hello phase, chapter 13.4.4.1

        private async Task StartHelloAsync(CancellationToken CancellationToken)
        {

            SetState(SHIPMessageExchangeStates.SmeHelloState);

            if (trustProvider.IsTrusted(RemoteSKI))
            {

                SetState(SHIPMessageExchangeStates.SmeHelloStateReadyInit);

                await SendHelloAsync(ConnectionHelloPhase.Ready, Timeouts.HelloInit, false, CancellationToken);

                SetState(SHIPMessageExchangeStates.SmeHelloStateReadyListen);
                StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.HelloInit);

            }

            else
            {

                SetState(SHIPMessageExchangeStates.SmeHelloStatePendingInit);

                await SendHelloAsync(ConnectionHelloPhase.Pending, Timeouts.HelloInit, false, CancellationToken);

                SetState(SHIPMessageExchangeStates.SmeHelloStatePendingListen);
                StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.HelloInit);

                // Nobody is going to accept this communication partner, so there is
                // no point in letting it wait.
                if (!trustProvider.AllowWaitingForTrust(RemoteSKI))
                    await AbortHelloAsync("The communication partner is not trusted!", CancellationToken);

            }

        }

        private async Task ProcessHelloAsync(ConnectionHello    Hello,
                                             CancellationToken  CancellationToken)
        {

            if (!Hello.Phase.IsDefined)
            {
                await AbortHelloAsync($"Unknown connection hello phase '{Hello.Phase}'!", CancellationToken);
                return;
            }

            #region "aborted": the communication partner gave up

            if (Hello.Phase == ConnectionHelloPhase.Aborted)
            {
                StopTimer();
                SetState(SHIPMessageExchangeStates.SmeHelloStateRemoteAbortDone);
                await CloseTransportAsync("The communication partner aborted the connection!", CancellationToken);
                return;
            }

            #endregion

            #region We announced "ready" and are waiting for the communication partner

            if (State == SHIPMessageExchangeStates.SmeHelloStateReadyListen)
            {

                // Both sides are ready.
                if (Hello.Phase == ConnectionHelloPhase.Ready)
                {

                    StopTimer();
                    SetState(SHIPMessageExchangeStates.SmeHelloStateOk);

                    await StartProtocolHandshakeAsync(CancellationToken);
                    return;

                }

                // The communication partner asks us to keep waiting.
                if (Hello.ProlongationRequest == true)
                {

                    if (trustProvider.AllowWaitingForTrust(RemoteSKI))
                        StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.HelloInit);

                    await SendHelloAsync(ConnectionHelloPhase.Ready, Timeouts.HelloInit, false, CancellationToken);
                    return;

                }

                // The communication partner is still waiting for a trust decision
                // of its user; there is nothing for us to do but to keep waiting.
                return;

            }

            #endregion

            #region We announced "pending" and are waiting for a trust decision

            // The communication partner is ready and announces how long it waits,
            // so that we can ask for a prolongation in time.
            if (Hello.Phase == ConnectionHelloPhase.Ready)
            {

                if (!Hello.Waiting.HasValue)
                {
                    await AbortHelloAsync("A connection hello of phase 'ready' has to announce its waiting time!", CancellationToken);
                    return;
                }

                remoteAnnouncedReady  = true;
                lastReceivedWaiting    = TimeSpan.FromMilliseconds(Hello.Waiting.Value);

                if (!TryScheduleProlongation(Hello.Waiting.Value, out var readyErrorResponse))
                    await AbortHelloAsync(readyErrorResponse, CancellationToken);

                return;

            }

            // The communication partner is waiting as well and announces its waiting time.
            if (Hello.Waiting.HasValue && !Hello.ProlongationRequest.HasValue)
            {

                lastReceivedWaiting = TimeSpan.FromMilliseconds(Hello.Waiting.Value);

                if (!TryScheduleProlongation(Hello.Waiting.Value, out var pendingErrorResponse))
                    await AbortHelloAsync(pendingErrorResponse, CancellationToken);

                return;

            }

            // The communication partner asks us to keep waiting.
            if (Hello.ProlongationRequest == true)
            {
                await SendHelloAsync(ConnectionHelloPhase.Pending, Timeouts.HelloInit, false, CancellationToken);
                return;
            }

            #endregion

            await AbortHelloAsync("Received an invalid connection hello!", CancellationToken);

        }

        private Boolean TryScheduleProlongation(UInt32 WaitingMilliseconds, out String ErrorResponse)
        {

            ErrorResponse = "";

            var waiting = TimeSpan.FromMilliseconds(WaitingMilliseconds);

            // A waiting time below the minimum is a protocol violation: it would
            // leave no room for the prolongation mechanism (chapter 13.4.4.1.3).
            if (waiting < Timeouts.HelloProlongMinimum)
            {
                ErrorResponse = $"The announced waiting time of {waiting.TotalMilliseconds} ms is below the minimum of {Timeouts.HelloProlongMinimum.TotalMilliseconds} ms!";
                return false;
            }

            StopTimer();

            // Ask for a prolongation before the timer of the communication partner expires.
            var prolongIn = waiting >= Timeouts.HelloProlongThresholdIncrement
                                ? waiting - Timeouts.HelloProlongThresholdIncrement
                                : Timeouts.HelloProlongThresholdIncrement;

            StartTimer(SHIPHandshakeTimers.SendProlongationRequest, prolongIn);

            return true;

        }

        private async Task HelloPendingTimeoutAsync(SHIPHandshakeTimers  ExpiredTimer,
                                                    CancellationToken    CancellationToken)
        {

            // Nobody is going to accept this communication partner any more.
            if (!trustProvider.AllowWaitingForTrust(RemoteSKI))
            {
                SetState(SHIPMessageExchangeStates.SmeHelloStatePendingTimeout);
                await AbortHelloAsync("No trust decision was made in time!", CancellationToken);
                return;
            }

            if (ExpiredTimer != SHIPHandshakeTimers.SendProlongationRequest)
            {
                SetState(SHIPMessageExchangeStates.SmeHelloStatePendingTimeout);
                await AbortHelloAsync("No trust decision was made in time!", CancellationToken);
                return;
            }

            await SendHelloAsync(ConnectionHelloPhase.Pending, TimeSpan.Zero, true, CancellationToken);

            StartTimer(
                SHIPHandshakeTimers.ProlongationRequestReply,
                lastReceivedWaiting > TimeSpan.Zero
                    ? lastReceivedWaiting
                    : Timeouts.HelloInit
            );

        }

        private Task SendHelloAsync(ConnectionHelloPhase  Phase,
                                    TimeSpan              Waiting,
                                    Boolean               ProlongationRequest,
                                    CancellationToken     CancellationToken)

            => SendAsync(
                   new SHIPHelloMessage(
                       new ConnectionHello(
                           Phase,
                           Waiting > TimeSpan.Zero ? (UInt32) Waiting.TotalMilliseconds : null,
                           ProlongationRequest     ? true                               : null
                       )
                   ),
                   CancellationToken
               );

        private async Task AbortHelloAsync(String             Reason,
                                           CancellationToken  CancellationToken)
        {

            StopTimer();
            SetState(SHIPMessageExchangeStates.SmeHelloStateAbort);

            await SendHelloAsync(ConnectionHelloPhase.Aborted, TimeSpan.Zero, false, CancellationToken);

            SetState(SHIPMessageExchangeStates.SmeHelloStateAbortDone);

            await CloseTransportAsync(Reason, CancellationToken);

        }

        #endregion

        #region (private) Protocol handshake, chapter 13.4.4.2

        private async Task StartProtocolHandshakeAsync(CancellationToken CancellationToken)
        {

            switch (Role)
            {

                case SHIPRoles.Server:
                    SetState(SHIPMessageExchangeStates.SmeProtHStateServerInit);
                    StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.CMI);
                    SetState(SHIPMessageExchangeStates.SmeProtHStateServerListenProposal);
                    break;

                case SHIPRoles.Client:
                    SetState(SHIPMessageExchangeStates.SmeProtHStateClientInit);
                    await SendProtocolHandshakeAsync(ProtocolHandshakeTypeTypes.announceMax, CancellationToken);
                    StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.CMI);
                    SetState(SHIPMessageExchangeStates.SmeProtHStateClientListenChoice);
                    break;

            }

        }

        private async Task ProcessProtocolHandshakeAsync(ASHIPMessage       Message,
                                                         CancellationToken  CancellationToken)
        {

            // The communication partner rejected our proposal.
            if (Message is SHIPHandshakeErrorMessage errorMessage)
            {
                await FailAsync($"The communication partner aborted the protocol handshake: {(MessageProtocolHandshakeErrors) errorMessage.MessageProtocolHandshakeError.Error}", CancellationToken);
                return;
            }

            if (Message is not SHIPHandshakeMessage handshakeMessage)
            {
                await AbortProtocolHandshakeAsync(MessageProtocolHandshakeErrors.UnexpectedMessage, CancellationToken);
                return;
            }

            var handshake = handshakeMessage.MessageProtocolHandshake;

            switch (State)
            {

                #region The server receives the proposal of the client and selects

                case SHIPMessageExchangeStates.SmeProtHStateServerListenProposal:
                    {

                        if (handshake.HandshakeType != ProtocolHandshakeTypeTypes.announceMax)
                        {
                            await AbortProtocolHandshakeAsync(MessageProtocolHandshakeErrors.UnexpectedMessage, CancellationToken);
                            return;
                        }

                        if (!IsSupported(handshake))
                        {
                            await AbortProtocolHandshakeAsync(MessageProtocolHandshakeErrors.SelectionMismatch, CancellationToken);
                            return;
                        }

                        StopTimer();

                        await SendProtocolHandshakeAsync(ProtocolHandshakeTypeTypes.select, CancellationToken);

                        StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.CMI);
                        SetState(SHIPMessageExchangeStates.SmeProtHStateServerListenConfirm);

                    }
                    return;

                #endregion

                #region The server receives the confirmation of its selection

                case SHIPMessageExchangeStates.SmeProtHStateServerListenConfirm:
                    {

                        if (handshake.HandshakeType != ProtocolHandshakeTypeTypes.select)
                        {
                            await AbortProtocolHandshakeAsync(MessageProtocolHandshakeErrors.SelectionMismatch, CancellationToken);
                            return;
                        }

                        StopTimer();
                        SetState(SHIPMessageExchangeStates.SmeProtHStateServerOk);

                        await StartPinVerificationAsync(CancellationToken);

                    }
                    return;

                #endregion

                #region The client receives the selection of the server and confirms

                case SHIPMessageExchangeStates.SmeProtHStateClientListenChoice:
                    {

                        if (handshake.HandshakeType != ProtocolHandshakeTypeTypes.select)
                        {
                            await AbortProtocolHandshakeAsync(MessageProtocolHandshakeErrors.UnexpectedMessage, CancellationToken);
                            return;
                        }

                        if (!IsSupported(handshake))
                        {
                            await AbortProtocolHandshakeAsync(MessageProtocolHandshakeErrors.SelectionMismatch, CancellationToken);
                            return;
                        }

                        StopTimer();

                        // Confirm the selection of the server by echoing it.
                        await SendProtocolHandshakeAsync(ProtocolHandshakeTypeTypes.select, CancellationToken);

                        SetState(SHIPMessageExchangeStates.SmeProtHStateClientOk);

                        await StartPinVerificationAsync(CancellationToken);

                    }
                    return;

                #endregion

            }

        }

        /// <summary>
        /// Whether the announced or selected protocol can be spoken by this implementation.
        /// </summary>
        private static Boolean IsSupported(MessageProtocolHandshake Handshake)

            => Handshake.Version.Major == Version.Major &&
               Handshake.Version.Minor == Version.Minor &&
               Handshake.Formats.Contains(MessageProtocolFormat.JSON_UTF8);

        private Task SendProtocolHandshakeAsync(ProtocolHandshakeTypeTypes  HandshakeType,
                                                CancellationToken           CancellationToken)

            => SendAsync(
                   new SHIPHandshakeMessage(
                       new MessageProtocolHandshake(
                           HandshakeType,
                           new MessageProtocolHandshakeVersion(Version.Major, Version.Minor),
                           // JSON-UTF16 is optional and not implemented here.
                           [ MessageProtocolFormat.JSON_UTF8 ]
                       )
                   ),
                   CancellationToken
               );

        private async Task AbortProtocolHandshakeAsync(MessageProtocolHandshakeErrors  Error,
                                                       CancellationToken               CancellationToken)
        {

            StopTimer();

            await SendAsync(
                      new SHIPHandshakeErrorMessage(
                          new MessageProtocolHandshakeError((Byte) Error)
                      ),
                      CancellationToken
                  );

            await FailAsync($"The protocol handshake failed: {Error}", CancellationToken);

        }

        #endregion

        #region (private) PIN verification, chapter 13.4.5

        private async Task StartPinVerificationAsync(CancellationToken CancellationToken)
        {

            SetState(SHIPMessageExchangeStates.SmePinStateCheckInit);

            // This implementation does not support PIN based authentication,
            // which is what all known implementations do.
            await SendAsync(
                      new SHIPPinStateMessage(
                          new ConnectionPinState(PinState.None)
                      ),
                      CancellationToken
                  );

            SetState(SHIPMessageExchangeStates.SmePinStateCheckListen);
            StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.CMI);

        }

        private async Task ProcessPinStateAsync(ConnectionPinState  PinState,
                                                CancellationToken   CancellationToken)
        {

            StopTimer();

            if (PinState.PinState != SHIP.PinState.None)
            {
                SetState(SHIPMessageExchangeStates.SmePinStateCheckError);
                await FailAsync($"The PIN state '{PinState.PinState}' is not supported!", CancellationToken);
                return;
            }

            SetState(SHIPMessageExchangeStates.SmePinStateCheckOk);

            await StartAccessMethodsAsync(CancellationToken);

        }

        #endregion

        #region (private) Access methods, chapter 13.4.6

        private async Task StartAccessMethodsAsync(CancellationToken CancellationToken)
        {

            accessMethodsAnswered  = false;
            accessMethodsReceived  = false;

            await SendAsync(new SHIPAccessMethodsRequestMessage(), CancellationToken);

            SetState(SHIPMessageExchangeStates.SmeAccessMethodsRequest);
            StartTimer(SHIPHandshakeTimers.WaitForReady, Timeouts.CMI);

        }

        private async Task ProcessAccessMethodsAsync(ASHIPMessage       Message,
                                                     CancellationToken  CancellationToken)
        {

            switch (Message)
            {

                case SHIPAccessMethodsRequestMessage:
                    await SendAsync(
                              new SHIPAccessMethodsMessage(
                                  new AccessMethodsType(LocalSHIPId, null, null)
                              ),
                              CancellationToken
                          );
                    accessMethodsAnswered = true;
                    break;

                case SHIPAccessMethodsMessage accessMethodsMessage:
                    RemoteSHIPId           = accessMethodsMessage.AccessMethods.Id;
                    accessMethodsReceived  = true;
                    break;

                default:
                    await FailAsync("Expected an access methods request or response!", CancellationToken);
                    return;

            }

            if (accessMethodsAnswered && accessMethodsReceived)
            {

                StopTimer();

                SetState(SHIPMessageExchangeStates.SmeStateApproved);
                SetState(SHIPMessageExchangeStates.SmeStateComplete);

                OnCompleted?.Invoke(this);

            }

        }

        #endregion

        #region (private) Connection close, chapter 13.4.7

        private async Task ProcessCloseAsync(SHIPCloseMessage   CloseMessage,
                                             CancellationToken  CancellationToken)
        {

            StopTimer();

            if (CloseMessage.ConnectionClose.Phase == ConnectionClosePhases.Announce)
                await SendAsync(
                          new SHIPCloseMessage(
                              new ConnectionClose(ConnectionClosePhases.Confirm)
                          ),
                          CancellationToken
                      );

            await CloseTransportAsync(null, CancellationToken);

        }

        #endregion


        #region (private) Helpers

        private async Task SendAsync(ASHIPMessage       Message,
                                     CancellationToken  CancellationToken)
        {
            if (!transport.IsClosed)
                await transport.SendAsync(Message.ToByteArray(), CancellationToken);
        }

        private async Task FailAsync(String             ErrorResponse,
                                     CancellationToken  CancellationToken)
        {

            Error = ErrorResponse;

            StopTimer();
            SetState(SHIPMessageExchangeStates.SmeStateError);

            await CloseTransportAsync(ErrorResponse, CancellationToken);

        }

        private async Task CloseTransportAsync(String?            Reason,
                                               CancellationToken  CancellationToken)
        {

            if (!transport.IsClosed)
                await transport.CloseAsync(Reason, CancellationToken);

            OnClosed?.Invoke(this, Reason);

        }

        private void SetState(SHIPMessageExchangeStates NewState)
        {

            State = NewState;

            OnStateChanged?.Invoke(this, NewState);

        }

        private void StartTimer(SHIPHandshakeTimers  Timer,
                                TimeSpan             Timeout)
        {

            StopTimer();

            runningTimer = Timer;

            timer = timeProvider.CreateTimer(
                        _ => _ = TimeoutAsync(),
                        null,
                        Timeout,
                        System.Threading.Timeout.InfiniteTimeSpan
                    );

        }

        private void StopTimer()
        {

            timer?.Dispose();
            timer         = null;
            runningTimer  = SHIPHandshakeTimers.None;

        }

        #endregion

    }

}
