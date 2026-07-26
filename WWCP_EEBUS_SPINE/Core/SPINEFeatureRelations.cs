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
    /// One agreed relation between a client feature and a server feature.
    /// </summary>
    /// <param name="Id">The number this device gave the relation.</param>
    /// <param name="ClientAddress">The address of the client feature.</param>
    /// <param name="ServerAddress">The address of the server feature.</param>
    public sealed record SPINEFeatureRelation(UInt32              Id,
                                              FeatureAddressType  ClientAddress,
                                              FeatureAddressType  ServerAddress)
    {

        /// <summary>
        /// Return a text representation of this relation.
        /// </summary>
        public override String ToString()

            => $"{Id}: {ClientAddress} -> {ServerAddress}";

    }


    /// <summary>
    /// The subscriptions or the bindings of this device.
    ///
    /// SPINE has two relations between a client feature and a server feature,
    /// and they are the same shape and different things (SPINE 1.3.0, 7.5 and
    /// 7.6):
    ///
    /// * a **subscription** means "tell me when this changes" - the server
    ///   notifies the client;
    /// * a **binding** means "I may change this" - without one, a write is
    ///   refused with error 9, "binding is necessary for this command".
    ///
    /// A device may hold both for the same pair of features, and neither
    /// implies the other.
    /// </summary>
    public class SPINEFeatureRelations
    {

        #region Data

        private readonly Lock                          relationsLock  = new ();

        private readonly List<SPINEFeatureRelation>    relations      = [];

        private          UInt32                        nextId         = 1;

        #endregion

        #region Properties

        /// <summary>
        /// All relations of this kind.
        /// </summary>
        public IEnumerable<SPINEFeatureRelation> All
        {
            get
            {
                lock (relationsLock)
                {
                    return [.. relations];
                }
            }
        }

        #endregion


        #region Add(ClientAddress, ServerAddress)

        /// <summary>
        /// Agree to a relation, or answer the one which is already there.
        /// </summary>
        /// <param name="ClientAddress">The address of the client feature.</param>
        /// <param name="ServerAddress">The address of the server feature.</param>
        public SPINEFeatureRelation Add(FeatureAddressType  ClientAddress,
                                        FeatureAddressType  ServerAddress)
        {
            lock (relationsLock)
            {

                var existing = Find(ClientAddress, ServerAddress);

                if (existing is not null)
                    return existing;

                var relation = new SPINEFeatureRelation(nextId++,
                                                        ClientAddress.Clone(),
                                                        ServerAddress.Clone());

                relations.Add(relation);

                return relation;

            }
        }

        #endregion

        #region Remove(ClientAddress, ServerAddress) / RemoveAllOf(Device)

        /// <summary>
        /// Give up a relation.
        /// </summary>
        /// <param name="ClientAddress">The address of the client feature.</param>
        /// <param name="ServerAddress">The address of the server feature.</param>
        /// <returns>False when there was none.</returns>
        public Boolean Remove(FeatureAddressType  ClientAddress,
                              FeatureAddressType  ServerAddress)
        {
            lock (relationsLock)
            {

                var existing = Find(ClientAddress, ServerAddress);

                return existing is not null &&
                       relations.Remove(existing);

            }
        }


        /// <summary>
        /// Give up every relation with the given device, which is what happens
        /// when it goes away.
        /// </summary>
        /// <param name="Device">The address of a device.</param>
        public UInt32 RemoveAllOf(String? Device)
        {
            lock (relationsLock)
            {
                return (UInt32) relations.RemoveAll(
                           relation => String.Equals(relation.ClientAddress.Device, Device, StringComparison.OrdinalIgnoreCase) ||
                                       String.Equals(relation.ServerAddress.Device, Device, StringComparison.OrdinalIgnoreCase)
                       );
            }
        }

        #endregion

        #region Has(ClientAddress, ServerAddress)

        /// <summary>
        /// Whether the given client and server features have this relation.
        /// </summary>
        /// <param name="ClientAddress">The address of the client feature.</param>
        /// <param name="ServerAddress">The address of the server feature.</param>
        public Boolean Has(FeatureAddressType?  ClientAddress,
                           FeatureAddressType?  ServerAddress)
        {
            lock (relationsLock)
            {
                return Find(ClientAddress, ServerAddress) is not null;
            }
        }

        #endregion

        #region ClientsOf(ServerAddress)

        /// <summary>
        /// The client features which have this relation with the given server
        /// feature.
        /// </summary>
        /// <param name="ServerAddress">The address of a server feature.</param>
        public IEnumerable<FeatureAddressType> ClientsOf(FeatureAddressType ServerAddress)
        {
            lock (relationsLock)
            {
                return [.. relations.
                             Where (relation => SPINEAddresses.AreEqual(relation.ServerAddress, ServerAddress)).
                             Select(relation => relation.ClientAddress)];
            }
        }

        #endregion

        #region (private) Find(ClientAddress, ServerAddress)

        private SPINEFeatureRelation? Find(FeatureAddressType?  ClientAddress,
                                           FeatureAddressType?  ServerAddress)

            => relations.FirstOrDefault(relation => SPINEAddresses.AreEqual(relation.ClientAddress, ClientAddress) &&
                                                    SPINEAddresses.AreEqual(relation.ServerAddress, ServerAddress));

        #endregion

    }

}
