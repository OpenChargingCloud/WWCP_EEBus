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
using cloud.charging.open.protocols.EEBUS.UseCases.EVCEM;
using cloud.charging.open.protocols.EEBUS.UseCases.EVSOC;
using cloud.charging.open.protocols.EEBUS.UseCases.Monitoring;
using cloud.charging.open.protocols.EEBUS.UseCases.ChargingCurrent;
using cloud.charging.open.protocols.EEBUS.UseCases.OPEV;
using cloud.charging.open.protocols.EEBUS.UseCases.OSCEV;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// One car, four use cases, one entity.
    ///
    /// This is what an electric vehicle in the field actually is. EVCC says what
    /// it is, EVCEM says what is going into it, EVSOC says how full it is and
    /// OPEV curtails it - all on the same EV entity, because SPINE allows at
    /// most one feature of a given feature type and role per entity (1.3.0,
    /// Table 21). Three of the four write to the same electrical connection
    /// feature and two to the same measurement feature.
    ///
    /// ADR 0006 is about exactly this, and each of these tests exists because
    /// something here used to overwrite something else.
    /// </summary>
    [TestFixture]
    public class EMobilityCoexistenceTests
    {

        #region Data

        private FakeTimeProvider          time         = null!;
        private SPINELoopback             wire         = null!;

        private SPINELocalEntity          evEntity     = null!;
        private SPINELocalEntity          cemEntity    = null!;

        private EVCCElectricVehicle       commissioned = null!;
        private EVCEMElectricVehicle      measured     = null!;
        private EVSOCElectricVehicle      charged      = null!;

        private EVCCEnergyManager         cem          = null!;
        private EVCEMEnergyManager        meter        = null!;
        private EVSOCMonitoringAppliance  display      = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            evEntity     = evse.AddEntity(EntityTypeType.EV);
            cemEntity    = hems.AddEntity(EntityTypeType.CEM);

            // The order matters as little as possible, and that is the point:
            // whichever of them is built first, none may take the feature for
            // itself.
            commissioned = new EVCCElectricVehicle (evEntity,
                                                    Identifier:            "01-23-45-67-89-AB",
                                                    Manufacturer:          new ManufacturerData(DeviceName: "e-Golf"),
                                                    MinimumChargingPower:  1400,
                                                    MaximumChargingPower:  11000,
                                                    SleepMode:             true);

            measured     = new EVCEMElectricVehicle(evEntity,
                                                    Current:  true,
                                                    Power:    true,
                                                    Energy:   true);

            charged      = new EVSOCElectricVehicle(evEntity,
                                                    NominalCapacity:  58000,
                                                    StateOfHealth:    true);

            cem          = new EVCCEnergyManager       (cemEntity);
            meter        = new EVCEMEnergyManager      (cemEntity);
            display      = new EVSOCMonitoringAppliance(cemEntity);

            wire = new SPINELoopback(hems, evse);

            await commissioned.Register();
            await measured.    Register();
            await charged.     Register();
            await cem.         Register();
            await meter.       Register();
            await display.     Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

        }

        #endregion

        #region (private) The car, as the energy manager sees it

        private SPINERemoteEntity EV
            => wire.BAsSeenByA.Entity([ 1 ])!;

        private static readonly ElectricalConnectionPhaseNameType A = ElectricalConnectionPhaseNameType.A;

        #endregion


        #region OneEntityHasOneFeatureOfEachType()

        /// <summary>
        /// SPINE 1.3.0, Table 21 and the note beneath it. Three use cases wanted
        /// an electrical connection server and two wanted a measurement server;
        /// there is one of each.
        /// </summary>
        [Test]
        public void OneEntityHasOneFeatureOfEachType()
        {

            var servers = evEntity.Features.Where(feature => feature.Role == RoleType.Server).ToList();

            Assert.Multiple(() => {

                foreach (var group in servers.GroupBy(feature => feature.FeatureType))
                    Assert.That(group.Count(), Is.EqualTo(1),
                                $"The EV entity has {group.Count()} {group.Key} server features.");

                Assert.That(commissioned.Electrical, Is.SameAs(measured.Electrical));
                Assert.That(measured.Electrical,     Is.SameAs(charged.Capacity));
                Assert.That(measured.Measurement,    Is.SameAs(charged.Measurement));

            });

        }

        #endregion

        #region AllFourUseCasesAreAnnouncedAtOnce()

        [Test]
        public void AllFourUseCasesAreAnnouncedAtOnce()
        {

            Assert.Multiple(() => {
                Assert.That(cem.    PartnerFor(EV)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
                Assert.That(meter.  PartnerFor(EV)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3 }));
                Assert.That(display.PartnerFor(EV)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3 }));
            });

        }

        #endregion

        #region NoUseCaseOverwritesAnothersDescriptions()

        /// <summary>
        /// The measurement descriptions of EVCEM and EVSOC live in one list, and
        /// the electrical connection parameters of EVCC and EVCEM in another.
        /// Before this work package the identifiers of both started at zero and
        /// the second one to be built erased the first.
        /// </summary>
        [Test]
        public async Task NoUseCaseOverwritesAnothersDescriptions()
        {

            await meter.  Subscribe(EV);
            await display.Subscribe(EV);

            var descriptions = measured.Measurement.
                                   DataCopy<MeasurementDescriptionListDataType>(MonitoringFunctions.MeasurementDescriptionListData)!.
                                   MeasurementDescriptionData!;

            var scopes       = descriptions.Select(description => description.ScopeType).ToList();

            Assert.Multiple(() => {

                // EVCEM: 3 currents + total power + charged energy.
                Assert.That(scopes, Contains.Item(ScopeTypeType.AcCurrent));
                Assert.That(scopes, Contains.Item(ScopeTypeType.AcPower));
                Assert.That(scopes, Contains.Item(ScopeTypeType.Charge));

                // EVSOC: state of charge + state of health.
                Assert.That(scopes, Contains.Item(ScopeTypeType.StateOfCharge));
                Assert.That(scopes, Contains.Item(ScopeTypeType.StateOfHealth));

                Assert.That(descriptions.Select(description => description.MeasurementId).Distinct().Count(),
                            Is.EqualTo(descriptions.Count),
                            "Two use cases gave a measurement the same identifier.");

            });

        }

        #endregion

        #region ALimitWithNoMeasurementSurvivesAMeasurementWithNoLimit()

        /// <summary>
        /// The one which is easiest to get wrong. EVCC puts a parameter on the
        /// electrical connection which describes **no measurement at all** - the
        /// charging power limits of scenario 6 - and EVCEM puts one per
        /// measurement next to it. Matching "somebody else's" on
        /// "measurementId ?? 0" would have deleted the EVCC entry the moment a
        /// measurement got identifier zero, which is exactly what the lowest
        /// free identifier is.
        /// </summary>
        [Test]
        public async Task ALimitWithNoMeasurementSurvivesAMeasurementWithNoLimit()
        {

            await cem.  Subscribe(EV);
            await meter.Subscribe(EV);

            var parameters = measured.Electrical!.
                                 DataCopy<ElectricalConnectionParameterDescriptionListDataType>(MonitoringFunctions.ParameterDescriptionListData)!.
                                 ElectricalConnectionParameterDescriptionData!;

            Assert.Multiple(() => {

                Assert.That(parameters.Count(parameter => parameter.MeasurementId is null), Is.EqualTo(1),
                            "The charging power limit parameter of EVCC was lost.");

                Assert.That(parameters.Count(parameter => parameter.MeasurementId is not null), Is.EqualTo(5),
                            "The measurement parameters of EVCEM were lost.");

                Assert.That(parameters.Select(parameter => parameter.ParameterId).Distinct().Count(),
                            Is.EqualTo(parameters.Count),
                            "Two use cases gave a parameter the same identifier.");

                // And both are still readable at the other end.
                Assert.That(cem.  ChargingPowerLimits(EV)?.Minimum, Is.EqualTo(1400));
                Assert.That(meter.Quantities(EV),                   Has.Count.EqualTo(7));

            });

        }

        #endregion

        #region EachUseCaseStillReadsItsOwnValues()

        /// <summary>
        /// The end-to-end version: everything published on the shared features
        /// arrives at the right client, named correctly.
        /// </summary>
        [Test]
        public async Task EachUseCaseStillReadsItsOwnValues()
        {

            await cem.    Subscribe(EV);
            await meter.  Subscribe(EV);
            await display.Subscribe(EV);

            await measured.Set([
                (MeasurementOfElectricityDuringEVCharging.Current(A),    16),
                (MeasurementOfElectricityDuringEVCharging.PowerTotal, 11000),
                (MeasurementOfElectricityDuringEVCharging.EnergyCharged, 8400)
            ]);

            await charged.Set([
                (EVStateOfCharge.StateOfCharge,  62.5m),
                (EVStateOfCharge.StateOfHealth,  91)
            ]);

            await commissioned.FallAsleep();

            Assert.Multiple(() => {

                Assert.That(cem.Identifier(EV),           Is.EqualTo("01-23-45-67-89-AB"));
                Assert.That(cem.Manufacturer(EV)?.DeviceName, Is.EqualTo("e-Golf"));
                Assert.That(cem.IsAsleep(EV),             Is.True);

                Assert.That(meter.Current(EV, A),         Is.EqualTo(16));
                Assert.That(meter.Power(EV),              Is.EqualTo(11000));
                Assert.That(meter.EnergyCharged(EV),      Is.EqualTo(8400));

                Assert.That(display.StateOfCharge(EV),    Is.EqualTo(62.5m));
                Assert.That(display.StateOfHealth(EV),    Is.EqualTo(91));
                Assert.That(display.NominalCapacity(EV),  Is.EqualTo(58000));

            });

        }

        #endregion

        #region AStateOfChargeIsNotReadAsAnElectricalMeasurement()

        /// <summary>
        /// The two clients read the same measurement feature, and each of them
        /// has to name only what its own use case carries. The state of charge
        /// has no electrical connection parameter, so the electricity meter must
        /// not report it as a quantity of its own - and the display must not
        /// report the charging current.
        /// </summary>
        [Test]
        public async Task AStateOfChargeIsNotReadAsAnElectricalMeasurement()
        {

            await meter.  Subscribe(EV);
            await display.Subscribe(EV);

            Assert.Multiple(() => {

                Assert.That(meter.Quantities(EV).Values.Any(quantity => quantity.Scope == ScopeTypeType.StateOfCharge &&
                                                                         quantity.Scenario != 0),
                            Is.False,
                            "The electricity meter placed the state of charge in one of its own scenarios.");

                Assert.That(display.Quantities(EV).Values.Any(quantity => quantity.Scope == ScopeTypeType.AcCurrent &&
                                                                           quantity.Scenario != 0),
                            Is.False,
                            "The state of charge display placed the charging current in one of its own scenarios.");

                Assert.That(display.Quantities(EV).Values.
                                Single(quantity => quantity.Scope == ScopeTypeType.StateOfCharge).Scenario,
                            Is.EqualTo(EVStateOfCharge.ScenarioStateOfCharge));

            });

        }

        #endregion

        #region OPEVCanCurtailTheSameCar()

        /// <summary>
        /// And the fifth: the overload protection writes a load control limit to
        /// the same entity while everything above is running. It brings its own
        /// feature types, so nothing here collides - but a car in the field runs
        /// all five, and a test bench should say so out loud.
        /// </summary>
        [Test]
        public async Task OPEVCanCurtailTheSameCar()
        {

            var curtailed = new OPEVElectricVehicle(evEntity);
            var guard     = new OPEVEnergyGuard    (cemEntity);

            await curtailed.Register();
            await guard.    Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

            await cem.  Subscribe(EV);
            await meter.Subscribe(EV);

            Assert.Multiple(() => {

                Assert.That(guard.PartnerFor(EV), Is.Not.Null,
                            "The overload protection did not find the car it shares an entity with.");

                Assert.That(cem.  ChargingPowerLimits(EV)?.Minimum, Is.EqualTo(1400),
                            "Adding the overload protection erased the charging power limits of EVCC.");

                Assert.That(meter.Quantities(EV), Has.Count.EqualTo(7),
                            "Adding the overload protection erased the measurement descriptions of EVCEM.");

            });

        }

        #endregion

        #region BothChargingCurrentUseCasesCanRunOnOneCar()

        /// <summary>
        /// And the sixth. A car is regularly told two different things about its
        /// charging current at once: the overload protection says what it must
        /// not exceed, the optimisation of self consumption says what the sun is
        /// currently giving. Both write to the **same** load control feature,
        /// both describe the **same** three phases, and telling one from the
        /// other is done by the limit category and the scope rather than by the
        /// numbers.
        ///
        /// The phases in particular: a car has three of them, not six. A second
        /// use case which invented its own parameter descriptions would tell the
        /// other side that this car charges on six wires.
        /// </summary>
        [Test]
        public async Task BothChargingCurrentUseCasesCanRunOnOneCar()
        {

            var curtailed   = new OPEVElectricVehicle (evEntity);
            var optimised   = new OSCEVElectricVehicle(evEntity);

            curtailed.SetPermittedCurrents(6, 16);
            optimised.SetPermittedCurrents(6, 16);

            var guard       = new OPEVEnergyGuard   (cemEntity);
            var sunshine    = new OSCEVEnergyManager(cemEntity);

            await curtailed.Register();
            await optimised.Register();
            await guard.    Register();
            await sunshine. Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

            var obligations     = await guard.   ReadPhases(EV);
            var recommendations = await sunshine.ReadPhases(EV);

            var parameters      = curtailed.Electrical.
                                      DataCopy<ElectricalConnectionParameterDescriptionListDataType>(
                                          ChargingCurrentFunctions.ParameterDescriptionListData)!.
                                      ElectricalConnectionParameterDescriptionData!;

            Assert.Multiple(() => {

                Assert.That(curtailed.LoadControl, Is.SameAs(optimised.LoadControl));
                Assert.That(curtailed.Electrical,  Is.SameAs(optimised.Electrical));

                Assert.That(obligations,     Has.Count.EqualTo(3),
                            "The overload protection lost its limits to the self-consumption use case.");

                Assert.That(recommendations, Has.Count.EqualTo(3),
                            "The self-consumption use case lost its limits to the overload protection.");

                Assert.That(obligations.Select(phase => phase.LimitId).
                                Intersect(recommendations.Select(phase => phase.LimitId)),
                            Is.Empty,
                            "The two use cases were given the same limit identifiers.");

                // Three phases, not six: the second use case pointed its limits
                // at the parameters the first one had already described.
                Assert.That(parameters.Count(parameter => parameter.ScopeType == ScopeTypeType.AcCurrent),
                            Is.EqualTo(3),
                            "A car with three phases described six of them.");

                Assert.That(obligations.    Select(phase => phase.Phase),
                            Is.EquivalentTo(recommendations.Select(phase => phase.Phase)));

                Assert.That(obligations.All(phase => phase.MinimumCurrent == 6 &&
                                                      phase.MaximumCurrent == 16),
                            Is.True,
                            "The permitted currents were lost when the second use case wrote its own.");

            });

        }

        #endregion

        #region OneCarFollowsAnObligationAndIgnoresAdviceAtTheSameTime()

        /// <summary>
        /// The end-to-end version of the same thing, and the reason the two use
        /// cases are told apart at all: when both energy managers go quiet, the
        /// obligation falls back to a safe current and the advice simply stops.
        /// A car which treated them alike would either ignore its fuse or crawl
        /// because a cloud passed.
        /// </summary>
        [Test]
        public async Task OneCarFollowsAnObligationAndIgnoresAdviceAtTheSameTime()
        {

            var curtailed  = new OPEVElectricVehicle (evEntity);
            var optimised  = new OSCEVElectricVehicle(evEntity);

            curtailed.SetPermittedCurrents(6, 16);
            optimised.SetPermittedCurrents(6, 16);
            curtailed.SafeCurrent = 6;

            var guard      = new OPEVEnergyGuard   (cemEntity);
            var sunshine   = new OSCEVEnergyManager(cemEntity);

            await curtailed.Register();
            await optimised.Register();
            await guard.    Register();
            await sunshine. Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

            var partner = wire.AAsSeenByB.Entity([ 1 ])!;

            await new UseCaseFeature(FeatureTypeType.DeviceDiagnosis, evEntity, partner).Subscribe();

            var loadControl = guard.LoadControlOf(EV);
            await loadControl.Subscribe();
            await loadControl.Bind();
            await guard.ElectricalOf(EV).Subscribe();

            // One diagnosis feature and therefore one heartbeat for both.
            await guard.StartHeartbeat();

            curtailed.Check();
            optimised.Check();

            await guard.   WriteCurrentLimit        (EV, 16);
            await sunshine.WriteSelfProducedCurrent (EV, 10);

            Assert.Multiple(() => {
                Assert.That(curtailed.ChargingCurrents,    Is.EqualTo(new Decimal[]  { 16, 16, 16 }));
                Assert.That(optimised.RecommendedCurrents, Is.EqualTo(new Decimal?[] { 10, 10, 10 }));
            });

            guard.StopHeartbeat();
            time.Advance(TimeSpan.FromSeconds(5));

            curtailed.Check();
            optimised.Check();

            Assert.Multiple(() => {

                Assert.That(curtailed.ChargingCurrents,    Is.EqualTo(new Decimal[] { 6, 6, 6 }),
                            "The obligation did not fall back to the safe current.");

                Assert.That(optimised.RecommendedCurrents, Is.EqualTo(new Decimal?[] { null, null, null }),
                            "The recommendation fell back to something instead of simply ceasing.");

            });

        }

        #endregion

    }

}
