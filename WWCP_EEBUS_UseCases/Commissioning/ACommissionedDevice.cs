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

namespace cloud.charging.open.protocols.EEBUS.UseCases.Commissioning
{

    /// <summary>
    /// The commissioned side of a commissioning use case - the device which was
    /// plugged in and now says what it is.
    ///
    /// It is the server actor, and it publishes two things: who made it, and how
    /// it is doing. Both of them are read once and then subscribed to, and
    /// neither is ever written by the other side. That is the whole of the
    /// EVSE commissioning use case and the spine of the EV one.
    /// </summary>
    public abstract class ACommissionedDevice : AUseCase
    {

        #region Properties

        /// <summary>
        /// Which of the commissioning use cases this is.
        /// </summary>
        public CommissioningProfile  Profile           { get; }

        /// <summary>
        /// The device classification server feature, which holds the
        /// manufacturer data - or null when this device does not publish it.
        /// </summary>
        public SPINELocalFeature?    Classification    { get; }

        /// <summary>
        /// The device diagnosis server feature, which holds the operating state
        /// - or null when this device does not publish it.
        /// </summary>
        public SPINELocalFeature?    Diagnosis         { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the commissioned side of a commissioning use case to an entity.
        /// </summary>
        /// <param name="Entity">The entity which was plugged in.</param>
        /// <param name="Profile">Which of the commissioning use cases this is.</param>
        /// <param name="Scenarios">Which optional scenarios it supports. The mandatory ones are always included.</param>
        /// <param name="Manufacturer">Who made it. Null when it does not say.</param>
        protected ACommissionedDevice(SPINELocalEntity      Entity,
                                      CommissioningProfile  Profile,
                                      IEnumerable<UInt32>?  Scenarios,
                                      ManufacturerData?     Manufacturer   = null)

            : base(Entity,
                   Profile.ServerActor,
                   Profile.UseCaseName,
                   Profile.Version,
                   Profile.SupportedScenarios(ForClient: false, Scenarios: Scenarios),
                   [ Profile.ClientActor ],
                   PartnerEntityTypes:   null,
                   DocumentSubRevision:  Profile.DocumentSubRevision)

        {

            this.Profile = Profile;

            var supported = this.Scenarios.Select(scenario => scenario.Number).ToHashSet();

            #region The device classification server: who made this device

            if (supported.Contains(Profile.ManufacturerScenario))
            {

                Classification = Entity.Feature(FeatureTypeType.DeviceClassification, RoleType.Server)
                                     ?? Entity.AddFeature(FeatureTypeType.DeviceClassification, RoleType.Server);

                Classification.AddFunction(CommissioningFunctions.ManufacturerData);

                // Not through SetData: nobody can have subscribed to a feature
                // which is being built.
                Classification.FunctionData(CommissioningFunctions.ManufacturerData)!.SetData(
                    (Manufacturer ?? new ManufacturerData()).ToSPINE()
                );

            }

            #endregion

            #region The device diagnosis server: how it is doing

            if (supported.Contains(Profile.StateScenario))
            {

                Diagnosis = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Server)
                                ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Server);

                Diagnosis.AddFunction(CommissioningFunctions.DiagnosisStateData);

                // A device which has just been plugged in and says nothing about
                // its state is working, until it says otherwise. Leaving the
                // function empty would make "no answer yet" and "no problem"
                // look the same to the other side.
                if (Diagnosis.DataCopy<DeviceDiagnosisStateDataType>(CommissioningFunctions.DiagnosisStateData) is null)
                    Diagnosis.FunctionData(CommissioningFunctions.DiagnosisStateData)!.SetData(
                        new DeviceDiagnosisStateDataType {
                            OperatingState  = DeviceDiagnosisOperatingStateType.NormalOperation,
                            Timestamp       = AbsoluteOrRelativeTimeType.Parse(Device.TimeProvider.GetUtcNow())
                        }
                    );

            }

            #endregion

        }

        #endregion


        #region Manufacturer / SetManufacturer(Manufacturer, ...)

        /// <summary>
        /// Who made this device, as it currently publishes it - or null when it
        /// does not publish manufacturer data at all.
        /// </summary>
        public ManufacturerData? Manufacturer

            => ManufacturerData.FromSPINE(
                   Classification?.DataCopy<DeviceClassificationManufacturerDataType>(CommissioningFunctions.ManufacturerData)
               );


        /// <summary>
        /// Publish new manufacturer data, and tell whoever subscribed.
        ///
        /// Not as fixed as it sounds: a software revision changes with every
        /// update, and the use cases say so by asking the client to subscribe
        /// rather than to read once.
        /// </summary>
        /// <param name="Manufacturer">Who made this device.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">When this device does not publish manufacturer data.</exception>
        public async Task SetManufacturer(ManufacturerData   Manufacturer,
                                          CancellationToken  CancellationToken   = default)
        {

            if (Classification is null)
                throw new InvalidOperationException($"This {Profile.ServerActor} does not publish manufacturer data.");

            await Classification.SetData(CommissioningFunctions.ManufacturerData,
                                         Manufacturer.ToSPINE(),
                                         CancellationToken: CancellationToken);

        }

        #endregion

        #region OperatingState / LastErrorCode / SetOperatingState(State, ...)

        /// <summary>
        /// How this device is currently doing, or null when it does not publish
        /// an operating state.
        /// </summary>
        public DeviceDiagnosisOperatingStateType? OperatingState

            => Diagnosis?.DataCopy<DeviceDiagnosisStateDataType>(CommissioningFunctions.DiagnosisStateData)?.
                   OperatingState;


        /// <summary>
        /// What went wrong the last time something did, where this device says.
        /// </summary>
        public String? LastErrorCode

            => Diagnosis?.DataCopy<DeviceDiagnosisStateDataType>(CommissioningFunctions.DiagnosisStateData)?.
                   LastErrorCode;


        /// <summary>
        /// Publish a new operating state, and tell whoever subscribed.
        /// </summary>
        /// <param name="State">How this device is doing now.</param>
        /// <param name="LastErrorCode">What went wrong, where something did.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">When this device does not publish an operating state.</exception>
        public async Task SetOperatingState(DeviceDiagnosisOperatingStateType  State,
                                            String?                            LastErrorCode       = null,
                                            CancellationToken                  CancellationToken   = default)
        {

            if (Diagnosis is null)
                throw new InvalidOperationException($"This {Profile.ServerActor} does not publish an operating state.");

            var data = Diagnosis.DataCopy<DeviceDiagnosisStateDataType>(CommissioningFunctions.DiagnosisStateData)
                           ?? new DeviceDiagnosisStateDataType();

            data.OperatingState  = State;
            data.Timestamp       = AbsoluteOrRelativeTimeType.Parse(Device.TimeProvider.GetUtcNow());

            // Kept rather than cleared when no new one is given: the code says
            // what went wrong last, not what is wrong now, and a device which
            // has recovered has still had the fault.
            if (LastErrorCode is not null)
                data.LastErrorCode = LastErrorCode;

            await Diagnosis.SetData(CommissioningFunctions.DiagnosisStateData,
                                    data,
                                    CancellationToken: CancellationToken);

        }

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at whichever of the two features this
        /// device has.
        /// </summary>
        protected override SPINEFeature Feature()

            => Classification
                   ?? Diagnosis
                   ?? base.Feature();

        #endregion

    }

}
