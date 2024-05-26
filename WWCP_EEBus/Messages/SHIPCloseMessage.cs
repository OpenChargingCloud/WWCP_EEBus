﻿/*
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

namespace cloud.charging.open.protocols.EEBus.SHIP
{

    public class SHIPCloseMessage
    {
        public ConnectionCloseType connectionClose { get; set; } = new ConnectionCloseType();
    }

    public partial class ConnectionCloseType
    {

        public ConnectionClosePhaseTypes    Phase      { get; set; }

        public UInt32?                      MaxTime    { get; set; }

        public ConnectionCloseReasonTypes?  Reason     { get; set; }


    }



    public enum ConnectionClosePhaseTypes
    {
        announce,
        confirm
    }


    public enum ConnectionCloseReasonTypes
    {
        unspecific,
        removedConnection
    }


}