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
using cloud.charging.open.protocols.EEBUS.UseCases.EVSECC;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "EVSE Commissioning and Configuration", both actors, over the wire.
    ///
    /// The smallest use case in the e-mobility family and the one the rest
    /// stands on: a charging station says who made it and whether it is working.
    /// </summary>
    [TestFixture]
    public class EVSECCTests
    {

        #region Data

        private FakeTimeProvider        time      = null!;
        private SPINELoopback           wire      = null!;

        private EVSECCEnergyManager     manager   = null!;
        private EVSECCChargingStation   station   = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems  = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse  = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            manager = new EVSECCEnergyManager  (hems.AddEntity(EntityTypeType.CEM));

            station = new EVSECCChargingStation(evse.AddEntity(EntityTypeType.EVSE),
                                                new ManufacturerData(DeviceName:         "Wallbox 22",
                                                                     DeviceCode:         "WB-22-EU",
                                                                     VendorName:         "GraphDefined",
                                                                     VendorCode:         "GD",
                                                                     BrandName:          "OpenCharging",
                                                                     ManufacturerLabel:  "Made in Jena",
                                                                     SerialNumber:       "SN-0001",
                                                                     SoftwareRevision:   "2.1.0"));

            wire = new SPINELoopback(hems, evse);

            await manager.Register();
            await station.Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

        }

        #endregion

        #region (private) The charging station, as the energy manager sees it

        private SPINERemoteEntity EVSE
            => wire.BAsSeenByA.Entity([ 1 ])!;

        #endregion


        #region TheTwoActorsFindEachOther()

        [Test]
        public void TheTwoActorsFindEachOther()
        {

            Assert.Multiple(() => {
                Assert.That(manager.PartnerFor(EVSE),                     Is.Not.Null);
                Assert.That(manager.PartnerFor(EVSE)?.Scenarios,          Is.EquivalentTo(new UInt32[] { 1, 2 }));
                Assert.That(manager.PartnerFor(EVSE)?.Version.ToString(), Is.EqualTo("1.0.1"));
                Assert.That(station.Actor,                                Is.EqualTo("EVSE"));
                Assert.That(manager.Actor,                                Is.EqualTo("CEM"));
            });

        }

        #endregion

        #region TheErrorStateIsMandatoryAndTheManufacturerDataIsNot()

        /// <summary>
        /// Table 1, and it is the other way round from what one would guess: a
        /// charging station which has no name is a nuisance, one which cannot
        /// say it has failed is a problem.
        /// </summary>
        [Test]
        public async Task TheErrorStateIsMandatoryAndTheManufacturerDataIsNot()
        {

            var hems      = new SPINELocalDevice("d:_i:19667_HEMS2", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse      = new SPINELocalDevice("d:_i:19667_EVSE2", DeviceTypeType.ChargingStation,        TimeProvider: time);

            // No manufacturer data at all: scenario 1 is not supported.
            var anonymous = new EVSECCChargingStation(evse.AddEntity(EntityTypeType.EVSE));
            var watcher   = new EVSECCEnergyManager  (hems.AddEntity(EntityTypeType.CEM));

            var other     = new SPINELoopback(hems, evse);

            await anonymous.Register();
            await watcher.  Register();

            await other.A.NodeManagement.RequestDetailedDiscovery(other.BAsSeenByA);
            await other.A.NodeManagement.RequestUseCaseData      (other.BAsSeenByA);

            var partner = other.BAsSeenByA.Entity([ 1 ])!;

            Assert.Multiple(() => {

                Assert.That(watcher.PartnerFor(partner)?.Scenarios, Is.EquivalentTo(new UInt32[] { 2 }),
                            "A charging station without manufacturer data announced scenario 1.");

                Assert.That(anonymous.Classification, Is.Null,
                            "A charging station without manufacturer data offered a device classification feature.");

                Assert.That(anonymous.Diagnosis,      Is.Not.Null,
                            "The mandatory scenario was left out.");

            });

        }

        #endregion

        #region Scenario1_TheManufacturerDataArrivesAtTheEnergyManager()

        [Test]
        public async Task Scenario1_TheManufacturerDataArrivesAtTheEnergyManager()
        {

            await manager.Subscribe(EVSE);

            var manufacturer = manager.Manufacturer(EVSE);

            Assert.Multiple(() => {
                Assert.That(manufacturer,                    Is.Not.Null);
                Assert.That(manufacturer?.DeviceName,        Is.EqualTo("Wallbox 22"));
                Assert.That(manufacturer?.DeviceCode,        Is.EqualTo("WB-22-EU"));
                Assert.That(manufacturer?.VendorName,        Is.EqualTo("GraphDefined"));
                Assert.That(manufacturer?.VendorCode,        Is.EqualTo("GD"));
                Assert.That(manufacturer?.BrandName,         Is.EqualTo("OpenCharging"));
                Assert.That(manufacturer?.ManufacturerLabel, Is.EqualTo("Made in Jena"));
                Assert.That(manufacturer?.SerialNumber,      Is.EqualTo("SN-0001"));
                Assert.That(manufacturer?.SoftwareRevision,  Is.EqualTo("2.1.0"));
            });

        }

        #endregion

        #region Scenario1_ASoftwareUpdateIsNotifiedRatherThanPolled()

        /// <summary>
        /// The manufacturer data is not as fixed as it sounds - a software
        /// revision changes with every update - and the runtime scenario
        /// communication is a notify, not a poll (section 3.4.1.3).
        /// </summary>
        [Test]
        public async Task Scenario1_ASoftwareUpdateIsNotifiedRatherThanPolled()
        {

            await manager.Subscribe(EVSE);

            var before = wire.AToB.Datagrams.Count;

            await station.SetManufacturer(station.Manufacturer! with { SoftwareRevision = "2.2.0" });

            Assert.Multiple(() => {

                Assert.That(manager.Manufacturer(EVSE)?.SoftwareRevision, Is.EqualTo("2.2.0"));

                Assert.That(manager.Manufacturer(EVSE)?.DeviceName,       Is.EqualTo("Wallbox 22"),
                            "Publishing one changed element dropped the others.");

                Assert.That(wire.AToB.Datagrams, Has.Count.EqualTo(before),
                            "The energy manager asked instead of being told.");

            });

        }

        #endregion

        #region Scenario2_AFailureReachesTheEnergyManager()

        /// <summary>
        /// The mandatory scenario. [EVSECC-020] is not just a red dot: while a
        /// charging station has failed, the numbers coming from the car behind
        /// it may no longer be valid either.
        /// </summary>
        [Test]
        public async Task Scenario2_AFailureReachesTheEnergyManager()
        {

            await manager.Subscribe(EVSE);

            Assert.That(manager.HasFailed(EVSE), Is.False,
                        "A charging station which has just been plugged in was reported as failed.");

            await station.Fail("E-4711");

            Assert.Multiple(() => {
                Assert.That(station.HasFailed,             Is.True);
                Assert.That(manager.HasFailed(EVSE),       Is.True);
                Assert.That(manager.OperatingState(EVSE),  Is.EqualTo(DeviceDiagnosisOperatingStateType.Failure));
                Assert.That(manager.LastErrorCode(EVSE),   Is.EqualTo("E-4711"));
            });

            await station.Recover();

            Assert.Multiple(() => {

                Assert.That(manager.HasFailed(EVSE),      Is.False);
                Assert.That(manager.OperatingState(EVSE), Is.EqualTo(DeviceDiagnosisOperatingStateType.NormalOperation));

                Assert.That(manager.LastErrorCode(EVSE),  Is.EqualTo("E-4711"),
                            "Recovering erased what had gone wrong; the last error code says what happened last, not what is wrong now.");

            });

        }

        #endregion

        #region Scenario2_ANewlyPluggedInStationSaysItIsWorking()

        /// <summary>
        /// A charging station which has published nothing and one which is
        /// working have to look different to the energy manager, because only
        /// one of them is a reason to stop believing the car's numbers.
        /// </summary>
        [Test]
        public void Scenario2_ANewlyPluggedInStationSaysItIsWorking()
        {

            Assert.Multiple(() => {

                Assert.That(station.OperatingState,       Is.EqualTo(DeviceDiagnosisOperatingStateType.NormalOperation));

                Assert.That(manager.OperatingState(EVSE), Is.Null,
                            "The energy manager knew the state before reading or subscribing to it.");

            });

        }

        #endregion

        #region NeitherScenarioNeedsABinding()

        /// <summary>
        /// Both scenarios say "Binding SHOULD NOT be used for this Scenario"
        /// (sections 3.4.1.1 and 3.4.2.1). Nothing here is ever written by the
        /// other side.
        /// </summary>
        [Test]
        public async Task NeitherScenarioNeedsABinding()
        {

            await manager.Subscribe(EVSE);

            Assert.Multiple(() => {

                Assert.That(manager.ClassificationOf(EVSE)?.HasSubscription, Is.True);
                Assert.That(manager.DiagnosisOf     (EVSE)?.HasSubscription, Is.True);

                Assert.That(manager.ClassificationOf(EVSE)?.HasBinding,      Is.False,
                            "The energy manager bound to a feature it never writes to.");

                Assert.That(manager.DiagnosisOf     (EVSE)?.HasBinding,      Is.False,
                            "The energy manager bound to a feature it never writes to.");

            });

        }

        #endregion

        #region AStationWhichAnnouncesTheWrongActorIsStillAccepted()

        /// <summary>
        /// The Porsche PMCC announces this use case as the actor **EV** rather
        /// than EVSE, which the specification does not allow and the field
        /// contains anyway. An energy manager which insisted on the letter would
        /// refuse to name a charging station somebody owns.
        /// </summary>
        [Test]
        public async Task AStationWhichAnnouncesTheWrongActorIsStillAccepted()
        {

            var hems     = new SPINELocalDevice("d:_i:19667_HEMS3", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var pmcc     = new SPINELocalDevice("d:_i:19667_PMCC",  DeviceTypeType.ChargingStation,        TimeProvider: time);

            var entity   = pmcc.AddEntity(EntityTypeType.EVSE);

            // Built like a charging station, but never registered - so that the
            // only announcement is the wrong one made below.
            var quirky   = new EVSECCChargingStation(entity,
                                                     new ManufacturerData(DeviceName: "Mobile Charger Connect"));

            var tolerant = new EVSECCEnergyManager(hems.AddEntity(EntityTypeType.CEM));
            var strict   = new EVSECCEnergyManager(hems.AddEntity(EntityTypeType.CEM),
                                                   StrictActor: true);

            var other    = new SPINELoopback(hems, pmcc);

            await tolerant.Register();
            await strict.  Register();

            await pmcc.NodeManagement.AddUseCaseSupport(quirky.Diagnosis!.Address,
                                                        UseCaseActors.EV,
                                                        EVSECommissioningAndConfiguration.Name,
                                                        "1.0.1",
                                                        [ 1u, 2u ],
                                                        "release");

            await other.A.NodeManagement.RequestDetailedDiscovery(other.BAsSeenByA);
            await other.A.NodeManagement.RequestUseCaseData      (other.BAsSeenByA);

            var partner = other.BAsSeenByA.Entity([ 1 ])!;

            Assert.Multiple(() => {

                Assert.That(tolerant.PartnerFor(partner), Is.Not.Null,
                            "A charging station announcing the actor 'EV' was ignored.");

                Assert.That(tolerant.PartnerFor(partner)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2 }));

                Assert.That(strict.PartnerFor(partner), Is.Null,
                            "The strict energy manager accepted an actor the specification does not allow.");

            });

        }

        #endregion

    }

}
