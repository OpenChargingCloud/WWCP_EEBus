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
    /// The grid connection point of "Monitoring of Grid Connection Point" - the
    /// meter at the boundary between a building and the grid.
    ///
    /// The measuring half is the shared work of every monitoring use case (see
    /// <see cref="AMonitoredDevice"/>); what this adds is scenario 1, which is
    /// not a measurement: the curtailment limit factor is a configuration value
    /// which the grid connection point was told and now publishes.
    /// </summary>
    public class MGCPGridConnectionPoint : AMonitoredDevice
    {

        #region Data

        private const UInt32 curtailmentKeyId = 1;

        #endregion

        #region Properties

        /// <summary>
        /// The device configuration server feature, which holds the curtailment
        /// limit factor - or null when this grid connection point does not
        /// support scenario 1.
        /// </summary>
        public SPINELocalFeature?  Configuration    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the grid connection point of MGCP to an entity.
        ///
        /// Scenarios 2, 3 and 4 are mandatory and always there: the momentary
        /// power, the energy fed in and the energy drawn. The rest is what this
        /// particular meter happens to know.
        /// </summary>
        /// <param name="Entity">The entity which measures the grid connection point.</param>
        /// <param name="Phases">Which phases it measures. All three by default; an empty list means it measures only totals.</param>
        /// <param name="Curtailment">Whether it publishes the PV curtailment limit factor (scenario 1).</param>
        /// <param name="Current">Whether it measures current (scenario 5).</param>
        /// <param name="Voltage">Whether it measures voltage (scenario 6).</param>
        /// <param name="Frequency">Whether it measures the grid frequency (scenario 7).</param>
        public MGCPGridConnectionPoint(SPINELocalEntity                                 Entity,
                                       IEnumerable<ElectricalConnectionPhaseNameType>?  Phases        = null,
                                       Boolean                                          Curtailment   = false,
                                       Boolean                                          Current       = false,
                                       Boolean                                          Voltage       = false,
                                       Boolean                                          Frequency     = false)

            : base(Entity,
                   MonitoringOfGridConnectionPoint.Profile,
                   Measures(Phases, Current, Voltage, Frequency),
                   Phases,

                   // Scenario 1 is supported without being measured: the
                   // curtailment limit factor is a configuration value.
                   AlsoSupports: Curtailment
                                     ? [ MonitoringOfGridConnectionPoint.ScenarioCurtailment ]
                                     : null)

        {

            if (!Curtailment)
                return;

            #region The device configuration server: the curtailment limit factor (scenario 1)

            Configuration = Entity.Feature(FeatureTypeType.DeviceConfiguration, RoleType.Server)
                                ?? Entity.AddFeature(FeatureTypeType.DeviceConfiguration, RoleType.Server);

            Configuration.AddFunction(MonitoringOfGridConnectionPoint.KeyValueDescriptionListData);
            Configuration.AddFunction(MonitoringOfGridConnectionPoint.KeyValueListData,
                                      Read:         true,
                                      PartialRead:  true);

            var descriptions = Configuration.DataCopy<DeviceConfigurationKeyValueDescriptionListDataType>(MonitoringOfGridConnectionPoint.KeyValueDescriptionListData)?.
                                   DeviceConfigurationKeyValueDescriptionData?.ToList() ?? [];

            descriptions.Add(new DeviceConfigurationKeyValueDescriptionDataType {
                                 KeyId      = curtailmentKeyId,
                                 KeyName    = MonitoringOfGridConnectionPoint.CurtailmentLimitFactorKey,
                                 ValueType  = DeviceConfigurationKeyValueTypeType.ScaledNumber
                             });

            Configuration.FunctionData(MonitoringOfGridConnectionPoint.KeyValueDescriptionListData)!.SetData(
                new DeviceConfigurationKeyValueDescriptionListDataType {
                    DeviceConfigurationKeyValueDescriptionData = descriptions
                }
            );

            // Nothing is curtailed until somebody says otherwise: a factor of
            // one is "feed in everything you have". Written straight into the
            // function rather than through SetCurtailmentLimitFactor, because
            // nobody can have subscribed to a feature which is being built.
            Configuration.FunctionData(MonitoringOfGridConnectionPoint.KeyValueListData)!.SetData(
                new DeviceConfigurationKeyValueListDataType {
                    DeviceConfigurationKeyValueData = [ Factor(1) ]
                }
            );

            #endregion

        }


        /// <summary>
        /// Which quantities a grid connection point with these measurements
        /// publishes. Scenario 1 is not among them - it is not measured.
        /// </summary>
        private static IEnumerable<MonitoringQuantity> Measures(IEnumerable<ElectricalConnectionPhaseNameType>?  Phases,
                                                                Boolean                                          Current,
                                                                Boolean                                          Voltage,
                                                                Boolean                                          Frequency)
        {

            var phases      = Phases ?? [ ElectricalConnectionPhaseNameType.A,
                                          ElectricalConnectionPhaseNameType.B,
                                          ElectricalConnectionPhaseNameType.C ];

            var quantities  = new List<MonitoringQuantity> {
                                  MonitoringOfGridConnectionPoint.Power,
                                  MonitoringOfGridConnectionPoint.EnergyFeedIn,
                                  MonitoringOfGridConnectionPoint.EnergyConsumed
                              };

            if (Current)
                quantities.AddRange(phases.Select(MonitoringOfGridConnectionPoint.Current));

            if (Voltage)
                quantities.AddRange(phases.Select(MonitoringOfGridConnectionPoint.Voltage));

            if (Frequency)
                quantities.Add(MonitoringOfGridConnectionPoint.Frequency);

            return quantities;

        }

        #endregion


        #region CurtailmentLimitFactor / SetCurtailmentLimitFactor(Factor, ...)

        /// <summary>
        /// How much of what the photovoltaic system behind this grid connection
        /// point could produce it is currently allowed to feed in, between zero
        /// and one - or null when this grid connection point does not publish it
        /// (scenario 1).
        /// </summary>
        public Decimal? CurtailmentLimitFactor

            => Configuration?.DataCopy<DeviceConfigurationKeyValueListDataType>(MonitoringOfGridConnectionPoint.KeyValueListData)?.
                   DeviceConfigurationKeyValueData?.FirstOrDefault(entry => entry.KeyId == curtailmentKeyId)?.
                   Value?.ScaledNumber?.Value;


        /// <summary>
        /// Publish a new curtailment limit factor, and tell whoever subscribed.
        /// </summary>
        /// <param name="Factor">How much may be fed in, between zero and one.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="ArgumentOutOfRangeException">When the factor is not between zero and one.</exception>
        /// <exception cref="InvalidOperationException">When this grid connection point does not support scenario 1.</exception>
        public async Task SetCurtailmentLimitFactor(Decimal            Factor,
                                                    CancellationToken  CancellationToken   = default)
        {

            if (Factor < 0 || Factor > 1)
                throw new ArgumentOutOfRangeException(nameof(Factor),
                                                      Factor,
                                                      "A curtailment limit factor is a fraction of what could be produced, so it is between zero and one.");

            if (Configuration is null)
                throw new InvalidOperationException("This grid connection point does not support scenario 1 of the monitoring of a grid connection point.");

            var data = Configuration.DataCopy<DeviceConfigurationKeyValueListDataType>(MonitoringOfGridConnectionPoint.KeyValueListData)
                           ?? new DeviceConfigurationKeyValueListDataType { DeviceConfigurationKeyValueData = [] };

            data.DeviceConfigurationKeyValueData ??= [];
            data.DeviceConfigurationKeyValueData.RemoveAll(value => value.KeyId == curtailmentKeyId);
            data.DeviceConfigurationKeyValueData.Add(MGCPGridConnectionPoint.Factor(Factor));

            await Configuration.SetData(MonitoringOfGridConnectionPoint.KeyValueListData,
                                        data,
                                        CancellationToken: CancellationToken);

        }


        /// <summary>
        /// The curtailment limit factor as this use case carries it.
        ///
        /// Not changeable: this use case watches the factor, it does not set it.
        /// Whoever curtails does so elsewhere.
        /// </summary>
        private static DeviceConfigurationKeyValueDataType Factor(Decimal Value)

            => new () {
                   KeyId              = curtailmentKeyId,
                   IsValueChangeable  = false,
                   Value              = new DeviceConfigurationKeyValueValueType {
                                            ScaledNumber = ScaledNumberType.FromValue(Value)
                                        }
               };

        #endregion

    }

}
