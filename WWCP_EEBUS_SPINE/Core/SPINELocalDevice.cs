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

using Newtonsoft.Json.Linq;

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// This device.
    ///
    /// It holds the entities and features we offer, the other devices we are
    /// talking to, and the two registries which decide what those devices may do
    /// with us. Every datagram which arrives goes through
    /// <see cref="ProcessDatagram(DatagramType, SPINERemoteDevice, CancellationToken)"/>,
    /// which is where the addressing, the permissions and the routing are - and
    /// where every refusal turns into the SPINE result the sender expects.
    /// </summary>
    public class SPINELocalDevice
    {

        #region Data

        private readonly ConcurrentDictionary<String, SPINELocalEntity>  entities       = new (StringComparer.Ordinal);

        private readonly ConcurrentDictionary<String, SPINERemoteDevice> remoteDevices  = new (StringComparer.Ordinal);

        private          UInt32                                          nextEntityId   = 1;

        #endregion

        #region Properties

        /// <summary>
        /// The address of this device.
        /// </summary>
        public String                            DeviceAddress    { get; }

        /// <summary>
        /// Which kind of device this is.
        /// </summary>
        public DeviceTypeType                    DeviceType       { get; }

        /// <summary>
        /// Which network management feature set this device has.
        /// </summary>
        public NetworkManagementFeatureSetType   FeatureSet       { get; }

        /// <summary>
        /// The entities of this device.
        /// </summary>
        public IEnumerable<SPINELocalEntity>     Entities
            => entities.Values;

        /// <summary>
        /// The other devices we are talking to.
        /// </summary>
        public IEnumerable<SPINERemoteDevice>    RemoteDevices
            => remoteDevices.Values;

        /// <summary>
        /// Who wants to be told when the data of one of our features changes
        /// (SPINE 1.3.0, 7.5).
        /// </summary>
        public SPINEFeatureRelations             Subscriptions    { get; } = new ();

        /// <summary>
        /// Who may change the data of one of our features (SPINE 1.3.0, 7.6).
        /// </summary>
        public SPINEFeatureRelations             Bindings         { get; } = new ();

        /// <summary>
        /// The entity every device has, which carries node management.
        /// </summary>
        public SPINELocalEntity                  DeviceInformation    { get; }

        /// <summary>
        /// The node management feature of this device, which every device has at
        /// entity 0, feature 0 (SPINE 1.3.0, 7.1).
        /// </summary>
        public SPINENodeManagement               NodeManagement       { get; }

        /// <summary>
        /// What this device tells the world about itself.
        /// </summary>
        public SPINEEvents                       Events               { get; }

        /// <summary>
        /// Where the time comes from. Everything with a timestamp or a timer
        /// within the SPINE core asks this, and nothing asks the clock.
        /// </summary>
        public TimeProvider                      TimeProvider         { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create this device.
        /// </summary>
        /// <param name="DeviceAddress">The address of this device, i.e. "d:_i:19667_HEMS".</param>
        /// <param name="DeviceType">Which kind of device it is.</param>
        /// <param name="FeatureSet">Which network management feature set it has.</param>
        /// <param name="TimeProvider">Where the time comes from. The system clock by default; the tests use a fake one.</param>
        public SPINELocalDevice(String                            DeviceAddress,
                                DeviceTypeType                    DeviceType,
                                NetworkManagementFeatureSetType?  FeatureSet     = null,
                                TimeProvider?                     TimeProvider   = null)
        {

            this.DeviceAddress      = DeviceAddress;
            this.DeviceType         = DeviceType;
            this.FeatureSet         = FeatureSet ?? NetworkManagementFeatureSetType.Smart;
            this.TimeProvider       = TimeProvider ?? System.TimeProvider.System;
            this.Events             = new SPINEEvents(this.TimeProvider);

            // Every device has entity 0 with node management on feature 0
            // (SPINE 1.3.0, 7.1). Which functions it offers is filled in by the
            // node management itself.
            this.DeviceInformation  = AddEntity([ SPINEAddresses.NodeManagementEntity ],
                                                EntityTypeType.DeviceInformation);

            this.NodeManagement     = DeviceInformation.AddFeature(
                                          (id, entity) => new SPINENodeManagement(id, entity)
                                      );

        }

        #endregion


        #region AddEntity(EntityId, EntityType) / AddEntity(EntityType)

        /// <summary>
        /// Add an entity with the given address.
        /// </summary>
        /// <param name="EntityId">The path of numbers naming it below this device.</param>
        /// <param name="EntityType">Which kind of entity it is.</param>
        public SPINELocalEntity AddEntity(IEnumerable<UInt32>  EntityId,
                                          EntityTypeType       EntityType)
        {

            var entity = new SPINELocalEntity(this, EntityId, EntityType);

            entities[KeyOf(entity.EntityId)] = entity;

            return entity;

        }


        /// <summary>
        /// Add an entity directly below this device. It gets the next free
        /// number, counted from 1: entity 0 is the device information.
        /// </summary>
        /// <param name="EntityType">Which kind of entity it is.</param>
        public SPINELocalEntity AddEntity(EntityTypeType EntityType)

            => AddEntity([ Interlocked.Increment(ref nextEntityId) - 1 ],
                         EntityType);

        #endregion

        #region Entity(EntityId) / Feature(Address)

        /// <summary>
        /// The entity with the given address, or null when this device has none.
        /// </summary>
        /// <param name="EntityId">The path of numbers naming the entity.</param>
        public SPINELocalEntity? Entity(IEnumerable<UInt32>? EntityId)

            => EntityId is null
                   ? null
                   : entities.GetValueOrDefault(KeyOf(EntityId));


        /// <summary>
        /// The feature with the given address, or null when this device has
        /// none.
        ///
        /// The device part of the address is not compared: a message may address
        /// us before it knows our name, and the SHIP connection has already said
        /// who it reached.
        /// </summary>
        /// <param name="Address">The address of a feature.</param>
        public SPINELocalFeature? Feature(FeatureAddressType? Address)

            => Address?.Feature is UInt32 id
                   ? Entity(Address.Entity)?.Feature(id)
                   : null;


        /// <summary>
        /// Give up an entity of this device.
        ///
        /// Whoever agreed to something with one of its features is not told
        /// here: that is a notify of the detailed discovery, and it has to go
        /// out before the entity is gone
        /// (<see cref="SPINENodeManagement.NotifyEntityRemoved"/>).
        /// </summary>
        /// <param name="EntityId">The path of numbers naming the entity.</param>
        public Boolean RemoveEntity(IEnumerable<UInt32> EntityId)
        {

            var key = KeyOf(EntityId);

            // Entity 0 is what makes this a SPINE device.
            if (key == KeyOf([ SPINEAddresses.NodeManagementEntity ]))
                return false;

            return entities.TryRemove(key, out _);

        }

        #endregion


        #region AddRemoteDevice(SKI, Writer, MsgCounter = null) / RemoveRemoteDevice(SKI)

        /// <summary>
        /// Start talking to another device.
        /// </summary>
        /// <param name="SKI">The subject key identifier of its certificate.</param>
        /// <param name="Writer">Where datagrams to it go.</param>
        /// <param name="MsgCounter">Where the message counters come from.</param>
        public SPINERemoteDevice AddRemoteDevice(String         SKI,
                                                 ISPINEWriter   Writer,
                                                 Func<UInt64>?  MsgCounter   = null)
        {

            var device = new SPINERemoteDevice(SKI, Writer, MsgCounter);

            remoteDevices[SKI] = device;

            return device;

        }


        /// <summary>
        /// Stop talking to another device, and give up every subscription and
        /// every binding with it.
        /// </summary>
        /// <param name="SKI">The subject key identifier of its certificate.</param>
        public Boolean RemoveRemoteDevice(String SKI)
        {

            if (!remoteDevices.TryRemove(SKI, out var device))
                return false;

            Subscriptions.RemoveAllOf(device.DeviceAddress);
            Bindings.     RemoveAllOf(device.DeviceAddress);

            return true;

        }


        /// <summary>
        /// The other device with the given subject key identifier, or null when
        /// we are not talking to it.
        /// </summary>
        /// <param name="SKI">The subject key identifier of a certificate.</param>
        public SPINERemoteDevice? RemoteDeviceForSKI(String SKI)

            => remoteDevices.GetValueOrDefault(SKI);


        /// <summary>
        /// The other device with the given address, or null when we are not
        /// talking to it.
        /// </summary>
        /// <param name="Address">The address of a device.</param>
        public SPINERemoteDevice? RemoteDeviceForAddress(String? Address)

            => Address is null
                   ? null
                   : remoteDevices.Values.FirstOrDefault(
                         device => String.Equals(device.DeviceAddress, Address, StringComparison.OrdinalIgnoreCase));

        #endregion


        #region ProcessDatagram(JSON, RemoteDevice, CancellationToken = default)

        /// <summary>
        /// Act on a datagram which arrived as JSON, which is how the SHIP layer
        /// hands it over.
        /// </summary>
        /// <param name="JSON">The payload of a SHIP data message.</param>
        /// <param name="RemoteDevice">The device it came from.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <returns>Null when the datagram was handled, the result which was sent back otherwise.</returns>
        public async Task<ResultDataType?> ProcessDatagram(JObject            JSON,
                                                           SPINERemoteDevice  RemoteDevice,
                                                           CancellationToken  CancellationToken   = default)
        {

            DatagramType? datagram;

            try
            {
                datagram = SPINEJSON.Read<DatagramType>(JSON["datagram"] ?? JSON);
            }
            catch (Exception e)
            {
                // Nothing can be answered here: without a header there is no
                // message counter to refer to and no address to answer.
                return ResultDataType.Error(SPINEErrorNumbers.GeneralError,
                                            $"The datagram could not be read: {e.Message}");
            }

            return datagram is null
                       ? ResultDataType.Error(SPINEErrorNumbers.GeneralError, "The datagram is empty.")
                       : await ProcessDatagram(datagram, RemoteDevice, CancellationToken);

        }

        #endregion

        #region ProcessDatagram(Datagram, RemoteDevice, CancellationToken = default)

        /// <summary>
        /// Act on a datagram.
        ///
        /// Everything which can be refused is refused here, in the order the
        /// specification puts it, and every refusal is sent back as a result -
        /// a device which asks something it may not ask has to be told, or it
        /// will wait forever.
        /// </summary>
        /// <param name="Datagram">The datagram.</param>
        /// <param name="RemoteDevice">The device it came from.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <returns>Null when the datagram was handled, the result which was sent back otherwise.</returns>
        public async Task<ResultDataType?> ProcessDatagram(DatagramType       Datagram,
                                                           SPINERemoteDevice  RemoteDevice,
                                                           CancellationToken  CancellationToken   = default)
        {

            var header = Datagram.Header;

            #region The datagram has to have a header, a source, a classifier and one command

            if (header is null)
                return Refused(Datagram, null, RemoteDevice,
                               ResultDataType.Error(SPINEErrorNumbers.GeneralError, "The datagram has no header."),
                               CancellationToken);

            if (header.AddressSource is null)
                return Refused(Datagram, header, RemoteDevice,
                               ResultDataType.Error(SPINEErrorNumbers.GeneralError, "The datagram says nothing about where it came from."),
                               CancellationToken);

            if (Datagram.Payload?.Cmd is not [ var cmd, .. ])
                return await Refuse(Datagram, header, RemoteDevice, null,
                                    ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported, "The datagram carries no command."),
                                    CancellationToken);

            if (header.CmdClassifier is not CmdClassifierType cmdClassifier)
                return await Refuse(Datagram, header, RemoteDevice, null,
                                    ResultDataType.Error(SPINEErrorNumbers.DestinationUnknown, "The datagram does not say what kind of message it is."),
                                    CancellationToken);

            #endregion

            #region The feature it came from, and the one it is addressed to

            var remoteFeature = RemoteDevice.Feature(header.AddressSource);

            // A device may talk to us before we have discovered it - the
            // detailed discovery itself arrives that way. Remembering the
            // feature it came from is better than refusing to listen.
            remoteFeature ??= RemoteDevice.
                                  GetOrAddEntity(header.AddressSource.Entity ?? [],
                                                 EntityTypeType.Generic).
                                  GetOrAddFeature(header.AddressSource.Feature ?? 0,
                                                  FeatureTypeType.Generic,
                                                  RoleType.Special);

            var localFeature = Feature(header.AddressDestination);

            if (localFeature is null)
                return await Refuse(Datagram, header, RemoteDevice, null,
                                    ResultDataType.Error(SPINEErrorNumbers.DestinationUnknown,
                                                         $"This device has no feature {header.AddressDestination}."),
                                    CancellationToken);

            #endregion

            #region What the command is about

            var partialFilter  = cmd.Filter?.FirstOrDefault(filter => filter.IsPartial);
            var deleteFilter   = cmd.Filter?.FirstOrDefault(filter => filter.IsDelete);
            var dataFunction   = cmd.DataFunction;
            var function       = cmd.Function?.ToString() ?? dataFunction;

            // SPINE 1.3.0, 5.3.4.1: the function name shall be stated if and
            // only if any other cmdOption is used. Where a filter is present and
            // the name disagrees with the payload, the message says two
            // different things about which data type it carries - and picking
            // one of them is how a filter ends up being applied to the wrong
            // type.
            if ((partialFilter is not null || deleteFilter is not null) &&
                cmd.Function is null)
                return await Refuse(Datagram, header, RemoteDevice, localFeature,
                                    ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                         "The command uses a filter, but does not state its function (SPINE 1.3.0, 5.3.4.1)."),
                                    CancellationToken);

            if (cmd.Function is not null &&
                dataFunction is not null &&
                dataFunction != cmd.Function.ToString())
                return await Refuse(Datagram, header, RemoteDevice, localFeature,
                                    ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                         $"The command says it is about '{cmd.Function}', but carries '{dataFunction}'."),
                                    CancellationToken);

            foreach (var filter in cmd.Filter ?? [])
                if (filter.FilterFunction is String filterFunction &&
                    filterFunction != function)
                    return await Refuse(Datagram, header, RemoteDevice, localFeature,
                                        ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                             $"The command is about '{function}', but a filter of it is about '{filterFunction}'."),
                                        CancellationToken);

            #endregion

            #region A write has to be offered, and needs a binding

            if (cmdClassifier == CmdClassifierType.Write)
            {

                if (function is null)
                    return await Refuse(Datagram, header, RemoteDevice, localFeature,
                                        ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported, "The write carries no function."),
                                        CancellationToken);

                var functionData = localFeature.FunctionData(function);

                if (functionData is null || !functionData.Operations.CanWrite)
                    return await Refuse(Datagram, header, RemoteDevice, localFeature,
                                        ResultDataType.Error(SPINEErrorNumbers.CommandNotSupported,
                                                             functionData is null
                                                                 ? $"This feature does not have the function '{function}'."
                                                                 : $"The function '{function}' may not be written."),
                                        CancellationToken);

                // SPINE 1.3.0, 7.6: a client may only write to a server feature
                // it is bound to.
                if (!Bindings.Has(remoteFeature.Address, localFeature.Address))
                    return await Refuse(Datagram, header, RemoteDevice, localFeature,
                                        ResultDataType.Error(SPINEErrorNumbers.BindingIsNecessaryForThisCommand,
                                                             $"There is no binding from {remoteFeature.Address} to {localFeature.Address}."),
                                        CancellationToken);

            }

            #endregion

            var message = new SPINEMessage(header,
                                           cmd,
                                           cmdClassifier,
                                           function,
                                           function is not null ? cmd.GetData(function) : null,
                                           partialFilter,
                                           deleteFilter,
                                           remoteFeature);

            var error = await localFeature.HandleMessage(message, CancellationToken);

            if (error is not null)
            {

                // A result is never answered with a result: that is how two
                // devices talk to each other until one of them gives up.
                if (cmdClassifier != CmdClassifierType.Result)
                    return await Refuse(Datagram, header, RemoteDevice, localFeature, error, CancellationToken);

                Events.Publish(timestamp => new SPINEDatagramRefused(timestamp, Datagram, error));

                return error;

            }

            // SPINE 1.3.0, 5.2.4: acknowledge where the sender asked for it.
            // A write acknowledges itself, once it is done.
            if (header.AckRequest == true &&
                cmdClassifier != CmdClassifierType.Result &&
                cmdClassifier != CmdClassifierType.Read)
                await RemoteDevice.Sender.Result(header,
                                                 localFeature.Address,
                                                 null,
                                                 CancellationToken);

            return null;

        }

        #endregion

        #region NotifySubscribers(Feature, Cmd, CancellationToken = default)

        /// <summary>
        /// Tell everybody who subscribed to the given feature what its data is
        /// now (SPINE 1.3.0, 7.5).
        /// </summary>
        /// <param name="Feature">One of our features.</param>
        /// <param name="Cmd">The command carrying its data.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task NotifySubscribers(SPINELocalFeature  Feature,
                                            CmdType            Cmd,
                                            CancellationToken  CancellationToken   = default)
        {

            foreach (var subscriber in Subscriptions.ClientsOf(Feature.Address))
            {

                var remoteDevice = RemoteDeviceForAddress(subscriber.Device);

                if (remoteDevice is null)
                    continue;

                await remoteDevice.Sender.Notify(Feature.Address,
                                                 subscriber,
                                                 Cmd,
                                                 CancellationToken);

            }

        }

        #endregion

        #region (internal) Published(Change)

        /// <summary>
        /// Tell whoever is listening that the data of a function changed.
        /// </summary>
        internal void Published(SPINEDataChange Change)
        {
            Events.Publish(timestamp => new SPINEDataChanged(timestamp, Change));
        }

        #endregion


        #region (private) Refuse(...) / Refused(...)

        /// <summary>
        /// Send the given result back and answer with it.
        /// </summary>
        private async Task<ResultDataType> Refuse(DatagramType        Datagram,
                                                  HeaderType          Header,
                                                  SPINERemoteDevice   RemoteDevice,
                                                  SPINELocalFeature?  LocalFeature,
                                                  ResultDataType      Error,
                                                  CancellationToken   CancellationToken)
        {

            Events.Publish(timestamp => new SPINEDatagramRefused(timestamp, Datagram, Error));

            await RemoteDevice.Sender.Result(Header,
                                             LocalFeature?.Address ?? Header.AddressDestination ?? new FeatureAddressType { Device = DeviceAddress },
                                             Error,
                                             CancellationToken);

            return Error;

        }


        /// <summary>
        /// Answer with the given result without sending anything: there is
        /// nothing to send it to.
        /// </summary>
        private ResultDataType Refused(DatagramType       Datagram,
                                       HeaderType?        Header,
                                       SPINERemoteDevice  RemoteDevice,
                                       ResultDataType     Error,
                                       CancellationToken  CancellationToken)
        {

            Events.Publish(timestamp => new SPINEDatagramRefused(timestamp, Datagram, Error));

            return Error;

        }

        #endregion

        #region (private static) KeyOf(EntityId)

        private static String KeyOf(IEnumerable<UInt32> EntityId)

            => String.Join(',', EntityId);

        #endregion

        #region Information()

        /// <summary>
        /// This device as the detailed discovery states it.
        /// </summary>
        public NodeManagementDetailedDiscoveryDeviceInformationType Information()

            => new () {
                   Description = new NetworkManagementDeviceDescriptionDataType {
                                     DeviceAddress      = new DeviceAddressType { Device = DeviceAddress },
                                     DeviceType         = DeviceType,
                                     NetworkFeatureSet  = FeatureSet
                                 }
               };

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this device.
        /// </summary>
        public override String ToString()

            => $"{DeviceAddress} ({DeviceType}), {entities.Count} entities, {remoteDevices.Count} partners";

        #endregion

    }

}
