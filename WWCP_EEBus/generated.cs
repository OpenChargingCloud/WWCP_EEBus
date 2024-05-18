/*
 * Copyright (c) 2014-2023 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of WWCP ChargingStation <https://github.com/OpenChargingCloud/WWCP_ChargingStation>
 *
 * Licensed under the Affero GPL license, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/agpl.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Xml.Serialization;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Illias.Votes;
using org.GraphDefined.Vanaheimr.Styx.Arrows;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    [XmlRoot("connectionHello")]
    public class ConnectionHelloType
    {
        public ConnectionHelloPhaseType phase { get; set; }
        public uint? waiting { get; set; }
        public bool? prolongationRequest { get; set; }
    }

    public enum ConnectionHelloPhaseType
    {
        pending,
        ready,
        aborted
    }

    [XmlRoot("messageProtocolHandshake")]
    public class MessageProtocolHandshakeType
    {
        public ProtocolHandshakeTypeType handshakeType { get; set; }

        public Version version { get; set; }

        public MessageProtocolFormatsType formats { get; set; }
    }

    public class Version
    {
        public ushort major { get; set; }
        public ushort minor { get; set; }
    }

    public enum ProtocolHandshakeTypeType
    {
        announceMax,
        select
    }

    public class MessageProtocolFormatsType
    {
        [XmlElement("format")]
        public string[] format { get; set; }
    }

    [XmlRoot("messageProtocolHandshakeError")]
    public class MessageProtocolHandshakeErrorType
    {
        public byte error { get; set; }
    }

    public enum PinStateType
    {
        required,
        optional,
        pinOk,
        none
    }

    public enum PinInputPermissionType
    {
        busy,
        ok
    }

    [XmlRoot("connectionPinState")]
    public class ConnectionPinStateType
    {
        public PinStateType pinState { get; set; }
        public PinInputPermissionType? inputPermission { get; set; }
    }

    [XmlRoot("connectionPinInput")]
    public class ConnectionPinInputType
    {
        public string pin { get; set; }
    }

    [XmlRoot("connectionPinError")]
    public class ConnectionPinErrorType
    {
        public byte error { get; set; }
    }

    public class HeaderType
    {
        public string protocolId { get; set; }
    }

    public class ExtensionType
    {
        public string extensionId { get; set; }
        public byte[] binary { get; set; }
        public string @string { get; set; }
    }

    [XmlRoot("data")]
    public class DataType
    {
        public HeaderType header { get; set; }
        public object payload { get; set; }
        public ExtensionType extension { get; set; }
    }

    public enum ConnectionClosePhaseType
    {
        announce,
        confirm
    }

    public enum ConnectionCloseReasonType
    {
        unspecific,
        removedConnection
    }

    [XmlRoot("connectionClose")]
    public class ConnectionCloseType
    {
        public ConnectionClosePhaseType phase { get; set; }
        public uint? maxTime { get; set; }
        public ConnectionCloseReasonType? reason { get; set; }
    }

    [XmlRoot("accessMethodsRequest")]
    public class AccessMethodsRequestType { }

    [XmlRoot("accessMethods")]
    public class AccessMethodsType
    {
        public string id { get; set; }
        public DnsSd_mDns dnsSd_mDns { get; set; }
        public Dns dns { get; set; }
    }

    public class DnsSd_mDns
    {
    }

    public class Dns
    {
        public string uri { get; set; }
    }

}
