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
using cloud.charging.open.protocols.EEBUS.UseCases.Commissioning;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.EVCC
{

    /// <summary>
    /// The energy manager of "EV Commissioning and Configuration" - the side
    /// which notices that a car has arrived and writes down what it is.
    ///
    /// Reading the manufacturer data and the sleep mode is the shared work of
    /// every commissioning use case (see <see cref="ACommissioningAppliance"/>).
    /// What this adds is the three questions only a car can be asked, and the
    /// two scenarios which are not questions at all: scenarios 1 and 8 are the
    /// car's entity appearing in and disappearing from the detailed discovery,
    /// which the use case framework already watches for. <see cref="IsConnected"/>
    /// is that, given a name.
    /// </summary>
    public class EVCCEnergyManager : ACommissioningAppliance
    {

        #region Constructor(s)

        /// <summary>
        /// Add the CEM of EVCC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which commissions.</param>
        /// <param name="Scenarios">Which scenarios it is interested in. The mandatory ones are always included.</param>
        public EVCCEnergyManager(SPINELocalEntity      Entity,
                                 IEnumerable<UInt32>?  Scenarios   = null)

            : base(Entity,
                   EVCommissioningAndConfiguration.Profile,
                   Scenarios)

        { }

        #endregion


        #region ConfigurationOf(Partner) / IdentificationOf(Partner) / ElectricalOf(Partner)

        /// <summary>
        /// The device configuration of a car, which holds what it speaks and
        /// whether it charges asymmetrically.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public UseCaseFeature? ConfigurationOf(SPINERemoteEntity Partner)

            => Pair(FeatureTypeType.DeviceConfiguration, Partner);


        /// <summary>
        /// Its identification, where it has one.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public UseCaseFeature? IdentificationOf(SPINERemoteEntity Partner)

            => Pair(FeatureTypeType.Identification, Partner);


        /// <summary>
        /// Its electrical connection, which holds the charging power limits.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public UseCaseFeature? ElectricalOf(SPINERemoteEntity Partner)

            => Pair(FeatureTypeType.ElectricalConnection, Partner);

        #endregion

        #region (override) Subscribe(Partner, CancellationToken = default)

        /// <summary>
        /// Read what a car publishes about itself, and ask to be told when it
        /// changes.
        ///
        /// The descriptions first and then the values, in that order and for the
        /// same reason as everywhere else: a key value without its description
        /// is a number under an identifier nobody can name. The specification
        /// asks for the description read to be a partial one selecting on the
        /// key name (section 3.4.2.2); <see cref="UseCaseFeature.RequestData"/>
        /// drops the selector by itself when the car cannot answer a part.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public override async Task Subscribe(SPINERemoteEntity  Partner,
                                             CancellationToken  CancellationToken   = default)
        {

            await base.Subscribe(Partner, CancellationToken);

            if (ConfigurationOf(Partner) is UseCaseFeature configuration)
            {
                await configuration.Subscribe(CancellationToken);
                await configuration.RequestData(EVCommissioningAndConfiguration.KeyValueDescriptionListData, CancellationToken: CancellationToken);
                await configuration.RequestData(EVCommissioningAndConfiguration.KeyValueListData,            CancellationToken: CancellationToken);
            }

            if (IdentificationOf(Partner) is UseCaseFeature identification)
            {
                await identification.Subscribe(CancellationToken);
                await identification.RequestData(EVCommissioningAndConfiguration.IdentificationListData, CancellationToken: CancellationToken);
            }

            if (ElectricalOf(Partner) is UseCaseFeature electrical)
            {
                await electrical.Subscribe(CancellationToken);
                await electrical.RequestData(EVCommissioningAndConfiguration.ParameterDescriptionListData, CancellationToken: CancellationToken);
                await electrical.RequestData(EVCommissioningAndConfiguration.PermittedValueSetListData,    CancellationToken: CancellationToken);
            }

        }

        #endregion


        #region IsConnected(Partner)

        /// <summary>
        /// Whether a car is currently connected (scenarios 1 and 8).
        ///
        /// These two scenarios have no data at all. A car is connected because
        /// its entity is in the charging station's detailed discovery and the
        /// use case is announced as available, and disconnected because one of
        /// those stopped being true - which the framework already watches for
        /// and reports as a <see cref="UseCaseSupportChanged"/> event.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public Boolean IsConnected(SPINERemoteEntity? Partner)

            => PartnerFor(Partner)?.Available == true;

        #endregion

        #region CommunicationStandard(Partner) / IsDigital(Partner) / AsymmetricCharging(Partner)

        /// <summary>
        /// What a car speaks to the charging station, or null when it has not
        /// said (scenario 2).
        ///
        /// Found by key **name** rather than by identifier, and by either
        /// spelling of that name: the specification's content tables say
        /// "communicationStandard" and its sequence diagram section says
        /// "communicationsStandard", so a car built from either is a car which
        /// exists. See finding S9 in docs/spec-deviations.md.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public String? CommunicationStandard(SPINERemoteEntity Partner)

            => ConfigurationValue(Partner, EVCommissioningAndConfiguration.CommunicationStandardKeys)?.String;


        /// <summary>
        /// Whether there is a digital channel to this car at all (scenario 2).
        ///
        /// Under IEC 61851 there is not, and a manager which knows that will not
        /// wait for an identification or an energy demand which cannot come.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public Boolean IsDigital(SPINERemoteEntity Partner)

            => EVCommissioningAndConfiguration.IsDigital(CommunicationStandard(Partner));


        /// <summary>
        /// Whether the phases of this car may carry different currents, or null
        /// when it has not said (scenario 3).
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public Boolean? AsymmetricCharging(SPINERemoteEntity Partner)

            => ConfigurationValue(Partner, [ EVCommissioningAndConfiguration.AsymmetricChargingKey ])?.Boolean;


        /// <summary>
        /// The value a car published under any of the given key names.
        /// </summary>
        private DeviceConfigurationKeyValueValueType? ConfigurationValue(SPINERemoteEntity                          Partner,
                                                                         IEnumerable<DeviceConfigurationKeyNameType>  KeyNames)
        {

            if (ConfigurationOf(Partner) is not UseCaseFeature configuration)
                return null;

            var names  = KeyNames.ToHashSet();

            var keyId  = configuration.
                             Data<DeviceConfigurationKeyValueDescriptionListDataType>(EVCommissioningAndConfiguration.KeyValueDescriptionListData)?.
                             DeviceConfigurationKeyValueDescriptionData?.
                             FirstOrDefault(description => description.KeyName.HasValue &&
                                                            names.Contains(description.KeyName.Value))?.
                             KeyId;

            if (keyId is null)
                return null;

            return configuration.
                       Data<DeviceConfigurationKeyValueListDataType>(EVCommissioningAndConfiguration.KeyValueListData)?.
                       DeviceConfigurationKeyValueData?.
                       FirstOrDefault(entry => entry.KeyId == keyId)?.
                       Value;

        }

        #endregion

        #region Identifier(Partner) / ChargingPowerLimits(Partner) / IsAsleep(Partner)

        /// <summary>
        /// How a car identifies itself, or null when it does not (scenario 4).
        ///
        /// Under ISO 15118 this is the MAC address of the car's communication
        /// unit [EVCC-007], which is what lets a person tell one car from
        /// another and give one of them priority.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public String? Identifier(SPINERemoteEntity Partner)

            => IdentificationOf(Partner)?.
                   Data<IdentificationListDataType>(EVCommissioningAndConfiguration.IdentificationListData)?.
                   IdentificationData?.
                   FirstOrDefault(identification => identification.IdentificationType == IdentificationTypeType.Eui48 ||
                                                     identification.IdentificationType == IdentificationTypeType.Eui64)?.
                   IdentificationValue;


        /// <summary>
        /// What a car can charge with, in watts: the lowest it can do, the
        /// highest where it says, and what it draws while doing nothing
        /// (scenario 6).
        ///
        /// The minimum is the interesting one and it is the mandatory one:
        /// [EVCC-017] with the note "the minimum charging power is often not
        /// zero", which is the whole reason an energy manager cannot simply
        /// throttle a car towards zero and expect it to follow.
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public (Decimal Minimum, Decimal? Maximum, Decimal? Standby)? ChargingPowerLimits(SPINERemoteEntity Partner)
        {

            if (ElectricalOf(Partner) is not UseCaseFeature electrical)
                return null;

            var parameterId = electrical.
                                  Data<ElectricalConnectionParameterDescriptionListDataType>(EVCommissioningAndConfiguration.ParameterDescriptionListData)?.
                                  ElectricalConnectionParameterDescriptionData?.
                                  FirstOrDefault(parameter => parameter.ScopeType == ScopeTypeType.AcPowerTotal)?.
                                  ParameterId;

            if (parameterId is null)
                return null;

            var set = electrical.
                          Data<ElectricalConnectionPermittedValueSetListDataType>(EVCommissioningAndConfiguration.PermittedValueSetListData)?.
                          ElectricalConnectionPermittedValueSetData?.
                          FirstOrDefault(entry => entry.ParameterId == parameterId)?.
                          PermittedValueSet?.FirstOrDefault();

            if (set?.Range?.FirstOrDefault()?.Min?.Value is not Decimal minimum)
                return null;

            return (minimum,
                    set.Range.First().Max?.Value,
                    set.Value?.FirstOrDefault()?.Value);

        }


        /// <summary>
        /// Whether a car is currently in sleep mode, in which it does not charge
        /// (scenario 7).
        /// </summary>
        /// <param name="Partner">An entity of a car.</param>
        public Boolean IsAsleep(SPINERemoteEntity Partner)

            => IsReporting(Partner);

        #endregion

    }

}
