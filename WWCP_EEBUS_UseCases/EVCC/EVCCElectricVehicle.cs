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
    /// The car of "EV Commissioning and Configuration" - the side which says
    /// what it is.
    ///
    /// The manufacturer data and the sleep mode are the shared work of every
    /// commissioning use case (see <see cref="ACommissionedDevice"/>). What this
    /// adds is the three things which are only about a car: what it speaks, how
    /// it may be charged, and who it is.
    ///
    /// All of it is published on features this car very probably shares with
    /// other use cases - the electricity measurement writes to the same
    /// electrical connection, the state of charge to the same one again - so
    /// every identifier here is picked around what is already on the feature
    /// rather than started at one.
    /// </summary>
    public class EVCCElectricVehicle : ACommissionedDevice
    {

        #region Data

        private readonly UInt32   electricalConnectionId  = 0;

        private readonly UInt32?  communicationKeyId;
        private readonly UInt32?  asymmetricKeyId;
        private readonly UInt32?  identificationId;
        private readonly UInt32?  powerParameterId;

        #endregion

        #region Properties

        /// <summary>
        /// The device configuration server feature, which holds the
        /// communication standard and the asymmetric charging support -
        /// scenarios 2 and 3, both mandatory, so this is never null.
        /// </summary>
        public SPINELocalFeature   Configuration     { get; }

        /// <summary>
        /// The identification server feature - or null when this car does not
        /// identify itself (scenario 4).
        /// </summary>
        public SPINELocalFeature?  Identification    { get; }

        /// <summary>
        /// The electrical connection server feature, which holds the charging
        /// power limits - or null when this car does not publish them
        /// (scenario 6).
        /// </summary>
        public SPINELocalFeature?  Electrical        { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the EV of EVCC to an entity.
        ///
        /// Scenarios 1, 2, 3 and 8 are mandatory. The first and the last of them
        /// have nothing to publish: a car is connected because its entity is
        /// there, and disconnected because it is not.
        /// </summary>
        /// <param name="Entity">The entity of the car.</param>
        /// <param name="CommunicationStandard">What the car speaks to the charging station (scenario 2).</param>
        /// <param name="AsymmetricCharging">Whether the phases may carry different currents (scenario 3).</param>
        /// <param name="Identifier">How the car identifies itself, e.g. the MAC address of its ISO 15118 unit (scenario 4).</param>
        /// <param name="IdentifierType">Which kind of identifier that is. An EUI-48 by default.</param>
        /// <param name="Manufacturer">Who made it (scenario 5).</param>
        /// <param name="MinimumChargingPower">The lowest power the car can charge with, in watts - which is often not zero (scenario 6).</param>
        /// <param name="MaximumChargingPower">The highest, in watts.</param>
        /// <param name="StandbyPower">What it draws while doing nothing, in watts.</param>
        /// <param name="SleepMode">Whether the car reports that it is asleep (scenario 7).</param>
        public EVCCElectricVehicle(SPINELocalEntity       Entity,
                                   String                 CommunicationStandard   = EVCommissioningAndConfiguration.ISO15118_2_ed2,
                                   Boolean                AsymmetricCharging      = false,
                                   String?                Identifier              = null,
                                   IdentificationTypeType? IdentifierType         = null,
                                   ManufacturerData?      Manufacturer            = null,
                                   Decimal?               MinimumChargingPower    = null,
                                   Decimal?               MaximumChargingPower    = null,
                                   Decimal?               StandbyPower            = null,
                                   Boolean                SleepMode               = false)

            : base(Entity,
                   EVCommissioningAndConfiguration.Profile,
                   Supports(Identifier, Manufacturer, MinimumChargingPower, SleepMode),
                   Manufacturer)

        {

            #region The device configuration server: what the car speaks (scenarios 2 and 3)

            Configuration = Entity.Feature(FeatureTypeType.DeviceConfiguration, RoleType.Server)
                                ?? Entity.AddFeature(FeatureTypeType.DeviceConfiguration, RoleType.Server);

            Configuration.AddFunction(EVCommissioningAndConfiguration.KeyValueDescriptionListData,
                                      Read:         true,
                                      PartialRead:  true);

            Configuration.AddFunction(EVCommissioningAndConfiguration.KeyValueListData,
                                      Read:         true,
                                      PartialRead:  true);

            var descriptions = Configuration.DataCopy<DeviceConfigurationKeyValueDescriptionListDataType>(EVCommissioningAndConfiguration.KeyValueDescriptionListData)?.
                                   DeviceConfigurationKeyValueDescriptionData?.ToList() ?? [];

            var values       = Configuration.DataCopy<DeviceConfigurationKeyValueListDataType>(EVCommissioningAndConfiguration.KeyValueListData)?.
                                   DeviceConfigurationKeyValueData?.ToList() ?? [];

            var keyIds       = UseCaseIds.NextFree(descriptions.Select(description => description.KeyId),
                                                   Count: 2).ToList();

            communicationKeyId  = keyIds[0];
            asymmetricKeyId     = keyIds[1];

            descriptions.Add(new DeviceConfigurationKeyValueDescriptionDataType {
                                 KeyId      = communicationKeyId,
                                 KeyName    = EVCommissioningAndConfiguration.CommunicationStandardKey,
                                 ValueType  = DeviceConfigurationKeyValueTypeType.String
                             });

            descriptions.Add(new DeviceConfigurationKeyValueDescriptionDataType {
                                 KeyId      = asymmetricKeyId,
                                 KeyName    = EVCommissioningAndConfiguration.AsymmetricChargingKey,
                                 ValueType  = DeviceConfigurationKeyValueTypeType.Boolean
                             });

            values.Add(new DeviceConfigurationKeyValueDataType {
                           KeyId              = communicationKeyId,
                           IsValueChangeable  = false,
                           Value              = new DeviceConfigurationKeyValueValueType { String  = CommunicationStandard }
                       });

            values.Add(new DeviceConfigurationKeyValueDataType {
                           KeyId              = asymmetricKeyId,
                           IsValueChangeable  = false,
                           Value              = new DeviceConfigurationKeyValueValueType { Boolean = AsymmetricCharging }
                       });

            Configuration.FunctionData(EVCommissioningAndConfiguration.KeyValueDescriptionListData)!.SetData(
                new DeviceConfigurationKeyValueDescriptionListDataType { DeviceConfigurationKeyValueDescriptionData = descriptions }
            );

            Configuration.FunctionData(EVCommissioningAndConfiguration.KeyValueListData)!.SetData(
                new DeviceConfigurationKeyValueListDataType { DeviceConfigurationKeyValueData = values }
            );

            #endregion

            #region The identification server: who the car is (scenario 4)

            if (Identifier is not null)
            {

                Identification = Entity.Feature(FeatureTypeType.Identification, RoleType.Server)
                                     ?? Entity.AddFeature(FeatureTypeType.Identification, RoleType.Server);

                Identification.AddFunction(EVCommissioningAndConfiguration.IdentificationListData,
                                           Read:         true,
                                           PartialRead:  true);

                var identifications = Identification.DataCopy<IdentificationListDataType>(EVCommissioningAndConfiguration.IdentificationListData)?.
                                          IdentificationData?.ToList() ?? [];

                identificationId = UseCaseIds.NextFree(identifications.Select(identification => identification.IdentificationId),
                                                       StartingAt: 0);

                identifications.Add(new IdentificationDataType {
                                        IdentificationId     = identificationId,

                                        // An EUI-48 unless told otherwise: under
                                        // ISO 15118 the identifier is the MAC
                                        // address of the car's communication
                                        // unit [EVCC-007].
                                        IdentificationType   = IdentifierType ?? IdentificationTypeType.Eui48,
                                        IdentificationValue  = Identifier
                                    });

                Identification.FunctionData(EVCommissioningAndConfiguration.IdentificationListData)!.SetData(
                    new IdentificationListDataType { IdentificationData = identifications }
                );

            }

            #endregion

            #region The electrical connection server: how hard the car may be charged (scenario 6)

            if (MinimumChargingPower.HasValue)
            {

                Electrical = Entity.Feature(FeatureTypeType.ElectricalConnection, RoleType.Server)
                                 ?? Entity.AddFeature(FeatureTypeType.ElectricalConnection, RoleType.Server);

                Electrical.AddFunction(EVCommissioningAndConfiguration.ParameterDescriptionListData,
                                       Read:         true,
                                       PartialRead:  true);

                Electrical.AddFunction(EVCommissioningAndConfiguration.PermittedValueSetListData,
                                       Read:         true,
                                       PartialRead:  true);

                var parameters = Electrical.DataCopy<ElectricalConnectionParameterDescriptionListDataType>(EVCommissioningAndConfiguration.ParameterDescriptionListData)?.
                                     ElectricalConnectionParameterDescriptionData?.ToList() ?? [];

                var permitted  = Electrical.DataCopy<ElectricalConnectionPermittedValueSetListDataType>(EVCommissioningAndConfiguration.PermittedValueSetListData)?.
                                     ElectricalConnectionPermittedValueSetData?.ToList() ?? [];

                powerParameterId = UseCaseIds.NextFree(parameters.Select(parameter => parameter.ParameterId),
                                                       StartingAt: 0);

                // No measurement identifier: this parameter describes a limit
                // rather than something which is measured. The electricity
                // measurement use case puts its own parameters on this same
                // feature, each of which does name one.
                parameters.Add(new ElectricalConnectionParameterDescriptionDataType {
                                   ElectricalConnectionId  = electricalConnectionId,
                                   ParameterId             = powerParameterId,
                                   AcMeasuredPhases        = ElectricalConnectionPhaseNameType.Abc,
                                   ScopeType               = ScopeTypeType.AcPowerTotal
                               });

                permitted.Add(new ElectricalConnectionPermittedValueSetDataType {
                                  ElectricalConnectionId  = electricalConnectionId,
                                  ParameterId             = powerParameterId,
                                  PermittedValueSet       = [ PowerLimits(MinimumChargingPower.Value,
                                                                          MaximumChargingPower,
                                                                          StandbyPower) ]
                              });

                Electrical.FunctionData(EVCommissioningAndConfiguration.ParameterDescriptionListData)!.SetData(
                    new ElectricalConnectionParameterDescriptionListDataType { ElectricalConnectionParameterDescriptionData = parameters }
                );

                Electrical.FunctionData(EVCommissioningAndConfiguration.PermittedValueSetListData)!.SetData(
                    new ElectricalConnectionPermittedValueSetListDataType { ElectricalConnectionPermittedValueSetData = permitted }
                );

            }

            #endregion

        }


        /// <summary>
        /// Which of the optional scenarios a car built like this supports.
        /// </summary>
        private static IEnumerable<UInt32> Supports(String?    Identifier,
                                                    Object?    Manufacturer,
                                                    Decimal?   MinimumChargingPower,
                                                    Boolean    SleepMode)
        {

            var scenarios = new List<UInt32>();

            if (Identifier is not null)
                scenarios.Add(EVCommissioningAndConfiguration.ScenarioIdentification);

            if (Manufacturer is not null)
                scenarios.Add(EVCommissioningAndConfiguration.ScenarioManufacturerData);

            if (MinimumChargingPower.HasValue)
                scenarios.Add(EVCommissioningAndConfiguration.ScenarioChargingPowerLimits);

            if (SleepMode)
                scenarios.Add(EVCommissioningAndConfiguration.ScenarioSleepMode);

            return scenarios;

        }


        /// <summary>
        /// The charging power limits as SPINE carries them: a range for what the
        /// car can do, and a single value for what it draws while doing nothing.
        /// </summary>
        private static ScaledNumberSetType PowerLimits(Decimal   Minimum,
                                                       Decimal?  Maximum,
                                                       Decimal?  Standby)

            => new () {

                   Range  = [ new ScaledNumberRangeType {
                                  Min  = ScaledNumberType.FromValue(Minimum),
                                  Max  = Maximum.HasValue
                                             ? ScaledNumberType.FromValue(Maximum.Value)
                                             : null
                              } ],

                   Value  = Standby.HasValue
                                ? [ ScaledNumberType.FromValue(Standby.Value) ]
                                : null

               };

        #endregion


        #region CommunicationStandard / AsymmetricCharging / SetCommunicationStandard(...) / SetAsymmetricCharging(...)

        /// <summary>
        /// What this car speaks to the charging station (scenario 2).
        /// </summary>
        public String? CommunicationStandard

            => Configuration.DataCopy<DeviceConfigurationKeyValueListDataType>(EVCommissioningAndConfiguration.KeyValueListData)?.
                   DeviceConfigurationKeyValueData?.
                   FirstOrDefault(entry => entry.KeyId == communicationKeyId)?.
                   Value?.String;


        /// <summary>
        /// Whether the phases may carry different currents (scenario 3).
        /// </summary>
        public Boolean? AsymmetricCharging

            => Configuration.DataCopy<DeviceConfigurationKeyValueListDataType>(EVCommissioningAndConfiguration.KeyValueListData)?.
                   DeviceConfigurationKeyValueData?.
                   FirstOrDefault(entry => entry.KeyId == asymmetricKeyId)?.
                   Value?.Boolean;


        /// <summary>
        /// Publish a different communication standard, and tell whoever
        /// subscribed.
        ///
        /// Which happens: [EVCC-002] says "the used communication standard may
        /// alter during runtime", and it does when a car and a charging station
        /// fall back from ISO 15118 to a PWM signal mid-session.
        /// </summary>
        /// <param name="Standard">What the car speaks now.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task SetCommunicationStandard(String             Standard,
                                             CancellationToken  CancellationToken   = default)

            => SetKey(communicationKeyId,
                      new DeviceConfigurationKeyValueValueType { String = Standard },
                      CancellationToken);


        /// <summary>
        /// Publish different asymmetric charging support, and tell whoever
        /// subscribed. [EVCC-006] says this may change during runtime too.
        /// </summary>
        /// <param name="Supported">Whether the phases may carry different currents.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task SetAsymmetricCharging(Boolean            Supported,
                                          CancellationToken  CancellationToken   = default)

            => SetKey(asymmetricKeyId,
                      new DeviceConfigurationKeyValueValueType { Boolean = Supported },
                      CancellationToken);


        /// <summary>
        /// Change one of our configuration values, leaving everybody else's
        /// alone.
        /// </summary>
        private async Task SetKey(UInt32?                              KeyId,
                                  DeviceConfigurationKeyValueValueType  Value,
                                  CancellationToken                     CancellationToken)
        {

            var data = Configuration.DataCopy<DeviceConfigurationKeyValueListDataType>(EVCommissioningAndConfiguration.KeyValueListData)
                           ?? new DeviceConfigurationKeyValueListDataType { DeviceConfigurationKeyValueData = [] };

            data.DeviceConfigurationKeyValueData ??= [];

            var mine = data.DeviceConfigurationKeyValueData.FirstOrDefault(entry => entry.KeyId == KeyId);

            if (mine is null)
                return;

            mine.Value = Value;

            await Configuration.SetData(EVCommissioningAndConfiguration.KeyValueListData,
                                        data,
                                        CancellationToken: CancellationToken);

        }

        #endregion

        #region Identifier / IsAsleep / FallAsleep(...) / WakeUp(...)

        /// <summary>
        /// How this car identifies itself, or null when it does not
        /// (scenario 4).
        /// </summary>
        public String? Identifier

            => Identification?.DataCopy<IdentificationListDataType>(EVCommissioningAndConfiguration.IdentificationListData)?.
                   IdentificationData?.
                   FirstOrDefault(identification => identification.IdentificationId == identificationId)?.
                   IdentificationValue;


        /// <summary>
        /// Whether this car is currently in sleep mode, in which it does not
        /// charge (scenario 7).
        /// </summary>
        public Boolean IsAsleep

            => OperatingState == DeviceDiagnosisOperatingStateType.Standby;


        /// <summary>
        /// Go to sleep, and tell whoever subscribed.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task FallAsleep(CancellationToken CancellationToken = default)

            => SetOperatingState(DeviceDiagnosisOperatingStateType.Standby,
                                 CancellationToken: CancellationToken);


        /// <summary>
        /// Wake up, and tell whoever subscribed.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task WakeUp(CancellationToken CancellationToken = default)

            => SetOperatingState(DeviceDiagnosisOperatingStateType.NormalOperation,
                                 CancellationToken: CancellationToken);

        #endregion

    }

}
