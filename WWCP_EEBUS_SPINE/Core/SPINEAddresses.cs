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
    /// The addresses of SPINE (SPINE 1.3.0, 3.2).
    ///
    /// A device is named by a text, an entity by a path of numbers below it, and
    /// a feature by a number below that. The three of them are one address type
    /// in the XSD with more or fewer parts filled in, which is why comparing
    /// them is a job for one place rather than for every caller.
    ///
    /// Entities nest: the entity "1/4" is a child of the entity "1", and it is
    /// not the entity "4" (SPINE 1.3.0, 5.3.4.7.2). The comparison here is
    /// therefore over the whole path, never over its last number.
    /// </summary>
    public static class SPINEAddresses
    {

        #region Data

        /// <summary>
        /// The entity every device has, and which carries node management
        /// (SPINE 1.3.0, 7.1): entity 0, feature 0.
        /// </summary>
        public static readonly UInt32  NodeManagementEntity   = 0;

        /// <summary>
        /// The feature node management always sits on.
        /// </summary>
        public static readonly UInt32  NodeManagementFeature  = 0;

        #endregion


        #region Feature(Device, Entity, Feature) / Entity(...) / Device(...)

        /// <summary>
        /// The address of a feature.
        /// </summary>
        public static FeatureAddressType Feature(String?        Device,
                                                 IEnumerable<UInt32>?  Entity,
                                                 UInt32?        Feature)

            => new () {
                   Device   = Device,
                   Entity   = Entity is not null ? [.. Entity] : null,
                   Feature  = Feature
               };


        /// <summary>
        /// The address of the node management feature of the given device,
        /// which is entity 0, feature 0 on every device there is.
        /// </summary>
        /// <param name="Device">The address of a device.</param>
        public static FeatureAddressType NodeManagement(String? Device)

            => new () {
                   Device   = Device,
                   Entity   = [ NodeManagementEntity ],
                   Feature  = NodeManagementFeature
               };

        #endregion

        #region AreEqual(A, B) / EntitiesAreEqual(A, B)

        /// <summary>
        /// Whether two feature addresses name the same feature.
        ///
        /// Every part has to agree, and a part which one side leaves out has to
        /// be left out on the other side as well. This is not
        /// <see cref="FeatureAddressType.Matches"/>, which reads a missing part
        /// as "any" and answers a different question.
        /// </summary>
        public static Boolean AreEqual(FeatureAddressType? A,
                                       FeatureAddressType? B)

            => ReferenceEquals(A, B) ||
               (A is not null &&
                B is not null &&
                String.Equals(A.Device, B.Device, StringComparison.OrdinalIgnoreCase) &&
                EntitiesAreEqual(A.Entity, B.Entity) &&
                A.Feature == B.Feature);


        /// <summary>
        /// Whether two entity paths are the same path.
        /// </summary>
        public static Boolean EntitiesAreEqual(IReadOnlyList<UInt32>? A,
                                               IReadOnlyList<UInt32>? B)
        {

            if (A is null || B is null)
                return A is null && B is null;

            if (A.Count != B.Count)
                return false;

            for (var i = 0; i < A.Count; i++)
                if (A[i] != B[i])
                    return false;

            return true;

        }

        #endregion

        #region IsNodeManagement(Address)

        /// <summary>
        /// Whether the given address is the node management feature of its
        /// device.
        /// </summary>
        public static Boolean IsNodeManagement(FeatureAddressType? Address)

            => Address?.Feature == NodeManagementFeature &&
               Address.Entity is [ var entity ] &&
               entity == NodeManagementEntity;

        #endregion

        #region KeyOf(Address)

        /// <summary>
        /// A text which is the same for two addresses exactly when they name the
        /// same feature, so that addresses can be used as keys.
        /// </summary>
        public static String KeyOf(FeatureAddressType? Address)

            => Address is null
                   ? ""
                   : $"{Address.Device?.ToLowerInvariant()}:" +
                     $"[{(Address.Entity is not null ? String.Join(',', Address.Entity) : "")}]:" +
                     $"{Address.Feature}";

        #endregion

    }

}
