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

using Microsoft.Extensions.Time.Testing;

using NUnit.Framework;

using cloud.charging.open.protocols.EEBUS.SPINE;
using cloud.charging.open.protocols.EEBUS.SPINE.Model;
using cloud.charging.open.protocols.EEBUS.UseCases.CEVC;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "Coordinated EV Charging", all three actors, over the wire.
    ///
    /// The largest use case in the family: a car says how much energy it needs,
    /// an energy guard says how much power there will be, an energy broker says
    /// what it will cost, and the car answers with a plan. Everybody knows what
    /// will happen before it happens.
    /// </summary>
    [TestFixture]
    public class CEVCTests
    {

        #region Data

        private FakeTimeProvider     time    = null!;
        private SPINELoopback        wire    = null!;

        private CEVCElectricVehicle  car     = null!;
        private CEVCEnergyGuard      guard   = null!;
        private CEVCEnergyBroker     broker  = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            var cemEntity = hems.AddEntity(EntityTypeType.CEM);

            car    = new CEVCElectricVehicle(evse.AddEntity(EntityTypeType.EV));

            // One energy manager playing both coordinating actors, which is what
            // a home energy manager with a tariff subscription actually is.
            guard  = new CEVCEnergyGuard (cemEntity);
            broker = new CEVCEnergyBroker(cemEntity);

            wire = new SPINELoopback(hems, evse);

            await car.   Register();
            await guard. Register();
            await broker.Register();

            await Discover();

        }

        #endregion

        #region (private) Discover() / Commission() / the sides

        private async Task Discover()
        {
            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);
        }

        /// <summary>The car, as the energy manager sees it.</summary>
        private SPINERemoteEntity EV
            => wire.BAsSeenByA.Entity([ 1 ])!;

        /// <summary>The energy manager, as the car sees it.</summary>
        private SPINERemoteEntity CEM
            => wire.AAsSeenByB.Entity([ 1 ])!;


        private async Task Commission()
        {

            await guard. Subscribe(EV);
            await broker.Subscribe(EV);

            // The car watches whoever is coordinating it.
            await new UseCaseFeature(FeatureTypeType.DeviceDiagnosis, car.Entity, CEM).Subscribe();

        }


        private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

        #endregion


        #region AllThreeActorsFindEachOther()

        [Test]
        public void AllThreeActorsFindEachOther()
        {

            Assert.Multiple(() => {

                Assert.That(guard. PartnerFor(EV),            Is.Not.Null);
                Assert.That(broker.PartnerFor(EV),            Is.Not.Null);

                Assert.That(guard. PartnerFor(EV)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 4, 5, 7 }));
                Assert.That(broker.PartnerFor(EV)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 3, 4, 6, 8 }));

                Assert.That(guard. Actor,  Is.EqualTo("EnergyGuard"));
                Assert.That(broker.Actor,  Is.EqualTo("EnergyBroker"));

                Assert.That(car.PartnerFor(CEM)?.Scenarios,
                            Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

            });

        }

        #endregion

        #region TheThreeCurvesLiveOnOneFeatureAndAreToldApartByType()

        /// <summary>
        /// Table 5: one TimeSeries feature holds all three, and
        /// `timeSeriesType` is what distinguishes them. Only the constraints
        /// curve is writeable - the specification saying who owns which curve.
        /// </summary>
        [Test]
        public async Task TheThreeCurvesLiveOnOneFeatureAndAreToldApartByType()
        {

            await Commission();

            var descriptions = car.TimeSeries.
                                   DataCopy<TimeSeriesDescriptionListDataType>(CoordinatedEVCharging.TimeSeriesDescriptionListData)!.
                                   TimeSeriesDescriptionData!;

            Assert.Multiple(() => {

                Assert.That(descriptions, Has.Count.EqualTo(3));

                var demand      = descriptions.Single(description => description.TimeSeriesType == TimeSeriesTypeType.SingleDemand);
                var constraints = descriptions.Single(description => description.TimeSeriesType == TimeSeriesTypeType.Constraints);
                var plan        = descriptions.Single(description => description.TimeSeriesType == TimeSeriesTypeType.Plan);

                Assert.That(demand.Unit,                    Is.EqualTo(UnitOfMeasurementType.Wh),
                            "The energy demand is an energy, so it is in watt hours.");
                Assert.That(constraints.Unit,               Is.EqualTo(UnitOfMeasurementType.W));
                Assert.That(plan.Unit,                      Is.EqualTo(UnitOfMeasurementType.W));

                Assert.That(demand.TimeSeriesWriteable,      Is.False);
                Assert.That(constraints.TimeSeriesWriteable, Is.True);
                Assert.That(plan.TimeSeriesWriteable,        Is.False);

                Assert.That(descriptions.Select(description => description.TimeSeriesId).Distinct().Count(),
                            Is.EqualTo(3));

            });

        }

        #endregion

        #region Scenario1_TheDemandIsThreeNumbersRatherThanOne()

        /// <summary>
        /// [CEVC-003] to [CEVC-005], and the difference between the three is the
        /// whole reason the use case exists: enough to reach the hospital,
        /// enough to reach work, and everything the battery could still take if
        /// the energy happens to be free.
        /// </summary>
        [Test]
        public async Task Scenario1_TheDemandIsThreeNumbersRatherThanOne()
        {

            await Commission();

            await car.SetDemand(new ChargingDemand(Departure:      8 * Hour,
                                                    MinimumEnergy:  5_000,
                                                    OptimumEnergy:  22_000,
                                                    MaximumEnergy:  48_000));

            var demand = guard.DemandOf(EV);

            Assert.Multiple(() => {

                Assert.That(demand,                Is.Not.Null);
                Assert.That(demand?.MinimumEnergy, Is.EqualTo(5_000));
                Assert.That(demand?.OptimumEnergy, Is.EqualTo(22_000));
                Assert.That(demand?.MaximumEnergy, Is.EqualTo(48_000));
                Assert.That(demand?.Departure,     Is.EqualTo(8 * Hour));

                // And the broker reads exactly the same thing from exactly the
                // same place.
                Assert.That(broker.DemandOf(EV),   Is.EqualTo(demand));

            });

        }

        #endregion

        #region Scenario1_ADemandWithNothingInItIsRefused()

        /// <summary>
        /// "Of course, at least one value SHALL be communicated."
        /// </summary>
        [Test]
        public void Scenario1_ADemandWithNothingInItIsRefused()
        {

            Assert.That(async () => await car.SetDemand(new ChargingDemand()),
                        Throws.ArgumentException);

        }

        #endregion

        #region Scenario2_TheEnergyGuardWritesACurveRatherThanANumber()

        /// <summary>
        /// The difference from the curtailment use cases: this says what will be
        /// available in an hour as well as what is available now, so the car can
        /// decide *when* to charge rather than only how hard.
        /// </summary>
        [Test]
        public async Task Scenario2_TheEnergyGuardWritesACurveRatherThanANumber()
        {

            await Commission();

            var response = await guard.WritePowerLimits(EV, [
                                     new PowerSlot(2 * Hour, MaxValue:  3_600),
                                     new PowerSlot(3 * Hour, MaxValue: 11_000),
                                     new PowerSlot(3 * Hour, MaxValue:  1_400)
                                 ]);

            Assert.Multiple(() => {

                Assert.That(response.IsError, Is.False, response.Result?.Description);

                Assert.That(car.PowerLimits,  Has.Count.EqualTo(3));

                Assert.That(car.PowerLimits[0].Duration, Is.EqualTo(2 * Hour));
                Assert.That(car.PowerLimits[0].MaxValue, Is.EqualTo(3_600));
                Assert.That(car.PowerLimits[1].MaxValue, Is.EqualTo(11_000));
                Assert.That(car.PowerLimits[2].MaxValue, Is.EqualTo(1_400));

            });

        }

        #endregion

        #region Scenario2_ASlotOfZeroSecondsIsRefused()

        /// <summary>
        /// A slot "SHALL only contain values greater than zero seconds"; a slot
        /// of no length is a statement about no time at all.
        /// </summary>
        [Test]
        public async Task Scenario2_ASlotOfZeroSecondsIsRefused()
        {

            await Commission();

            Assert.That(async () => await guard.WritePowerLimits(EV, [ new PowerSlot(TimeSpan.Zero, MaxValue: 1) ]),
                        Throws.ArgumentException);

        }

        #endregion

        #region Scenario2_NobodyMayWriteTheDemandOrThePlan()

        /// <summary>
        /// The two curves which are the car's own. `timeSeriesWriteable: false`
        /// says so in the description, and this is what happens when a client
        /// ignores it: the demand is what the car wants and the plan is what the
        /// car intends, and neither becomes truer for somebody else writing it.
        /// </summary>
        [Test]
        public async Task Scenario2_NobodyMayWriteTheDemandOrThePlan()
        {

            await Commission();

            await car.SetDemand(new ChargingDemand(MinimumEnergy: 5_000));

            var demandId = car.TimeSeries.
                               DataCopy<TimeSeriesDescriptionListDataType>(CoordinatedEVCharging.TimeSeriesDescriptionListData)!.
                               TimeSeriesDescriptionData!.
                               Single(description => description.TimeSeriesType == TimeSeriesTypeType.SingleDemand).
                               TimeSeriesId;

            var response = await guard.TimeSeriesOf(EV).WriteData(
                                     CoordinatedEVCharging.TimeSeriesListData,
                                     new TimeSeriesListDataType {
                                         TimeSeriesData = [
                                             new TimeSeriesDataType {
                                                 TimeSeriesId    = demandId,
                                                 TimeSeriesSlot  = [
                                                     new TimeSeriesSlotType {
                                                         TimeSeriesSlotId  = 1,
                                                         MinValue          = ScaledNumberType.FromValue(999_999)
                                                     }
                                                 ]
                                             }
                                         ]
                                     });

            Assert.Multiple(() => {

                Assert.That(response.IsError,             Is.True,
                            "An energy guard rewrote the car's own energy demand.");

                Assert.That(response.Result?.Description, Does.Contain("not writeable"));

                Assert.That(car.Demand?.MinimumEnergy,    Is.EqualTo(5_000),
                            "The refused write changed the demand anyway.");

            });

        }

        #endregion

        #region Scenario3_TheBrokerWritesPricesIntoTheTariffTheCarDescribed()

        [Test]
        public async Task Scenario3_TheBrokerWritesPricesIntoTheTariffTheCarDescribed()
        {

            await Commission();

            Assert.That(broker.TariffIdOf(EV), Is.Not.Null,
                        "The car described no writeable tariff for the broker to fill in.");

            var response = await broker.WriteIncentives(EV, [
                                     new IncentiveSlot(TimeSpan.Zero, 3 * Hour, 0.32m),
                                     new IncentiveSlot(3 * Hour,      6 * Hour, 0.08m),
                                     new IncentiveSlot(6 * Hour,      9 * Hour, 0.24m)
                                 ]);

            Assert.Multiple(() => {

                Assert.That(response.IsError,     Is.False, response.Result?.Description);

                Assert.That(car.Incentives,       Has.Count.EqualTo(3));
                Assert.That(car.Incentives[0].Cost,  Is.EqualTo(0.32m));
                Assert.That(car.Incentives[1].Cost,  Is.EqualTo(0.08m));
                Assert.That(car.Incentives[1].Start, Is.EqualTo(3 * Hour));
                Assert.That(car.Incentives[2].End,   Is.EqualTo(9 * Hour));

            });

        }

        #endregion

        #region Scenario3_IncentiveSlotsMayNotOverlapAndTheLastOneHasToEnd()

        /// <summary>
        /// "The timeInterval of different incentiveSlots within an incentiveTable
        /// SHALL NOT overlap in time", and the last slot "SHALL" state an end - a
        /// price with no end is a promise nobody made.
        /// </summary>
        [Test]
        public async Task Scenario3_IncentiveSlotsMayNotOverlapAndTheLastOneHasToEnd()
        {

            await Commission();

            Assert.Multiple(() => {

                Assert.That(async () => await broker.WriteIncentives(EV, [
                                            new IncentiveSlot(TimeSpan.Zero, 4 * Hour, 0.30m),
                                            new IncentiveSlot(3 * Hour,      6 * Hour, 0.10m)
                                        ]),
                            Throws.ArgumentException,
                            "Overlapping incentive slots were accepted.");

                Assert.That(async () => await broker.WriteIncentives(EV, [
                                            new IncentiveSlot(TimeSpan.Zero, null, 0.30m)
                                        ]),
                            Throws.ArgumentException,
                            "A last incentive slot without an end was accepted.");

            });

        }

        #endregion

        #region Scenario4_TheCarAnswersWithAPlanEverybodyCanRead()

        /// <summary>
        /// What makes coordinated charging coordinated: given the demand, the
        /// available power and the prices, this is what the car will actually
        /// do - and both coordinators read it from the same place.
        /// </summary>
        [Test]
        public async Task Scenario4_TheCarAnswersWithAPlanEverybodyCanRead()
        {

            await Commission();

            await car.SetPlan([
                new PowerSlot(3 * Hour, Value:  1_400),
                new PowerSlot(3 * Hour, Value: 11_000),
                new PowerSlot(2 * Hour, Value:      0)
            ]);

            Assert.Multiple(() => {

                Assert.That(guard. PlanOf(EV), Has.Count.EqualTo(3));
                Assert.That(broker.PlanOf(EV), Has.Count.EqualTo(3));

                // The cheap hours are the ones it charges hardest in.
                Assert.That(guard.PlanOf(EV)[1].Value,    Is.EqualTo(11_000));
                Assert.That(guard.PlanOf(EV)[1].Duration, Is.EqualTo(3 * Hour));
                Assert.That(guard.PlanOf(EV)[2].Value,    Is.EqualTo(0));

            });

        }

        #endregion

        #region TheCarAsksByRaisingAFlagBecauseAServerCannotAsk()

        /// <summary>
        /// [CEVC-015] and [CEVC-030], and the most interesting mechanism in the
        /// use case. The car is the server of everything here, and a SPINE
        /// server answers rather than requests - so when it needs a fresh power
        /// limit curve it raises **updateRequired** on the description its
        /// clients are subscribed to. The flag *is* the request.
        ///
        /// And it goes down by itself: "The server SHALL set the updateRequired
        /// back to 'false', as soon as [the data] was updated successfully."
        /// </summary>
        [Test]
        public async Task TheCarAsksByRaisingAFlagBecauseAServerCannotAsk()
        {

            await Commission();

            var asked = 0;
            guard.OnPowerLimitsRequested += (_, _) => asked++;

            Assert.That(guard.PowerLimitsRequestedBy(EV), Is.False);

            await car.RequestPowerLimits();

            Assert.Multiple(() => {
                Assert.That(car.PowerLimitsRequested,         Is.True);
                Assert.That(guard.PowerLimitsRequestedBy(EV), Is.True,
                            "The energy guard was not told that the car wants a new curve.");
                Assert.That(asked,                            Is.EqualTo(1));
            });

            await guard.WritePowerLimits(EV, [ new PowerSlot(Hour, MaxValue: 4_000) ]);

            Assert.Multiple(() => {
                Assert.That(car.PowerLimitsRequested,         Is.False,
                            "The update request stayed up after the curve arrived.");
                Assert.That(guard.PowerLimitsRequestedBy(EV), Is.False);
                Assert.That(car.PowerLimits[0].MaxValue,      Is.EqualTo(4_000));
            });

        }

        #endregion

        #region TheSameFlagWorksForTheIncentiveTable()

        [Test]
        public async Task TheSameFlagWorksForTheIncentiveTable()
        {

            await Commission();

            var asked = 0;
            broker.OnIncentivesRequested += (_, _) => asked++;

            await car.RequestIncentives();

            Assert.Multiple(() => {
                Assert.That(car.IncentivesRequested,           Is.True);
                Assert.That(broker.IncentivesRequestedBy(EV),  Is.True);
                Assert.That(asked,                             Is.EqualTo(1));
            });

            await broker.WriteIncentives(EV, [ new IncentiveSlot(TimeSpan.Zero, Hour, 0.21m) ]);

            Assert.Multiple(() => {
                Assert.That(car.IncentivesRequested,           Is.False);
                Assert.That(broker.IncentivesRequestedBy(EV),  Is.False);
                Assert.That(car.Incentives[0].Cost,            Is.EqualTo(0.21m));
            });

        }

        #endregion

        #region ACarWithoutABrokerStillDoesTheRest()

        /// <summary>
        /// The energy broker is a separate actor and a car may have none - a
        /// house with an energy manager and no dynamic tariff. Scenarios 3, 6
        /// and 8 then do not exist, and the other five are unaffected.
        /// </summary>
        [Test]
        public async Task ACarWithoutABrokerStillDoesTheRest()
        {

            var hems  = new SPINELocalDevice("d:_i:19667_HEMS2", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse  = new SPINELocalDevice("d:_i:19667_EVSE2", DeviceTypeType.ChargingStation,        TimeProvider: time);

            var plain = new CEVCElectricVehicle(evse.AddEntity(EntityTypeType.EV), WithBroker: false);
            var watch = new CEVCEnergyGuard    (hems.AddEntity(EntityTypeType.CEM));

            var link  = new SPINELoopback(hems, evse);

            await plain.Register();
            await watch.Register();

            await link.A.NodeManagement.RequestDetailedDiscovery(link.BAsSeenByA);
            await link.A.NodeManagement.RequestUseCaseData      (link.BAsSeenByA);

            var partner = link.BAsSeenByA.Entity([ 1 ])!;

            await watch.Subscribe(partner);
            await watch.WritePowerLimits(partner, [ new PowerSlot(Hour, MaxValue: 7_000) ]);

            Assert.Multiple(() => {

                Assert.That(plain.IncentiveTable, Is.Null);

                Assert.That(watch.PartnerFor(partner)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 4, 5, 7 }));

                Assert.That(plain.PowerLimits[0].MaxValue, Is.EqualTo(7_000));

                Assert.That(async () => await plain.RequestIncentives(),
                            Throws.InvalidOperationException,
                            "A car without a broker asked one for prices.");

            });

        }

        #endregion

        #region Scenarios5To8_TheCarWatchesWhoeverCoordinatesIt()

        /// <summary>
        /// Four scenarios for two facts about two partners: is it there, and is
        /// it working. The car is the client of both diagnoses.
        /// </summary>
        [Test]
        public async Task Scenarios5To8_TheCarWatchesWhoeverCoordinatesIt()
        {

            await Commission();

            await guard.StartHeartbeat(TimeSpan.FromSeconds(30));

            var diagnosis = new UseCaseFeature(FeatureTypeType.DeviceDiagnosis, car.Entity, CEM);

            await guard.SetOperatingState(DeviceDiagnosisOperatingStateType.Failure, "E-9");

            Assert.Multiple(() => {

                Assert.That(diagnosis.Data<DeviceDiagnosisStateDataType>(CoordinatedEVCharging.StateData)?.OperatingState,
                            Is.EqualTo(DeviceDiagnosisOperatingStateType.Failure));

                Assert.That(diagnosis.Data<DeviceDiagnosisStateDataType>(CoordinatedEVCharging.StateData)?.LastErrorCode,
                            Is.EqualTo("E-9"));

                // One energy manager playing both actors has one diagnosis
                // feature and therefore one heartbeat, not two.
                Assert.That(guard.Diagnosis, Is.SameAs(broker.Diagnosis));

            });

        }

        #endregion

    }

}
