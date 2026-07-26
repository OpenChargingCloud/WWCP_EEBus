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
using cloud.charging.open.protocols.EEBUS.UseCases.LimitationOfPower;
using cloud.charging.open.protocols.EEBUS.UseCases.LPC;
using cloud.charging.open.protocols.EEBUS.UseCases.LPP;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "Limitation of Power Production", both actors, over the wire.
    ///
    /// The mirror of the limitation of power consumption: an energy guard limits
    /// how much a photovoltaic inverter, a battery or a combined heat and power
    /// unit may feed into the grid. The two specifications are the same document
    /// with three words changed, so what is tested here is exactly those three
    /// - that the direction, the failsafe key and the nominal maximum are the
    /// ones of **production** - plus the proof that the shared half really is
    /// shared.
    /// </summary>
    [TestFixture]
    public class LPPTests
    {

        #region Data

        private FakeTimeProvider        time    = null!;
        private SPINELoopback           wire    = null!;

        private LPPEnergyGuard          guard   = null!;
        private LPPControllableSystem   system  = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems     = new SPINELocalDevice("d:_i:19667_HEMS",     DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var inverter = new SPINELocalDevice("d:_i:19667_Inverter", DeviceTypeType.Inverter,               TimeProvider: time);

            guard   = new LPPEnergyGuard       (hems.    AddEntity(EntityTypeType.CEM));
            system  = new LPPControllableSystem(inverter.AddEntity(EntityTypeType.Inverter));

            system.FailsafeLimit            = 0;
            system.FailsafeDurationMinimum  = TimeSpan.FromHours(2);
            system.ConsumptionNominalMax    = 10000;

            wire = new SPINELoopback(hems, inverter);

            await guard. Register();
            await system.Register();

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

        /// <summary>The inverter, as the energy manager sees it.</summary>
        private SPINERemoteEntity CS
            => wire.BAsSeenByA.Entity([ 1 ])!;

        /// <summary>The energy manager, as the inverter sees it.</summary>
        private SPINERemoteEntity EG
            => wire.AAsSeenByB.Entity([ 1 ])!;


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


        #region ThePartnersRecogniseEachOther()

        [Test]
        public void ThePartnersRecogniseEachOther()
        {

            Assert.Multiple(() => {

                Assert.That(guard. PartnerFor(CS)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4 }));
                Assert.That(system.PartnerFor(EG)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4 }));

                var announced = wire.B.NodeManagement.UseCases.First().UseCaseSupport![0];

                Assert.That(announced.UseCaseName,     Is.EqualTo("limitationOfPowerProduction"));
                Assert.That(announced.UseCaseVersion,  Is.EqualTo("1.0.0"));

            });

        }

        #endregion

        #region TheLimitIsAboutProductionRatherThanConsumption()

        /// <summary>
        /// The first of the three differences: the limit description says
        /// "produce".
        ///
        /// It matters more than it looks. A device may hold both limits on the
        /// same load control feature - an inverter which both consumes and
        /// produces does - and the direction is what tells them apart. An energy
        /// guard which ignored it would curtail the wrong one.
        /// </summary>
        [Test]
        public async Task TheLimitIsAboutProductionRatherThanConsumption()
        {

            await Commission();

            var loadControl = guard.LoadControlOf(CS);

            await loadControl.RequestData(PowerLimitation.LimitDescriptionListData);

            var description = loadControl.Data<LoadControlLimitDescriptionListDataType>(PowerLimitation.LimitDescriptionListData)?.
                                  LoadControlLimitDescriptionData?.FirstOrDefault();

            Assert.Multiple(() => {

                Assert.That(description?.LimitDirection,  Is.EqualTo(EnergyDirectionType.Produce));

                Assert.That(PowerLimitation.Production. IsTheLimit(description), Is.True);
                Assert.That(PowerLimitation.Consumption.IsTheLimit(description), Is.False,
                            "The production limit was accepted as a consumption limit.");

            });

        }

        #endregion

        #region TheFailsafeKeyIsTheProductionOne()

        /// <summary>
        /// The second difference: the configuration key is
        /// "failsafeProductionActivePowerLimit".
        /// </summary>
        [Test]
        public async Task TheFailsafeKeyIsTheProductionOne()
        {

            await Commission();

            var configuration = guard.ConfigurationOf(CS);

            await configuration.RequestData(PowerLimitation.KeyValueDescriptionListData);

            var keys = configuration.Data<DeviceConfigurationKeyValueDescriptionListDataType>(PowerLimitation.KeyValueDescriptionListData)?.
                           DeviceConfigurationKeyValueDescriptionData ?? [];

            Assert.Multiple(() => {

                Assert.That(keys.Any(key => key.KeyName == DeviceConfigurationKeyNameType.FailsafeProductionActivePowerLimit),
                            Is.True);

                Assert.That(keys.Any(key => key.KeyName == DeviceConfigurationKeyNameType.FailsafeConsumptionActivePowerLimit),
                            Is.False,
                            "A production limiter announced the consumption failsafe key.");

                // The duration minimum is the same key in both use cases.
                Assert.That(keys.Any(key => key.KeyName == DeviceConfigurationKeyNameType.FailsafeDurationMinimum),
                            Is.True);

            });

        }

        #endregion

        #region TheNominalMaximumIsTheProductionOne()

        /// <summary>
        /// The third difference: an inverter reports what it can **produce**.
        /// </summary>
        [Test]
        public void TheNominalMaximumIsTheProductionOne()
        {

            var characteristic = system.Electrical.
                                     DataCopy<ElectricalConnectionCharacteristicListDataType>(PowerLimitation.CharacteristicListData)?.
                                     ElectricalConnectionCharacteristicData?.FirstOrDefault();

            Assert.That(characteristic?.CharacteristicType,
                        Is.EqualTo(ElectricalConnectionCharacteristicTypeType.PowerProductionNominalMax));

            // ... and an energy manager reports what it is allowed to produce.
            var device  = new SPINELocalDevice("d:_i:19667_CEM", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var manager = new LPPControllableSystem(device.AddEntity(EntityTypeType.CEM), IsEnergyManager: true);

            Assert.That(manager.Electrical.
                            DataCopy<ElectricalConnectionCharacteristicListDataType>(PowerLimitation.CharacteristicListData)?.
                            ElectricalConnectionCharacteristicData?.FirstOrDefault()?.CharacteristicType,
                        Is.EqualTo(ElectricalConnectionCharacteristicTypeType.ContractualProductionNominalMax));

        }

        #endregion


        #region Scenario1_TheEnergyGuardCurtailsTheFeedIn()

        [Test]
        public async Task Scenario1_TheEnergyGuardCurtailsTheFeedIn()
        {

            await Commission();

            var response = await guard.WriteConsumptionLimit(CS, 6000, IsActive: true);

            Assert.Multiple(() => {
                Assert.That(response.IsError,                  Is.False, response.Result?.Description);
                Assert.That(system.StateMachine.State,         Is.EqualTo(PowerLimitationState.Limited));
                Assert.That(system.ConsumptionLimit.Value,     Is.EqualTo(6000));
            });

        }

        #endregion

        #region Scenario3_TheSameFailsafeRuleApplies()

        /// <summary>
        /// The shared half, proven rather than assumed: an inverter whose energy
        /// guard goes quiet falls back to its failsafe production limit after
        /// the same 120 seconds, through the same state machine, quoting the
        /// rules of **its own** specification.
        ///
        /// For a photovoltaic system that failsafe value is usually zero: an
        /// inverter which cannot be reached stops feeding in.
        /// </summary>
        [Test]
        public async Task Scenario3_TheSameFailsafeRuleApplies()
        {

            var transitions = new List<PowerLimitationTransition>();

            system.OnTransition += (_, transition) => transitions.Add(transition);

            await Commission();

            await guard.WriteConsumptionLimit(CS, 6000, IsActive: true);

            Assert.That(system.StateMachine.State, Is.EqualTo(PowerLimitationState.Limited));

            guard.StopHeartbeat();

            time.Advance(TimeSpan.FromSeconds(121));

            var transition = await system.Check();

            Assert.Multiple(() => {

                Assert.That(transition?.Transition,           Is.EqualTo(7));
                Assert.That(system.StateMachine.State,        Is.EqualTo(PowerLimitationState.FailsafeState));
                Assert.That(system.StateMachine.Limitation,   Is.EqualTo(PowerLimitationApplied.FailsafeLimit));
                Assert.That(system.FailsafeLimit,             Is.EqualTo(0),
                            "An inverter which cannot be reached should stop feeding in.");

                // The rules quoted are the ones of this specification.
                Assert.That(transition?.Reason,               Does.Contain("LPP-912"),
                            "The production use case quoted the rules of the consumption one.");
                Assert.That(transitions.Select(entry => entry.Reason),
                            Has.None.Contains("LPC-"));

            });

        }

        #endregion

        #region BothUseCasesCanRunOnOneDeviceAtOnce()

        /// <summary>
        /// A battery both consumes and produces, so it may play the controllable
        /// system of both use cases at the same time - on the same entity, with
        /// two limits on one load control feature and two failsafe keys on one
        /// configuration feature.
        ///
        /// This is the case the shared implementation has to survive, and the
        /// one which would break first if the two were copies of each other.
        /// </summary>
        [Test]
        public async Task BothUseCasesCanRunOnOneDeviceAtOnce()
        {

            var hems     = new SPINELocalDevice("d:_i:19667_HEMS2",    DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var battery  = new SPINELocalDevice("d:_i:19667_Battery",  DeviceTypeType.Generic,                TimeProvider: time);

            var entity   = battery.AddEntity(EntityTypeType.Battery);

            var consumes = new LPCControllableSystem(entity);
            var produces = new LPPControllableSystem(entity);

            var lpcGuard = new LPCEnergyGuard(hems.AddEntity(EntityTypeType.CEM));

            var other    = new SPINELoopback(hems, battery);

            await consumes.Register();
            await produces.Register();
            await lpcGuard.Register();

            await other.A.NodeManagement.RequestDetailedDiscovery(other.BAsSeenByA);
            await other.A.NodeManagement.RequestUseCaseData      (other.BAsSeenByA);

            var announced = other.B.NodeManagement.UseCases.First();

            Assert.Multiple(() => {

                // One actor entry, two use cases.
                Assert.That(announced.Actor,             Is.EqualTo("ControllableSystem"));
                Assert.That(announced.UseCaseSupport,    Has.Count.EqualTo(2));
                Assert.That(announced.UseCaseSupport?.Select(support => support.UseCaseName),
                            Is.EquivalentTo(new[] { "limitationOfPowerConsumption",
                                                    "limitationOfPowerProduction" }));

                // The energy guard of the consumption use case finds the
                // consumption limit and not the production one.
                Assert.That(consumes.Profile.Direction,  Is.EqualTo(EnergyDirectionType.Consume));
                Assert.That(produces.Profile.Direction,  Is.EqualTo(EnergyDirectionType.Produce));

                Assert.That(lpcGuard.PartnerFor(other.BAsSeenByA.Entity([ 1 ])), Is.Not.Null);

                // Two state machines, one device: the battery may be limited in
                // one direction and free in the other.
                Assert.That(consumes.StateMachine, Is.Not.SameAs(produces.StateMachine));

                // One load control feature, but both limits on it, with
                // identifiers which do not collide.
                Assert.That(consumes.LoadControl,  Is.SameAs(produces.LoadControl));

                var descriptions = consumes.LoadControl.
                                       DataCopy<LoadControlLimitDescriptionListDataType>(PowerLimitation.LimitDescriptionListData)?.
                                       LoadControlLimitDescriptionData ?? [];

                Assert.That(descriptions,                                     Has.Count.EqualTo(2),
                            "The second use case overwrote the limit of the first.");
                Assert.That(descriptions.Select(entry => entry.LimitDirection),
                            Is.EquivalentTo(new[] { EnergyDirectionType.Consume, EnergyDirectionType.Produce }));
                Assert.That(descriptions.Select(entry => entry.LimitId).Distinct().Count(),
                            Is.EqualTo(2));

                var limits       = consumes.LoadControl.
                                       DataCopy<LoadControlLimitListDataType>(PowerLimitation.LimitListData)?.
                                       LoadControlLimitData ?? [];

                Assert.That(limits, Has.Count.EqualTo(2));

                // One device configuration feature, one failsafe limit per
                // direction - but only **one** ride-through duration, because
                // how long the battery can survive without an energy guard is a
                // property of the battery and not of a direction.
                var keys         = consumes.Configuration.
                                       DataCopy<DeviceConfigurationKeyValueDescriptionListDataType>(PowerLimitation.KeyValueDescriptionListData)?.
                                       DeviceConfigurationKeyValueDescriptionData ?? [];

                Assert.That(keys.Select(key => key.KeyName),
                            Is.EquivalentTo(new[] {
                                DeviceConfigurationKeyNameType.FailsafeConsumptionActivePowerLimit,
                                DeviceConfigurationKeyNameType.FailsafeProductionActivePowerLimit,
                                DeviceConfigurationKeyNameType.FailsafeDurationMinimum
                            }));

                Assert.That(keys.Select(key => key.KeyId).Distinct().Count(), Is.EqualTo(3));

                // Both nominal maxima, likewise.
                Assert.That(consumes.Electrical.
                                DataCopy<ElectricalConnectionCharacteristicListDataType>(PowerLimitation.CharacteristicListData)?.
                                ElectricalConnectionCharacteristicData?.Select(entry => entry.CharacteristicType),
                            Is.EquivalentTo(new[] {
                                ElectricalConnectionCharacteristicTypeType.PowerConsumptionNominalMax,
                                ElectricalConnectionCharacteristicTypeType.PowerProductionNominalMax
                            }));

            });

        }

        #endregion

    }

}
