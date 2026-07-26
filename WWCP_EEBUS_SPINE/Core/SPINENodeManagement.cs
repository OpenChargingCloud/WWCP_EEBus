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
    /// The node management feature (SPINE 1.3.0, 7).
    ///
    /// Every device has exactly one, at entity 0, feature 0, and it is the only
    /// feature two devices can talk about before they know anything else about
    /// each other. It answers four questions:
    ///
    /// * **what are you** - the detailed discovery, which lists every entity,
    ///   every feature and every function of the device;
    /// * **which use cases do you support** - the use case data;
    /// * **tell me when this changes** - subscriptions;
    /// * **may I change this** - bindings.
    ///
    /// Its role is "special": it is neither a client nor a server, because both
    /// devices ask each other the same questions.
    /// </summary>
    public class SPINENodeManagement : SPINELocalFeature
    {

        #region Data

        /// <summary>The detailed discovery of a device.</summary>
        public const String DetailedDiscoveryData    = "nodeManagementDetailedDiscoveryData";

        /// <summary>Which use cases a device supports.</summary>
        public const String UseCaseData              = "nodeManagementUseCaseData";

        /// <summary>The subscriptions a device holds.</summary>
        public const String SubscriptionData         = "nodeManagementSubscriptionData";

        /// <summary>Asking for a subscription.</summary>
        public const String SubscriptionRequestCall  = "nodeManagementSubscriptionRequestCall";

        /// <summary>Giving up a subscription.</summary>
        public const String SubscriptionDeleteCall   = "nodeManagementSubscriptionDeleteCall";

        /// <summary>The bindings a device holds.</summary>
        public const String BindingData              = "nodeManagementBindingData";

        /// <summary>Asking for a binding.</summary>
        public const String BindingRequestCall       = "nodeManagementBindingRequestCall";

        /// <summary>Giving up a binding.</summary>
        public const String BindingDeleteCall        = "nodeManagementBindingDeleteCall";

        /// <summary>Which devices are reachable through this one.</summary>
        public const String DestinationListData      = "nodeManagementDestinationListData";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the node management feature of this device.
        /// </summary>
        /// <param name="Id">The number of this feature, which is 0.</param>
        /// <param name="Entity">The entity it belongs to, which is entity 0.</param>
        public SPINENodeManagement(UInt32            Id,
                                   SPINELocalEntity  Entity)

            : base(Id,
                   Entity,
                   FeatureTypeType.NodeManagement,
                   RoleType.Special)

        {

            AddFunction(DetailedDiscoveryData,   Read: true);
            AddFunction(UseCaseData,             Read: true);
            AddFunction(SubscriptionData,        Read: true);
            AddFunction(SubscriptionRequestCall,  Read: false);
            AddFunction(SubscriptionDeleteCall,   Read: false);
            AddFunction(BindingData,             Read: true);
            AddFunction(BindingRequestCall,       Read: false);
            AddFunction(BindingDeleteCall,        Read: false);

            // A device of the "simple" feature set forwards nothing, so it has
            // no destination list (SPINE 1.3.0, 7.2).
            if (Entity.Device.FeatureSet != NetworkManagementFeatureSetType.Simple)
                AddFunction(DestinationListData, Read: true);

            SetUseCaseData(null);

        }

        #endregion


        #region RequestDetailedDiscovery(RemoteDevice, CancellationToken = default)

        /// <summary>
        /// Ask another device what it is.
        ///
        /// This is the first thing which happens on a new connection, and it is
        /// what turns a subject key identifier into a device with entities and
        /// features.
        /// </summary>
        /// <param name="RemoteDevice">The device to ask.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SPINEResponse> RequestDetailedDiscovery(SPINERemoteDevice  RemoteDevice,
                                                            CancellationToken  CancellationToken   = default)

            => Read(DetailedDiscoveryData,
                    RemoteDevice.NodeManagement(),
                    CancellationToken: CancellationToken);

        #endregion

        #region RequestUseCaseData(RemoteDevice, CancellationToken = default)

        /// <summary>
        /// Ask another device which use cases it supports.
        /// </summary>
        /// <param name="RemoteDevice">The device to ask.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SPINEResponse> RequestUseCaseData(SPINERemoteDevice  RemoteDevice,
                                                      CancellationToken  CancellationToken   = default)

            => Read(UseCaseData,
                    RemoteDevice.NodeManagement(),
                    CancellationToken: CancellationToken);

        #endregion

        #region SetUseCaseData(UseCases) / UseCases

        /// <summary>
        /// Say which use cases this device supports.
        /// </summary>
        /// <param name="UseCases">The use cases, or null for none.</param>
        public void SetUseCaseData(IEnumerable<UseCaseInformationDataType>? UseCases)
        {
            FunctionData(UseCaseData)!.SetData(
                new NodeManagementUseCaseDataType {
                    UseCaseInformation = UseCases is not null ? [.. UseCases] : []
                }
            );
        }


        /// <summary>
        /// Which use cases this device says it supports, by actor.
        /// </summary>
        public IEnumerable<UseCaseInformationDataType> UseCases

            => FunctionData(UseCaseData)?.
                   DataCopy<NodeManagementUseCaseDataType>()?.
                   UseCaseInformation ?? [];

        #endregion

        #region AddUseCaseSupport(...) / RemoveUseCaseSupport(...) / SetUseCaseAvailability(...)

        /// <summary>
        /// Say that an entity of this device supports a use case
        /// (SPINE 1.3.0, 7.3).
        ///
        /// The use case discovery is grouped by actor: one entry per address and
        /// actor, holding every use case that entity plays that actor in. Which
        /// names and actors exist is not decided here - SPINE leaves the
        /// enumerations of "useCaseName" and "actor" deliberately empty and lets
        /// the use case specifications fill them.
        /// </summary>
        /// <param name="Address">The feature of the entity which supports it.</param>
        /// <param name="Actor">Which actor of the use case it plays.</param>
        /// <param name="UseCaseName">The name of the use case.</param>
        /// <param name="UseCaseVersion">Which version of it is supported.</param>
        /// <param name="Scenarios">Which of its scenarios are supported.</param>
        /// <param name="DocumentSubRevision">The sub revision of the use case document, where there is one.</param>
        /// <param name="Available">Whether the use case can be used right now.</param>
        /// <param name="NotifySubscribers">Whether to tell the subscribers of node management.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<UseCaseSupportType> AddUseCaseSupport(FeatureAddressType   Address,
                                                                String               Actor,
                                                                String               UseCaseName,
                                                                String               UseCaseVersion,
                                                                IEnumerable<UInt32>  Scenarios,
                                                                String?              DocumentSubRevision   = null,
                                                                Boolean              Available             = true,
                                                                Boolean              NotifySubscribers     = true,
                                                                CancellationToken    CancellationToken     = default)
        {

            var support = new UseCaseSupportType {
                              UseCaseName                = UseCaseName,
                              UseCaseVersion             = UseCaseVersion,
                              UseCaseAvailable           = Available,
                              ScenarioSupport            = [.. Scenarios],
                              UseCaseDocumentSubRevision = DocumentSubRevision
                          };

            var useCases    = UseCases.ToList();

            var information = useCases.FirstOrDefault(entry => String.Equals(entry.Actor, Actor, StringComparison.Ordinal) &&
                                                               SPINEAddresses.AreEqual(entry.Address, Address));

            if (information is null)
            {

                information = new UseCaseInformationDataType {
                                  Address         = Address.Clone(),
                                  Actor           = Actor,
                                  UseCaseSupport  = []
                              };

                useCases.Add(information);

            }

            information.Set(support);

            await Announce(useCases, NotifySubscribers, CancellationToken);

            return support;

        }


        /// <summary>
        /// Say that an entity of this device no longer supports a use case.
        /// </summary>
        /// <param name="Actor">Which actor of the use case it played.</param>
        /// <param name="UseCaseName">The name of the use case.</param>
        /// <param name="NotifySubscribers">Whether to tell the subscribers of node management.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<Boolean> RemoveUseCaseSupport(String             Actor,
                                                        String             UseCaseName,
                                                        Boolean            NotifySubscribers   = true,
                                                        CancellationToken  CancellationToken   = default)
        {

            var useCases  = UseCases.ToList();
            var removed   = false;

            foreach (var information in useCases.ToArray())
            {

                if (!String.Equals(information.Actor, Actor, StringComparison.Ordinal))
                    continue;

                removed |= information.Remove(UseCaseName);

                // An actor which plays no use case any more says nothing.
                if (information.UseCaseSupport is null || information.UseCaseSupport.Count == 0)
                    useCases.Remove(information);

            }

            if (removed)
                await Announce(useCases, NotifySubscribers, CancellationToken);

            return removed;

        }


        /// <summary>
        /// Say whether a use case can be used right now.
        ///
        /// This is the switch a device flips when it can still talk about a use
        /// case but cannot currently carry it out - a charging station with no
        /// car plugged in, an inverter which is off.
        /// </summary>
        /// <param name="Actor">Which actor of the use case it plays.</param>
        /// <param name="UseCaseName">The name of the use case.</param>
        /// <param name="Available">Whether it can be used right now.</param>
        /// <param name="NotifySubscribers">Whether to tell the subscribers of node management.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task<Boolean> SetUseCaseAvailability(String             Actor,
                                                          String             UseCaseName,
                                                          Boolean            Available,
                                                          Boolean            NotifySubscribers   = true,
                                                          CancellationToken  CancellationToken   = default)
        {

            var useCases  = UseCases.ToList();
            var changed   = false;

            foreach (var information in useCases)
            {

                if (!String.Equals(information.Actor, Actor, StringComparison.Ordinal))
                    continue;

                if (information.Find(UseCaseName) is UseCaseSupportType support)
                {
                    support.UseCaseAvailable = Available;
                    changed = true;
                }

            }

            if (changed)
                await Announce(useCases, NotifySubscribers, CancellationToken);

            return changed;

        }


        /// <summary>
        /// Write the use cases back and tell whoever subscribed.
        /// </summary>
        private async Task Announce(List<UseCaseInformationDataType>  UseCases,
                                    Boolean                           NotifySubscribers,
                                    CancellationToken                 CancellationToken)
        {

            SetUseCaseData(UseCases);

            if (NotifySubscribers)
                await Device.NotifySubscribers(this,
                                               FunctionData(UseCaseData)!.ToCmd(),
                                               CancellationToken);

        }

        #endregion


        #region Subscribe  (ClientFeature, ServerFeature, CancellationToken = default)

        /// <summary>
        /// Ask a server feature of another device to tell us when its data
        /// changes (SPINE 1.3.0, 7.5).
        /// </summary>
        /// <param name="ClientFeature">Our feature which wants to be told.</param>
        /// <param name="ServerFeature">The feature of the other device.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SPINEResponse> Subscribe(SPINELocalFeature   ClientFeature,
                                             SPINERemoteFeature  ServerFeature,
                                             CancellationToken   CancellationToken   = default)

            => Remember(Device.SubscriptionsToOthers, ClientFeature, ServerFeature, Granted: true,
                        Call(SubscriptionRequestCall,
                    new NodeManagementSubscriptionRequestCallType {
                        SubscriptionRequest = new SubscriptionManagementRequestCallType {
                                                  ClientAddress      = ClientFeature.Address.Clone(),
                                                  ServerAddress      = ServerFeature.Address.Clone(),
                                                  ServerFeatureType  = ServerFeature.FeatureType
                                              }
                    },
                    ServerFeature.Device,
                    CancellationToken));

        #endregion

        #region Unsubscribe(ClientFeature, ServerFeature, CancellationToken = default)

        /// <summary>
        /// Give up a subscription.
        /// </summary>
        public Task<SPINEResponse> Unsubscribe(SPINELocalFeature   ClientFeature,
                                               SPINERemoteFeature  ServerFeature,
                                               CancellationToken   CancellationToken   = default)

            => Remember(Device.SubscriptionsToOthers, ClientFeature, ServerFeature, Granted: false,
                        Call(SubscriptionDeleteCall,
                    new NodeManagementSubscriptionDeleteCallType {
                        SubscriptionDelete = new SubscriptionManagementDeleteCallType {
                                                 ClientAddress  = ClientFeature.Address.Clone(),
                                                 ServerAddress  = ServerFeature.Address.Clone()
                                             }
                    },
                    ServerFeature.Device,
                    CancellationToken));

        #endregion

        #region Bind       (ClientFeature, ServerFeature, CancellationToken = default)

        /// <summary>
        /// Ask a server feature of another device for permission to write to it
        /// (SPINE 1.3.0, 7.6).
        /// </summary>
        /// <param name="ClientFeature">Our feature which wants to write.</param>
        /// <param name="ServerFeature">The feature of the other device.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task<SPINEResponse> Bind(SPINELocalFeature   ClientFeature,
                                        SPINERemoteFeature  ServerFeature,
                                        CancellationToken   CancellationToken   = default)

            => Remember(Device.BindingsToOthers, ClientFeature, ServerFeature, Granted: true,
                        Call(BindingRequestCall,
                    new NodeManagementBindingRequestCallType {
                        BindingRequest = new BindingManagementRequestCallType {
                                             ClientAddress      = ClientFeature.Address.Clone(),
                                             ServerAddress      = ServerFeature.Address.Clone(),
                                             ServerFeatureType  = ServerFeature.FeatureType
                                         }
                    },
                    ServerFeature.Device,
                    CancellationToken));

        #endregion

        #region Unbind     (ClientFeature, ServerFeature, CancellationToken = default)

        /// <summary>
        /// Give up a binding.
        /// </summary>
        public Task<SPINEResponse> Unbind(SPINELocalFeature   ClientFeature,
                                          SPINERemoteFeature  ServerFeature,
                                          CancellationToken   CancellationToken   = default)

            => Remember(Device.BindingsToOthers, ClientFeature, ServerFeature, Granted: false,
                        Call(BindingDeleteCall,
                    new NodeManagementBindingDeleteCallType {
                        BindingDelete = new BindingManagementDeleteCallType {
                                            ClientAddress  = ClientFeature.Address.Clone(),
                                            ServerAddress  = ServerFeature.Address.Clone()
                                        }
                    },
                    ServerFeature.Device,
                    CancellationToken));

        #endregion

        #region (private) Remember(...)

        /// <summary>
        /// Keep what the other device agreed to, once it has agreed to it.
        ///
        /// The subscription or the binding itself lives on the partner - it is
        /// the one which sends the notifies, and the one which allows the
        /// writes - so this side can only remember what it asked for and was
        /// granted. Without that a client has no way to answer "may I write
        /// this?" except by trying.
        /// </summary>
        private static async Task<SPINEResponse> Remember(SPINEFeatureRelations  Relations,
                                                          SPINELocalFeature      ClientFeature,
                                                          SPINERemoteFeature     ServerFeature,
                                                          Boolean                Granted,
                                                          Task<SPINEResponse>    Request)
        {

            var response = await Request;

            if (!response.IsError)
            {

                if (Granted)
                    Relations.Add   (ClientFeature.Address, ServerFeature.Address);

                else
                    Relations.Remove(ClientFeature.Address, ServerFeature.Address);

            }

            return response;

        }

        #endregion

        #region (private) Call(Function, Data, RemoteDevice, CancellationToken)

        /// <summary>
        /// Subscriptions and bindings are asked for with a "call", and always
        /// between the two node management features - never between the features
        /// they are about.
        /// </summary>
        private async Task<SPINEResponse> Call(String             Function,
                                               Object             Data,
                                               SPINERemoteDevice  RemoteDevice,
                                               CancellationToken  CancellationToken)
        {

            var cmd = new CmdType();

            cmd.SetData(Function, Data);

            return await Ask(CmdClassifierType.Call,
                             true,
                             cmd,
                             RemoteDevice.NodeManagement(),
                             CancellationToken);

        }

        #endregion


        #region NotifyEntityAdded  (Entity, CancellationToken = default)

        /// <summary>
        /// Tell the other devices that this device has a new entity.
        ///
        /// The message is a partial notify of the detailed discovery which
        /// carries only the entity and its features, with "lastStateChange"
        /// saying that it was added - the other side is not meant to read the
        /// whole device again.
        /// </summary>
        /// <param name="Entity">The new entity.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task NotifyEntityAdded(SPINELocalEntity   Entity,
                                      CancellationToken  CancellationToken   = default)

            => NotifyEntity(Entity,
                            NetworkManagementStateChangeType.Added,
                            CancellationToken);


        /// <summary>
        /// Tell the other devices that an entity of this device is gone.
        /// </summary>
        /// <param name="Entity">The entity which is gone.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task NotifyEntityRemoved(SPINELocalEntity   Entity,
                                        CancellationToken  CancellationToken   = default)

            => NotifyEntity(Entity,
                            NetworkManagementStateChangeType.Removed,
                            CancellationToken);


        private async Task NotifyEntity(SPINELocalEntity                  Entity,
                                        NetworkManagementStateChangeType  StateChange,
                                        CancellationToken                 CancellationToken)
        {

            var entityInformation = Entity.Information();

            entityInformation.Description!.LastStateChange = StateChange;

            var data = new NodeManagementDetailedDiscoveryDataType {
                           DeviceInformation  = Device.Information(),
                           EntityInformation  = [ entityInformation ],

                           // What an entity which is gone consists of is of no
                           // interest to anybody (SPINE 1.3.0, 7.1.1.5).
                           FeatureInformation = StateChange == NetworkManagementStateChangeType.Added
                                                    ? [.. Entity.Features.Select(feature => feature.Information())]
                                                    : null
                       };

            var cmd = new CmdType {
                          Function  = FunctionType.Parse(DetailedDiscoveryData),
                          Filter    = [ new FilterType { CmdControl = CmdControlType.ForPartial } ]
                      };

            cmd.SetData(DetailedDiscoveryData, data);

            await Device.NotifySubscribers(this, cmd, CancellationToken);

        }

        #endregion


        #region (override) HandleMessage(Message, CancellationToken)

        /// <summary>
        /// Node management answers its own questions.
        ///
        /// Everything it does not know about falls through to the ordinary
        /// handling of a feature, which reads, replies, notifies and writes by
        /// the function registry alone.
        /// </summary>
        protected internal override async Task<ResultDataType?> HandleMessage(SPINEMessage       Message,
                                                                              CancellationToken  CancellationToken)
        {

            switch (Message.Function)
            {

                case DetailedDiscoveryData:
                    return await HandleDetailedDiscovery(Message, CancellationToken);

                case SubscriptionRequestCall:
                    return HandleRelationCall(Message,
                                              Device.Subscriptions,
                                              (Message.Data as NodeManagementSubscriptionRequestCallType)?.SubscriptionRequest?.ClientAddress,
                                              (Message.Data as NodeManagementSubscriptionRequestCallType)?.SubscriptionRequest?.ServerAddress,
                                              "subscription",
                                              Adding: true);

                case SubscriptionDeleteCall:
                    return HandleRelationCall(Message,
                                              Device.Subscriptions,
                                              (Message.Data as NodeManagementSubscriptionDeleteCallType)?.SubscriptionDelete?.ClientAddress,
                                              (Message.Data as NodeManagementSubscriptionDeleteCallType)?.SubscriptionDelete?.ServerAddress,
                                              "subscription",
                                              Adding: false);

                case BindingRequestCall:
                    return HandleRelationCall(Message,
                                              Device.Bindings,
                                              (Message.Data as NodeManagementBindingRequestCallType)?.BindingRequest?.ClientAddress,
                                              (Message.Data as NodeManagementBindingRequestCallType)?.BindingRequest?.ServerAddress,
                                              "binding",
                                              Adding: true);

                case BindingDeleteCall:
                    return HandleRelationCall(Message,
                                              Device.Bindings,
                                              (Message.Data as NodeManagementBindingDeleteCallType)?.BindingDelete?.ClientAddress,
                                              (Message.Data as NodeManagementBindingDeleteCallType)?.BindingDelete?.ServerAddress,
                                              "binding",
                                              Adding: false);

                case SubscriptionData when Message.CmdClassifier == CmdClassifierType.Read:
                    return await ReplyWithRelations(Message,
                                                    new CmdType {
                                                        NodeManagementSubscriptionData = new NodeManagementSubscriptionDataType {
                                                            SubscriptionEntry = [.. Relations(Device.Subscriptions, Message).
                                                                                       Select(relation => new SubscriptionManagementEntryDataType {
                                                                                                              SubscriptionId  = relation.Id,
                                                                                                              ClientAddress   = relation.ClientAddress.Clone(),
                                                                                                              ServerAddress   = relation.ServerAddress.Clone()
                                                                                                          })]
                                                        }
                                                    },
                                                    CancellationToken);

                case BindingData when Message.CmdClassifier == CmdClassifierType.Read:
                    return await ReplyWithRelations(Message,
                                                    new CmdType {
                                                        NodeManagementBindingData = new NodeManagementBindingDataType {
                                                            BindingEntry = [.. Relations(Device.Bindings, Message).
                                                                                  Select(relation => new BindingManagementEntryDataType {
                                                                                                         BindingId      = relation.Id,
                                                                                                         ClientAddress  = relation.ClientAddress.Clone(),
                                                                                                         ServerAddress  = relation.ServerAddress.Clone()
                                                                                                     })]
                                                        }
                                                    },
                                                    CancellationToken);

                case DestinationListData when Message.CmdClassifier == CmdClassifierType.Read:
                    return await ReplyWithRelations(Message,
                                                    new CmdType {
                                                        NodeManagementDestinationListData = new NodeManagementDestinationListDataType {
                                                            NodeManagementDestinationData = [
                                                                new NodeManagementDestinationDataType {
                                                                    DeviceDescription = Device.Information().Description
                                                                }
                                                            ]
                                                        }
                                                    },
                                                    CancellationToken);

            }

            return await base.HandleMessage(Message, CancellationToken);

        }

        #endregion

        #region (private) HandleDetailedDiscovery(Message, CancellationToken)

        /// <summary>
        /// The detailed discovery: answering it, and taking over the answer of
        /// another device.
        /// </summary>
        private async Task<ResultDataType?> HandleDetailedDiscovery(SPINEMessage       Message,
                                                                    CancellationToken  CancellationToken)
        {

            #region Somebody wants to know what this device is

            if (Message.CmdClassifier == CmdClassifierType.Read)
            {

                var cmd = new CmdType {
                              NodeManagementDetailedDiscoveryData = new NodeManagementDetailedDiscoveryDataType {

                                  SpecificationVersionList  = new NodeManagementSpecificationVersionListType {
                                                                  SpecificationVersion = [ Version.String ]
                                                              },

                                  DeviceInformation         = Device.Information(),

                                  EntityInformation         = [.. Device.Entities.
                                                                    OrderBy(entity => String.Join(',', entity.EntityId)).
                                                                    Select (entity => entity.Information())],

                                  FeatureInformation        = [.. Device.Entities.
                                                                    OrderBy(entity  => String.Join(',', entity.EntityId)).
                                                                    SelectMany(entity => entity.Features).
                                                                    Select (feature => feature.Information())]

                              }
                          };

                await Message.RemoteDevice.Sender.Reply(Message.RequestHeader,
                                                        Address,
                                                        cmd,
                                                        CancellationToken);

                return null;

            }

            #endregion

            if (Message.Data is not NodeManagementDetailedDiscoveryDataType data)
                return ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported,
                                            "The detailed discovery carries no data.");

            var remoteDevice = Message.RemoteDevice;

            #region Who the other device says it is

            if (data.DeviceInformation?.Description is NetworkManagementDeviceDescriptionDataType description)
            {

                remoteDevice.DeviceAddress  = description.DeviceAddress?.Device ?? remoteDevice.DeviceAddress;
                remoteDevice.DeviceType     = description.DeviceType            ?? remoteDevice.DeviceType;
                remoteDevice.FeatureSet     = description.NetworkFeatureSet     ?? remoteDevice.FeatureSet;

            }

            #endregion

            // A reply, or a notify without a filter, is the whole device: what
            // it no longer lists, it no longer has. A partial notify says only
            // what changed, entity by entity (SPINE 1.3.0, 7.1.1.5).
            var isComplete = Message.CmdClassifier == CmdClassifierType.Reply ||
                             Message.PartialFilter is null;

            if (isComplete)
                foreach (var entity in remoteDevice.Entities.ToArray())
                    if (!(data.EntityInformation ?? []).Any(
                            information => SPINEAddresses.EntitiesAreEqual(information.Description?.EntityAddress?.Entity,
                                                                           entity.EntityId)))
                        Forget(remoteDevice, entity);

            foreach (var information in data.EntityInformation ?? [])
            {

                if (information.Description?.EntityAddress?.Entity is not List<UInt32> entityId)
                    continue;

                if (information.Description.LastStateChange == NetworkManagementStateChangeType.Removed)
                {

                    if (remoteDevice.Entity(entityId) is SPINERemoteEntity gone)
                        Forget(remoteDevice, gone);

                    continue;

                }

                var entity = remoteDevice.GetOrAddEntity(entityId,
                                                         information.Description.EntityType ?? EntityTypeType.Generic);

                entity.Description = information.Description.Description;

                foreach (var featureInformation in data.FeatureInformation ?? [])
                {

                    var featureDescription = featureInformation.Description;

                    if (featureDescription?.FeatureAddress?.Feature is not UInt32 featureId ||
                        !SPINEAddresses.EntitiesAreEqual(featureDescription.FeatureAddress.Entity, entityId))
                        continue;

                    var feature = entity.GetOrAddFeature(featureId,
                                                         featureDescription.FeatureType ?? FeatureTypeType.Generic,
                                                         featureDescription.Role        ?? RoleType.Server);

                    feature.Description       = featureDescription.Description;
                    feature.MaxResponseDelay  = featureDescription.MaxResponseDelay?.AsTimeSpan;

                    feature.SetOperations(featureDescription.SupportedFunction);

                }

                Device.Events.Publish(timestamp => new SPINEEntityChanged(timestamp, entity, true));

            }

            if (isComplete)
                Device.Events.Publish(timestamp => new SPINEDeviceDiscovered(timestamp, remoteDevice));

            if (Message.RequestHeader.MsgCounterReference is UInt64 reference)
            {

                remoteDevice.Sender.ResponseReceived(reference);

                Answered(new SPINEResponse(reference,
                                           Message.Function,
                                           data,
                                           null,
                                           Message.RemoteFeature));

            }

            return null;

        }


        /// <summary>
        /// Forget an entity of another device, and everything which was agreed
        /// with it.
        /// </summary>
        private void Forget(SPINERemoteDevice  RemoteDevice,
                            SPINERemoteEntity  Entity)
        {

            foreach (var feature in Entity.Features)
                foreach (var relations in new[] { Device.Subscriptions, Device.Bindings })
                    foreach (var relation in relations.All)
                        if (SPINEAddresses.AreEqual(relation.ClientAddress, feature.Address) ||
                            SPINEAddresses.AreEqual(relation.ServerAddress, feature.Address))
                            relations.Remove(relation.ClientAddress, relation.ServerAddress);

            RemoteDevice.RemoveEntity(Entity.EntityId);

            Device.Events.Publish(timestamp => new SPINEEntityChanged(timestamp, Entity, false));

        }

        #endregion

        #region (private) HandleRelationCall(...) / ReplyWithRelations(...)

        /// <summary>
        /// Another device asks for a subscription or a binding, or gives one up.
        ///
        /// The device part of an address may be missing: SPINE 1.3.0, 7.5.2 and
        /// 7.6.2 say that the receiver then has to work out who is meant. For a
        /// request that is unambiguous - only a client asks, so the server is
        /// the receiver. For a deletion either side may ask, so the one which is
        /// missing is the receiver.
        /// </summary>
        private ResultDataType? HandleRelationCall(SPINEMessage           Message,
                                                   SPINEFeatureRelations  Relations,
                                                   FeatureAddressType?    ClientAddress,
                                                   FeatureAddressType?    ServerAddress,
                                                   String                 What,
                                                   Boolean                Adding)
        {

            if (Message.CmdClassifier != CmdClassifierType.Call)
                return ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported,
                                            $"A {What} is asked for with a call, not with a {Message.CmdClassifier}.");

            if (ClientAddress is null || ServerAddress is null)
                return ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                            $"The {What} names no client or no server feature.");

            var client = ClientAddress.Clone();
            var server = ServerAddress.Clone();

            client.Device ??= Adding ? Message.RemoteDevice.DeviceAddress : Device.DeviceAddress;
            server.Device ??= Adding ? Device.DeviceAddress               : Message.RemoteDevice.DeviceAddress;

            if (Adding && Device.Feature(server) is null)
                return ResultDataType.Error(SPINEErrorNumbers.DestinationUnknown,
                                            $"This device has no feature {server}.");

            if (Adding)
            {

                var relation = Relations.Add(client, server);

                Device.Events.Publish(timestamp => new SPINERelationChanged(timestamp, relation, What, true));

            }

            else
            {

                var relation = new SPINEFeatureRelation(0, client, server);

                if (!Relations.Remove(client, server))
                    return ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                $"There is no {What} from {client} to {server}.");

                Device.Events.Publish(timestamp => new SPINERelationChanged(timestamp, relation, What, false));

            }

            return null;

        }


        /// <summary>
        /// Answer a read of the subscriptions, the bindings or the destination
        /// list.
        /// </summary>
        private async Task<ResultDataType?> ReplyWithRelations(SPINEMessage       Message,
                                                               CmdType            Cmd,
                                                               CancellationToken  CancellationToken)
        {

            await Message.RemoteDevice.Sender.Reply(Message.RequestHeader,
                                                    Address,
                                                    Cmd,
                                                    CancellationToken);

            return null;

        }


        /// <summary>
        /// The relations which concern the device asking for them: SPINE 1.3.0,
        /// 7.5.3 answers a read of the subscriptions with the ones of the asking
        /// device, not with all of them.
        /// </summary>
        private static IEnumerable<SPINEFeatureRelation> Relations(SPINEFeatureRelations  Relations,
                                                                   SPINEMessage           Message)

            => Relations.All.Where(relation => String.Equals(relation.ClientAddress.Device,
                                                             Message.RemoteDevice.DeviceAddress,
                                                             StringComparison.OrdinalIgnoreCase) ||
                                               String.Equals(relation.ServerAddress.Device,
                                                             Message.RemoteDevice.DeviceAddress,
                                                             StringComparison.OrdinalIgnoreCase));

        #endregion

    }

}
