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

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// Marks the property which says whether a remote peer may change this data.
    ///
    /// SPINE has three of those in the whole model - "isLimitChangeable" of a
    /// load control limit, "isValueChangeable" of a device configuration key
    /// value and "isSetpointChangeable" of a setpoint - and the XSD makes them
    /// look like any other boolean. They are the data owner's answer to "may
    /// somebody else write this?", so the update system has to ask them before
    /// it applies anything which arrived from another device.
    ///
    /// Where the property is absent or false, a write coming from a remote peer
    /// is refused. A local change is never checked: the owner of the data may
    /// always change it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EEBUSWriteCheckAttribute : Attribute
    { }

}
