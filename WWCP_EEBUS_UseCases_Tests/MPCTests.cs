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
using cloud.charging.open.protocols.EEBUS.UseCases.MPC;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "Monitoring of Power Consumption", both actors, over the wire.
    ///
    /// The plainest of the use cases: nothing is written, nothing has a state,
    /// nothing falls back. Which makes it the one where all the difficulty is in
    /// the descriptions - a measured value arrives as a number under an
    /// identifier, and what it means comes from two descriptions in two
    /// features.
    /// </summary>
    [TestFixture]
    public class MPCTests
    {

        #region Data

        private FakeTimeProvider         time       = null!;
        private SPINELoopback            wire       = null!;

        private MPCMonitoringAppliance   appliance  = null!;
        private MPCMonitoredUnit         unit       = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems  = new SPINELocalDevice("d:_i:19667_HEMS",  DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var meter = new SPINELocalDevice("d:_i:19667_Meter", DeviceTypeType.SubMeter,       TimeProvider: time);

            appliance = new MPCMonitoringAppliance(hems.AddEntity(EntityTypeType.CEM));

            unit      = new MPCMonitoredUnit(meter.AddEntity(EntityTypeType.SubMeterElectricity),
                                             PowerPerPhase:  true,
                                             Energy:         true,
                                             Current:        true,
                                             Voltage:        true,
                                             Frequency:      true);

            wire = new SPINELoopback(hems, meter);

            await appliance.Register();
            await unit.     Register();

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

        /// <summary>The meter, as the energy manager sees it.</summary>
        private SPINERemoteEntity MU
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
                Assert.That(appliance.PartnerFor(MU),            Is.Not.Null);
                Assert.That(appliance.PartnerFor(MU)?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3, 4, 5 }));
                Assert.That(appliance.PartnerFor(MU)?.Version.ToString(), Is.EqualTo("1.0.0"));
            });

        }

        #endregion

        #region AUnitWhichOnlyMeasuresPowerSupportsTheUseCaseCompletely()

        /// <summary>
        /// Only scenario 1 is mandatory (Table 1). A meter which knows nothing
        /// but its total active power implements this use case, and the
        /// monitoring appliance has to be able to tell that from a meter which
        /// simply forgot to announce the rest.
        /// </summary>
        [Test]
        public async Task AUnitWhichOnlyMeasuresPowerSupportsTheUseCaseCompletely()
        {

            var hems   = new SPINELocalDevice("d:_i:19667_HEMS2",  DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var meter  = new SPINELocalDevice("d:_i:19667_Meter2", DeviceTypeType.SubMeter,       TimeProvider: time);

            var simple = new MPCMonitoredUnit      (meter.AddEntity(EntityTypeType.SubMeterElectricity));
            var watch  = new MPCMonitoringAppliance(hems. AddEntity(EntityTypeType.CEM));

            var other  = new SPINELoopback(hems, meter);

            await simple.Register();
            await watch. Register();

            await other.A.NodeManagement.RequestDetailedDiscovery(other.BAsSeenByA);
            await other.A.NodeManagement.RequestUseCaseData      (other.BAsSeenByA);

            var partner = watch.PartnerFor(other.BAsSeenByA.Entity([ 1 ]));

            Assert.Multiple(() => {
                Assert.That(partner,            Is.Not.Null);
                Assert.That(partner?.Scenarios, Is.EquivalentTo(new UInt32[] { 1 }),
                            "A unit which measures only power announced more than scenario 1.");
            });

        }

        #endregion


        #region AMeasuredValueIsOnlyMeaningfulWithItsTwoDescriptions()

        /// <summary>
        /// The join which is the whole of the client side: the measurement
        /// description says what a measurement is and in which unit, the
        /// electrical connection parameter description says which phase it is
        /// on, and the measurement identifier connects them.
        /// </summary>
        [Test]
        public async Task AMeasuredValueIsOnlyMeaningfulWithItsTwoDescriptions()
        {

            await appliance.Subscribe(MU);

            var quantities = appliance.Quantities(MU);

            Assert.Multiple(() => {

                // 1 total power + 3 per phase + 2 energy + 3 current + 3 voltage + 1 frequency
                Assert.That(quantities, Has.Count.EqualTo(13));

                var total = quantities.Values.Single(quantity => quantity.Scope == ScopeTypeType.AcPowerTotal);

                Assert.That(total.Unit,      Is.EqualTo(UnitOfMeasurementType.W));
                Assert.That(total.Type,      Is.EqualTo(MeasurementTypeType.Power));
                Assert.That(total.Phase,     Is.Null,
                            "The total power was read as if it were on a single phase.");
                Assert.That(total.Scenario,  Is.EqualTo(1));

                var currentB = quantities.Values.Single(quantity => quantity.Scope == ScopeTypeType.AcCurrent &&
                                                                    quantity.Phase == B);

                Assert.That(currentB.Unit,     Is.EqualTo(UnitOfMeasurementType.A));
                Assert.That(currentB.Scenario, Is.EqualTo(3));

            });

        }

        #endregion

        #region Scenario1_ThePowerArrivesWithoutBeingAskedFor()

        /// <summary>
        /// Scenario 1, and the way the use case is meant to be used: the
        /// appliance subscribes once and the values arrive by themselves
        /// (general implementation guideline § 3.2.2).
        /// </summary>
        [Test]
        public async Task Scenario1_ThePowerArrivesWithoutBeingAskedFor()
        {

            await appliance.Subscribe(MU);

            var before = wire.AToB.Datagrams.Count;

            await unit.Set(MonitoringOfPowerConsumption.PowerTotal, 4321);

            var reading = appliance.Read(MU, MonitoringOfPowerConsumption.PowerTotal);

            Assert.Multiple(() => {

                Assert.That(reading?.Value,      Is.EqualTo(4321));
                Assert.That(reading?.Timestamp,  Is.EqualTo(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero)));

                Assert.That(wire.AToB.Datagrams, Has.Count.EqualTo(before),
                            "The appliance asked for the value instead of being told.");

            });

        }

        #endregion

        #region Scenario1_ThePowerOfEachPhaseIsItsOwnMeasurement()

        [Test]
        public async Task Scenario1_ThePowerOfEachPhaseIsItsOwnMeasurement()
        {

            await appliance.Subscribe(MU);

            await unit.Set([
                (MonitoringOfPowerConsumption.PowerTotal, 4321),
                (MonitoringOfPowerConsumption.Power(A),   1000),
                (MonitoringOfPowerConsumption.Power(B),   1500),
                (MonitoringOfPowerConsumption.Power(C),   1821)
            ]);

            Assert.Multiple(() => {
                Assert.That(appliance.Read(MU, MonitoringOfPowerConsumption.PowerTotal)?.Value, Is.EqualTo(4321));
                Assert.That(appliance.Read(MU, MonitoringOfPowerConsumption.Power(A))?.Value,   Is.EqualTo(1000));
                Assert.That(appliance.Read(MU, MonitoringOfPowerConsumption.Power(B))?.Value,   Is.EqualTo(1500));
                Assert.That(appliance.Read(MU, MonitoringOfPowerConsumption.Power(C))?.Value,   Is.EqualTo(1821));
            });

        }

        #endregion

        #region Scenarios2To5_EnergyCurrentVoltageAndFrequency()

        /// <summary>
        /// The four optional scenarios, each with its own unit - and the units
        /// are the point: a number of 230 is a voltage, a number of 16 is a
        /// current, and only the description says which.
        /// </summary>
        [Test]
        public async Task Scenarios2To5_EnergyCurrentVoltageAndFrequency()
        {

            await appliance.Subscribe(MU);

            await unit.Set([
                (MonitoringOfPowerConsumption.EnergyConsumed, 1234567),
                (MonitoringOfPowerConsumption.EnergyProduced,  765432),
                (MonitoringOfPowerConsumption.Current(A),           16),
                (MonitoringOfPowerConsumption.Voltage(A),          230),
                (MonitoringOfPowerConsumption.Frequency,            50)
            ]);

            Assert.Multiple(() => {

                var energy = appliance.Read(MU, MonitoringOfPowerConsumption.EnergyConsumed);

                Assert.That(energy?.Value,         Is.EqualTo(1234567));
                Assert.That(energy?.Quantity.Unit, Is.EqualTo(UnitOfMeasurementType.Wh));

                Assert.That(appliance.Read(MU, MonitoringOfPowerConsumption.EnergyProduced)?.Value, Is.EqualTo(765432));

                var current = appliance.Read(MU, MonitoringOfPowerConsumption.Current(A));

                Assert.That(current?.Value,         Is.EqualTo(16));
                Assert.That(current?.Quantity.Unit, Is.EqualTo(UnitOfMeasurementType.A));

                var voltage = appliance.Read(MU, MonitoringOfPowerConsumption.Voltage(A));

                Assert.That(voltage?.Value,         Is.EqualTo(230));
                Assert.That(voltage?.Quantity.Unit, Is.EqualTo(UnitOfMeasurementType.V));

                var frequency = appliance.Read(MU, MonitoringOfPowerConsumption.Frequency);

                Assert.That(frequency?.Value,         Is.EqualTo(50));
                Assert.That(frequency?.Quantity.Unit, Is.EqualTo(UnitOfMeasurementType.Hz));

            });

        }

        #endregion

        #region EveryReadingCarriesTheTimeItWasMeasured()

        /// <summary>
        /// A measured value without a timestamp is a value which may be an hour
        /// old. The timestamp comes from the device's time provider, so it is
        /// the device's own clock rather than the reader's.
        /// </summary>
        [Test]
        public async Task EveryReadingCarriesTheTimeItWasMeasured()
        {

            await appliance.Subscribe(MU);

            await unit.Set(MonitoringOfPowerConsumption.PowerTotal, 1000);

            time.Advance(TimeSpan.FromMinutes(5));

            await unit.Set(MonitoringOfPowerConsumption.PowerTotal, 2000);

            var reading = appliance.Read(MU, MonitoringOfPowerConsumption.PowerTotal);

            Assert.Multiple(() => {
                Assert.That(reading?.Value,     Is.EqualTo(2000));
                Assert.That(reading?.Timestamp, Is.EqualTo(new DateTimeOffset(2026, 7, 26, 12, 5, 0, TimeSpan.Zero)));
            });

        }

        #endregion

        #region AUnitDoesNotPublishWhatItDoesNotMeasure()

        /// <summary>
        /// Asking a unit to publish a quantity it never declared is a mistake in
        /// the device, not a message to send.
        /// </summary>
        [Test]
        public void AUnitDoesNotPublishWhatItDoesNotMeasure()
        {

            var meter  = new SPINELocalDevice("d:_i:19667_Meter3", DeviceTypeType.SubMeter, TimeProvider: time);

            var simple = new MPCMonitoredUnit(meter.AddEntity(EntityTypeType.SubMeterElectricity));

            Assert.Multiple(() => {

                Assert.That(() => simple.Set(MonitoringOfPowerConsumption.Frequency, 50),
                            Throws.ArgumentException,
                            "A unit published a quantity it never declared.");

                Assert.That(() => simple.Set(MonitoringOfPowerConsumption.PowerTotal, 1000),
                            Throws.Nothing);

            });

        }

        #endregion

        #region AnApplianceReadsNothingItCannotName()

        /// <summary>
        /// A measurement whose description never arrived is left out rather than
        /// guessed at. A number nobody can name is worse than no number - it
        /// looks like data.
        /// </summary>
        [Test]
        public async Task AnApplianceReadsNothingItCannotName()
        {

            // The values, but never the descriptions.
            await appliance.MeasurementOf(MU).RequestData(MonitoringOfPowerConsumption.MeasurementListData);

            await unit.Set(MonitoringOfPowerConsumption.PowerTotal, 4321);

            Assert.Multiple(() => {
                Assert.That(appliance.Quantities(MU), Is.Empty);
                Assert.That(appliance.Readings(MU),   Is.Empty,
                            "Values were reported although nothing said what they are.");
            });

            // With the descriptions, the same values become readings.
            await appliance.Subscribe(MU);

            Assert.That(appliance.Read(MU, MonitoringOfPowerConsumption.PowerTotal)?.Value, Is.EqualTo(4321));

        }

        #endregion

        #region AMinimumOrAnAverageIsNotTheValue()

        /// <summary>
        /// SPINE lets a measurement carry a minimum, a maximum or an average of
        /// the same quantity. This use case is about the value itself, and an
        /// appliance which takes the first entry it finds would report the
        /// minimum of the day as the current power.
        /// </summary>
        [Test]
        public async Task AMinimumOrAnAverageIsNotTheValue()
        {

            await appliance.Subscribe(MU);

            await unit.Set(MonitoringOfPowerConsumption.PowerTotal, 4321);

            // A device which also reports the minimum, under the same
            // measurement identifier and a different value type.
            var data = unit.Measurement.DataCopy<MeasurementListDataType>(MonitoringOfPowerConsumption.MeasurementListData)!;

            data.MeasurementData!.Insert(0,
                new MeasurementDataType {
                    MeasurementId  = data.MeasurementData[0].MeasurementId,
                    ValueType      = MeasurementValueTypeType.MinValue,
                    Value          = ScaledNumberType.FromValue(12)
                });

            await unit.Measurement.SetData(MonitoringOfPowerConsumption.MeasurementListData, data);

            Assert.That(appliance.Read(MU, MonitoringOfPowerConsumption.PowerTotal)?.Value,
                        Is.EqualTo(4321),
                        "The minimum was reported as the value.");

        }

        #endregion

        #region MPCIsWhatTheLimitationOfPowerConsumptionAsksFor()

        /// <summary>
        /// LPC 1.0.0, section 2.2: "The Energy Guard SHOULD monitor the actual
        /// power consumption of the CS. [...] the Use Case 'Monitoring of Power
        /// Consumption' SHALL be used by the CS."
        ///
        /// So an energy guard which limits and a monitoring appliance which
        /// watches are the same device, and the total active power of this use
        /// case is what tells it whether its limits are being kept.
        /// </summary>
        [Test]
        public async Task MPCIsWhatTheLimitationOfPowerConsumptionAsksFor()
        {

            await appliance.Subscribe(MU);

            await unit.Set(MonitoringOfPowerConsumption.PowerTotal, 11000);

            var beforeLimit = appliance.Read(MU, MonitoringOfPowerConsumption.PowerTotal)?.Value;

            await unit.Set(MonitoringOfPowerConsumption.PowerTotal, 4200);

            var afterLimit  = appliance.Read(MU, MonitoringOfPowerConsumption.PowerTotal)?.Value;

            Assert.Multiple(() => {
                Assert.That(beforeLimit, Is.EqualTo(11000));
                Assert.That(afterLimit,  Is.EqualTo(4200),
                            "The energy guard cannot see whether its limit is being kept.");
            });

        }

        #endregion

    }

}
