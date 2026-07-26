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

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// One command of an incoming datagram, with everything already looked up
    /// which is needed to act on it.
    /// </summary>
    /// <param name="RequestHeader">The header of the datagram it arrived in.</param>
    /// <param name="Cmd">The command.</param>
    /// <param name="CmdClassifier">What kind of message it is.</param>
    /// <param name="Function">The function it is about, where it names one.</param>
    /// <param name="Data">The payload, where it carries one.</param>
    /// <param name="PartialFilter">The filter marking a partial operation, where there is one.</param>
    /// <param name="DeleteFilter">The filter marking a deletion, where there is one.</param>
    /// <param name="RemoteFeature">The feature which sent it.</param>
    public sealed record SPINEMessage(HeaderType          RequestHeader,
                                      CmdType             Cmd,
                                      CmdClassifierType   CmdClassifier,
                                      String?             Function,
                                      Object?             Data,
                                      FilterType?         PartialFilter,
                                      FilterType?         DeleteFilter,
                                      SPINERemoteFeature  RemoteFeature)
    {

        /// <summary>
        /// The device which sent it.
        /// </summary>
        public SPINERemoteDevice  RemoteDevice
            => RemoteFeature.Device;

        /// <summary>
        /// Whether the sender wants to be told that the message arrived.
        /// </summary>
        public Boolean            AckRequested
            => RequestHeader.AckRequest == true;

        /// <summary>
        /// Whether the message carries any restricted function exchange at all.
        /// </summary>
        public Boolean            IsRestricted
            => PartialFilter is not null || DeleteFilter is not null;

    }


    /// <summary>
    /// What came back for a request.
    /// </summary>
    /// <param name="MsgCounterReference">The message counter of the request it answers.</param>
    /// <param name="Function">The function it is about, where it names one.</param>
    /// <param name="Data">The data of a reply, where it carries one.</param>
    /// <param name="Result">The result of an acknowledgement or a refusal, where it is one.</param>
    /// <param name="RemoteFeature">The feature which answered.</param>
    public sealed record SPINEResponse(UInt64              MsgCounterReference,
                                       String?             Function,
                                       Object?             Data,
                                       ResultDataType?     Result,
                                       SPINERemoteFeature  RemoteFeature)
    {

        /// <summary>
        /// Whether the request was refused.
        /// </summary>
        public Boolean IsError
            => Result?.IsError == true;

        /// <summary>
        /// Return a text representation of this response.
        /// </summary>
        public override String ToString()

            => IsError
                   ? $"{MsgCounterReference}: {Result}"
                   : $"{MsgCounterReference}: {Function ?? "ok"}";

    }


    /// <summary>
    /// The data of a function changed, because another device said so or asked
    /// for it.
    /// </summary>
    /// <param name="LocalFeature">Our feature which handled the message.</param>
    /// <param name="RemoteFeature">The feature which sent it.</param>
    /// <param name="CmdClassifier">Which kind of message changed it.</param>
    /// <param name="Function">The function whose data changed.</param>
    /// <param name="Data">The data the message carried.</param>
    public sealed record SPINEDataChange(SPINELocalFeature   LocalFeature,
                                         SPINERemoteFeature  RemoteFeature,
                                         CmdClassifierType   CmdClassifier,
                                         String              Function,
                                         Object?             Data)
    {

        /// <summary>
        /// Return a text representation of this change.
        /// </summary>
        public override String ToString()

            => $"{CmdClassifier} {Function} from {RemoteFeature.Address}";

    }

}
