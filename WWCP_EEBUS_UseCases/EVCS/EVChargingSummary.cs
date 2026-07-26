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

using cloud.charging.open.protocols.EEBUS.SPINE;
using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.EVCS
{

    /// <summary>
    /// What a charging session cost and where the electricity came from.
    ///
    /// The four numbers of [EVCS-001] to [EVCS-006], and the shape of them is
    /// the thing to get right: the **total** is absolute - watt hours and money
    /// - while the split between grid electricity and self-produced electricity
    /// is given as **percentages of that total**, not as watt hours and money
    /// again (Table 8). A reader which took the position values for absolute
    /// amounts would report a session which drew 20 kWh, 65 per cent of it from
    /// the grid, as having drawn 65 watt hours from the grid.
    ///
    /// The two percentages of a pair are also not the same number, and that is
    /// the whole point of the use case: energy from the sun is a share of the
    /// kilowatt hours and a much smaller share of the bill.
    /// </summary>
    /// <param name="Duration">How long the session lasted.</param>
    /// <param name="Energy">How much energy went into the car, in watt hours [EVCS-002].</param>
    /// <param name="Cost">What it cost, in the currency of the summary [EVCS-001].</param>
    /// <param name="Currency">Which currency that is.</param>
    /// <param name="GridEnergyPercentage">How much of the energy came from the grid, in per cent [EVCS-006].</param>
    /// <param name="GridCostPercentage">How much of the cost that was, in per cent [EVCS-005].</param>
    /// <param name="SelfProducedEnergyPercentage">How much of the energy was self-produced, in per cent [EVCS-004].</param>
    /// <param name="SelfProducedCostPercentage">How much of the cost that was, in per cent [EVCS-003].</param>
    public sealed record ChargingSummary(TimeSpan      Duration,
                                         Decimal       Energy,
                                         Decimal       Cost,
                                         CurrencyType  Currency,
                                         Decimal       GridEnergyPercentage,
                                         Decimal       GridCostPercentage,
                                         Decimal       SelfProducedEnergyPercentage,
                                         Decimal       SelfProducedCostPercentage)
    {

        #region GridEnergy / GridCost / SelfProducedEnergy / SelfProducedCost

        /// <summary>
        /// How much energy came from the grid, in watt hours - worked out from
        /// the percentage and the total, which is the only place it exists.
        /// </summary>
        public Decimal  GridEnergy          => Energy * GridEnergyPercentage         / 100;

        /// <summary>What that part cost.</summary>
        public Decimal  GridCost            => Cost   * GridCostPercentage           / 100;

        /// <summary>How much energy was self-produced, in watt hours.</summary>
        public Decimal  SelfProducedEnergy  => Energy * SelfProducedEnergyPercentage / 100;

        /// <summary>What that part cost, which is usually a good deal less.</summary>
        public Decimal  SelfProducedCost    => Cost   * SelfProducedCostPercentage   / 100;

        #endregion

        #region Adds Up

        /// <summary>
        /// Whether the two positions account for the whole session.
        ///
        /// A summary whose shares do not add up to a hundred per cent is not
        /// refused anywhere - the specification does not require it and the
        /// values "may contain estimated values" by its own admission - but it
        /// is a thing a conformance test should be able to ask about.
        /// </summary>
        /// <param name="Tolerance">How far off the two sums may be, in per cent.</param>
        public Boolean AddsUp(Decimal Tolerance = 0.5m)

            => Math.Abs(GridEnergyPercentage + SelfProducedEnergyPercentage - 100) <= Tolerance &&
               Math.Abs(GridCostPercentage   + SelfProducedCostPercentage   - 100) <= Tolerance;

        #endregion


        /// <summary>Return a text representation of this summary.</summary>
        public override String ToString()

            => $"{Energy} Wh for {Cost} {Currency} in {Duration}, " +
               $"{SelfProducedEnergyPercentage}% of it self-produced";

    }


    /// <summary>
    /// What "EV Charging Summary" is made of
    /// (EEBus_UC_TS_EVChargingSummary_V1.0.1).
    ///
    /// The smallest use case in the family - one scenario, one feature, three
    /// functions - and the only one with no Go reference implementation at all,
    /// so everything here comes from the document rather than from a stack
    /// proven in certification.
    ///
    /// It answers a question none of the other e-mobility use cases do: **what
    /// just happened**. Everything else is about what should happen next - a
    /// limit, a recommendation, a plan - and this one summarises a session which
    /// is over: how much energy, what it cost, and how much of it came from the
    /// sun rather than the grid. Section 2.1 is careful about what that is for:
    /// "The charging summary shall allow a customer to evaluate if cost and
    /// energy optimization goals are met [...] However, this information should
    /// not be used for billing purposes, as it may contain estimated values."
    ///
    /// Two things about it are worth knowing before reading the code:
    ///
    /// * **The direction is the other way round from what the name suggests.**
    ///   The energy broker knows the prices, so one would expect it to hold the
    ///   summary - but it is the **EVSE** which hosts the Bill feature and the
    ///   broker which writes into it. The EVSE is where a person is standing.
    /// * **The EVSE asks by raising a flag.** It is the server, and a SPINE
    ///   server answers rather than requests, so [EVCS-009] - "if the charging
    ///   process is finished the EVSE requests the charging session summary" -
    ///   is `updateRequired` on the bill description. The same mechanism as in
    ///   the coordinated EV charging, and for the same reason.
    /// </summary>
    public static class EVChargingSummary
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.EVChargingSummary;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 1);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        #endregion

        #region The scenario (section 2.3)

        /// <summary>Scenario 1: the energy broker sends a charging session summary to the EVSE. The only one.</summary>
        public const UInt32 ScenarioSummary = 1;

        #endregion

        #region The timings

        /// <summary>
        /// How long after the car is unplugged the energy broker still has to be
        /// able to produce the summary ([EVCS-008]).
        ///
        /// One minute, and only "if no EV was connected meanwhile" - the summary
        /// belongs to a session, and a new car starting to charge ends the old
        /// session's claim on the answer.
        /// </summary>
        public static readonly TimeSpan  AvailableAfterUnplug = TimeSpan.FromMinutes(1);

        #endregion

        #region The functions

        /// <summary>The function describing which bills the EVSE holds.</summary>
        public const String BillDescriptionListData = "billDescriptionListData";

        /// <summary>The function saying how many positions a bill may have.</summary>
        public const String BillConstraintsListData = "billConstraintsListData";

        /// <summary>The function carrying the summary itself.</summary>
        public const String BillListData            = "billListData";

        #endregion

        #region The positions (Table 7)

        /// <summary>
        /// How many positions a charging summary has: the grid share and the
        /// self-produced share, and nothing else.
        /// </summary>
        public const UInt32 PositionCount = 2;

        /// <summary>The position which is electricity taken from the grid.</summary>
        public const UInt32 GridPositionId         = 1;

        /// <summary>The position which is electricity produced on the premises.</summary>
        public const UInt32 SelfProducedPositionId = 2;

        #endregion

        #region The scenario as the framework needs it

        /// <summary>
        /// The one scenario, with the server features the partner needs.
        /// </summary>
        /// <param name="ForBroker">Whether the list is for the energy broker.</param>
        public static IEnumerable<UseCaseScenario> Scenarios(Boolean ForBroker)

            => [
                   new (ScenarioSummary,
                        ForBroker ? [ FeatureTypeType.Bill ] : [ ],
                        "Energy Broker sends Charging Session Summary to EVSE") { Mandatory = true }
               ];

        #endregion

        #region ToSPINE(BillId, Summary) / FromSPINE(Data)

        /// <summary>
        /// A charging summary as SPINE carries it (Table 8).
        /// </summary>
        /// <param name="BillId">Which bill of the EVSE it is.</param>
        /// <param name="Summary">The summary.</param>
        public static BillDataType ToSPINE(UInt32           BillId,
                                           ChargingSummary  Summary)

            => new () {

                   BillId    = BillId,
                   BillType  = BillTypeType.ChargingSummary,

                   Total     = new BillPositionType {

                                   TimePeriod  = new TimePeriodType {
                                                     StartTime  = AbsoluteOrRelativeTimeType.Parse(-Summary.Duration),
                                                     EndTime    = AbsoluteOrRelativeTimeType.Parse(TimeSpan.Zero)
                                                 },

                                   Value       = [
                                       new BillValueType {
                                           Unit   = UnitOfMeasurementType.Wh,
                                           Value  = ScaledNumberType.FromValue(Summary.Energy)
                                       }
                                   ],

                                   Cost        = [
                                       new BillCostType {
                                           CostType  = BillCostTypeType.AbsolutePrice,
                                           Currency  = Summary.Currency,
                                           Cost      = ScaledNumberType.FromValue(Summary.Cost)
                                       }
                                   ]

                               },

                   // Percentages of the total rather than amounts of their own;
                   // see ChargingSummary for why that is worth saying twice.
                   Position  = [
                       Share(GridPositionId,
                             BillPositionTypeType.GridElectricEnergy,
                             Summary.GridEnergyPercentage,
                             Summary.GridCostPercentage),

                       Share(SelfProducedPositionId,
                             BillPositionTypeType.SelfProducedElectricEnergy,
                             Summary.SelfProducedEnergyPercentage,
                             Summary.SelfProducedCostPercentage)
                   ]

               };


        /// <summary>
        /// The summary a bill carries, or null when it carries none.
        /// </summary>
        /// <param name="Data">A bill.</param>
        public static ChargingSummary? FromSPINE(BillDataType? Data)
        {

            if (Data?.Total is not BillPositionType total)
                return null;

            var energy    = total.Value?.FirstOrDefault()?.Value?.Value;
            var cost      = total.Cost?.FirstOrDefault();

            var start     = total.TimePeriod?.StartTime?.AsTimeSpan;
            var end       = total.TimePeriod?.EndTime?.AsTimeSpan;

            if (energy is not Decimal      charged ||
                cost?.Cost?.Value is not Decimal  paid ||
                cost.Currency is not CurrencyType currency)
                return null;

            var grid      = Data.Position?.FirstOrDefault(position => position.PositionType == BillPositionTypeType.GridElectricEnergy);
            var self      = Data.Position?.FirstOrDefault(position => position.PositionType == BillPositionTypeType.SelfProducedElectricEnergy);

            return new ChargingSummary(

                       // Both times are relative to now and the session has
                       // ended, so the start is the negative one.
                       Duration:                      (end ?? TimeSpan.Zero) - (start ?? TimeSpan.Zero),

                       Energy:                        charged,
                       Cost:                          paid,
                       Currency:                      currency,

                       GridEnergyPercentage:          Percentage(grid?.Value),
                       GridCostPercentage:            CostPercentage(grid?.Cost),
                       SelfProducedEnergyPercentage:  Percentage(self?.Value),
                       SelfProducedCostPercentage:    CostPercentage(self?.Cost)

                   );

        }

        #endregion

        #region (private) Share(...) / Percentage(...) / CostPercentage(...)

        /// <summary>
        /// One position of a charging summary: a share of the energy and a share
        /// of the cost.
        /// </summary>
        private static BillPositionType Share(UInt32                PositionId,
                                              BillPositionTypeType  PositionType,
                                              Decimal               EnergyPercentage,
                                              Decimal               CostPercentage)

            => new () {

                   PositionId    = PositionId,
                   PositionType  = PositionType,

                   Value         = [
                       new BillValueType {
                           ValuePercentage = ScaledNumberType.FromValue(EnergyPercentage)
                       }
                   ],

                   Cost          = [
                       new BillCostType {
                           CostPercentage = ScaledNumberType.FromValue(CostPercentage)
                       }
                   ]

               };


        private static Decimal Percentage(List<BillValueType>? Values)

            => Values?.FirstOrDefault()?.ValuePercentage?.Value ?? 0;


        private static Decimal CostPercentage(List<BillCostType>? Costs)

            => Costs?.FirstOrDefault()?.CostPercentage?.Value ?? 0;

        #endregion

    }

}
