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
using cloud.charging.open.protocols.EEBUS.UseCases.MGCP;
using cloud.charging.open.protocols.EEBUS.UseCases.MPC;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "Monitoring of Grid Connection Point", both actors, over the wire.
    ///
    /// The monitoring of power consumption pointed at the boundary between a
    /// building and the grid. What is worth testing is where it differs: the
    /// grid scopes, the signed momentary power, and scenario 1 - which is not a
    /// measurement at all but a configuration value.
    /// </summary>
    [TestFixture]
    public class MGCPTests
    {

        #region Data

        private FakeTimeProvider          time       = null!;
        private SPINELoopback             wire       = null!;

        private MGCPMonitoringAppliance   appliance  = null!;
        private MGCPGridConnectionPoint   meterPoint = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems  = new SPINELocalDevice("d:_i:19667_HEMS",  DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var meter = new SPINELocalDevice("d:_i:19667_GCP",   DeviceTypeType.ElectricitySupplySystem, TimeProvider: time);

            appliance  = new MGCPMonitoringAppliance(hems.AddEntity(EntityTypeType.CEM));

            meterPoint = new MGCPGridConnectionPoint(meter.AddEntity(EntityTypeType.GridConnectionPointOfPremises),
                                                     Curtailment:  true,
                                                     Current:      true,
                                                     Voltage:      true,
                                                     Frequency:    true);

            wire = new SPINELoopback(hems, meter);

            await appliance. Register();
            await meterPoint.Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

        }

        #endregion

        #region (private) The grid connection point, as the energy manager sees it

        private SPINERemoteEntity GCP
            => wire.BAsSeenByA.Entity([ 1 ])!;

        private static readonly ElectricalConnectionPhaseNameType A = ElectricalConnectionPhaseNameType.A;
        private static readonly ElectricalConnectionPhaseNameType B = ElectricalConnectionPhaseNameType.B;
        private static readonly ElectricalConnectionPhaseNameType C = ElectricalConnectionPhaseNameType.C;

        #endregion


        #region TheTwoActorsFindEachOther()

        [Test]
        public void TheTwoActorsFindEachOther()
        {

            var announced = wire.B.NodeManagement.UseCases.First();

            Assert.Multiple(() => {

                Assert.That(announced.Actor,                                Is.EqualTo("GridConnectionPoint"));
                Assert.That(announced.UseCaseSupport?[0].UseCaseName,       Is.EqualTo("monitoringOfGridConnectionPoint"));
                Assert.That(announced.UseCaseSupport?[0].UseCaseVersion,    Is.EqualTo("1.0.0"));

                // All seven: three mandatory and the four this meter happens to
                // support.
                Assert.That(appliance.PartnerFor(GCP)?.Scenarios,
                            Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4, 5, 6, 7 }));

            });

        }

        #endregion

        #region TheMandatoryScenariosCannotBeOptedOutOf()

        /// <summary>
        /// A grid connection point which cannot say what is flowing and what has
        /// flowed is not a grid connection point: scenarios 2, 3 and 4 are
        /// mandatory, and a meter which asks for none of the optional ones still
        /// announces those three.
        /// </summary>
        [Test]
        public void TheMandatoryScenariosCannotBeOptedOutOf()
        {

            var device  = new SPINELocalDevice("d:_i:19667_Plain", DeviceTypeType.ElectricitySupplySystem, TimeProvider: time);
            var plain   = new MGCPGridConnectionPoint(device.AddEntity(EntityTypeType.GridConnectionPointOfPremises));

            Assert.Multiple(() => {

                Assert.That(plain.Scenarios.Select(scenario => scenario.Number),
                            Is.EquivalentTo(new UInt32[] { 2, 3, 4 }));

                // Not supporting scenario 1 means not offering the feature it
                // would live in.
                Assert.That(plain.Configuration,             Is.Null);
                Assert.That(plain.CurtailmentLimitFactor,    Is.Null);

            });

        }

        #endregion


        #region Scenario2_TheMomentaryPowerIsSigned()

        /// <summary>
        /// One measurement answers both questions: positive while the building
        /// draws from the grid, negative while it feeds in.
        ///
        /// This is the difference from the monitoring of power consumption which
        /// is easiest to get wrong. A monitoring appliance which took the
        /// absolute value would report a house exporting three kilowatts as a
        /// house importing three kilowatts.
        /// </summary>
        [Test]
        public async Task Scenario2_TheMomentaryPowerIsSigned()
        {

            await appliance.Subscribe(GCP);

            await meterPoint.Set(MonitoringOfGridConnectionPoint.Power, 4200);

            Assert.That(appliance.Power(GCP), Is.EqualTo(4200), "The building is drawing from the grid.");

            // The sun comes out.
            await meterPoint.Set(MonitoringOfGridConnectionPoint.Power, -3000);

            Assert.That(appliance.Power(GCP), Is.EqualTo(-3000), "The building is feeding in.");

        }

        #endregion

        #region Scenarios3And4_TheEnergyCountersAreGridScoped()

        /// <summary>
        /// The two energy counters are told apart by their scope, and the scopes
        /// are the ones of a grid connection point: it counts what crosses it,
        /// not what a device did.
        /// </summary>
        [Test]
        public async Task Scenarios3And4_TheEnergyCountersAreGridScoped()
        {

            await appliance.Subscribe(GCP);

            await meterPoint.Set([
                (MonitoringOfGridConnectionPoint.EnergyFeedIn,     11_000),
                (MonitoringOfGridConnectionPoint.EnergyConsumed,  456_000)
            ]);

            var quantities = appliance.Quantities(GCP).Values.ToList();

            Assert.Multiple(() => {

                Assert.That(appliance.EnergyFeedIn  (GCP),  Is.EqualTo( 11_000));
                Assert.That(appliance.EnergyConsumed(GCP),  Is.EqualTo(456_000));

                Assert.That(quantities.Any(quantity => quantity.Scope == ScopeTypeType.GridFeedIn),      Is.True);
                Assert.That(quantities.Any(quantity => quantity.Scope == ScopeTypeType.GridConsumption), Is.True);

                // Not the scopes of the monitoring of power consumption, which
                // are about a device rather than a boundary.
                Assert.That(quantities.Any(quantity => quantity.Scope == ScopeTypeType.AcEnergyProduced), Is.False);
                Assert.That(quantities.Any(quantity => quantity.Scope == ScopeTypeType.AcEnergyConsumed), Is.False);

                // And each of them belongs to its own scenario here, where the
                // monitoring of power consumption puts both under one.
                Assert.That(quantities.First(quantity => quantity.Scope == ScopeTypeType.GridFeedIn).Scenario,      Is.EqualTo(3));
                Assert.That(quantities.First(quantity => quantity.Scope == ScopeTypeType.GridConsumption).Scenario, Is.EqualTo(4));

            });

        }

        #endregion

        #region Scenarios5To7_TheMeasurementsPerPhaseArriveNamed()

        [Test]
        public async Task Scenarios5To7_TheMeasurementsPerPhaseArriveNamed()
        {

            await appliance.Subscribe(GCP);

            await meterPoint.Set([
                (MonitoringOfGridConnectionPoint.Current(A),  16),
                (MonitoringOfGridConnectionPoint.Current(B),  12),
                (MonitoringOfGridConnectionPoint.Current(C),   8),
                (MonitoringOfGridConnectionPoint.Voltage(A), 230),
                (MonitoringOfGridConnectionPoint.Frequency,   50)
            ]);

            Assert.Multiple(() => {

                Assert.That(appliance.Read(GCP, MonitoringOfGridConnectionPoint.Current(A))?.Value,  Is.EqualTo( 16));
                Assert.That(appliance.Read(GCP, MonitoringOfGridConnectionPoint.Current(B))?.Value,  Is.EqualTo( 12));
                Assert.That(appliance.Read(GCP, MonitoringOfGridConnectionPoint.Current(C))?.Value,  Is.EqualTo(  8));
                Assert.That(appliance.Read(GCP, MonitoringOfGridConnectionPoint.Voltage(A))?.Value,  Is.EqualTo(230));
                Assert.That(appliance.Read(GCP, MonitoringOfGridConnectionPoint.Frequency)?.Value,   Is.EqualTo( 50));

                // The frequency of the grid is not on a phase; the current is.
                Assert.That(appliance.Read(GCP, MonitoringOfGridConnectionPoint.Frequency)?.Quantity.Phase,   Is.Null);
                Assert.That(appliance.Read(GCP, MonitoringOfGridConnectionPoint.Current(B))?.Quantity.Phase,  Is.EqualTo(B));

            });

        }

        #endregion


        #region Scenario1_TheCurtailmentFactorIsReadAndFollowed()

        /// <summary>
        /// Scenario 1 is not a measurement: the curtailment limit factor is a
        /// configuration value, in a different feature, under a key found by
        /// name.
        ///
        /// It is also the one number in this use case which is an instruction
        /// rather than an observation - what a grid operator's curtailment
        /// order looks like by the time it reaches the building - so a change to
        /// it has to arrive, not wait to be polled.
        /// </summary>
        [Test]
        public async Task Scenario1_TheCurtailmentFactorIsReadAndFollowed()
        {

            await appliance.Subscribe(GCP);

            // Nothing is curtailed until somebody says otherwise.
            Assert.That(appliance.CurtailmentLimitFactor(GCP), Is.EqualTo(1));

            // The grid operator asks for 70 %.
            await meterPoint.SetCurtailmentLimitFactor(0.7m);

            Assert.Multiple(() => {

                Assert.That(meterPoint.CurtailmentLimitFactor,     Is.EqualTo(0.7m));

                // ... and it arrived by itself, without being asked for again.
                Assert.That(appliance.CurtailmentLimitFactor(GCP), Is.EqualTo(0.7m),
                            "The change to the curtailment factor was not notified.");

            });

        }

        #endregion

        #region Scenario1_ACurtailmentFactorIsAFraction()

        [Test]
        public void Scenario1_ACurtailmentFactorIsAFraction()
        {

            Assert.Multiple(() => {

                Assert.That(async () => await meterPoint.SetCurtailmentLimitFactor(1.5m),
                            Throws.TypeOf<ArgumentOutOfRangeException>());

                Assert.That(async () => await meterPoint.SetCurtailmentLimitFactor(-0.1m),
                            Throws.TypeOf<ArgumentOutOfRangeException>());

                // Zero is a fraction, and a meaningful one: feed in nothing.
                Assert.That(async () => await meterPoint.SetCurtailmentLimitFactor(0),
                            Throws.Nothing);

            });

        }

        #endregion

        #region Scenario1_AnApplianceWhichDoesNotWatchItDoesNotAskForIt()

        /// <summary>
        /// Scenario 1 is optional on both sides. An appliance which did not
        /// announce it must not subscribe to a feature it has no business
        /// watching - which is also what keeps the shared Subscribe of the
        /// monitoring use cases honest.
        /// </summary>
        [Test]
        public async Task Scenario1_AnApplianceWhichDoesNotWatchItDoesNotAskForIt()
        {

            var hems  = new SPINELocalDevice("d:_i:19667_HEMS2", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var meter = new SPINELocalDevice("d:_i:19667_GCP2",  DeviceTypeType.ElectricitySupplySystem, TimeProvider: time);

            var plain = new MGCPMonitoringAppliance(hems.AddEntity(EntityTypeType.CEM),
                                                    Scenarios: []);

            var point = new MGCPGridConnectionPoint(meter.AddEntity(EntityTypeType.GridConnectionPointOfPremises),
                                                    Curtailment: true);

            var other = new SPINELoopback(hems, meter);

            await plain.Register();
            await point.Register();

            await other.A.NodeManagement.RequestDetailedDiscovery(other.BAsSeenByA);
            await other.A.NodeManagement.RequestUseCaseData      (other.BAsSeenByA);

            var partner = other.BAsSeenByA.Entity([ 1 ])!;

            await plain.Subscribe(partner);

            Assert.Multiple(() => {

                Assert.That(plain.WatchesCurtailment,                     Is.False);
                Assert.That(plain.CurtailmentLimitFactor(partner),        Is.Null,
                            "An appliance which does not watch scenario 1 read the curtailment factor anyway.");

                // The measurements it did announce still arrive.
                Assert.That(point.Configuration,                          Is.Not.Null);
                Assert.That(plain.Quantities(partner),                    Is.Not.Empty);

            });

        }

        #endregion


        #region BothMonitoringUseCasesCanRunOnOneDevice()

        /// <summary>
        /// A meter at the grid connection point of a building may well also be
        /// the monitored unit of the monitoring of power consumption - the same
        /// entity, the same measurement feature, two use cases.
        ///
        /// The measurements of the two are told apart by scope, which is what
        /// makes that possible: "gridFeedIn" and "acEnergyProduced" are
        /// different statements about the same wire.
        /// </summary>
        [Test]
        public async Task BothMonitoringUseCasesCanRunOnOneDevice()
        {

            var hems   = new SPINELocalDevice("d:_i:19667_HEMS3", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var meter  = new SPINELocalDevice("d:_i:19667_GCP3",  DeviceTypeType.ElectricitySupplySystem, TimeProvider: time);

            var entity = meter.AddEntity(EntityTypeType.GridConnectionPointOfPremises);

            var grid   = new MGCPGridConnectionPoint(entity);
            var unit   = new MPCMonitoredUnit       (entity, Energy: true);

            var watch  = new MGCPMonitoringAppliance(hems.AddEntity(EntityTypeType.CEM), Scenarios: []);

            var other  = new SPINELoopback(hems, meter);

            await grid. Register();
            await unit. Register();
            await watch.Register();

            await other.A.NodeManagement.RequestDetailedDiscovery(other.BAsSeenByA);
            await other.A.NodeManagement.RequestUseCaseData      (other.BAsSeenByA);

            var partner = other.BAsSeenByA.Entity([ 1 ])!;

            await watch.Subscribe(partner);

            await grid.Set(MonitoringOfGridConnectionPoint.EnergyFeedIn,     11_000);
            await unit.Set(MonitoringOfPowerConsumption.   EnergyProduced,    9_500);

            var quantities = watch.Quantities(partner).Values.ToList();

            Assert.Multiple(() => {

                // One measurement feature, both sets of measurements on it,
                // under identifiers which do not collide.
                Assert.That(grid.Measurement, Is.SameAs(unit.Measurement));

                Assert.That(quantities.Any(quantity => quantity.Scope == ScopeTypeType.GridFeedIn),       Is.True);
                Assert.That(quantities.Any(quantity => quantity.Scope == ScopeTypeType.AcEnergyProduced), Is.True);

                Assert.That(watch.EnergyFeedIn(partner), Is.EqualTo(11_000),
                            "The two use cases got their measurements mixed up.");

                // Two announcements, one entity.
                Assert.That(other.B.NodeManagement.UseCases.SelectMany(entry => entry.UseCaseSupport ?? []).
                                Select(support => support.UseCaseName),
                            Is.EquivalentTo(new[] { "monitoringOfGridConnectionPoint",
                                                    "monitoringOfPowerConsumption" }));

            });

        }

        #endregion

    }

}
