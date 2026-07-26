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
using cloud.charging.open.protocols.EEBUS.UseCases.ChargingCurrent;
using cloud.charging.open.protocols.EEBUS.UseCases.OSCEV;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "Optimization of Self-Consumption During EV Charging", both actors, over
    /// the wire.
    ///
    /// The overload protection with two words changed - a recommendation instead
    /// of an obligation - and the tests here are about what follows from those
    /// two words, because everything else is shared and already tested by
    /// <see cref="OPEVTests"/>.
    /// </summary>
    [TestFixture]
    public class OSCEVTests
    {

        #region Data

        private FakeTimeProvider      time     = null!;
        private SPINELoopback         wire     = null!;

        private OSCEVEnergyManager    manager  = null!;
        private OSCEVElectricVehicle  ev       = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var car  = new SPINELocalDevice("d:_i:19667_EV",   DeviceTypeType.Generic,                TimeProvider: time);

            manager = new OSCEVEnergyManager  (hems.AddEntity(EntityTypeType.CEM));
            ev      = new OSCEVElectricVehicle(car. AddEntity(EntityTypeType.EV));

            ev.SetPermittedCurrents(6, 16);

            wire = new SPINELoopback(hems, car);

            await manager.Register();
            await ev.     Register();

            await Discover();

        }

        #endregion

        #region (private) Discover() / Commission() / the two sides

        private async Task Discover()
        {
            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);
        }

        /// <summary>The EV, as the energy manager sees it.</summary>
        private SPINERemoteEntity EV
            => wire.BAsSeenByA.Entity([ 1 ])!;

        /// <summary>The energy manager, as the EV sees it.</summary>
        private SPINERemoteEntity CEM
            => wire.AAsSeenByB.Entity([ 1 ])!;


        private async Task Commission()
        {

            await new UseCaseFeature(FeatureTypeType.DeviceDiagnosis, ev.Entity, CEM).Subscribe();

            var loadControl = manager.LoadControlOf(EV);

            await loadControl.Subscribe();
            await loadControl.Bind();

            await manager.ElectricalOf(EV).Subscribe();

            await manager.StartHeartbeat();

            ev.Check();

        }

        #endregion


        #region TheTwoActorsFindEachOther()

        [Test]
        public void TheTwoActorsFindEachOther()
        {

            Assert.Multiple(() => {
                Assert.That(manager.PartnerFor(EV),                     Is.Not.Null);
                Assert.That(manager.PartnerFor(EV)?.Scenarios,          Is.EquivalentTo(new UInt32[] { 1, 2, 3 }));
                Assert.That(manager.PartnerFor(EV)?.Version.ToString(), Is.EqualTo("1.0.1"));
                Assert.That(manager.Actor,                              Is.EqualTo("CEM"));
                Assert.That(ev.     PartnerFor(CEM),                    Is.Not.Null);
            });

        }

        #endregion

        #region TheLimitIsARecommendationRatherThanAnObligation()

        /// <summary>
        /// Table 6 - and this one word is the whole use case. An obligation is
        /// kept because a fuse is behind it; a recommendation is followed
        /// because it is a good idea.
        /// </summary>
        [Test]
        public void TheLimitIsARecommendationRatherThanAnObligation()
        {

            var descriptions = ev.LoadControl.
                                  DataCopy<LoadControlLimitDescriptionListDataType>(SelfConsumptionOptimization.LimitDescriptionListData)!.
                                  LoadControlLimitDescriptionData!;

            Assert.Multiple(() => {

                Assert.That(descriptions, Has.Count.EqualTo(3));

                foreach (var description in descriptions)
                {
                    Assert.That(description.LimitCategory, Is.EqualTo(LoadControlCategoryType.Recommendation));
                    Assert.That(description.ScopeType,     Is.EqualTo(ScopeTypeType.SelfConsumption));
                    Assert.That(description.Unit,          Is.EqualTo(UnitOfMeasurementType.A));
                }

                Assert.That(descriptions.Any(SelfConsumptionOptimization.IsALimit), Is.True);

                Assert.That(descriptions.Any(OPEV.OverloadProtection.IsALimit), Is.False,
                            "A self-consumption recommendation was mistaken for an overload protection obligation.");

            });

        }

        #endregion

        #region Scenario1_TheSelfProducedCurrentReachesTheCar()

        [Test]
        public async Task Scenario1_TheSelfProducedCurrentReachesTheCar()
        {

            await Commission();

            var response = await manager.WriteSelfProducedCurrent(EV, 12);

            Assert.Multiple(() => {
                Assert.That(response.IsError,           Is.False, response.Result?.Description);
                Assert.That(ev.Trust,                    Is.EqualTo(ChargingCurrentTrust.Following));
                Assert.That(ev.RecommendedCurrents,      Is.EqualTo(new Decimal?[] { 12, 12, 12 }));
            });

        }

        #endregion

        #region Scenario1_EachPhaseMayGetItsOwnCurrent()

        /// <summary>
        /// [OSCEV-002]: where the car charges asymmetrically the manager should
        /// tell it about each phase separately, so that a phase which is already
        /// loaded gets less than one with spare production.
        /// </summary>
        [Test]
        public async Task Scenario1_EachPhaseMayGetItsOwnCurrent()
        {

            await Commission();

            await manager.WriteSelfProducedCurrents(EV, [ 6, 10, 16 ]);

            Assert.That(ev.RecommendedCurrents, Is.EqualTo(new Decimal?[] { 6, 10, 16 }));

        }

        #endregion

        #region Scenario2_LosingTheManagerMeansNoAdviceRatherThanASafeCurrent()

        /// <summary>
        /// The difference between the two charging current use cases, and the
        /// reason they are one implementation with a profile rather than one
        /// implementation.
        ///
        /// Under the overload protection a silent energy guard means falling
        /// back to a low safe current, because the fuse is still there. Here it
        /// means no advice at all: the car charges as it otherwise would.
        /// Falling back to a safe current would slow a charging session down
        /// because a photovoltaic forecast stopped arriving.
        /// </summary>
        [Test]
        public async Task Scenario2_LosingTheManagerMeansNoAdviceRatherThanASafeCurrent()
        {

            await Commission();

            await manager.WriteSelfProducedCurrent(EV, 16);

            Assert.That(ev.RecommendedCurrents[0], Is.EqualTo(16));

            manager.StopHeartbeat();

            time.Advance(SelfConsumptionOptimization.HeartbeatTimeout + TimeSpan.FromSeconds(1));

            var change = ev.Check();

            Assert.Multiple(() => {

                Assert.That(change?.To,        Is.EqualTo(ChargingCurrentTrust.HeartbeatMissing));
                Assert.That(change?.Reason,    Does.Contain("OSCEV-005"));
                Assert.That(change?.Reason,    Does.Not.Contain("OPEV-"),
                            "The message quoted the wrong specification.");

                Assert.That(ev.RecommendedCurrents, Is.EqualTo(new Decimal?[] { null, null, null }),
                            "A car which lost its energy manager fell back to a safe current instead of simply ignoring the advice.");

            });

        }

        #endregion

        #region Scenario3_AnAnnouncedFailureIsFasterThanASilence()

        /// <summary>
        /// [OSCEV-007]: the manager is still beating, so the availability check
        /// would never notice - but it has said that it is not working, and
        /// self-consumption advice from a broken energy manager is worse than
        /// none.
        /// </summary>
        [Test]
        public async Task Scenario3_AnAnnouncedFailureIsFasterThanASilence()
        {

            await Commission();

            await manager.WriteSelfProducedCurrent(EV, 16);

            Assert.That(ev.RecommendedCurrents[0], Is.EqualTo(16));

            await manager.SetOperatingState(DeviceDiagnosisOperatingStateType.Failure, "E-1");

            Assert.Multiple(() => {
                Assert.That(ev.Trust,                Is.EqualTo(ChargingCurrentTrust.PartnerFailed));
                Assert.That(ev.RecommendedCurrents,  Is.EqualTo(new Decimal?[] { null, null, null }));
            });

            await manager.SetOperatingState(DeviceDiagnosisOperatingStateType.NormalOperation);

            Assert.Multiple(() => {
                Assert.That(ev.Trust,                Is.EqualTo(ChargingCurrentTrust.Following));
                Assert.That(ev.RecommendedCurrents,  Is.EqualTo(new Decimal?[] { 16, 16, 16 }));
            });

        }

        #endregion

        #region Scenario1_AnInactiveRecommendationIsNoRecommendation()

        /// <summary>
        /// The sun went behind a cloud. The manager is there and healthy and has
        /// nothing to advise, which is a different fact from the manager being
        /// gone - and both come out as "charge as you otherwise would".
        /// </summary>
        [Test]
        public async Task Scenario1_AnInactiveRecommendationIsNoRecommendation()
        {

            await Commission();

            await manager.WriteSelfProducedCurrent(EV, 16);
            await manager.WriteSelfProducedCurrent(EV, 0, IsActive: false);

            Assert.Multiple(() => {

                Assert.That(ev.Trust,                Is.EqualTo(ChargingCurrentTrust.Following),
                            "An inactive recommendation was mistaken for a missing energy manager.");

                Assert.That(ev.RecommendedCurrents,  Is.EqualTo(new Decimal?[] { null, null, null }));

            });

        }

        #endregion

        #region Scenario1_ACarWithNoRoomLeftWithdrawsTheScenario()

        /// <summary>
        /// [OSCEV-009]: "If the EV has no more flexibility to consume
        /// self-produced energy (e.g. the EV has reached the maximum energy
        /// capacity), the EV SHALL stop to support this scenario."
        ///
        /// The scenario, not the use case. Scenarios 2 and 3 are still true -
        /// the car is still watching whether the manager is there - and an
        /// energy manager reading the use case data should see a car which
        /// implements the optimisation and currently has nothing to optimise.
        /// </summary>
        [Test]
        public async Task Scenario1_ACarWithNoRoomLeftWithdrawsTheScenario()
        {

            await Commission();

            Assert.That(ev.HasFlexibility, Is.True);

            await ev.SetFlexibility(false);
            await Discover();

            Assert.Multiple(() => {

                Assert.That(ev.HasFlexibility, Is.False);

                Assert.That(manager.PartnerFor(EV)?.Scenarios, Is.EquivalentTo(new UInt32[] { 2, 3 }),
                            "A full car kept announcing that it can consume self-produced current.");

                Assert.That(manager.PartnerFor(EV)?.Available, Is.True,
                            "The whole use case was withdrawn where only one scenario should have been.");

            });

            await ev.SetFlexibility(true);
            await Discover();

            Assert.That(manager.PartnerFor(EV)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3 }));

        }

        #endregion

        #region TheReactionBudgetIsThreeSecondsRatherThanSix()

        /// <summary>
        /// [OSCEV-004] against OPEV section 2.1. Not a fuse, so nothing trips if
        /// the car is late - but a photovoltaic system's output moves with the
        /// clouds, so late advice is advice about weather which has passed.
        /// </summary>
        [Test]
        public void TheReactionBudgetIsThreeSecondsRatherThanSix()
        {

            Assert.Multiple(() => {

                Assert.That(SelfConsumptionOptimization.ReactionBudget,   Is.EqualTo(TimeSpan.FromSeconds(3)));
                Assert.That(OPEV.OverloadProtection.ReactionBudget,        Is.EqualTo(TimeSpan.FromSeconds(6)));

                Assert.That(SelfConsumptionOptimization.HeartbeatTimeout,  Is.EqualTo(TimeSpan.FromSeconds(4)),
                            "Both use cases give the heartbeat four seconds (OSCEV Table 10, OPEV Table 10).");

            });

        }

        #endregion

    }

}
