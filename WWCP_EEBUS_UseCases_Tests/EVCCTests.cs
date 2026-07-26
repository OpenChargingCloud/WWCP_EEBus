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
using cloud.charging.open.protocols.EEBUS.UseCases.Commissioning;
using cloud.charging.open.protocols.EEBUS.UseCases.EVCC;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "EV Commissioning and Configuration", both actors, over the wire.
    ///
    /// The use case every other e-mobility one leans on, and the one where two
    /// of the eight scenarios have no data at all: a car is connected because
    /// its entity is there.
    /// </summary>
    [TestFixture]
    public class EVCCTests
    {

        #region Data

        private FakeTimeProvider     time     = null!;
        private SPINELoopback        wire     = null!;

        private EVCCEnergyManager    manager  = null!;
        private EVCCElectricVehicle  car      = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            manager = new EVCCEnergyManager  (hems.AddEntity(EntityTypeType.CEM));

            car     = new EVCCElectricVehicle(evse.AddEntity(EntityTypeType.EV),
                                              CommunicationStandard:  EVCommissioningAndConfiguration.ISO15118_2_ed2,
                                              AsymmetricCharging:     true,
                                              Identifier:             "01-23-45-67-89-AB",
                                              Manufacturer:           new ManufacturerData(DeviceName:  "e-Golf",
                                                                                            VendorName:  "Volkswagen",
                                                                                            BrandName:   "VW"),
                                              MinimumChargingPower:   1400,
                                              MaximumChargingPower:   11000,
                                              StandbyPower:           12,
                                              SleepMode:              true);

            wire = new SPINELoopback(hems, evse);

            await manager.Register();
            await car.    Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

        }

        #endregion

        #region (private) The car, as the energy manager sees it

        private SPINERemoteEntity EV
            => wire.BAsSeenByA.Entity([ 1 ])!;

        #endregion


        #region TheTwoActorsFindEachOther()

        [Test]
        public void TheTwoActorsFindEachOther()
        {

            Assert.Multiple(() => {
                Assert.That(manager.PartnerFor(EV),                     Is.Not.Null);
                Assert.That(manager.PartnerFor(EV)?.Scenarios,          Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
                Assert.That(manager.PartnerFor(EV)?.Version.ToString(), Is.EqualTo("1.0.1"));
            });

        }

        #endregion

        #region Scenarios1And8_ConnectedIsAnEntityBeingThere()

        /// <summary>
        /// The two scenarios with no data. A car is connected because its entity
        /// is in the detailed discovery and the use case is announced as
        /// available; it is disconnected when that stops being true.
        /// </summary>
        [Test]
        public async Task Scenarios1And8_ConnectedIsAnEntityBeingThere()
        {

            Assert.That(manager.IsConnected(EV), Is.True,
                        "A car which announced the use case was not seen as connected.");

            // Unplugged: the car is still an entity, but it can no longer do
            // anything with the use case.
            await car.SetAvailable(false);
            await wire.A.NodeManagement.RequestUseCaseData(wire.BAsSeenByA);

            Assert.That(manager.IsConnected(EV), Is.False,
                        "A car which withdrew the use case was still seen as connected.");

        }

        #endregion

        #region Scenarios1And8_TheEnergyManagerIsToldRatherThanAsked()

        /// <summary>
        /// The framework raises an event when a partner's support changes, which
        /// is what an energy manager acts on: scenario 1 exists so that it can
        /// "start further Use Cases with the connected EV" [EVCC-001].
        /// </summary>
        [Test]
        public async Task Scenarios1And8_TheEnergyManagerIsToldRatherThanAsked()
        {

            var events = new List<UseCaseSupportChanged>();

            wire.A.Events.Subscribe<UseCaseSupportChanged>(
                changed => { if (changed.UseCase == manager) events.Add(changed); }
            );

            await car.SetAvailable(false);
            await wire.A.NodeManagement.RequestUseCaseData(wire.BAsSeenByA);

            Assert.Multiple(() => {
                Assert.That(events,                  Is.Not.Empty, "Nobody was told that the car went away.");
                Assert.That(events[^1].Partner?.Available, Is.False);
            });

        }

        #endregion

        #region Scenario2_TheCommunicationStandardReachesTheEnergyManager()

        [Test]
        public async Task Scenario2_TheCommunicationStandardReachesTheEnergyManager()
        {

            await manager.Subscribe(EV);

            Assert.Multiple(() => {
                Assert.That(car.CommunicationStandard,              Is.EqualTo("iso15118-2ed2"));
                Assert.That(manager.CommunicationStandard(EV),      Is.EqualTo("iso15118-2ed2"));
                Assert.That(manager.IsDigital(EV),                  Is.True);
            });

        }

        #endregion

        #region Scenario2_TheKeyIsSpelledTheWayTheResourceSpecificationSpellsIt()

        /// <summary>
        /// The EVCC document contradicts itself: its content tables say
        /// "communicationStandard" and its sequence diagram section says
        /// "communicationsStandard". The SPINE resource specification and the
        /// certified Go implementation use the second, so that is what we send -
        /// and a client of ours reads either, because a car built from the
        /// tables is a car which exists. See finding S9.
        /// </summary>
        [Test]
        public async Task Scenario2_TheKeyIsSpelledTheWayTheResourceSpecificationSpellsIt()
        {

            await manager.Subscribe(EV);

            var descriptions = car.Configuration.
                                   DataCopy<DeviceConfigurationKeyValueDescriptionListDataType>(
                                       EVCommissioningAndConfiguration.KeyValueDescriptionListData)?.
                                   DeviceConfigurationKeyValueDescriptionData;

            Assert.Multiple(() => {

                Assert.That(descriptions?.Any(description => description.KeyName?.ToString() == "communicationsStandard"),
                            Is.True,
                            "The key was not sent under the spelling the resource specification uses.");

                Assert.That(EVCommissioningAndConfiguration.CommunicationStandardKeys.Select(key => key.ToString()),
                            Is.EquivalentTo(new[] { "communicationsStandard", "communicationStandard" }),
                            "A client of ours does not accept both spellings.");

            });

        }

        #endregion

        #region Scenario2_ACarSpellingItTheOtherWayIsStillUnderstood()

        /// <summary>
        /// The other half of the same finding: a car built literally from
        /// Table 6 announces "communicationStandard", and an energy manager
        /// which only looked for the other spelling would decide it did not know
        /// what the car speaks - and therefore that no further use case is
        /// possible with it.
        /// </summary>
        [Test]
        public async Task Scenario2_ACarSpellingItTheOtherWayIsStillUnderstood()
        {

            var hems  = new SPINELocalDevice("d:_i:19667_HEMS2", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse  = new SPINELocalDevice("d:_i:19667_EVSE2", DeviceTypeType.ChargingStation,        TimeProvider: time);

            var other = new EVCCElectricVehicle(evse.AddEntity(EntityTypeType.EV));
            var watch = new EVCCEnergyManager  (hems.AddEntity(EntityTypeType.CEM));

            var link  = new SPINELoopback(hems, evse);

            // Re-spell the key the way the content tables do.
            var descriptions = other.Configuration.
                                   DataCopy<DeviceConfigurationKeyValueDescriptionListDataType>(
                                       EVCommissioningAndConfiguration.KeyValueDescriptionListData)!;

            foreach (var description in descriptions.DeviceConfigurationKeyValueDescriptionData!.
                                            Where(description => description.KeyName == DeviceConfigurationKeyNameType.CommunicationsStandard))
                description.KeyName = DeviceConfigurationKeyNameType.Parse("communicationStandard");

            other.Configuration.FunctionData(EVCommissioningAndConfiguration.KeyValueDescriptionListData)!.SetData(descriptions);

            await other.Register();
            await watch.Register();

            await link.A.NodeManagement.RequestDetailedDiscovery(link.BAsSeenByA);
            await link.A.NodeManagement.RequestUseCaseData      (link.BAsSeenByA);

            var partner = link.BAsSeenByA.Entity([ 1 ])!;

            await watch.Subscribe(partner);

            Assert.That(watch.CommunicationStandard(partner), Is.EqualTo("iso15118-2ed2"),
                        "A car which spelled the key the way the content tables do was not understood.");

        }

        #endregion

        #region Scenario2_UnderIEC61851ThereIsNoChannelToTheCar()

        /// <summary>
        /// Section 2.3.2.1: under IEC 61851 the charging station has a PWM duty
        /// cycle and nothing else, so an identification and an energy demand
        /// cannot come from the car at all. A manager which knows that will not
        /// wait for them.
        /// </summary>
        [Test]
        public async Task Scenario2_UnderIEC61851ThereIsNoChannelToTheCar()
        {

            await manager.Subscribe(EV);

            await car.SetCommunicationStandard(EVCommissioningAndConfiguration.IEC61851);

            Assert.Multiple(() => {
                Assert.That(manager.CommunicationStandard(EV), Is.EqualTo("iec61851"));
                Assert.That(manager.IsDigital(EV),             Is.False);
            });

        }

        #endregion

        #region Scenario3_AsymmetricChargingIsABooleanAndMayChange()

        /// <summary>
        /// [EVCC-006] says the support of asymmetric charging "may change during
        /// runtime", which is why the client subscribes rather than reads once.
        /// </summary>
        [Test]
        public async Task Scenario3_AsymmetricChargingIsABooleanAndMayChange()
        {

            await manager.Subscribe(EV);

            Assert.That(manager.AsymmetricCharging(EV), Is.True);

            var before = wire.AToB.Datagrams.Count;

            await car.SetAsymmetricCharging(false);

            Assert.Multiple(() => {

                Assert.That(manager.AsymmetricCharging(EV),     Is.False);
                Assert.That(manager.CommunicationStandard(EV),  Is.EqualTo("iso15118-2ed2"),
                            "Changing one configuration value dropped the other.");

                Assert.That(wire.AToB.Datagrams, Has.Count.EqualTo(before),
                            "The energy manager asked instead of being told.");

            });

        }

        #endregion

        #region Scenario4_TheIdentificationIsAMacAddress()

        [Test]
        public async Task Scenario4_TheIdentificationIsAMacAddress()
        {

            await manager.Subscribe(EV);

            Assert.Multiple(() => {
                Assert.That(car.Identifier,          Is.EqualTo("01-23-45-67-89-AB"));
                Assert.That(manager.Identifier(EV),  Is.EqualTo("01-23-45-67-89-AB"));
            });

        }

        #endregion

        #region Scenario6_TheMinimumChargingPowerIsOftenNotZero()

        /// <summary>
        /// [EVCC-017] with the note which is the whole point of the scenario:
        /// "the minimum charging power is often not zero". An energy manager
        /// which throttles a car below it will not get a car charging slowly, it
        /// will get a car which stops.
        /// </summary>
        [Test]
        public async Task Scenario6_TheMinimumChargingPowerIsOftenNotZero()
        {

            await manager.Subscribe(EV);

            var limits = manager.ChargingPowerLimits(EV);

            Assert.Multiple(() => {
                Assert.That(limits?.Minimum,  Is.EqualTo(1400));
                Assert.That(limits?.Maximum,  Is.EqualTo(11000));
                Assert.That(limits?.Standby,  Is.EqualTo(12));
            });

        }

        #endregion

        #region Scenario7_ACarInSleepModeSaysSo()

        [Test]
        public async Task Scenario7_ACarInSleepModeSaysSo()
        {

            await manager.Subscribe(EV);

            Assert.That(manager.IsAsleep(EV), Is.False);

            await car.FallAsleep();

            Assert.Multiple(() => {
                Assert.That(car.IsAsleep,                Is.True);
                Assert.That(manager.IsAsleep(EV),        Is.True);
                Assert.That(manager.OperatingState(EV),  Is.EqualTo(DeviceDiagnosisOperatingStateType.Standby));
            });

            await car.WakeUp();

            Assert.That(manager.IsAsleep(EV), Is.False);

        }

        #endregion

        #region ACarWhichOnlyDoesTheMandatoryScenariosIsStillACar()

        /// <summary>
        /// Scenarios 1, 2, 3 and 8 are mandatory; the other four are not. A car
        /// which does only those implements this use case, and the energy
        /// manager has to be able to tell that from a car which forgot the rest.
        /// </summary>
        [Test]
        public async Task ACarWhichOnlyDoesTheMandatoryScenariosIsStillACar()
        {

            var hems   = new SPINELocalDevice("d:_i:19667_HEMS3", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse   = new SPINELocalDevice("d:_i:19667_EVSE3", DeviceTypeType.ChargingStation,        TimeProvider: time);

            var simple = new EVCCElectricVehicle(evse.AddEntity(EntityTypeType.EV),
                                                 CommunicationStandard: EVCommissioningAndConfiguration.IEC61851);

            var watch  = new EVCCEnergyManager  (hems.AddEntity(EntityTypeType.CEM));

            var link   = new SPINELoopback(hems, evse);

            await simple.Register();
            await watch. Register();

            await link.A.NodeManagement.RequestDetailedDiscovery(link.BAsSeenByA);
            await link.A.NodeManagement.RequestUseCaseData      (link.BAsSeenByA);

            var partner = link.BAsSeenByA.Entity([ 1 ])!;

            await watch.Subscribe(partner);

            Assert.Multiple(() => {

                Assert.That(watch.PartnerFor(partner)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3, 8 }));

                Assert.That(simple.Identification,           Is.Null);
                Assert.That(simple.Electrical,               Is.Null);
                Assert.That(simple.Classification,           Is.Null);
                Assert.That(simple.Diagnosis,                Is.Null);

                Assert.That(watch.Identifier(partner),          Is.Null);
                Assert.That(watch.ChargingPowerLimits(partner), Is.Null);
                Assert.That(watch.Manufacturer(partner),        Is.Null);

                Assert.That(watch.IsAsleep(partner), Is.False,
                            "A car which does not report a sleep mode was reported as asleep.");

            });

        }

        #endregion

    }

}
