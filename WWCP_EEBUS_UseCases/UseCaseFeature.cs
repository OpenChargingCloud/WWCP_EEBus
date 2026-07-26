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

using cloud.charging.open.protocols.EEBUS.SPINE;
using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases
{

    /// <summary>
    /// A client feature of ours together with the server feature of a partner it
    /// talks to.
    ///
    /// This is what a use case works with: not two addresses, but the pair, with
    /// the rules of the general implementation guideline applied to it. There is
    /// one of these rather than one per feature type - the SPINE core reads and
    /// writes any function by name, so a helper per feature type would be a list
    /// of names and nothing else. The Go reference implementation needs nine of
    /// them because Go needs a typed method per data type.
    ///
    /// What it does add over the bare core:
    ///
    /// * it refuses to ask for something the partner did not announce, rather
    ///   than sending a read the partner will refuse;
    /// * it drops the selectors of a partial read when the partner did not
    ///   announce a partial read, because the answer would come back in full
    ///   anyway (see docs/spec-deviations.md, S8 for what spine-go does here);
    /// * it knows whether there is a subscription, which is what the guideline
    ///   § 3.2.2 and § 3.2.3 are about: subscriptions come first, and polling
    ///   next to a working subscription is an anti-pattern.
    /// </summary>
    public class UseCaseFeature
    {

        #region Properties

        /// <summary>
        /// Which kind of feature this is about.
        /// </summary>
        public FeatureTypeType      FeatureType    { get; }

        /// <summary>
        /// Our client feature.
        /// </summary>
        public SPINELocalFeature    Local          { get; }

        /// <summary>
        /// The server feature of the partner.
        /// </summary>
        public SPINERemoteFeature   Remote         { get; }

        /// <summary>
        /// The device this feature belongs to.
        /// </summary>
        public SPINELocalDevice     Device
            => Local.Device;

        /// <summary>
        /// Whether the partner is telling us about changes by itself.
        ///
        /// This is what we asked for and were granted. The subscription itself
        /// lives on the partner - it is the one which sends the notifies - and
        /// the only way to see its side of it is to read its
        /// "nodeManagementSubscriptionData" (SPINE 1.3.0, 7.5.3), which is a
        /// question a conformance test asks rather than a client.
        /// </summary>
        public Boolean              HasSubscription
            => Device.SubscriptionsToOthers.Has(Local.Address, Remote.Address);

        /// <summary>
        /// Whether we may write to the partner.
        /// </summary>
        public Boolean              HasBinding
            => Device.BindingsToOthers.Has(Local.Address, Remote.Address);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Pair one of our client features with a server feature of a partner.
        /// </summary>
        /// <param name="FeatureType">Which kind of feature.</param>
        /// <param name="LocalEntity">The entity of ours which holds the client feature.</param>
        /// <param name="RemoteEntity">The entity of the partner which holds the server feature.</param>
        /// <exception cref="ArgumentException">When either side does not have the feature.</exception>
        public UseCaseFeature(FeatureTypeType    FeatureType,
                              SPINELocalEntity   LocalEntity,
                              SPINERemoteEntity  RemoteEntity)
        {

            this.FeatureType  = FeatureType;

            // A device may serve several feature types from one generic client
            // feature; SPINE allows that and devices do it.
            this.Local        = LocalEntity.Feature(FeatureType,               RoleType.Client)
                                    ?? LocalEntity.Feature(FeatureTypeType.Generic, RoleType.Client)
                                    ?? throw new ArgumentException($"The entity {LocalEntity.Address} has no {FeatureType} client feature.",
                                                                   nameof(LocalEntity));

            this.Remote       = RemoteEntity.Feature(FeatureType, RoleType.Server)
                                    ?? throw new ArgumentException($"The entity {RemoteEntity.Address} has no {FeatureType} server feature.",
                                                                   nameof(RemoteEntity));

        }

        #endregion


        #region Subscribe(CancellationToken = default) / Unsubscribe(...)

        /// <summary>
        /// Ask the partner to tell us when its data changes.
        ///
        /// The general implementation guideline § 3.2.2 makes this the primary
        /// way of getting data: polling is for the case where this fails.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> Subscribe(CancellationToken CancellationToken = default)
        {

            return await Device.NodeManagement.Subscribe(Local, Remote, CancellationToken);

        }


        /// <summary>
        /// Stop being told.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> Unsubscribe(CancellationToken CancellationToken = default)
        {

            return await Device.NodeManagement.Unsubscribe(Local, Remote, CancellationToken);

        }

        #endregion

        #region Bind(CancellationToken = default) / Unbind(...)

        /// <summary>
        /// Ask the partner for permission to write to it (SPINE 1.3.0, 7.6).
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> Bind(CancellationToken CancellationToken = default)
        {

            return await Device.NodeManagement.Bind(Local, Remote, CancellationToken);

        }


        /// <summary>
        /// Give up the permission to write.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<SPINEResponse> Unbind(CancellationToken CancellationToken = default)
        {

            return await Device.NodeManagement.Unbind(Local, Remote, CancellationToken);

        }

        #endregion


        #region Supports(Function, ForWriting = false)

        /// <summary>
        /// Whether the partner announced that it has the given function, and
        /// that it may be read or written.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="ForWriting">Whether a write is meant rather than a read.</param>
        public Boolean Supports(String Function, Boolean ForWriting = false)
        {

            var functionData = Remote.FunctionData(Function);

            return functionData is not null &&
                   (ForWriting
                        ? functionData.Operations.CanWrite
                        : functionData.Operations.CanRead);

        }

        #endregion

        #region Data<T>(Function)

        /// <summary>
        /// What we last heard about the given function of the partner, or null
        /// when we have not heard anything yet.
        /// </summary>
        /// <typeparam name="T">The data type of the function.</typeparam>
        /// <param name="Function">The name of a SPINE function.</param>
        public T? Data<T>(String Function) where T : class

            => Remote.DataCopy<T>(Function);

        #endregion

        #region RequestData(Function, Selectors = null, Elements = null, CancellationToken = default)

        /// <summary>
        /// Ask the partner for the data of a function.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Selectors">Which entries of a list are wanted.</param>
        /// <param name="Elements">Which elements are wanted.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">When the partner did not announce the function as readable.</exception>
        public Task<SPINEResponse> RequestData(String             Function,
                                               Object?            Selectors           = null,
                                               Object?            Elements            = null,
                                               CancellationToken  CancellationToken   = default)
        {

            var functionData = Remote.FunctionData(Function);

            if (functionData is null || !functionData.Operations.CanRead)
                throw new InvalidOperationException(
                          $"The feature {Remote.Address} does not offer '{Function}' for reading.");

            // A partner which cannot answer a part will answer the whole thing
            // whatever we ask (SPINE 1.3.0, 5.3.4.5), so asking for a part is
            // just a filter it has to ignore.
            if (!functionData.Operations.CanReadPartial)
            {
                Selectors  = null;
                Elements   = null;
            }

            return Local.Read(Function,
                              Remote,
                              Selectors,
                              Elements,
                              CancellationToken);

        }

        #endregion

        #region WriteData(Function, Data, Partial = true, CancellationToken = default)

        /// <summary>
        /// Ask the partner to change the data of a function.
        ///
        /// Partial by default, and the guideline § 3.1 says why: "in cases where
        /// not all Elements are writeable by a client (which is the case in many
        /// Use Cases), the client SHALL use RFE for the write command.
        /// Otherwise, the full write command would be rejected by the
        /// recipient."
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Data">The data to write.</param>
        /// <param name="Partial">Whether only the stated parts are meant.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">When the partner did not announce the function as writable, or when there is no binding.</exception>
        public Task<SPINEResponse> WriteData(String             Function,
                                             Object             Data,
                                             Boolean            Partial             = true,
                                             CancellationToken  CancellationToken   = default)
        {

            var functionData = Remote.FunctionData(Function);

            if (functionData is null || !functionData.Operations.CanWrite)
                throw new InvalidOperationException(
                          $"The feature {Remote.Address} does not offer '{Function}' for writing.");

            if (!HasBinding)
                throw new InvalidOperationException(
                          $"There is no binding to {Remote.Address}; SPINE 1.3.0, 7.6 requires one before a write.");

            return Local.Write(Function,
                               Data,
                               Remote,
                               Partial,
                               CancellationToken);

        }

        #endregion

        #region IsRedundantPolling(Function)

        /// <summary>
        /// Whether reading this function now would be polling next to a working
        /// subscription.
        ///
        /// The general implementation guideline § 3.2.3 calls that an
        /// anti-pattern: "if a subscription is active and the client is
        /// receiving notify messages, the client SHOULD NOT perform additional
        /// polling for the same data points". It is a SHOULD NOT rather than a
        /// SHALL NOT, so this answers the question instead of refusing - and a
        /// conformance test can ask it about somebody else's device just as
        /// well as about ours.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        public Boolean IsRedundantPolling(String Function)

            => HasSubscription &&
               Remote.DataCopy(Function) is not null;

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this pair.
        /// </summary>
        public override String ToString()

            => $"{FeatureType}: {Local.Address} -> {Remote.Address}" +
               $"{(HasSubscription ? ", subscribed" : "")}" +
               $"{(HasBinding      ? ", bound"      : "")}";

        #endregion

    }

}
