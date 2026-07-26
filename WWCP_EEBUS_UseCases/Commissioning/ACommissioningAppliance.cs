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
    /// The commissioning side of a commissioning use case - the energy manager
    /// which writes down what was plugged in.
    ///
    /// It is the client actor and it has nothing to offer: no server feature, no
    /// heartbeat, nothing writable. Both scenarios say the same thing about how
    /// to get the data - "Binding SHOULD NOT be used for this Scenario. Actors
    /// SHALL create a subscription for each server Feature" - so this reads once
    /// and then listens.
    /// </summary>
    public abstract class ACommissioningAppliance : AUseCase
    {

        #region Properties

        /// <summary>
        /// Which of the commissioning use cases this is.
        /// </summary>
        public CommissioningProfile  Profile    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the commissioning side of a commissioning use case to an entity.
        /// </summary>
        /// <param name="Entity">The entity which commissions.</param>
        /// <param name="Profile">Which of the commissioning use cases this is.</param>
        /// <param name="Scenarios">Which optional scenarios it is interested in. The mandatory ones are always included.</param>
        /// <param name="PartnerActors">Which actors it accepts at the other side. The one the profile names, by default.</param>
        protected ACommissioningAppliance(SPINELocalEntity      Entity,
                                          CommissioningProfile  Profile,
                                          IEnumerable<UInt32>?  Scenarios       = null,
                                          IEnumerable<String>?  PartnerActors   = null)

            : base(Entity,
                   Profile.ClientActor,
                   Profile.UseCaseName,
                   Profile.Version,
                   Profile.SupportedScenarios(ForClient: true,
                                              Scenarios: Scenarios ?? Profile.Scenarios.Select(scenario => scenario.Number)),
                   PartnerActors ?? [ Profile.ServerActor ],
                   PartnerEntityTypes:   Profile.ServerEntityTypes,
                   DocumentSubRevision:  Profile.DocumentSubRevision)

        {

            this.Profile = Profile;

            // Whatever any of the supported scenarios needs at the other side,
            // this side needs a client feature for.
            foreach (var featureType in this.Scenarios.SelectMany(scenario => scenario.ServerFeatures).Distinct())
                if (Entity.Feature(featureType, RoleType.Client) is null)
                    Entity.AddFeature(featureType, RoleType.Client);

        }

        #endregion


        #region ClassificationOf(Partner) / DiagnosisOf(Partner)

        /// <summary>
        /// The device classification of a commissioned device, which holds its
        /// manufacturer data - or null when it does not publish any.
        /// </summary>
        /// <param name="Partner">An entity of a commissioned device.</param>
        public UseCaseFeature? ClassificationOf(SPINERemoteEntity Partner)

            => Pair(FeatureTypeType.DeviceClassification, Partner);


        /// <summary>
        /// Its device diagnosis, which holds its operating state - or null when
        /// it does not publish one.
        /// </summary>
        /// <param name="Partner">An entity of a commissioned device.</param>
        public UseCaseFeature? DiagnosisOf(SPINERemoteEntity Partner)

            => Pair(FeatureTypeType.DeviceDiagnosis, Partner);


        /// <summary>
        /// One of our client features paired with a partner's server feature,
        /// where both sides have it.
        ///
        /// Null rather than an exception: a device may support one scenario of a
        /// commissioning use case and not the other, and asking a charging
        /// station which publishes no manufacturer data for its manufacturer
        /// data is a question with an answer.
        /// </summary>
        protected UseCaseFeature? Pair(FeatureTypeType    FeatureType,
                                       SPINERemoteEntity  Partner)

            => Entity. Feature(FeatureType, RoleType.Client) is not null &&
               Partner.Feature(FeatureType, RoleType.Server) is not null

                   ? new (FeatureType, Entity, Partner)
                   : null;

        #endregion

        #region Subscribe(Partner, CancellationToken = default)

        /// <summary>
        /// Read what a commissioned device publishes, and ask to be told when it
        /// changes.
        /// </summary>
        /// <param name="Partner">An entity of a commissioned device.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public virtual async Task Subscribe(SPINERemoteEntity  Partner,
                                            CancellationToken  CancellationToken   = default)
        {

            if (ClassificationOf(Partner) is UseCaseFeature classification)
            {
                await classification.Subscribe(CancellationToken);
                await classification.RequestData(CommissioningFunctions.ManufacturerData, CancellationToken: CancellationToken);
            }

            if (DiagnosisOf(Partner) is UseCaseFeature diagnosis)
            {
                await diagnosis.Subscribe(CancellationToken);
                await diagnosis.RequestData(CommissioningFunctions.DiagnosisStateData, CancellationToken: CancellationToken);
            }

        }

        #endregion


        #region Manufacturer(Partner) / OperatingState(Partner) / LastErrorCode(Partner) / IsReporting(Partner)

        /// <summary>
        /// Who made a commissioned device, as it published it - or null when it
        /// has published nothing yet.
        /// </summary>
        /// <param name="Partner">An entity of a commissioned device.</param>
        public ManufacturerData? Manufacturer(SPINERemoteEntity Partner)

            => ManufacturerData.FromSPINE(
                   ClassificationOf(Partner)?.
                       Data<DeviceClassificationManufacturerDataType>(CommissioningFunctions.ManufacturerData)
               );


        /// <summary>
        /// How a commissioned device says it is doing, or null when it has said
        /// nothing yet.
        /// </summary>
        /// <param name="Partner">An entity of a commissioned device.</param>
        public DeviceDiagnosisOperatingStateType? OperatingState(SPINERemoteEntity Partner)

            => DiagnosisOf(Partner)?.
                   Data<DeviceDiagnosisStateDataType>(CommissioningFunctions.DiagnosisStateData)?.
                   OperatingState;


        /// <summary>
        /// What went wrong at a commissioned device the last time something did.
        /// </summary>
        /// <param name="Partner">An entity of a commissioned device.</param>
        public String? LastErrorCode(SPINERemoteEntity Partner)

            => DiagnosisOf(Partner)?.
                   Data<DeviceDiagnosisStateDataType>(CommissioningFunctions.DiagnosisStateData)?.
                   LastErrorCode;


        /// <summary>
        /// Whether a commissioned device is currently in the state its
        /// diagnosis scenario exists to report - a failure for a charging
        /// station, standby for a car.
        /// </summary>
        /// <param name="Partner">An entity of a commissioned device.</param>
        public Boolean IsReporting(SPINERemoteEntity Partner)

            => OperatingState(Partner) == Profile.ReportedState;

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at whichever client feature this appliance
        /// has.
        /// </summary>
        protected override SPINEFeature Feature()

            => Entity.Feature(FeatureTypeType.DeviceClassification, RoleType.Client)
                   ?? Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Client)
                   ?? base.Feature();

        #endregion

    }

}
