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
using cloud.charging.open.protocols.EEBUS.UseCases.EVSOC;
using cloud.charging.open.protocols.EEBUS.UseCases.Monitoring;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "EV State of Charge", both actors, over the wire.
    ///
    /// The monitoring shape again, and the use case which shows where that shape
    /// ends: none of these measurements is on a wire, so none of them has a
    /// phase, and one of the four scenarios is not a measurement at all.
    /// </summary>
    [TestFixture]
    public class EVSOCTests
    {

        #region Data

        private FakeTimeProvider          time     = null!;
        private SPINELoopback             wire     = null!;

        private EVSOCMonitoringAppliance  watcher  = null!;
        private EVSOCElectricVehicle      car      = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            watcher = new EVSOCMonitoringAppliance(hems.AddEntity(EntityTypeType.CEM));

            car     = new EVSOCElectricVehicle(evse.AddEntity(EntityTypeType.EV),
                                               NominalCapacity:  58000,
                                               StateOfHealth:    true,
                                               TravelRange:      true);

            wire = new SPINELoopback(hems, evse);

            await watcher.Register();
            await car.    Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

        }

        #endregion

        #region (private) The car, as the appliance sees it

        private SPINERemoteEntity EV
            => wire.BAsSeenByA.Entity([ 1 ])!;

        #endregion


        #region TheTwoActorsFindEachOther()

        [Test]
        public void TheTwoActorsFindEachOther()
        {

            Assert.Multiple(() => {
                Assert.That(watcher.PartnerFor(EV),                     Is.Not.Null);
                Assert.That(watcher.PartnerFor(EV)?.Scenarios,          Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4 }));
                Assert.That(watcher.PartnerFor(EV)?.Version.ToString(), Is.EqualTo("1.0.0"));
                Assert.That(watcher.DocumentSubRevision,                Is.EqualTo("RC1"));
            });

        }

        #endregion

        #region TheWatchingActorGoesByTwoNames()

        /// <summary>
        /// Section 3.2.2 names this side MonitoringAppliance; the Go reference
        /// implementation announces a CEM. A car which accepted only one of them
        /// would ignore half the field - the same thing OPEV ran into, from the
        /// other direction.
        /// </summary>
        [Test]
        public async Task TheWatchingActorGoesByTwoNames()
        {

            Assert.That(watcher.Actor, Is.EqualTo("MonitoringAppliance"));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS2", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE2", DeviceTypeType.ChargingStation,        TimeProvider: time);

            var goish = new EVSOCMonitoringAppliance(hems.AddEntity(EntityTypeType.CEM),
                                                     AnnounceAsCEM: true);

            var other = new EVSOCElectricVehicle(evse.AddEntity(EntityTypeType.EV));

            var link  = new SPINELoopback(hems, evse);

            await goish.Register();
            await other.Register();

            await link.A.NodeManagement.RequestDetailedDiscovery(link.BAsSeenByA);
            await link.B.NodeManagement.RequestDetailedDiscovery(link.AAsSeenByB);
            await link.A.NodeManagement.RequestUseCaseData      (link.BAsSeenByA);
            await link.B.NodeManagement.RequestUseCaseData      (link.AAsSeenByB);

            Assert.Multiple(() => {

                Assert.That(goish.Actor, Is.EqualTo("CEM"));

                Assert.That(goish.PartnerFor(link.BAsSeenByA.Entity([ 1 ])), Is.Not.Null);

                Assert.That(other.PartnerFor(link.AAsSeenByB.Entity([ 1 ])), Is.Not.Null,
                            "A car refused an appliance which announced itself the way the Go implementation does.");

            });

        }

        #endregion

        #region NoneOfTheseMeasurementsIsOnAWire()

        /// <summary>
        /// The finding of this work package. Table 6 lists no electrical
        /// connection parameter description at all, because a state of charge in
        /// per cent has no phase, no rms variant and no voltage type. Every
        /// other monitoring use case we have publishes one per measurement;
        /// doing it here would put a claim on the wire the document does not
        /// make.
        /// </summary>
        [Test]
        public async Task NoneOfTheseMeasurementsIsOnAWire()
        {

            await watcher.Subscribe(EV);

            var quantities = watcher.Quantities(EV);

            Assert.Multiple(() => {

                Assert.That(EVStateOfCharge.Profile.ElectricalParameters, Is.False);

                Assert.That(car.Electrical, Is.Null,
                            "The state of charge published electrical connection parameter descriptions.");

                Assert.That(quantities.Values.All(quantity => quantity.Phase is null), Is.True,
                            "A state of charge was read as if it were on a phase.");

            });

        }

        #endregion

        #region OnlyTheStateOfChargeIsAMeasurementOfElectricity()

        /// <summary>
        /// Table 7 names a commodity type for scenario 1 and leaves it out for
        /// scenarios 3 and 4: the state of health of a battery and a distance in
        /// metres are measurements of the car, not of electricity.
        /// </summary>
        [Test]
        public void OnlyTheStateOfChargeIsAMeasurementOfElectricity()
        {

            var descriptions = car.Measurement.
                                   DataCopy<MeasurementDescriptionListDataType>(MonitoringFunctions.MeasurementDescriptionListData)!.
                                   MeasurementDescriptionData!;

            Assert.Multiple(() => {

                Assert.That(descriptions.Single(description => description.ScopeType == ScopeTypeType.StateOfCharge).CommodityType,
                            Is.EqualTo(CommodityTypeType.Electricity));

                Assert.That(descriptions.Single(description => description.ScopeType == ScopeTypeType.StateOfHealth).CommodityType,
                            Is.Null,
                            "The state of health of a battery was published as a measurement of electricity.");

                Assert.That(descriptions.Single(description => description.ScopeType == ScopeTypeType.TravelRange).CommodityType,
                            Is.Null,
                            "A distance in metres was published as a measurement of electricity.");

            });

        }

        #endregion

        #region Scenario1_TheStateOfChargeReachesTheAppliance()

        [Test]
        public async Task Scenario1_TheStateOfChargeReachesTheAppliance()
        {

            await watcher.Subscribe(EV);

            var before = wire.AToB.Datagrams.Count;

            await car.Set(EVStateOfCharge.StateOfCharge, 62.5m);

            Assert.Multiple(() => {

                Assert.That(watcher.StateOfCharge(EV), Is.EqualTo(62.5m));

                Assert.That(watcher.Quantities(EV).Values.Single(quantity => quantity.Scope == ScopeTypeType.StateOfCharge).Unit,
                            Is.EqualTo(UnitOfMeasurementType.Pct));

                Assert.That(wire.AToB.Datagrams, Has.Count.EqualTo(before),
                            "The appliance asked for the value instead of being told.");

            });

        }

        #endregion

        #region Scenario2_TheNominalCapacityIsACharacteristicRatherThanAReading()

        /// <summary>
        /// Table 10: the nominal capacity is an
        /// electricalConnectionCharacteristic with the context "entity" and the
        /// type "energyCapacityNominalMax", in watt hours. It is what the car is,
        /// not what it is doing - which is why it is not in the measurement
        /// feature at all.
        /// </summary>
        [Test]
        public async Task Scenario2_TheNominalCapacityIsACharacteristicRatherThanAReading()
        {

            await watcher.Subscribe(EV);

            Assert.Multiple(() => {

                Assert.That(car.NominalCapacity,          Is.EqualTo(58000));
                Assert.That(watcher.NominalCapacity(EV),  Is.EqualTo(58000));

                Assert.That(watcher.Quantities(EV).Values.Any(quantity => quantity.Scenario == EVStateOfCharge.ScenarioNominalCapacity),
                            Is.False,
                            "The nominal capacity was published as a measurement.");

            });

        }

        #endregion

        #region Scenario2_ACarWhichDoesNotPublishItIsNotAsked()

        [Test]
        public async Task Scenario2_ACarWhichDoesNotPublishItIsNotAsked()
        {

            var hems  = new SPINELocalDevice("d:_i:19667_HEMS3", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse  = new SPINELocalDevice("d:_i:19667_EVSE3", DeviceTypeType.ChargingStation,        TimeProvider: time);

            var plain = new EVSOCElectricVehicle(evse.AddEntity(EntityTypeType.EV));
            var watch = new EVSOCMonitoringAppliance(hems.AddEntity(EntityTypeType.CEM));

            var link  = new SPINELoopback(hems, evse);

            await plain.Register();
            await watch.Register();

            await link.A.NodeManagement.RequestDetailedDiscovery(link.BAsSeenByA);
            await link.A.NodeManagement.RequestUseCaseData      (link.BAsSeenByA);

            var partner = link.BAsSeenByA.Entity([ 1 ])!;

            await watch.Subscribe(partner);

            Assert.Multiple(() => {

                Assert.That(watch.PartnerFor(partner)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1 }));

                Assert.That(plain.Capacity, Is.Null);

                Assert.That(watch.NominalCapacity(partner), Is.Null,
                            "Asking a car which publishes no capacity threw instead of answering.");

            });

        }

        #endregion

        #region Scenarios3And4_TheOptionalOnesAreReadWithTheirOwnUnits()

        [Test]
        public async Task Scenarios3And4_TheOptionalOnesAreReadWithTheirOwnUnits()
        {

            await watcher.Subscribe(EV);

            await car.Set([
                (EVStateOfCharge.StateOfHealth,  91),
                (EVStateOfCharge.TravelRange,    284000)
            ]);

            var quantities = watcher.Quantities(EV);

            Assert.Multiple(() => {

                Assert.That(watcher.StateOfHealth(EV),  Is.EqualTo(91));
                Assert.That(watcher.TravelRange(EV),    Is.EqualTo(284000));

                var range = quantities.Values.Single(quantity => quantity.Scope == ScopeTypeType.TravelRange);

                Assert.That(range.Unit,      Is.EqualTo(UnitOfMeasurementType.M),
                            "The travel range was published in something other than metres.");
                Assert.That(range.Type,      Is.EqualTo(MeasurementTypeType.Distance));
                Assert.That(range.Scenario,  Is.EqualTo(4));

            });

        }

        #endregion

    }

}
