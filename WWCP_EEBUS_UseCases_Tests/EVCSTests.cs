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
using cloud.charging.open.protocols.EEBUS.UseCases.EVCS;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// "EV Charging Summary", both actors, over the wire.
    ///
    /// The only use case in the family with no Go reference implementation, so
    /// everything here comes from the document. It is also the only one which
    /// answers "what just happened" rather than "what should happen next".
    /// </summary>
    [TestFixture]
    public class EVCSTests
    {

        #region Data

        private FakeTimeProvider     time     = null!;
        private SPINELoopback        wire     = null!;

        private EVCSEnergyBroker     broker   = null!;
        private EVCSChargingStation  station  = null!;

        #endregion

        #region Setup()

        [SetUp]
        public async Task Setup()
        {

            time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem, TimeProvider: time);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation,        TimeProvider: time);

            broker  = new EVCSEnergyBroker   (hems.AddEntity(EntityTypeType.CEM));
            station = new EVCSChargingStation(evse.AddEntity(EntityTypeType.EVSE));

            wire = new SPINELoopback(hems, evse);

            await broker. Register();
            await station.Register();

            await wire.A.NodeManagement.RequestDetailedDiscovery(wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestDetailedDiscovery(wire.AAsSeenByB);
            await wire.A.NodeManagement.RequestUseCaseData      (wire.BAsSeenByA);
            await wire.B.NodeManagement.RequestUseCaseData      (wire.AAsSeenByB);

        }

        #endregion

        #region (private) The charging station, as the broker sees it

        private SPINERemoteEntity EVSE
            => wire.BAsSeenByA.Entity([ 1 ])!;


        /// <summary>
        /// Twenty kilowatt hours over four hours for six euros, two thirds of it
        /// from the grid - but four fifths of the money, because the sun is the
        /// cheap part. Which is the whole reason a customer is shown this.
        /// </summary>
        private static ChargingSummary ASession()

            => new (Duration:                      TimeSpan.FromHours(4),
                    Energy:                        20_000,
                    Cost:                          6.00m,
                    Currency:                      CurrencyType.EUR,
                    GridEnergyPercentage:          65,
                    GridCostPercentage:            80,
                    SelfProducedEnergyPercentage:  35,
                    SelfProducedCostPercentage:    20);

        #endregion


        #region TheTwoActorsFindEachOther()

        [Test]
        public void TheTwoActorsFindEachOther()
        {

            Assert.Multiple(() => {
                Assert.That(broker. PartnerFor(EVSE),                     Is.Not.Null);
                Assert.That(broker. PartnerFor(EVSE)?.Scenarios,          Is.EquivalentTo(new UInt32[] { 1 }));
                Assert.That(broker. PartnerFor(EVSE)?.Version.ToString(), Is.EqualTo("1.0.1"));
                Assert.That(broker. Actor,                                Is.EqualTo("EnergyBroker"));
                Assert.That(station.Actor,                                Is.EqualTo("EVSE"));
            });

        }

        #endregion

        #region TheChargingStationIsTheServerRatherThanTheBroker()

        /// <summary>
        /// The direction is the other way round from what the name suggests: the
        /// broker knows the prices, but the charging station is where somebody
        /// is standing, so it holds the Bill feature and the broker writes into
        /// it.
        /// </summary>
        [Test]
        public void TheChargingStationIsTheServerRatherThanTheBroker()
        {

            Assert.Multiple(() => {

                Assert.That(station.Entity.Feature(FeatureTypeType.Bill, RoleType.Server), Is.Not.Null);
                Assert.That(station.Entity.Feature(FeatureTypeType.Bill, RoleType.Client), Is.Null);

                Assert.That(broker.Entity.Feature(FeatureTypeType.Bill, RoleType.Client),  Is.Not.Null);
                Assert.That(broker.Entity.Feature(FeatureTypeType.Bill, RoleType.Server),  Is.Null);

                Assert.That(station.Bill.FunctionData(EVChargingSummary.BillListData)?.Operations.CanWrite,
                            Is.True,
                            "The bill the broker has to fill in was not writeable.");

            });

        }

        #endregion

        #region Scenario1_TheSummaryReachesTheChargingStation()

        [Test]
        public async Task Scenario1_TheSummaryReachesTheChargingStation()
        {

            await broker.Subscribe(EVSE);

            var response = await broker.WriteSummary(EVSE, ASession());

            var summary  = station.Summary;

            Assert.Multiple(() => {

                Assert.That(response.IsError, Is.False, response.Result?.Description);

                Assert.That(summary,           Is.Not.Null);
                Assert.That(summary?.Energy,   Is.EqualTo(20_000));
                Assert.That(summary?.Cost,     Is.EqualTo(6.00m));
                Assert.That(summary?.Currency, Is.EqualTo(CurrencyType.EUR));
                Assert.That(summary?.Duration, Is.EqualTo(TimeSpan.FromHours(4)));

            });

        }

        #endregion

        #region Scenario1_ThePositionsArePercentagesRatherThanAmounts()

        /// <summary>
        /// The thing this use case is easiest to get wrong. Table 8 gives the
        /// total in watt hours and money, and the split between grid and
        /// self-produced electricity as `valuePercentage` and `costPercentage` -
        /// **shares of that total**. A reader which took them for absolute
        /// amounts would report a 20 kWh session as having drawn 65 watt hours
        /// from the grid.
        /// </summary>
        [Test]
        public async Task Scenario1_ThePositionsArePercentagesRatherThanAmounts()
        {

            await broker.Subscribe(EVSE);
            await broker.WriteSummary(EVSE, ASession());

            var bill = station.Bill.
                           DataCopy<BillListDataType>(EVChargingSummary.BillListData)!.
                           BillData!.First();

            Assert.Multiple(() => {

                var grid = bill.Position!.Single(position => position.PositionType == BillPositionTypeType.GridElectricEnergy);

                Assert.That(grid.Value!.First().ValuePercentage?.Value, Is.EqualTo(65));
                Assert.That(grid.Value!.First().Value,                  Is.Null,
                            "A position carried an absolute energy, which Table 8 does not have.");

                Assert.That(grid.Cost!.First().CostPercentage?.Value,   Is.EqualTo(80));
                Assert.That(grid.Cost!.First().Cost,                    Is.Null,
                            "A position carried an absolute cost, which Table 8 does not have.");

                // And what the reading side makes of that.
                var summary = station.Summary!;

                Assert.That(summary.GridEnergy,         Is.EqualTo(13_000));
                Assert.That(summary.GridCost,           Is.EqualTo(4.80m));
                Assert.That(summary.SelfProducedEnergy, Is.EqualTo(7_000));
                Assert.That(summary.SelfProducedCost,   Is.EqualTo(1.20m));

            });

        }

        #endregion

        #region Scenario1_TheEnergyShareAndTheCostShareAreDifferentNumbers()

        /// <summary>
        /// And the reason a customer is shown any of this: a third of the
        /// kilowatt hours came from the roof and only a fifth of the money did.
        /// An implementation which reused one percentage for both would show a
        /// household that its photovoltaic system saves nothing.
        /// </summary>
        [Test]
        public async Task Scenario1_TheEnergyShareAndTheCostShareAreDifferentNumbers()
        {

            await broker.Subscribe(EVSE);
            await broker.WriteSummary(EVSE, ASession());

            var summary = station.Summary!;

            Assert.Multiple(() => {

                Assert.That(summary.SelfProducedEnergyPercentage, Is.EqualTo(35));
                Assert.That(summary.SelfProducedCostPercentage,   Is.EqualTo(20));

                Assert.That(summary.SelfProducedCost / summary.SelfProducedEnergy,
                            Is.LessThan(summary.GridCost / summary.GridEnergy),
                            "The self-produced electricity did not come out cheaper per watt hour than the grid electricity.");

                Assert.That(summary.AddsUp(), Is.True);

            });

        }

        #endregion

        #region Scenario1_TheChargingStationAsksByRaisingAFlag()

        /// <summary>
        /// [EVCS-009]. The charging station is the server and a SPINE server
        /// answers rather than requests, so "the EVSE requests the charging
        /// session summary" is `updateRequired` on the bill description. Same
        /// mechanism as the coordinated EV charging, same reason.
        /// </summary>
        [Test]
        public async Task Scenario1_TheChargingStationAsksByRaisingAFlag()
        {

            await broker.Subscribe(EVSE);

            var asked = 0;
            broker.OnSummaryRequested += (_, _) => asked++;

            Assert.That(broker.SummaryRequestedBy(EVSE), Is.False);

            await station.RequestSummary();

            Assert.Multiple(() => {
                Assert.That(station.SummaryRequested,        Is.True);
                Assert.That(broker.SummaryRequestedBy(EVSE), Is.True,
                            "The energy broker was not told that the charging station wants a summary.");
                Assert.That(asked,                           Is.EqualTo(1));
            });

            var told = 0;
            station.OnSummaryWritten += (_, _) => told++;

            await broker.WriteSummary(EVSE, ASession());

            Assert.Multiple(() => {
                Assert.That(station.SummaryRequested,        Is.False,
                            "The update request stayed up after the summary arrived.");
                Assert.That(broker.SummaryRequestedBy(EVSE), Is.False);
                Assert.That(told,                            Is.EqualTo(1));
            });

        }

        #endregion

        #region Scenario1_ThePositionCountIsConstrainedToTwo()

        /// <summary>
        /// Table 7: `positionCountMax` is two, because a charging summary has
        /// exactly two positions - the grid share and the self-produced share.
        /// A broker which sends a third is describing a category this use case
        /// does not have.
        /// </summary>
        [Test]
        public async Task Scenario1_ThePositionCountIsConstrainedToTwo()
        {

            await broker.Subscribe(EVSE);

            var constraints = station.Bill.
                                  DataCopy<BillConstraintsListDataType>(EVChargingSummary.BillConstraintsListData)!.
                                  BillConstraintsData!.First();

            Assert.That(constraints.PositionCountMax, Is.EqualTo(2));

            var billId   = broker.BillIdOf(EVSE)!.Value;

            var response = await broker.BillOf(EVSE).WriteData(
                                     EVChargingSummary.BillListData,
                                     new BillListDataType {
                                         BillData = [
                                             new BillDataType {
                                                 BillId    = billId,
                                                 BillType  = BillTypeType.ChargingSummary,
                                                 Position  = [
                                                     new BillPositionType { PositionId = 1, PositionType = BillPositionTypeType.GridElectricEnergy },
                                                     new BillPositionType { PositionId = 2, PositionType = BillPositionTypeType.SelfProducedElectricEnergy },
                                                     new BillPositionType { PositionId = 3, PositionType = BillPositionTypeType.GridElectricEnergy }
                                                 ]
                                             }
                                         ]
                                     });

            Assert.Multiple(() => {
                Assert.That(response.IsError,             Is.True,
                            "A charging summary with three positions was accepted.");
                Assert.That(response.Result?.Description, Does.Contain("at most 2 positions"));
            });

        }

        #endregion

        #region Scenario1_ABillWhichIsNotAChargingSummaryIsRefused()

        /// <summary>
        /// The charging station offers one bill and says what kind it is. A bill
        /// of another kind is not a thing it asked for - and the summary "should
        /// not be used for actual billing", so a device which sent a real
        /// invoice here has misread the use case.
        /// </summary>
        [Test]
        public async Task Scenario1_ABillWhichIsNotAChargingSummaryIsRefused()
        {

            await broker.Subscribe(EVSE);

            var response = await broker.BillOf(EVSE).WriteData(
                                     EVChargingSummary.BillListData,
                                     new BillListDataType {
                                         BillData = [
                                             new BillDataType {
                                                 BillId    = broker.BillIdOf(EVSE)!.Value,
                                                 BillType  = BillTypeType.Parse("invoice")
                                             }
                                         ]
                                     });

            Assert.Multiple(() => {
                Assert.That(response.IsError,             Is.True);
                Assert.That(response.Result?.Description, Does.Contain("chargingSummary"));
                Assert.That(station.Summary,              Is.Null,
                            "The refused bill was stored anyway.");
            });

        }

        #endregion

        #region TheBrokerWritesIntoTheBillTheStationOffered()

        /// <summary>
        /// Found by looking at what the station described - writeable, and
        /// supporting a charging summary - rather than by assuming an
        /// identifier. A station may hold other bills for other reasons.
        /// </summary>
        [Test]
        public async Task TheBrokerWritesIntoTheBillTheStationOffered()
        {

            await broker.Subscribe(EVSE);

            var description = station.Bill.
                                  DataCopy<BillDescriptionListDataType>(EVChargingSummary.BillDescriptionListData)!.
                                  BillDescriptionData!.First();

            Assert.Multiple(() => {

                Assert.That(broker.BillIdOf(EVSE),             Is.EqualTo(description.BillId));
                Assert.That(description.BillWriteable,         Is.True);
                Assert.That(description.SupportedBillType,     Contains.Item(BillTypeType.ChargingSummary));

            });

        }

        #endregion

        #region NothingHereIsRequestedWithoutABinding()

        /// <summary>
        /// The one write of this use case needs one, and the description and the
        /// constraints are read and subscribed to rather than polled.
        /// </summary>
        [Test]
        public async Task NothingHereIsRequestedWithoutABinding()
        {

            await broker.Subscribe(EVSE);

            Assert.Multiple(() => {
                Assert.That(broker.BillOf(EVSE).HasBinding,      Is.True);
                Assert.That(broker.BillOf(EVSE).HasSubscription, Is.True);
            });

        }

        #endregion

        #region ASummaryStaysAvailableForAMinuteAfterTheCarIsUnplugged()

        /// <summary>
        /// [EVCS-007] and [EVCS-008]: the broker has to be able to answer while
        /// the car is connected **and** for one minute after it is unplugged,
        /// "if no EV was connected meanwhile". Easy to fail by throwing the
        /// summary away the moment the plug comes out - which is exactly when a
        /// charging station's screen asks for it.
        ///
        /// The number lives in the use case rather than in either actor, because
        /// what keeps the session data is the application; what a test bench can
        /// pin is that the number is one minute and that the summary written
        /// before the unplug is still the one being read after it.
        /// </summary>
        [Test]
        public async Task ASummaryStaysAvailableForAMinuteAfterTheCarIsUnplugged()
        {

            await broker.Subscribe(EVSE);
            await broker.WriteSummary(EVSE, ASession());

            Assert.That(EVChargingSummary.AvailableAfterUnplug, Is.EqualTo(TimeSpan.FromMinutes(1)));

            // The car is unplugged; nothing on the wire says so, and the summary
            // of the session which just ended is exactly what is wanted now.
            time.Advance(TimeSpan.FromSeconds(59));

            Assert.That(station.Summary?.Energy, Is.EqualTo(20_000),
                        "The summary of the finished session was gone before the minute was up.");

        }

        #endregion

    }

}
