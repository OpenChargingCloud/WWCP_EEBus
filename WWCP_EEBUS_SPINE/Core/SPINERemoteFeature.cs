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
    /// A feature of another device, as we know it.
    ///
    /// What it can do comes from the detailed discovery; what its data is comes
    /// from the replies and notifies it sends. Both are a copy of what the other
    /// device says about itself, and this class is careful to say no more than
    /// that: it is a cache, not a source.
    /// </summary>
    public class SPINERemoteFeature : SPINEFeature
    {

        #region Properties

        /// <summary>
        /// The entity this feature belongs to.
        /// </summary>
        public SPINERemoteEntity  Entity              { get; }

        /// <summary>
        /// The device this feature belongs to.
        /// </summary>
        public SPINERemoteDevice  Device
            => Entity.Device;

        /// <summary>
        /// How long this feature may take to answer, where it says so.
        /// </summary>
        public TimeSpan?          MaxResponseDelay    { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a feature of another device.
        /// </summary>
        /// <param name="Id">The number of this feature within its entity.</param>
        /// <param name="Entity">The entity it belongs to.</param>
        /// <param name="FeatureType">Which kind of feature it is.</param>
        /// <param name="Role">Whether it offers its data or asks for it.</param>
        public SPINERemoteFeature(UInt32             Id,
                                  SPINERemoteEntity  Entity,
                                  FeatureTypeType    FeatureType,
                                  RoleType           Role)

            : base(Id,
                   Entity.Address,
                   FeatureType,
                   Role)

        {
            this.Entity = Entity;
        }

        #endregion


        #region SetOperations(Functions)

        /// <summary>
        /// Take over what the detailed discovery says this feature can do.
        ///
        /// A function which the other device no longer announces is forgotten,
        /// together with whatever we had cached for it: a device which stops
        /// offering something has not merely stopped talking about it.
        /// </summary>
        /// <param name="Functions">The functions of the detailed discovery.</param>
        public void SetOperations(IEnumerable<FunctionPropertyType>? Functions)
        {

            var announced = new HashSet<String>(StringComparer.Ordinal);

            foreach (var property in Functions ?? [])
            {

                var function = property.Function?.ToString();

                if (function is null || SPINEFunctions.Get(function) is null)
                    continue;

                announced.Add(function);

                if (functions.TryGetValue(function, out var existing))
                    existing.Operations = property.PossibleOperations ?? existing.Operations;

                else
                    functions.TryAdd(function,
                                     new SPINEFunctionData(function, property.PossibleOperations));

            }

            foreach (var function in functions.Keys.ToArray())
                if (!announced.Contains(function))
                    functions.TryRemove(function, out _);

        }

        #endregion

        #region UpdateData(Function, Data, Cmd)

        /// <summary>
        /// Take over what this feature told us about one of its functions.
        ///
        /// This is never a remote write: the data belongs to the other device,
        /// and a notify or a reply is that device telling us what it is - there
        /// is nothing here to permit.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Data">The data the message carries.</param>
        /// <param name="Cmd">The command, whose filters say which part is meant.</param>
        public SPINEUpdateResult<Object> UpdateData(String   Function,
                                                    Object?  Data,
                                                    CmdType  Cmd)
        {

            // A device may notify us about a function it never announced. That
            // is worth keeping rather than dropping: the notify is evidence, and
            // whether the announcement was complete is a question for the
            // conformance tests.
            var functionData = functions.GetOrAdd(Function,
                                                  name => new SPINEFunctionData(name));

            return functionData.UpdateData(Data,
                                           Cmd,
                                           SPINEUpdateOptions.Notify);

        }

        #endregion

    }

}
