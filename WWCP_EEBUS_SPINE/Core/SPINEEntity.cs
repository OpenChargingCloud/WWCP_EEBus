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
    /// An entity of a device (SPINE 1.3.0, 2.1.2).
    ///
    /// An entity is one addressable thing within a device: a charging station
    /// has one entity per connector, a heat pump one per circuit. Entities nest,
    /// which is why their address is a path of numbers rather than a number.
    ///
    /// Entity 0 exists on every device and carries node management. Its features
    /// are counted from 0; the features of every other entity are counted from 1
    /// (SPINE 1.3.0, 7.1).
    /// </summary>
    public abstract class SPINEEntity
    {

        #region Data

        private UInt32 nextFeatureId;

        #endregion

        #region Properties

        /// <summary>
        /// The address of this entity.
        /// </summary>
        public EntityAddressType  Address       { get; }

        /// <summary>
        /// Which kind of entity this is.
        /// </summary>
        public EntityTypeType     EntityType    { get; }

        /// <summary>
        /// A text describing this entity, for humans.
        /// </summary>
        public String?            Description   { get; set; }

        /// <summary>
        /// The path of numbers naming this entity below its device.
        /// </summary>
        public IReadOnlyList<UInt32>  EntityId
            => Address.Entity ?? [];

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new entity.
        /// </summary>
        /// <param name="Device">The address of the device it belongs to.</param>
        /// <param name="EntityId">The path of numbers naming it below that device.</param>
        /// <param name="EntityType">Which kind of entity it is.</param>
        protected SPINEEntity(String?              Device,
                              IEnumerable<UInt32>  EntityId,
                              EntityTypeType       EntityType)
        {

            this.Address        = new EntityAddressType {
                                      Device  = Device,
                                      Entity  = [.. EntityId]
                                  };

            this.EntityType     = EntityType;

            // The features of the node management entity start at 0, all others
            // at 1 (SPINE 1.3.0, 7.1).
            this.nextFeatureId  = this.Address.Entity is [ var first ] &&
                                  first == SPINEAddresses.NodeManagementEntity
                                      ? 0u
                                      : 1u;

        }

        #endregion


        #region (protected) NextFeatureId()

        /// <summary>
        /// The number the next feature of this entity gets.
        /// </summary>
        protected UInt32 NextFeatureId()

            => Interlocked.Increment(ref nextFeatureId) - 1;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this entity.
        /// </summary>
        public override String ToString()

            => $"{EntityType} at {Address}";

        #endregion

    }

}
