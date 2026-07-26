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

namespace cloud.charging.open.protocols.EEBUS.SHIP
{

    /// <summary>
    /// The states of the SHIP Message Exchange (SME) state machines
    /// (SHIP TS 1.0.1, chapter 13.4).
    ///
    /// The numeric values are assigned explicitly and match those of the ship-go
    /// reference implementation, so that log data of both stacks can be compared
    /// directly while debugging an interoperability problem.
    /// </summary>
    public enum SHIPMessageExchangeStates : Byte
    {

        #region Connection Mode Initialisation (CMI), chapter 13.4.3

        /// <summary>
        /// The starting point of the connection mode initialisation.
        /// </summary>
        CmiStateInitStart              =  0,

        /// <summary>
        /// The client sends its CMI message.
        /// </summary>
        CmiStateClientSend             =  1,

        /// <summary>
        /// The client waits for the CMI message of the server.
        /// </summary>
        CmiStateClientWait             =  2,

        /// <summary>
        /// The client evaluates the CMI message of the server.
        /// </summary>
        CmiStateClientEvaluate         =  3,

        /// <summary>
        /// The server waits for the CMI message of the client.
        /// </summary>
        CmiStateServerWait             =  4,

        /// <summary>
        /// The server evaluates the CMI message of the client.
        /// </summary>
        CmiStateServerEvaluate         =  5,

        #endregion

        #region Connection Data Preparation ("Hello"), chapter 13.4.4.1

        /// <summary>
        /// The entry state of the "Hello" phase.
        /// </summary>
        SmeHelloState                  =  6,

        /// <summary>
        /// The communication partner is trusted: announce "ready".
        /// </summary>
        SmeHelloStateReadyInit         =  7,

        /// <summary>
        /// Waiting for the "ready" of the communication partner.
        /// </summary>
        SmeHelloStateReadyListen       =  8,

        /// <summary>
        /// The communication partner did not become ready in time.
        /// </summary>
        SmeHelloStateReadyTimeout      =  9,

        /// <summary>
        /// The communication partner is not trusted yet: announce "pending".
        /// </summary>
        SmeHelloStatePendingInit       = 10,

        /// <summary>
        /// Waiting for the trust decision, prolonging the timer as needed.
        /// </summary>
        SmeHelloStatePendingListen     = 11,

        /// <summary>
        /// The trust decision did not arrive in time.
        /// </summary>
        SmeHelloStatePendingTimeout    = 12,

        /// <summary>
        /// Both communication partners are ready.
        /// </summary>
        SmeHelloStateOk                = 13,

        /// <summary>
        /// Sending an abort to the communication partner.
        /// </summary>
        SmeHelloStateAbort             = 14,

        /// <summary>
        /// Sending the abort to the communication partner is done.
        /// </summary>
        SmeHelloStateAbortDone         = 15,

        /// <summary>
        /// An abort was received from the communication partner.
        /// </summary>
        SmeHelloStateRemoteAbortDone   = 16,

        /// <summary>
        /// The connection was closed after the communication partner was pending:
        /// "4452: Node rejected by application".
        /// </summary>
        SmeHelloStateRejected          = 17,

        #endregion

        #region Protocol Handshake, chapter 13.4.4.2

        /// <summary>
        /// The server enters the protocol handshake.
        /// </summary>
        SmeProtHStateServerInit           = 18,

        /// <summary>
        /// The client enters the protocol handshake and announces its maximum version.
        /// </summary>
        SmeProtHStateClientInit           = 19,

        /// <summary>
        /// The server waits for the proposal of the client.
        /// </summary>
        SmeProtHStateServerListenProposal = 20,

        /// <summary>
        /// The server waits for the confirmation of its selection.
        /// </summary>
        SmeProtHStateServerListenConfirm  = 21,

        /// <summary>
        /// The client waits for the selection of the server.
        /// </summary>
        SmeProtHStateClientListenChoice   = 22,

        /// <summary>
        /// The protocol handshake timed out.
        /// </summary>
        SmeProtHStateTimeout              = 23,

        /// <summary>
        /// The client agreed on the protocol.
        /// </summary>
        SmeProtHStateClientOk             = 24,

        /// <summary>
        /// The server agreed on the protocol.
        /// </summary>
        SmeProtHStateServerOk             = 25,

        #endregion

        #region PIN verification, chapter 13.4.5

        /// <summary>
        /// The PIN check is entered.
        /// </summary>
        SmePinStateCheckInit      = 26,

        /// <summary>
        /// Waiting for the PIN state of the communication partner.
        /// </summary>
        SmePinStateCheckListen    = 27,

        /// <summary>
        /// The PIN check failed.
        /// </summary>
        SmePinStateCheckError     = 28,

        /// <summary>
        /// The communication partner is busy.
        /// </summary>
        SmePinStateCheckBusyInit  = 29,

        /// <summary>
        /// Waiting while the communication partner is busy.
        /// </summary>
        SmePinStateCheckBusyWait  = 30,

        /// <summary>
        /// No PIN is required by either side.
        /// </summary>
        SmePinStateCheckOk        = 31,

        /// <summary>
        /// A PIN has to be asked for.
        /// </summary>
        SmePinStateAskInit        = 32,

        /// <summary>
        /// The PIN input is being processed.
        /// </summary>
        SmePinStateAskProcess     = 33,

        /// <summary>
        /// The PIN input is restricted.
        /// </summary>
        SmePinStateAskRestricted  = 34,

        /// <summary>
        /// The PIN was accepted.
        /// </summary>
        SmePinStateAskOk          = 35,

        #endregion

        #region Access Methods Identification, chapter 13.4.6

        /// <summary>
        /// The access methods of the communication partner are requested.
        /// </summary>
        SmeAccessMethodsRequest = 36,

        #endregion

        #region Result

        /// <summary>
        /// The handshake was approved on both ends.
        /// </summary>
        SmeStateApproved = 37,

        /// <summary>
        /// The handshake completed successfully; SPINE data may now be exchanged.
        /// </summary>
        SmeStateComplete = 38,

        /// <summary>
        /// The handshake ended with an error.
        /// </summary>
        SmeStateError    = 39

        #endregion

    }

}
