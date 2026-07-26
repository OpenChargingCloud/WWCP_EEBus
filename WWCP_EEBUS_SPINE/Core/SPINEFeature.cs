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
    /// A feature of an entity (SPINE 1.3.0, 2.1.3).
    ///
    /// A feature is what a device can do, in one place: a feature type says
    /// which kind of thing it is ("LoadControl", "Measurement"), a role says
    /// whether it offers that thing or asks for it, and a set of functions says
    /// what data it has.
    ///
    /// The role is the part which is easy to get backwards. A **server** owns
    /// the data and answers reads; a **client** asks. A load control server is
    /// therefore the device which is being limited, not the one doing the
    /// limiting.
    /// </summary>
    public abstract class SPINEFeature
    {

        #region Data

        /// <summary>
        /// The functions of this feature, by name.
        /// </summary>
        protected readonly ConcurrentDictionary<String, SPINEFunctionData> functions = new (StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>
        /// The address of this feature.
        /// </summary>
        public FeatureAddressType  Address        { get; }

        /// <summary>
        /// The number of this feature within its entity.
        /// </summary>
        public UInt32              Id             { get; }

        /// <summary>
        /// Which kind of feature this is.
        /// </summary>
        public FeatureTypeType     FeatureType    { get; }

        /// <summary>
        /// Whether this feature offers its data or asks for it.
        /// </summary>
        public RoleType            Role           { get; }

        /// <summary>
        /// A text describing this feature, for humans.
        /// </summary>
        public String?             Description    { get; set; }

        /// <summary>
        /// The names of the functions this feature has.
        /// </summary>
        public IEnumerable<String> Functions
            => functions.Keys;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new feature.
        /// </summary>
        /// <param name="Id">The number of this feature within its entity.</param>
        /// <param name="EntityAddress">The address of the entity it belongs to.</param>
        /// <param name="FeatureType">Which kind of feature this is.</param>
        /// <param name="Role">Whether it offers its data or asks for it.</param>
        protected SPINEFeature(UInt32             Id,
                               EntityAddressType  EntityAddress,
                               FeatureTypeType    FeatureType,
                               RoleType           Role)
        {

            this.Id           = Id;
            this.FeatureType  = FeatureType;
            this.Role         = Role;

            this.Address      = new FeatureAddressType {
                                    Device   = EntityAddress.Device,
                                    Entity   = EntityAddress.Entity is not null ? [.. EntityAddress.Entity] : null,
                                    Feature  = Id
                                };

        }

        #endregion


        #region Function(Function) / HasFunction(Function)

        /// <summary>
        /// The data of the given function, or null when this feature does not
        /// have it.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        public SPINEFunctionData? FunctionData(String Function)

            => functions.GetValueOrDefault(Function);


        /// <summary>
        /// Whether this feature has the given function.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        public Boolean HasFunction(String Function)

            => functions.ContainsKey(Function);

        #endregion

        #region DataCopy(Function)

        /// <summary>
        /// A copy of the data of the given function, or null when this feature
        /// does not have it or holds nothing for it.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        public Object? DataCopy(String Function)

            => FunctionData(Function)?.DataCopy();


        /// <summary>
        /// A copy of the data of the given function as the given type.
        /// </summary>
        /// <typeparam name="T">The data type of the function.</typeparam>
        /// <param name="Function">The name of a SPINE function.</param>
        public T? DataCopy<T>(String Function) where T : class

            => DataCopy(Function) as T;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this feature.
        /// </summary>
        public override String ToString()

            => $"{FeatureType} {Role} at {Address}";

        #endregion

    }

}
