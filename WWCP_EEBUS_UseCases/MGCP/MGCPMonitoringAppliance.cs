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

namespace cloud.charging.open.protocols.EEBUS.UseCases.MGCP
{

    /// <summary>
    /// The monitoring appliance of "Monitoring of Grid Connection Point" - the
    /// device which watches what crosses the boundary to the grid.
    ///
    /// Reading the measurements is the shared work of every monitoring use case
    /// (see <see cref="AMonitoringAppliance"/>); what this adds is scenario 1,
    /// which is read from the device configuration feature rather than the
    /// measurement one.
    /// </summary>
    public class MGCPMonitoringAppliance : AMonitoringAppliance
    {

        #region Properties

        /// <summary>
        /// Whether this appliance also watches the curtailment limit factor
        /// (scenario 1).
        /// </summary>
        public Boolean  WatchesCurtailment    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the monitoring appliance of MGCP to an entity.
        /// </summary>
        /// <param name="Entity">The entity which watches.</param>
        /// <param name="Scenarios">Which scenarios it is interested in. Scenarios 2, 3 and 4 are always included.</param>
        public MGCPMonitoringAppliance(SPINELocalEntity      Entity,
                                       IEnumerable<UInt32>?  Scenarios   = null)

            : base(Entity,
                   MonitoringOfGridConnectionPoint.Profile,
                   Scenarios ?? [ MonitoringOfGridConnectionPoint.ScenarioCurtailment,
                                  MonitoringOfGridConnectionPoint.ScenarioCurrent,
                                  MonitoringOfGridConnectionPoint.ScenarioVoltage,
                                  MonitoringOfGridConnectionPoint.ScenarioFrequency ])

        {

            WatchesCurtailment = this.Scenarios.Any(scenario => scenario.Number == MonitoringOfGridConnectionPoint.ScenarioCurtailment);

        }

        #endregion


        #region ConfigurationOf(Partner)

        /// <summary>
        /// The device configuration of a grid connection point, which holds the
        /// curtailment limit factor.
        /// </summary>
        /// <param name="Partner">An entity of a grid connection point.</param>
        public UseCaseFeature ConfigurationOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.DeviceConfiguration, Entity, Partner);

        #endregion

        #region (override) Subscribe(Partner, CancellationToken = default)

        /// <summary>
        /// Read what the grid connection point publishes, and ask to be told
        /// when it changes.
        ///
        /// The measurements as in every monitoring use case, and - for an
        /// appliance which watches scenario 1 - the curtailment limit factor as
        /// well. It changes rarely and matters when it does, which is exactly
        /// what a subscription is for.
        /// </summary>
        /// <param name="Partner">An entity of a grid connection point.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public override async Task Subscribe(SPINERemoteEntity  Partner,
                                             CancellationToken  CancellationToken   = default)
        {

            await base.Subscribe(Partner, CancellationToken);

            if (!WatchesCurtailment)
                return;

            var configuration = ConfigurationOf(Partner);

            await configuration.RequestData(MonitoringOfGridConnectionPoint.KeyValueDescriptionListData, CancellationToken: CancellationToken);
            await configuration.Subscribe(CancellationToken);
            await configuration.RequestData(MonitoringOfGridConnectionPoint.KeyValueListData,            CancellationToken: CancellationToken);

        }

        #endregion


        #region CurtailmentLimitFactor(Partner)

        /// <summary>
        /// How much of what the photovoltaic system behind a grid connection
        /// point could produce it is currently allowed to feed in, between zero
        /// and one - or null when that grid connection point does not publish it
        /// (scenario 1).
        ///
        /// Found by key **name** rather than by identifier: which identifier a
        /// device gave the key is its own business, and the name is the only
        /// thing the use case fixes.
        /// </summary>
        /// <param name="Partner">An entity of a grid connection point.</param>
        public Decimal? CurtailmentLimitFactor(SPINERemoteEntity Partner)
        {

            // An appliance which did not announce scenario 1 has no device
            // configuration client feature to read it with, and asking is not
            // an error - it is simply something this appliance does not watch.
            if (!WatchesCurtailment)
                return null;

            var configuration = ConfigurationOf(Partner);

            var keyId         = configuration.
                                    Data<DeviceConfigurationKeyValueDescriptionListDataType>(MonitoringOfGridConnectionPoint.KeyValueDescriptionListData)?.
                                    DeviceConfigurationKeyValueDescriptionData?.
                                    FirstOrDefault(description => description.KeyName == MonitoringOfGridConnectionPoint.CurtailmentLimitFactorKey)?.
                                    KeyId;

            if (keyId is null)
                return null;

            return configuration.
                       Data<DeviceConfigurationKeyValueListDataType>(MonitoringOfGridConnectionPoint.KeyValueListData)?.
                       DeviceConfigurationKeyValueData?.
                       FirstOrDefault(entry => entry.KeyId == keyId)?.
                       Value?.ScaledNumber?.Value;

        }

        #endregion

        #region Power(Partner) / EnergyFeedIn(Partner) / EnergyConsumed(Partner)

        /// <summary>
        /// The momentary power at a grid connection point, in watts: positive
        /// while the building draws from the grid, negative while it feeds in
        /// (scenario 2).
        /// </summary>
        /// <param name="Partner">An entity of a grid connection point.</param>
        public Decimal? Power(SPINERemoteEntity Partner)

            => Read(Partner, MonitoringOfGridConnectionPoint.Power)?.Value;


        /// <summary>
        /// The total energy fed into the grid, in watt hours (scenario 3).
        /// </summary>
        /// <param name="Partner">An entity of a grid connection point.</param>
        public Decimal? EnergyFeedIn(SPINERemoteEntity Partner)

            => Read(Partner, MonitoringOfGridConnectionPoint.EnergyFeedIn)?.Value;


        /// <summary>
        /// The total energy drawn from the grid, in watt hours (scenario 4).
        /// </summary>
        /// <param name="Partner">An entity of a grid connection point.</param>
        public Decimal? EnergyConsumed(SPINERemoteEntity Partner)

            => Read(Partner, MonitoringOfGridConnectionPoint.EnergyConsumed)?.Value;

        #endregion

    }

}
