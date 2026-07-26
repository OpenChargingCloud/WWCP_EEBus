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
using cloud.charging.open.protocols.EEBUS.UseCases.EVCEM;
using cloud.charging.open.protocols.EEBUS.UseCases.Monitoring;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "Measurement of Electricity during EV Charging", both actors, over the
    /// wire.
    ///
    /// The monitoring shape pointed at the charging cable. What is its own is
    /// the rule that no single scenario is mandatory but silence is not allowed,
    /// and that the energy it counts is charged energy rather than what a
    /// connection has passed since it was installed.
    /// </summary>
    [TestFixture]
    public class EVCEMTests
    {

        #region Data

        private FakeTimeProvider      time     = null!;
        private SPINELoopback         wire     = null!;

        private EVCEMEnergyManager    manager  = null!;
        private EVCEMElectricVehicle  car      = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            manager = new EVCEMEnergyManager  (hems.AddEntity(EntityTypeType.CEM));

            car     = new EVCEMElectricVehicle(evse.AddEntity(EntityTypeType.EV),
                                               Current:  true,
                                               Power:    true,
                                               Energy:   true);

            wire = new SPINELoopback(hems, evse);

            await manager.Register();
            await car.    Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

        }

        #endregion

        #region (private) The car and the phases

        private SPINERemoteEntity EV
            => wire.BAsSeenByA.Entity([ 1 ])!;

        private static readonly ElectricalConnectionPhaseNameType A = ElectricalConnectionPhaseNameType.A;
        private static readonly ElectricalConnectionPhaseNameType B = ElectricalConnectionPhaseNameType.B;
        private static readonly ElectricalConnectionPhaseNameType C = ElectricalConnectionPhaseNameType.C;

        #endregion


        #region TheTwoActorsFindEachOther()

        [Test]
        public void TheTwoActorsFindEachOther()
        {

            Assert.Multiple(() => {
                Assert.That(manager.PartnerFor(EV),                     Is.Not.Null);
                Assert.That(manager.PartnerFor(EV)?.Scenarios,          Is.EquivalentTo(new UInt32[] { 1, 2, 3 }));
                Assert.That(manager.PartnerFor(EV)?.Version.ToString(), Is.EqualTo("1.0.1"));

                Assert.That(manager.Actor, Is.EqualTo("CEM"),
                            "Chapter 2 calls this side the Energy Guard, but section 3.2.2 says the wire says CEM.");
            });

        }

        #endregion

        #region NoScenarioIsMandatoryButSilenceIsNotAllowed()

        /// <summary>
        /// Section 2.3: "The EV SHALL support at least one of Scenario 1, 2 or
        /// 3, as all 3 scenarios measure electricity and can be converted into
        /// each other." So none of the three is mandatory on its own, and a car
        /// which publishes nothing is still a failure - a different check, not
        /// a missing one.
        /// </summary>
        [Test]
        public void NoScenarioIsMandatoryButSilenceIsNotAllowed()
        {

            var evse = new SPINELocalDevice("d:_i:19667_EVSE2", DeviceTypeType.ChargingStation, TimeProvider: time);

            Assert.Multiple(() => {

                Assert.That(MeasurementOfElectricityDuringEVCharging.Profile.MandatoryScenarios, Is.Empty,
                            "One of the three scenarios was declared mandatory on its own.");

                Assert.That(() => new EVCEMElectricVehicle(evse.AddEntity(EntityTypeType.EV),
                                                            Current:  false,
                                                            Power:    false,
                                                            Energy:   false),
                            Throws.ArgumentException,
                            "A car which measures nothing at all was accepted.");

            });

        }

        #endregion

        #region ACarWhichOnlyMeasuresCurrentSupportsTheUseCase()

        /// <summary>
        /// Current is the default, and section 2.3 says why: it "delivers the
        /// most reliable values", and it is the one measurement the other two
        /// can be derived from rather than the other way round.
        /// </summary>
        [Test]
        public async Task ACarWhichOnlyMeasuresCurrentSupportsTheUseCase()
        {

            var hems  = new SPINELocalDevice("d:_i:19667_HEMS3", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse  = new SPINELocalDevice("d:_i:19667_EVSE3", DeviceTypeType.ChargingStation,        TimeProvider: time);

            var plain = new EVCEMElectricVehicle(evse.AddEntity(EntityTypeType.EV));
            var watch = new EVCEMEnergyManager  (hems.AddEntity(EntityTypeType.CEM));

            var link  = new SPINELoopback(hems, evse);

            await plain.Register();
            await watch.Register();

            await link.A.NodeManagement.RequestDetailedDiscovery(link.BAsSeenByA);
            await link.A.NodeManagement.RequestUseCaseData      (link.BAsSeenByA);

            var partner = link.BAsSeenByA.Entity([ 1 ])!;

            Assert.That(watch.PartnerFor(partner)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1 }));

        }

        #endregion

        #region Scenario1_TheChargingCurrentIsPerPhase()

        /// <summary>
        /// [EVCEM-002]: the current is measured per phase, which is what makes
        /// asymmetric charging visible at all. A total in ampere would not.
        /// </summary>
        [Test]
        public async Task Scenario1_TheChargingCurrentIsPerPhase()
        {

            await manager.Subscribe(EV);

            await car.Set([
                (MeasurementOfElectricityDuringEVCharging.Current(A), 16),
                (MeasurementOfElectricityDuringEVCharging.Current(B), 10),
                (MeasurementOfElectricityDuringEVCharging.Current(C),  6)
            ]);

            Assert.Multiple(() => {
                Assert.That(manager.Current(EV, A), Is.EqualTo(16));
                Assert.That(manager.Current(EV, B), Is.EqualTo(10));
                Assert.That(manager.Current(EV, C), Is.EqualTo(6));
            });

        }

        #endregion

        #region Scenario3_TheEnergyIsChargedEnergyRatherThanConsumedEnergy()

        /// <summary>
        /// The scope is "charge", not "acEnergyConsumed" (Table 6). It counts
        /// what went into this car during this session, not what a connection
        /// has passed since it was installed - which is the difference between a
        /// charging summary and a meter reading.
        /// </summary>
        [Test]
        public async Task Scenario3_TheEnergyIsChargedEnergyRatherThanConsumedEnergy()
        {

            await manager.Subscribe(EV);

            await car.Set(MeasurementOfElectricityDuringEVCharging.EnergyCharged, 8400);

            var quantities = manager.Quantities(EV);

            Assert.Multiple(() => {

                Assert.That(manager.EnergyCharged(EV), Is.EqualTo(8400));

                var energy = quantities.Values.Single(quantity => quantity.Type == MeasurementTypeType.Energy);

                Assert.That(energy.Scope, Is.EqualTo(ScopeTypeType.Charge));
                Assert.That(energy.Unit,  Is.EqualTo(UnitOfMeasurementType.Wh));

                Assert.That(quantities.Values.Any(quantity => quantity.Scope == ScopeTypeType.AcEnergyConsumed),
                            Is.False,
                            "The charged energy was published with the scope of a meter reading.");

            });

        }

        #endregion

        #region TheCarDescribesItsConnectionAsConsumingAndAC()

        /// <summary>
        /// Table 9: powerSupplyType "ac" and positiveEnergyDirection "consume".
        /// Without them a client reading a positive number does not know which
        /// way the electricity was going.
        /// </summary>
        [Test]
        public void TheCarDescribesItsConnectionAsConsumingAndAC()
        {

            var connection = car.Electrical!.
                                 DataCopy<ElectricalConnectionDescriptionListDataType>(MonitoringFunctions.ElectricalDescriptionListData)?.
                                 ElectricalConnectionDescriptionData?.FirstOrDefault();

            Assert.Multiple(() => {
                Assert.That(connection,                          Is.Not.Null);
                Assert.That(connection?.PowerSupplyType,         Is.EqualTo(ElectricalConnectionVoltageTypeType.Ac));
                Assert.That(connection?.PositiveEnergyDirection, Is.EqualTo(EnergyDirectionType.Consume));
                Assert.That(connection?.AcConnectedPhases,       Is.EqualTo(3));
            });

        }

        #endregion

        #region EveryMeasurementIsJoinedToAPhaseByItsOwnParameter()

        /// <summary>
        /// The join again, and the reason the parameter identifiers are their
        /// own sequence: each measurement gets one parameter description, and
        /// no two share one.
        /// </summary>
        [Test]
        public async Task EveryMeasurementIsJoinedToAPhaseByItsOwnParameter()
        {

            await manager.Subscribe(EV);

            var parameters = car.Electrical!.
                                 DataCopy<ElectricalConnectionParameterDescriptionListDataType>(MonitoringFunctions.ParameterDescriptionListData)!.
                                 ElectricalConnectionParameterDescriptionData!;

            Assert.Multiple(() => {

                // 3 currents + 1 total power + 1 charged energy
                Assert.That(parameters,                                                              Has.Count.EqualTo(5));
                Assert.That(parameters.Select(parameter => parameter.ParameterId).  Distinct().Count(), Is.EqualTo(5));
                Assert.That(parameters.Select(parameter => parameter.MeasurementId).Distinct().Count(), Is.EqualTo(5));

                Assert.That(manager.Quantities(EV), Has.Count.EqualTo(5));

            });

        }

        #endregion

    }

}
