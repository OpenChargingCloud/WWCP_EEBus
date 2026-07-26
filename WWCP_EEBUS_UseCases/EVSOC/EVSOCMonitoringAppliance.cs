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
    /// The monitoring appliance of "EV State of Charge" - the side which shows
    /// the state of a car's battery to a person.
    ///
    /// Reading the measurements is the shared work of every monitoring use case
    /// (see <see cref="AMonitoringAppliance"/>); what this adds is scenario 2,
    /// which is read from the electrical connection feature as a characteristic
    /// rather than from the measurement one as a reading.
    /// </summary>
    public class EVSOCMonitoringAppliance : AMonitoringAppliance
    {

        #region Properties

        /// <summary>
        /// Whether this appliance also watches the nominal capacity of the
        /// battery (scenario 2).
        /// </summary>
        public Boolean  WatchesCapacity    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the monitoring appliance of EVSOC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which watches.</param>
        /// <param name="Scenarios">Which scenarios it is interested in. Scenario 1 is always included.</param>
        /// <param name="AnnounceAsCEM">
        /// Whether to announce the actor as "CEM" rather than
        /// "MonitoringAppliance". The specification says MonitoringAppliance;
        /// the Go reference implementation says CEM.
        /// </param>
        public EVSOCMonitoringAppliance(SPINELocalEntity      Entity,
                                        IEnumerable<UInt32>?  Scenarios       = null,
                                        Boolean               AnnounceAsCEM   = false)

            : base(Entity,
                   EVStateOfCharge.Profile,
                   Scenarios ?? [ EVStateOfCharge.ScenarioNominalCapacity,
                                  EVStateOfCharge.ScenarioStateOfHealth,
                                  EVStateOfCharge.ScenarioTravelRange ],
                   AnnounceAsAlternateActor: AnnounceAsCEM)

        {

            WatchesCapacity = this.Scenarios.Any(scenario => scenario.Number == EVStateOfCharge.ScenarioNominalCapacity);

        }

        #endregion


        #region CapacityOf(Partner)

        /// <summary>
        /// The electrical connection of a car, which holds the nominal capacity
        /// of its battery - or null when this appliance does not watch it or the
        /// car does not publish it.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public UseCaseFeature? CapacityOf(SPINERemoteEntity Partner)

            => WatchesCapacity &&
               Partner.Feature(FeatureTypeType.ElectricalConnection, RoleType.Server) is not null

                   ? new (FeatureTypeType.ElectricalConnection, Entity, Partner)
                   : null;

        #endregion

        #region (override) Subscribe(Partner, CancellationToken = default)

        /// <summary>
        /// Read what the car publishes, and ask to be told when it changes.
        ///
        /// The measurements as in every monitoring use case, and - for an
        /// appliance which watches scenario 2 - the nominal capacity of the
        /// battery as well. It changes about as often as a firmware update, but
        /// a subscription is still the right way to hear about it.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public override async Task Subscribe(SPINERemoteEntity  Partner,
                                             CancellationToken  CancellationToken   = default)
        {

            await base.Subscribe(Partner, CancellationToken);

            if (CapacityOf(Partner) is not UseCaseFeature capacity)
                return;

            await capacity.RequestData(EVStateOfCharge.CharacteristicListData, CancellationToken: CancellationToken);
            await capacity.Subscribe(CancellationToken);

        }

        #endregion


        #region StateOfCharge(Partner) / StateOfHealth(Partner) / TravelRange(Partner) / NominalCapacity(Partner)

        /// <summary>
        /// How much of the usable capacity of the car's battery has been
        /// charged, in per cent (scenario 1).
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public Decimal? StateOfCharge(SPINERemoteEntity Partner)

            => Read(Partner, EVStateOfCharge.StateOfCharge)?.Value;


        /// <summary>
        /// How healthy the battery of the car still is, in per cent
        /// (scenario 3).
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public Decimal? StateOfHealth(SPINERemoteEntity Partner)

            => Read(Partner, EVStateOfCharge.StateOfHealth)?.Value;


        /// <summary>
        /// How far the car can still travel, in metres (scenario 4).
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public Decimal? TravelRange(SPINERemoteEntity Partner)

            => Read(Partner, EVStateOfCharge.TravelRange)?.Value;


        /// <summary>
        /// The usable capacity of the car's battery in watt hours, or null when
        /// the car does not publish it (scenario 2).
        ///
        /// Found by characteristic **type** rather than by identifier: which
        /// identifier a car gave the entry is its own business, and the type is
        /// the only thing the use case fixes.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public Decimal? NominalCapacity(SPINERemoteEntity Partner)

            => CapacityOf(Partner)?.
                   Data<ElectricalConnectionCharacteristicListDataType>(EVStateOfCharge.CharacteristicListData)?.
                   ElectricalConnectionCharacteristicData?.
                   FirstOrDefault(characteristic => characteristic.CharacteristicType == ElectricalConnectionCharacteristicTypeType.EnergyCapacityNominalMax &&
                                                     characteristic.CharacteristicContext == ElectricalConnectionCharacteristicContextType.Entity)?.
                   Value?.Value;

        #endregion

    }

}
