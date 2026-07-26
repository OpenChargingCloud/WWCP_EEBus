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

namespace cloud.charging.open.protocols.EEBUS.UseCases.CEVC
{

    /// <summary>
    /// What the energy guard and the energy broker of "Coordinated EV Charging"
    /// have in common.
    ///
    /// Both are clients of the car's TimeSeries feature, both read the demand
    /// and the plan, and both host a heartbeat and an operating state of their
    /// own so that the car can tell whether they are still there (scenarios 5
    /// to 8). What they write differs entirely - a power limitation curve
    /// against an incentive table - and that is in the two subclasses.
    /// </summary>
    public abstract class ACEVCCoordinator : AUseCase
    {

        #region Properties

        /// <summary>
        /// The device diagnosis server feature, which carries the heartbeat and
        /// the state of this device.
        /// </summary>
        public SPINELocalFeature  Diagnosis    { get; }

        /// <summary>
        /// The heartbeat, which the car watches.
        /// </summary>
        public SPINEHeartbeat     Heartbeat    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add a coordinating side of CEVC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which coordinates.</param>
        /// <param name="Actor">Which actor of the use case it plays.</param>
        /// <param name="Scenarios">Which scenarios it supports.</param>
        /// <param name="ClientFeatures">Which of the car's features it reads and writes.</param>
        protected ACEVCCoordinator(SPINELocalEntity              Entity,
                                   String                        Actor,
                                   IEnumerable<UseCaseScenario>  Scenarios,
                                   IEnumerable<FeatureTypeType>  ClientFeatures)

            : base(Entity,
                   Actor,
                   CoordinatedEVCharging.Name,
                   CoordinatedEVCharging.Version,
                   Scenarios,
                   [ UseCaseActors.EV ],
                   [ EntityTypeType.EV ],
                   CoordinatedEVCharging.DocumentSubRevision)

        {

            foreach (var featureType in ClientFeatures)
                if (Entity.Feature(featureType, RoleType.Client) is null)
                    Entity.AddFeature(featureType, RoleType.Client);

            // Shared with whatever else on this entity hosts a diagnosis: an
            // energy manager which is the energy guard of the coordinated
            // charging and of the overload protection at once has one heartbeat.
            Diagnosis = Entity.Feature(FeatureTypeType.DeviceDiagnosis, RoleType.Server)
                            ?? Entity.AddFeature(FeatureTypeType.DeviceDiagnosis, RoleType.Server);

            Diagnosis.AddFunction(CoordinatedEVCharging.HeartbeatData);
            Diagnosis.AddFunction(CoordinatedEVCharging.StateData);

            if (Diagnosis.DataCopy<DeviceDiagnosisStateDataType>(CoordinatedEVCharging.StateData) is null)
                Diagnosis.FunctionData(CoordinatedEVCharging.StateData)!.SetData(
                    new DeviceDiagnosisStateDataType {
                        OperatingState = DeviceDiagnosisOperatingStateType.NormalOperation
                    }
                );

            Heartbeat = new SPINEHeartbeat(Diagnosis);

        }

        #endregion


        #region StartHeartbeat(...) / StopHeartbeat() / SetOperatingState(...)

        /// <summary>
        /// Start proving that this device is there (scenario 5 or 6).
        /// </summary>
        /// <param name="Interval">How often.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task StartHeartbeat(TimeSpan?          Interval            = null,
                                   CancellationToken  CancellationToken   = default)

            => Heartbeat.Start(Interval ?? TimeSpan.FromSeconds(30),
                               CancellationToken);


        /// <summary>
        /// Stop.
        /// </summary>
        public void StopHeartbeat()
        {
            Heartbeat.Stop();
        }


        /// <summary>
        /// Say how this device is doing (scenario 7 or 8).
        /// </summary>
        /// <param name="OperatingState">The state, i.e. "normalOperation" or "failure".</param>
        /// <param name="LastErrorCode">What went wrong.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task SetOperatingState(DeviceDiagnosisOperatingStateType  OperatingState,
                                      String?                            LastErrorCode       = null,
                                      CancellationToken                  CancellationToken   = default)

            => Diagnosis.SetData(
                   CoordinatedEVCharging.StateData,
                   new DeviceDiagnosisStateDataType {
                       Timestamp       = AbsoluteOrRelativeTimeType.Parse(Device.TimeProvider.GetUtcNow()),
                       OperatingState  = OperatingState,
                       LastErrorCode   = LastErrorCode
                   },
                   CancellationToken: CancellationToken
               );

        #endregion


        #region TimeSeriesOf(Partner) / Subscribe(Partner, ...)

        /// <summary>
        /// The time series of a car, paired with our client feature.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        public UseCaseFeature TimeSeriesOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.TimeSeries, Entity, Partner);


        /// <summary>
        /// Read what a car publishes and ask to be told when it changes.
        ///
        /// The descriptions first: they say which of the three curves is which,
        /// and without them a time series is a list of numbers under an
        /// identifier. A binding as well, because everything this side does
        /// after reading is a write.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public virtual async Task Subscribe(SPINERemoteEntity  Partner,
                                            CancellationToken  CancellationToken   = default)
        {

            var timeSeries = TimeSeriesOf(Partner);

            await timeSeries.Subscribe(CancellationToken);
            await timeSeries.Bind     (CancellationToken);

            await timeSeries.RequestData(CoordinatedEVCharging.TimeSeriesDescriptionListData, CancellationToken: CancellationToken);
            await timeSeries.RequestData(CoordinatedEVCharging.TimeSeriesListData,            CancellationToken: CancellationToken);

        }

        #endregion

        #region DemandOf(Partner) / PlanOf(Partner)

        /// <summary>
        /// How much energy a car is asking for and by when (scenario 1), or null
        /// when it has not said.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        public ChargingDemand? DemandOf(SPINERemoteEntity Partner)
        {

            var series = SeriesOf(Partner, CoordinatedEVCharging.Demand);
            var slot   = series?.TimeSeriesSlot?.FirstOrDefault();

            if (slot is null)
                return null;

            return new ChargingDemand(slot.Duration?.AsTimeSpan,
                                      slot.MinValue?.Value,
                                      slot.Value?.Value,
                                      slot.MaxValue?.Value,
                                      series?.TimePeriod?.StartTime?.AsTimeSpan);

        }


        /// <summary>
        /// What a car intends to draw over time (scenario 4).
        ///
        /// This is what makes coordinated charging coordinated: everybody
        /// involved knows what will happen before it happens, rather than
        /// finding out afterwards from a meter.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        public IReadOnlyList<PowerSlot> PlanOf(SPINERemoteEntity Partner)

            => CoordinatedEVCharging.Slots(SeriesOf(Partner, CoordinatedEVCharging.Plan));

        #endregion

        #region (protected) SeriesIdOf(Partner, Type) / SeriesOf(Partner, Type) / DescriptionOf(Partner, Type)

        /// <summary>
        /// Which identifier a car gave one of the three curves.
        ///
        /// By **type** rather than by number: which identifier a car chose is
        /// its own business, and the type is the only thing the use case fixes.
        /// </summary>
        protected UInt32? SeriesIdOf(SPINERemoteEntity  Partner,
                                     TimeSeriesTypeType  Type)

            => DescriptionOf(Partner, Type)?.TimeSeriesId;


        /// <summary>
        /// The description of one of the three curves.
        /// </summary>
        protected TimeSeriesDescriptionDataType? DescriptionOf(SPINERemoteEntity   Partner,
                                                               TimeSeriesTypeType  Type)

            => TimeSeriesOf(Partner).
                   Data<TimeSeriesDescriptionListDataType>(CoordinatedEVCharging.TimeSeriesDescriptionListData)?.
                   TimeSeriesDescriptionData?.
                   FirstOrDefault(description => description.TimeSeriesType == Type);


        /// <summary>
        /// One of the three curves itself.
        /// </summary>
        protected TimeSeriesDataType? SeriesOf(SPINERemoteEntity   Partner,
                                                TimeSeriesTypeType  Type)
        {

            if (SeriesIdOf(Partner, Type) is not UInt32 id)
                return null;

            return TimeSeriesOf(Partner).
                       Data<TimeSeriesListDataType>(CoordinatedEVCharging.TimeSeriesListData)?.
                       TimeSeriesData?.FirstOrDefault(series => series.TimeSeriesId == id);

        }

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the time series client feature.
        /// </summary>
        protected override SPINEFeature Feature()

            => Entity.Feature(FeatureTypeType.TimeSeries, RoleType.Client) ?? Diagnosis;

        #endregion

    }

}
