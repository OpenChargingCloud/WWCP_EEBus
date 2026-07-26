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
using cloud.charging.open.protocols.EEBUS.UseCases.Monitoring;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.EVSOC
{

    /// <summary>
    /// The car of "EV State of Charge" - the side which knows how full its
    /// battery is.
    ///
    /// The measuring half is the shared work of every monitoring use case (see
    /// <see cref="AMonitoredDevice"/>), minus the electrical connection
    /// parameter descriptions, which this use case does not have. What this adds
    /// is scenario 2: the nominal capacity of the battery, which is not measured
    /// but is a fixed characteristic of the electrical connection.
    /// </summary>
    public class EVSOCElectricVehicle : AMonitoredDevice
    {

        #region Data

        private readonly UInt32  electricalConnectionId  = 0;
        private readonly UInt32  parameterId             = 0;
        private readonly UInt32  characteristicId;

        #endregion

        #region Properties

        /// <summary>
        /// The electrical connection server feature, which holds the nominal
        /// capacity of the battery - or null when this car does not publish it
        /// (scenario 2).
        /// </summary>
        public SPINELocalFeature?  Capacity    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the EV of EVSOC to an entity.
        ///
        /// Scenario 1 is mandatory and always there: a car which cannot say how
        /// full it is is not playing this use case. The rest is what this
        /// particular car happens to know about itself.
        /// </summary>
        /// <param name="Entity">The entity of the car.</param>
        /// <param name="NominalCapacity">The usable capacity of the battery in watt hours, or null when the car does not publish it (scenario 2).</param>
        /// <param name="StateOfHealth">Whether the car publishes the state of health of its battery (scenario 3).</param>
        /// <param name="TravelRange">Whether the car publishes its remaining travel range (scenario 4).</param>
        public EVSOCElectricVehicle(SPINELocalEntity  Entity,
                                    Decimal?          NominalCapacity   = null,
                                    Boolean           StateOfHealth     = false,
                                    Boolean           TravelRange       = false)

            : base(Entity,
                   EVStateOfCharge.Profile,
                   Measures(StateOfHealth, TravelRange),

                   // Nothing here is on a phase, so the list is empty rather
                   // than the usual three.
                   Phases:        [],

                   // Scenario 2 is supported without being measured: a nominal
                   // capacity is a property of the battery, not a reading.
                   AlsoSupports:  NominalCapacity.HasValue
                                      ? [ EVStateOfCharge.ScenarioNominalCapacity ]
                                      : null)

        {

            if (!NominalCapacity.HasValue)
                return;

            #region The electrical connection server: the nominal capacity (scenario 2)

            Capacity = Entity.Feature(FeatureTypeType.ElectricalConnection, RoleType.Server)
                           ?? Entity.AddFeature(FeatureTypeType.ElectricalConnection, RoleType.Server);

            Capacity.AddFunction(EVStateOfCharge.CharacteristicListData,
                                 Read:         true,
                                 PartialRead:  true);

            // The car may already be the server of the electricity measurement
            // use case, which puts its own parameters on this very feature.
            var characteristics = Capacity.DataCopy<ElectricalConnectionCharacteristicListDataType>(EVStateOfCharge.CharacteristicListData)?.
                                      ElectricalConnectionCharacteristicData?.ToList() ?? [];

            characteristicId = UseCaseIds.NextFree(characteristics.Select(characteristic => characteristic.CharacteristicId),
                                                   StartingAt: 0);

            characteristics.Add(new ElectricalConnectionCharacteristicDataType {
                                    ElectricalConnectionId  = electricalConnectionId,
                                    ParameterId             = parameterId,
                                    CharacteristicId        = characteristicId,

                                    // Of the car as a whole rather than of one
                                    // battery: section 2.1 asks a car with
                                    // several of them to add them up first.
                                    CharacteristicContext   = ElectricalConnectionCharacteristicContextType.Entity,
                                    CharacteristicType      = ElectricalConnectionCharacteristicTypeType.EnergyCapacityNominalMax,
                                    Value                   = ScaledNumberType.FromValue(NominalCapacity.Value),
                                    Unit                    = UnitOfMeasurementType.Wh
                                });

            Capacity.FunctionData(EVStateOfCharge.CharacteristicListData)!.SetData(
                new ElectricalConnectionCharacteristicListDataType {
                    ElectricalConnectionCharacteristicData = characteristics
                }
            );

            #endregion

        }


        /// <summary>
        /// Which quantities a car with these measurements publishes. The nominal
        /// capacity is not among them - it is not measured.
        /// </summary>
        private static IEnumerable<MonitoringQuantity> Measures(Boolean  StateOfHealth,
                                                                Boolean  TravelRange)
        {

            var quantities = new List<MonitoringQuantity> {
                                 EVStateOfCharge.StateOfCharge
                             };

            if (StateOfHealth)
                quantities.Add(EVStateOfCharge.StateOfHealth);

            if (TravelRange)
                quantities.Add(EVStateOfCharge.TravelRange);

            return quantities;

        }

        #endregion


        #region NominalCapacity / SetNominalCapacity(Capacity, ...)

        /// <summary>
        /// The usable capacity of the battery in watt hours, or null when this
        /// car does not publish it (scenario 2).
        /// </summary>
        public Decimal? NominalCapacity

            => Capacity?.DataCopy<ElectricalConnectionCharacteristicListDataType>(EVStateOfCharge.CharacteristicListData)?.
                   ElectricalConnectionCharacteristicData?.
                   FirstOrDefault(characteristic => characteristic.CharacteristicId == characteristicId)?.
                   Value?.Value;


        /// <summary>
        /// Publish a new nominal capacity, and tell whoever subscribed.
        ///
        /// Rarely needed and not never: the usable capacity of a battery is what
        /// the battery management system currently believes it to be, and that
        /// belief changes with age and with a firmware update.
        /// </summary>
        /// <param name="Capacity">The usable capacity in watt hours.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">When this car does not support scenario 2.</exception>
        public async Task SetNominalCapacity(Decimal            Capacity,
                                             CancellationToken  CancellationToken   = default)
        {

            if (this.Capacity is null)
                throw new InvalidOperationException("This electric vehicle does not support scenario 2 of the EV state of charge.");

            var data = this.Capacity.DataCopy<ElectricalConnectionCharacteristicListDataType>(EVStateOfCharge.CharacteristicListData)
                           ?? new ElectricalConnectionCharacteristicListDataType { ElectricalConnectionCharacteristicData = [] };

            data.ElectricalConnectionCharacteristicData ??= [];

            var mine = data.ElectricalConnectionCharacteristicData.
                           FirstOrDefault(characteristic => characteristic.CharacteristicId == characteristicId);

            if (mine is null)
                return;

            mine.Value = ScaledNumberType.FromValue(Capacity);

            await this.Capacity.SetData(EVStateOfCharge.CharacteristicListData,
                                        data,
                                        CancellationToken: CancellationToken);

        }

        #endregion

    }

}
