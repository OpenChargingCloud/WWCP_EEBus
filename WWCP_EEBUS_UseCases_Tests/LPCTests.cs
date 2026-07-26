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
using cloud.charging.open.protocols.EEBUS.UseCases.LPC;
using cloud.charging.open.protocols.EEBUS.UseCases.LimitationOfPower;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "Limitation of Power Consumption", both actors, over the wire.
    ///
    /// An energy manager limits a charging station: discover each other, agree
    /// the subscriptions and the binding, send heartbeats, write a limit - and
    /// then stop sending heartbeats, which is the case the whole use case exists
    /// for.
    /// </summary>
    [TestFixture]
    public class LPCTests
    {

        #region Data

        private FakeTimeProvider        time    = null!;
        private SPINELoopback           wire    = null!;

        private LPCEnergyGuard          guard   = null!;
        private LPCControllableSystem   system  = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            guard   = new LPCEnergyGuard       (hems.AddEntity(EntityTypeType.CEM));
            system  = new LPCControllableSystem(evse.AddEntity(EntityTypeType.EVSE));

            system.FailsafeLimit            = 4200;
            system.FailsafeDurationMinimum  = TimeSpan.FromHours(2);
            system.ConsumptionNominalMax    = 11000;

            wire = new SPINELoopback(hems, evse);

            await guard. Register();
            await system.Register();

            await Discover();

        }

        #endregion

        #region (private) Discover() / the two sides

        private async Task Discover()
        {
            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);
        }

        /// <summary>The charging station, as the energy manager sees it.</summary>
        private SPINERemoteEntity CS
            => wire.BAsSeenByA.Entity([ 1 ])!;

        /// <summary>The energy manager, as the charging station sees it.</summary>
        private SPINERemoteEntity EG
            => wire.AAsSeenByB.Entity([ 1 ])!;


        /// <summary>
        /// What the two actors do before anything else: the charging station
        /// subscribes to the heartbeat, the energy manager subscribes to the
        /// limit and binds so that it may write it.
        /// </summary>
        private async Task Commission()
        {

            await new UseCaseFeature(FeatureTypeType.DeviceDiagnosis, system.Entity, EG).Subscribe();

            var loadControl = guard.LoadControlOf(CS);

            await loadControl.Subscribe();
            await loadControl.Bind();

            await guard.ConfigurationOf(CS).Bind();

            await guard.StartHeartbeat();

        }

        #endregion


        #region TheTwoActorsFindEachOther()

        /// <summary>
        /// Both actors announce themselves, and each finds the other with the
        /// four scenarios of the use case.
        /// </summary>
        [Test]
        public void TheTwoActorsFindEachOther()
        {

            Assert.Multiple(() => {

                var partnerOfGuard  = guard. PartnerFor(CS);
                var partnerOfSystem = system.PartnerFor(EG);

                Assert.That(partnerOfGuard,             Is.Not.Null,
                            "The energy guard did not recognise the charging station.");
                Assert.That(partnerOfGuard?.Scenarios,  Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4 }));
                Assert.That(partnerOfGuard?.Version.ToString(), Is.EqualTo("1.0.0"));

                Assert.That(partnerOfSystem,            Is.Not.Null,
                            "The charging station did not recognise the energy guard.");
                Assert.That(partnerOfSystem?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4 }),
                            "The energy guard has to have a device diagnosis server for scenario 3.");

            });

        }

        #endregion

        #region TheUseCaseIsAnnouncedWithItsActorAndScenarios()

        /// <summary>
        /// What goes over the wire is the use case discovery of section 3.1.2:
        /// the name, the actor and the four scenarios.
        /// </summary>
        [Test]
        public void TheUseCaseIsAnnouncedWithItsActorAndScenarios()
        {

            var announced = wire.B.NodeManagement.UseCases.First();
            var support   = announced.UseCaseSupport![0];

            Assert.Multiple(() => {
                Assert.That(announced.Actor,                    Is.EqualTo("ControllableSystem"));
                Assert.That(support.UseCaseName,                Is.EqualTo("limitationOfPowerConsumption"));
                Assert.That(support.UseCaseVersion,             Is.EqualTo("1.0.0"));
                Assert.That(support.UseCaseDocumentSubRevision,  Is.EqualTo("release"));
                Assert.That(support.ScenarioSupport,            Is.EqualTo(new UInt32[] { 1, 2, 3, 4 }));
                Assert.That(support.UseCaseAvailable,           Is.True);
            });

        }

        #endregion

        #region TheLimitIsFoundByItsDescriptionRatherThanItsNumber()

        /// <summary>
        /// A device may have several load control limits on one feature, and
        /// only the description says which is the active power consumption limit
        /// of this use case (Table 14). The energy guard looks it up rather than
        /// assuming an identifier.
        /// </summary>
        [Test]
        public async Task TheLimitIsFoundByItsDescriptionRatherThanItsNumber()
        {

            await Commission();

            var loadControl  = guard.LoadControlOf(CS);

            await loadControl.RequestData(PowerLimitation.LimitDescriptionListData);

            var description  = loadControl.Data<LoadControlLimitDescriptionListDataType>(PowerLimitation.LimitDescriptionListData)?.
                                   LoadControlLimitDescriptionData?.FirstOrDefault();

            Assert.Multiple(() => {
                Assert.That(PowerLimitation.Consumption.IsTheLimit(description),  Is.True);
                Assert.That(description?.LimitType,       Is.EqualTo(LoadControlLimitTypeType.SignDependentAbsValueLimit));
                Assert.That(description?.LimitCategory,   Is.EqualTo(LoadControlCategoryType.Obligation));
                Assert.That(description?.LimitDirection,  Is.EqualTo(EnergyDirectionType.Consume));
                Assert.That(description?.ScopeType,       Is.EqualTo(ScopeTypeType.ActivePowerLimit));
                Assert.That(description?.Unit,            Is.EqualTo(UnitOfMeasurementType.W));
            });

        }

        #endregion

        #region Scenario1_TheEnergyGuardLimitsTheChargingStation()

        /// <summary>
        /// Scenario 1: the energy guard writes an activated limit, the
        /// controllable system accepts it and goes into "limited".
        /// </summary>
        [Test]
        public async Task Scenario1_TheEnergyGuardLimitsTheChargingStation()
        {

            await Commission();

            var response = await guard.WriteConsumptionLimit(CS, 4200, IsActive: true);

            Assert.Multiple(() => {

                Assert.That(response.IsError,                    Is.False, response.Result?.Description);

                Assert.That(system.StateMachine.State,           Is.EqualTo(PowerLimitationState.Limited));
                Assert.That(system.StateMachine.Limitation,      Is.EqualTo(PowerLimitationApplied.ActivePowerLimit));

                Assert.That(system.ConsumptionLimit.Value,       Is.EqualTo(4200));
                Assert.That(system.ConsumptionLimit.IsActive,    Is.True);

            });

            // ... and the energy guard sees it, because it subscribed.
            var seen = await guard.ReadConsumptionLimit(CS);

            Assert.Multiple(() => {
                Assert.That(seen.Value,     Is.EqualTo(4200));
                Assert.That(seen.IsActive,  Is.True);
            });

        }

        #endregion

        #region Scenario1_ALimitBelowZeroIsRefused()

        /// <summary>
        /// Section 2.2: "A limit lower than 0W SHALL be rejected."
        ///
        /// The energy guard of this stack will not even send one; a partner
        /// which does gets a NACK and changes nothing.
        /// </summary>
        [Test]
        public async Task Scenario1_ALimitBelowZeroIsRefused()
        {

            await Commission();

            await guard.WriteConsumptionLimit(CS, 4200, IsActive: true);

            Assert.That(() => guard.WriteConsumptionLimit(CS, -1, IsActive: true),
                        Throws.InstanceOf<ArgumentOutOfRangeException>(),
                        "The energy guard sent a limit below zero.");

            // The same thing arriving from somebody less careful.
            var loadControl = guard.LoadControlOf(CS);

            var response    = await loadControl.WriteData(
                                        PowerLimitation.LimitListData,
                                        new LoadControlLimitListDataType {
                                            LoadControlLimitData = [
                                                new LoadControlLimitDataType {
                                                    LimitId        = 1,
                                                    IsLimitActive  = true,
                                                    Value          = ScaledNumberType.FromValue(-1)
                                                }
                                            ]
                                        }
                                    );

            Assert.Multiple(() => {
                Assert.That(response.IsError,              Is.True);
                Assert.That(response.Result?.ErrorNumber,  Is.EqualTo(SPINEErrorNumbers.CommandRejected));
                Assert.That(response.Result?.Description,  Does.Contain("below zero"));
                Assert.That(system.ConsumptionLimit.Value, Is.EqualTo(4200),
                            "The refused limit was applied anyway.");
            });

        }

        #endregion

        #region Scenario1_ALimitWhichCannotBeAppliedIsNackedAndStillEndsTheFailsafeState()

        /// <summary>
        /// [LPC-003/1]: a limit which the controllable system cannot apply is
        /// answered with a NACK - and [LPC-918] adds that it nevertheless takes
        /// the system out of its failsafe state, into "unlimited/controlled".
        ///
        /// Both halves matter: the energy guard learns that its limit did not
        /// take effect, and the controllable system stops limiting itself to its
        /// failsafe value, because somebody is clearly there.
        /// </summary>
        [Test]
        public async Task Scenario1_ALimitWhichCannotBeAppliedIsNackedAndStillEndsTheFailsafeState()
        {

            await Commission();

            system.CanApplyLimit = _ => false;

            var response = await guard.WriteConsumptionLimit(CS, 4200, IsActive: true);

            Assert.Multiple(() => {
                Assert.That(response.IsError,              Is.True);
                Assert.That(response.Result?.ErrorNumber,  Is.EqualTo(SPINEErrorNumbers.CommandRejected));
                Assert.That(system.StateMachine.State,     Is.EqualTo(PowerLimitationState.UnlimitedControlled));
            });

        }

        #endregion

        #region Scenario2_TheEnergyGuardReadsAndChangesTheFailsafeValues()

        /// <summary>
        /// Scenario 2: the failsafe values are pre-configured in the
        /// controllable system, read by the energy guard, and MAY be changed by
        /// it ([LPC-021/2], [LPC-022/2]).
        /// </summary>
        [Test]
        public async Task Scenario2_TheEnergyGuardReadsAndChangesTheFailsafeValues()
        {

            await Commission();

            var before = await guard.ReadFailsafeValues(CS);

            Assert.Multiple(() => {
                Assert.That(before.Limit,            Is.EqualTo(4200));
                Assert.That(before.DurationMinimum,  Is.EqualTo(TimeSpan.FromHours(2)));
            });

            var response = await guard.WriteFailsafeValues(CS,
                                                           Limit:            2300,
                                                           DurationMinimum:  TimeSpan.FromHours(4));

            var after    = await guard.ReadFailsafeValues(CS);

            Assert.Multiple(() => {
                Assert.That(response.IsError,       Is.False, response.Result?.Description);
                Assert.That(after.Limit,            Is.EqualTo(2300));
                Assert.That(after.DurationMinimum,  Is.EqualTo(TimeSpan.FromHours(4)));
            });

        }

        #endregion

        #region Scenario2_AFailsafeDurationOutsideTheAllowedRangeIsRefused()

        /// <summary>
        /// [LPC-022/3]: "The Energy Guard SHALL choose a value between 2 hours
        /// and 24 hours", and [LPC-022/4] lets the controllable system refuse
        /// anything else.
        /// </summary>
        [Test]
        public async Task Scenario2_AFailsafeDurationOutsideTheAllowedRangeIsRefused()
        {

            await Commission();

            Assert.Multiple(() => {

                Assert.That(() => guard.WriteFailsafeValues(CS, DurationMinimum: TimeSpan.FromHours(1)),
                            Throws.InstanceOf<ArgumentOutOfRangeException>(),
                            "The energy guard sent a failsafe duration below two hours.");

                Assert.That(() => guard.WriteFailsafeValues(CS, DurationMinimum: TimeSpan.FromHours(25)),
                            Throws.InstanceOf<ArgumentOutOfRangeException>(),
                            "The energy guard sent a failsafe duration above 24 hours.");

            });

            // And the controllable system refuses it whoever sends it.
            var response = await guard.ConfigurationOf(CS).WriteData(
                                     PowerLimitation.KeyValueListData,
                                     new DeviceConfigurationKeyValueListDataType {
                                         DeviceConfigurationKeyValueData = [
                                             new DeviceConfigurationKeyValueDataType {
                                                 KeyId  = 2,
                                                 Value  = new DeviceConfigurationKeyValueValueType {
                                                              Duration = DurationType.Parse(TimeSpan.FromMinutes(30))
                                                          }
                                             }
                                         ]
                                     }
                                 );

            Assert.Multiple(() => {
                Assert.That(response.IsError,                  Is.True);
                Assert.That(response.Result?.Description,      Does.Contain("LPC-022/3"));
                Assert.That(system.FailsafeDurationMinimum,    Is.EqualTo(TimeSpan.FromHours(2)));
            });

        }

        #endregion

        #region Scenario3_WhenTheHeartbeatStopsTheChargingStationLimitsItself()

        /// <summary>
        /// Scenario 3, and the reason the whole use case exists: an energy guard
        /// which goes quiet does not leave the charging station unlimited. After
        /// 120 seconds without a heartbeat it holds itself to its failsafe
        /// value ([LPC-912]).
        /// </summary>
        [Test]
        public async Task Scenario3_WhenTheHeartbeatStopsTheChargingStationLimitsItself()
        {

            await Commission();

            await guard.WriteConsumptionLimit(CS, 4200, IsActive: true);

            Assert.That(system.StateMachine.State, Is.EqualTo(PowerLimitationState.Limited));

            // The heartbeats keep arriving, and nothing happens.
            for (var i = 0; i < 3; i++)
            {
                time.Advance(TimeSpan.FromSeconds(58));
                await system.Check();
            }

            Assert.That(system.StateMachine.State, Is.EqualTo(PowerLimitationState.Limited),
                        "The charging station fell into its failsafe state although the heartbeats arrived.");

            // The energy manager is unplugged.
            guard.StopHeartbeat();

            time.Advance(TimeSpan.FromSeconds(121));

            var transition = await system.Check();

            Assert.Multiple(() => {

                Assert.That(transition?.Transition,          Is.EqualTo(7));
                Assert.That(system.StateMachine.State,       Is.EqualTo(PowerLimitationState.FailsafeState));
                Assert.That(system.StateMachine.Limitation,  Is.EqualTo(PowerLimitationApplied.FailsafeLimit));

                // [LPC-009/2]: and it says so, so an energy guard reading the
                // limit sees what is actually happening.
                Assert.That(system.ConsumptionLimit.IsActive, Is.False);

            });

        }

        #endregion

        #region Scenario3_TheEnergyGuardIsToldThatTheLimitWasDeactivated()

        /// <summary>
        /// [LPC-009]: the controllable system sets the limit to activated or
        /// deactivated according to its own state - and the energy guard, which
        /// subscribed, hears about it without asking.
        /// </summary>
        [Test]
        public async Task Scenario3_TheEnergyGuardIsToldThatTheLimitWasDeactivated()
        {

            await Commission();

            await guard.WriteConsumptionLimit(CS, 4200, IsActive: true);

            guard.StopHeartbeat();

            time.Advance(TimeSpan.FromSeconds(121));

            await system.Check();

            // No read: the notify came by itself.
            var loadControl = guard.LoadControlOf(CS);

            var seen        = loadControl.Data<LoadControlLimitListDataType>(PowerLimitation.LimitListData)?.
                                  LoadControlLimitData?.FirstOrDefault();

            Assert.Multiple(() => {
                Assert.That(seen?.IsLimitActive, Is.False,
                            "The energy guard still believes its limit is active.");
                Assert.That(seen?.Value?.Value,  Is.EqualTo(4200),
                            "The value stays; only the activation follows the state.");
            });

        }

        #endregion

        #region Scenario3_AfterTheHeartbeatReturnsALimitTakesEffectAgain()

        /// <summary>
        /// [LPC-913] and [LPC-919]: after communication is restored the energy
        /// guard sends a heartbeat and a limit, and the charging station leaves
        /// its failsafe state.
        /// </summary>
        [Test]
        public async Task Scenario3_AfterTheHeartbeatReturnsALimitTakesEffectAgain()
        {

            await Commission();

            await guard.WriteConsumptionLimit(CS, 4200, IsActive: true);

            guard.StopHeartbeat();
            time.Advance(TimeSpan.FromSeconds(121));
            await system.Check();

            Assert.That(system.StateMachine.State, Is.EqualTo(PowerLimitationState.FailsafeState));

            // The energy manager comes back: a heartbeat, and then a limit.
            await guard.StartHeartbeat();

            var response = await guard.WriteConsumptionLimit(CS, 6000, IsActive: true);

            Assert.Multiple(() => {
                Assert.That(response.IsError,               Is.False, response.Result?.Description);
                Assert.That(system.StateMachine.State,      Is.EqualTo(PowerLimitationState.Limited));
                Assert.That(system.ConsumptionLimit.Value,  Is.EqualTo(6000));
            });

        }

        #endregion

        #region Scenario3_ALimitWithoutAHeartbeatIsRefusedInTheFailsafeState()

        /// <summary>
        /// Section 2.2: in the failsafe state a write of the limit is only
        /// evaluated when it follows a heartbeat within 60 seconds. A device
        /// which sends limits without proving that it is there does not get to
        /// control anything.
        /// </summary>
        [Test]
        public async Task Scenario3_ALimitWithoutAHeartbeatIsRefusedInTheFailsafeState()
        {

            await Commission();

            await guard.WriteConsumptionLimit(CS, 4200, IsActive: true);

            guard.StopHeartbeat();
            time.Advance(TimeSpan.FromSeconds(121));
            await system.Check();

            // A limit, but no heartbeat before it.
            var response = await guard.WriteConsumptionLimit(CS, 6000, IsActive: true);

            Assert.Multiple(() => {
                Assert.That(response.IsError,             Is.True);
                Assert.That(response.Result?.Description, Does.Contain("heartbeat"));
                Assert.That(system.StateMachine.State,    Is.EqualTo(PowerLimitationState.FailsafeState),
                            "A limit without a heartbeat took the charging station out of its failsafe state.");
            });

        }

        #endregion

        #region Scenario4_TheEnergyGuardReadsTheNominalMaximum()

        /// <summary>
        /// Scenario 4: the controllable system tells the energy guard how much
        /// it could consume at most ([LPC-041]), which is what lets the energy
        /// guard turn a percentage from outside into a limit in watts.
        /// </summary>
        [Test]
        public async Task Scenario4_TheEnergyGuardReadsTheNominalMaximum()
        {

            await Commission();

            var nominalMax = await guard.ReadConsumptionNominalMax(CS);

            Assert.That(nominalMax, Is.EqualTo(11000));

        }

        #endregion

        #region AnEnergyManagerReportsItsContractualMaximumInstead()

        /// <summary>
        /// [LPC-042]: a controllable system which is itself an energy manager
        /// reports what it is *allowed* to consume rather than what it *can*.
        /// </summary>
        [Test]
        public void AnEnergyManagerReportsItsContractualMaximumInstead()
        {

            var device = new SPINELocalDevice("d:_i:19667_CEM", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);

            var manager = new LPCControllableSystem(device.AddEntity(EntityTypeType.CEM),
                                                    IsEnergyManager: true);

            var characteristic = manager.Electrical.
                                     DataCopy<ElectricalConnectionCharacteristicListDataType>(PowerLimitation.CharacteristicListData)?.
                                     ElectricalConnectionCharacteristicData?.FirstOrDefault();

            Assert.That(characteristic?.CharacteristicType,
                        Is.EqualTo(ElectricalConnectionCharacteristicTypeType.ContractualConsumptionNominalMax));

        }

        #endregion

        #region AWriteWithoutABindingIsRefusedEvenForTheEnergyGuard()

        /// <summary>
        /// The use case does not suspend SPINE: without a binding, no write.
        /// </summary>
        [Test]
        public async Task AWriteWithoutABindingIsRefusedEvenForTheEnergyGuard()
        {

            await new UseCaseFeature(FeatureTypeType.DeviceDiagnosis, system.Entity, EG).Subscribe();
            await guard.StartHeartbeat();

            Assert.That(() => guard.WriteConsumptionLimit(CS, 4200, IsActive: true),
                        Throws.InvalidOperationException,
                        "A limit was written without a binding.");

        }

        #endregion

    }

}
