/*
 * Copyright (c) 2014-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    public class SHIPHandshakeMessage
    {
        public MessageProtocolHandshakeType messageProtocolHandshake { get; set; } = new MessageProtocolHandshakeType();

    }

    //[XmlRoot("messageProtocolHandshake")]
    //public class MessageProtocolHandshakeType
    //{
    //    public ProtocolHandshakeTypeType handshakeType { get; set; }

    //    public Version version { get; set; }

    //    public MessageProtocolFormatsType formats { get; set; }
    //}

    //public class MessageProtocolFormatsType
    //{
    //    [XmlElement("format")]
    //    public string[] format { get; set; }
    //}


    public partial class MessageProtocolHandshakeType
    {

        public ProtocolHandshakeTypeTypes            HandshakeType    { get; set; }

        public MessageProtocolHandshakeTypeVersions  Version          { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("format", IsNullable = false)]
        public String[]                              Formats          { get; set; }


    }



    public enum ProtocolHandshakeTypeTypes
    {
        announceMax,
        select
    }


    public partial class MessageProtocolHandshakeTypeVersions
    {

        public UInt16  Major    { get; set; }

        public UInt16  Minor    { get; set; }

    }



}
