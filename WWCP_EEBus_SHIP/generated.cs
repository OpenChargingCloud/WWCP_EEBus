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

#endregion

namespace cloud.charging.open.protocols.EEBus.SHIP
{

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


}
