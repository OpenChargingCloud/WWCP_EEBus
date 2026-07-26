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
    /// An entity of another device, as we know it.
    /// </summary>
    public class SPINERemoteEntity : SPINEEntity
    {

        #region Data

        private readonly ConcurrentDictionary<UInt32, SPINERemoteFeature> features = new ();

        #endregion

        #region Properties

        /// <summary>
        /// The device this entity belongs to.
        /// </summary>
        public SPINERemoteDevice                 Device     { get; }

        /// <summary>
        /// The features of this entity, in the order of their numbers.
        /// </summary>
        public IEnumerable<SPINERemoteFeature>   Features
            => features.Values.OrderBy(feature => feature.Id);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an entity of another device.
        /// </summary>
        /// <param name="Device">The device it belongs to.</param>
        /// <param name="EntityId">The path of numbers naming it below that device.</param>
        /// <param name="EntityType">Which kind of entity it is.</param>
        public SPINERemoteEntity(SPINERemoteDevice    Device,
                                 IEnumerable<UInt32>  EntityId,
                                 EntityTypeType       EntityType)

            : base(Device.DeviceAddress,
                   EntityId,
                   EntityType)

        {
            this.Device = Device;
        }

        #endregion


        #region GetOrAddFeature(Id, FeatureType, Role)

        /// <summary>
        /// The feature with the given number, added when it is not known yet.
        /// </summary>
        /// <param name="Id">The number of the feature.</param>
        /// <param name="FeatureType">Which kind of feature it is.</param>
        /// <param name="Role">Whether it offers its data or asks for it.</param>
        public SPINERemoteFeature GetOrAddFeature(UInt32           Id,
                                                  FeatureTypeType  FeatureType,
                                                  RoleType         Role)

            => features.GetOrAdd(Id,
                                 id => new SPINERemoteFeature(id, this, FeatureType, Role));

        #endregion

        #region Feature(Id) / Feature(FeatureType, Role)

        /// <summary>
        /// The feature with the given number, or null when this entity has none.
        /// </summary>
        /// <param name="Id">The number of a feature.</param>
        public SPINERemoteFeature? Feature(UInt32 Id)

            => features.GetValueOrDefault(Id);


        /// <summary>
        /// The first feature of the given kind and role, or null when this
        /// entity has none.
        /// </summary>
        /// <param name="FeatureType">Which kind of feature.</param>
        /// <param name="Role">Which role.</param>
        public SPINERemoteFeature? Feature(FeatureTypeType  FeatureType,
                                           RoleType         Role)

            => Features.FirstOrDefault(feature => feature.FeatureType == FeatureType &&
                                                  feature.Role        == Role);

        #endregion

        #region RemoveFeature(Id)

        /// <summary>
        /// Forget a feature the other device no longer has.
        /// </summary>
        /// <param name="Id">The number of a feature.</param>
        public Boolean RemoveFeature(UInt32 Id)

            => features.TryRemove(Id, out _);

        #endregion

    }

}
