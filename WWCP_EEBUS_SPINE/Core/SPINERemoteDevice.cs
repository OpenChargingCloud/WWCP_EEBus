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
    /// Another device, as we know it.
    ///
    /// It is created when a SHIP connection to it is established, and it is
    /// nearly empty at that point: which entities and features it has comes out
    /// of the detailed discovery, and even its device address is something it
    /// only tells us there. Until then, the one thing which is certain is the
    /// subject key identifier of its certificate.
    /// </summary>
    public class SPINERemoteDevice
    {

        #region Data

        private readonly ConcurrentDictionary<String, SPINERemoteEntity> entities = new (StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>
        /// The subject key identifier of the certificate this device uses, which
        /// identifies it before anything else does.
        /// </summary>
        public String                             SKI              { get; }

        /// <summary>
        /// The address of this device, once it has told us.
        /// </summary>
        public String?                            DeviceAddress    { get; set; }

        /// <summary>
        /// Which kind of device this is, once it has told us.
        /// </summary>
        public DeviceTypeType?                    DeviceType       { get; set; }

        /// <summary>
        /// Which network management feature set it has.
        /// </summary>
        public NetworkManagementFeatureSetType?   FeatureSet       { get; set; }

        /// <summary>
        /// Everything we send to this device.
        /// </summary>
        public SPINESender                        Sender           { get; }

        /// <summary>
        /// The entities of this device.
        /// </summary>
        public IEnumerable<SPINERemoteEntity>     Entities
            => entities.Values;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create another device.
        /// </summary>
        /// <param name="SKI">The subject key identifier of its certificate.</param>
        /// <param name="Writer">Where datagrams to it go.</param>
        /// <param name="MsgCounter">Where the message counters come from.</param>
        public SPINERemoteDevice(String         SKI,
                                 ISPINEWriter   Writer,
                                 Func<UInt64>?  MsgCounter   = null)
        {

            this.SKI     = SKI;
            this.Sender  = new SPINESender(Writer, MsgCounter);

        }

        #endregion


        #region GetOrAddEntity(EntityId, EntityType)

        /// <summary>
        /// The entity with the given address, added when it is not known yet.
        /// </summary>
        /// <param name="EntityId">The path of numbers naming the entity.</param>
        /// <param name="EntityType">Which kind of entity it is.</param>
        public SPINERemoteEntity GetOrAddEntity(IEnumerable<UInt32>  EntityId,
                                                EntityTypeType       EntityType)

            => entities.GetOrAdd(KeyOf(EntityId),
                                 _ => new SPINERemoteEntity(this, EntityId, EntityType));

        #endregion

        #region Entity(EntityId) / RemoveEntity(EntityId)

        /// <summary>
        /// The entity with the given address, or null when this device has none.
        /// </summary>
        /// <param name="EntityId">The path of numbers naming the entity.</param>
        public SPINERemoteEntity? Entity(IEnumerable<UInt32>? EntityId)

            => EntityId is null
                   ? null
                   : entities.GetValueOrDefault(KeyOf(EntityId));


        /// <summary>
        /// Forget an entity the other device no longer has.
        /// </summary>
        /// <param name="EntityId">The path of numbers naming the entity.</param>
        public Boolean RemoveEntity(IEnumerable<UInt32> EntityId)

            => entities.TryRemove(KeyOf(EntityId), out _);

        #endregion

        #region Feature(Address)

        /// <summary>
        /// The feature with the given address, or null when this device has
        /// none.
        /// </summary>
        /// <param name="Address">The address of a feature.</param>
        public SPINERemoteFeature? Feature(FeatureAddressType? Address)

            => Address?.Feature is UInt32 id
                   ? Entity(Address.Entity)?.Feature(id)
                   : null;

        #endregion

        #region NodeManagement()

        /// <summary>
        /// The node management feature of this device, which every device has at
        /// entity 0, feature 0 - added if the detailed discovery has not run
        /// yet, because it is the feature the detailed discovery is asked of.
        /// </summary>
        public SPINERemoteFeature NodeManagement()

            => GetOrAddEntity([ SPINEAddresses.NodeManagementEntity ],
                              EntityTypeType.DeviceInformation).
                   GetOrAddFeature(SPINEAddresses.NodeManagementFeature,
                                   FeatureTypeType.NodeManagement,
                                   RoleType.Special);

        #endregion

        #region UseCases

        /// <summary>
        /// Which use cases this device says it supports, as its node management
        /// last told us. Empty until the use case data has been read.
        /// </summary>
        public IEnumerable<UseCaseInformationDataType> UseCases

            => NodeManagement().
                   DataCopy<NodeManagementUseCaseDataType>("nodeManagementUseCaseData")?.
                   UseCaseInformation ?? [];

        #endregion


        #region (private static) KeyOf(EntityId)

        private static String KeyOf(IEnumerable<UInt32> EntityId)

            => String.Join(',', EntityId);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this device.
        /// </summary>
        public override String ToString()

            => $"{DeviceAddress ?? SKI}{(DeviceType is not null ? $" ({DeviceType})" : "")}";

        #endregion

    }

}
