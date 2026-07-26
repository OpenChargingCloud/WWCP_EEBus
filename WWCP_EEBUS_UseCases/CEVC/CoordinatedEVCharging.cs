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

namespace cloud.charging.open.protocols.EEBUS.UseCases.CEVC
{

    /// <summary>
    /// How much energy a car wants, and by when
    /// (CEVC scenario 1, [CEVC-001] to [CEVC-005]).
    ///
    /// Three numbers rather than one, and the difference between them is the
    /// whole reason coordinated charging exists. The specification's own
    /// example: the minimum could be "enough to reach the next hospital", the
    /// optimum "enough to reach the workplace", and the maximum is whatever is
    /// left in the battery - which matters most when there is free surplus from
    /// a photovoltaic system, because then the car may as well take all of it.
    ///
    /// Every field is optional and at least one has to be there: "The Scenario
    /// itself and all communicated values are optional and can depend on the
    /// implementation of the EV as well as the driver settings."
    /// </summary>
    /// <param name="Departure">How long until the car is needed. The window the energy has to arrive in.</param>
    /// <param name="MinimumEnergy">The energy the driver cannot do without, in watt hours [CEVC-003].</param>
    /// <param name="OptimumEnergy">The energy the driver would like, in watt hours [CEVC-004].</param>
    /// <param name="MaximumEnergy">How much the battery could still take, in watt hours [CEVC-005].</param>
    /// <param name="Arrival">How long until charging may begin, where that is not now [CEVC-001].</param>
    public sealed record ChargingDemand(TimeSpan?  Departure       = null,
                                        Decimal?   MinimumEnergy   = null,
                                        Decimal?   OptimumEnergy   = null,
                                        Decimal?   MaximumEnergy   = null,
                                        TimeSpan?  Arrival         = null)
    {

        /// <summary>
        /// Whether this demand says anything at all. A demand with no value in
        /// it is not a demand.
        /// </summary>
        public Boolean IsEmpty

            => !MinimumEnergy.HasValue &&
               !OptimumEnergy.HasValue &&
               !MaximumEnergy.HasValue &&
               !Departure.HasValue;


        /// <summary>Return a text representation of this demand.</summary>
        public override String ToString()

            => $"{MinimumEnergy}/{OptimumEnergy}/{MaximumEnergy} Wh" +
               $"{(Departure.HasValue ? $" within {Departure}" : "")}";

    }


    /// <summary>
    /// One stretch of time with a power value attached to it.
    ///
    /// The shape of both curves this use case carries: the maximum power the
    /// energy guard allows (scenario 2) and the power the car plans to draw
    /// (scenario 4). Times are relative - "SHALL be interpreted relative to
    /// 'now'" - and a positive power is consumption, a negative one production.
    /// </summary>
    /// <param name="Duration">How long this slot lasts. Always greater than zero.</param>
    /// <param name="Value">The power in watts, where the slot states one.</param>
    /// <param name="MinValue">The lowest power in watts, where the slot states one.</param>
    /// <param name="MaxValue">The highest power in watts, where the slot states one.</param>
    public sealed record PowerSlot(TimeSpan  Duration,
                                   Decimal?  Value      = null,
                                   Decimal?  MinValue   = null,
                                   Decimal?  MaxValue   = null)
    {

        /// <summary>Return a text representation of this slot.</summary>
        public override String ToString()

            => $"{Duration}: {Value?.ToString() ?? $"{MinValue}..{MaxValue}"} W";

    }


    /// <summary>
    /// One stretch of time with a price attached to it (CEVC scenario 3).
    /// </summary>
    /// <param name="Start">When the slot begins, relative to now.</param>
    /// <param name="End">When it ends, relative to now. Required for the last slot and wherever a gap follows.</param>
    /// <param name="Cost">What energy costs during it, in the currency of the table.</param>
    public sealed record IncentiveSlot(TimeSpan   Start,
                                       TimeSpan?  End,
                                       Decimal    Cost)
    {

        /// <summary>Return a text representation of this slot.</summary>
        public override String ToString()

            => $"{Start}..{End?.ToString() ?? "?"}: {Cost}";

    }


    /// <summary>
    /// What "Coordinated EV Charging" is made of
    /// (EEBus_UC_TS_CoordinatedEVCharging_V1.0.1).
    ///
    /// The largest use case in the e-mobility family and the only one with
    /// **three** actors. A car says how much energy it needs and by when; an
    /// energy guard says how much power the connection can give it over time; an
    /// energy broker says what that power will cost over time; and the car
    /// answers with a plan - a curve of what it intends to draw when. Everybody
    /// then knows what will happen before it happens, which is the difference
    /// between coordinated charging and the curtailment use cases, where the car
    /// only ever learns what it may do *right now*.
    ///
    /// Two data models carry all of it, and they are the most demanding in
    /// SPINE:
    ///
    /// * **TimeSeries** for the three curves. One feature holds all of them and
    ///   the `timeSeriesType` tells them apart: `singleDemand` is the energy the
    ///   car wants, `constraints` is what the energy guard allows, `plan` is
    ///   what the car intends. Only the constraints series is writeable, which
    ///   is the specification saying who owns which curve.
    /// * **IncentiveTable** for the prices. A tariff with tiers, each tier with
    ///   power boundaries and a cost incentive which varies over time.
    ///
    /// The one mechanism worth understanding before reading any of it is
    /// **updateRequired** ([CEVC-015], [CEVC-030]). A server which needs a
    /// client to write something new sets the flag on the description and clears
    /// it when the write arrives. It is how a car asks for a fresh power limit
    /// curve or a fresh incentive table without having a way to call anybody:
    /// the car is the server here, and a server cannot make a request. So it
    /// raises a flag on data the client is already subscribed to.
    /// </summary>
    public static class CoordinatedEVCharging
    {

        #region The use case

        /// <summary>The name of the use case.</summary>
        public const  String          Name                  = UseCaseNames.CoordinatedEVCharging;

        /// <summary>The version this implementation follows.</summary>
        public static UseCaseVersion  Version               { get; } = new (1, 0, 1);

        /// <summary>The sub revision of the use case document.</summary>
        public const  String          DocumentSubRevision   = "release";

        #endregion

        #region The scenarios (section 2.4)

        /// <summary>Scenario 1: the EV sends its charging energy demand.</summary>
        public const UInt32 ScenarioEnergyDemand    = 1;

        /// <summary>Scenario 2: the energy guard sends the maximum power limitation curve.</summary>
        public const UInt32 ScenarioPowerLimits     = 2;

        /// <summary>Scenario 3: the energy broker sends the incentive table.</summary>
        public const UInt32 ScenarioIncentiveTable  = 3;

        /// <summary>Scenario 4: the EV sends its charging plan curve.</summary>
        public const UInt32 ScenarioChargingPlan    = 4;

        /// <summary>Scenario 5: the energy guard heartbeat.</summary>
        public const UInt32 ScenarioGuardHeartbeat  = 5;

        /// <summary>Scenario 6: the energy broker heartbeat.</summary>
        public const UInt32 ScenarioBrokerHeartbeat = 6;

        /// <summary>Scenario 7: the energy guard error state.</summary>
        public const UInt32 ScenarioGuardState      = 7;

        /// <summary>Scenario 8: the energy broker error state.</summary>
        public const UInt32 ScenarioBrokerState     = 8;

        #endregion

        #region The functions

        /// <summary>The function describing the three curves.</summary>
        public const String TimeSeriesDescriptionListData = "timeSeriesDescriptionListData";

        /// <summary>The function saying how large a curve may get.</summary>
        public const String TimeSeriesConstraintsListData = "timeSeriesConstraintsListData";

        /// <summary>The function carrying them.</summary>
        public const String TimeSeriesListData            = "timeSeriesListData";

        /// <summary>The function describing the tariff and its tiers.</summary>
        public const String IncentiveTableDescriptionData = "incentiveTableDescriptionData";

        /// <summary>The function saying how large an incentive table may get.</summary>
        public const String IncentiveTableConstraintsData = "incentiveTableConstraintsData";

        /// <summary>The function carrying the prices.</summary>
        public const String IncentiveTableData            = "incentiveTableData";

        /// <summary>The function carrying a heartbeat.</summary>
        public const String HeartbeatData                 = "deviceDiagnosisHeartbeatData";

        /// <summary>The function carrying an operating state.</summary>
        public const String StateData                     = "deviceDiagnosisStateData";

        #endregion

        #region The time series

        /// <summary>
        /// The energy the car wants - scenario 1, written by nobody but the car
        /// itself, in watt hours.
        /// </summary>
        public static TimeSeriesTypeType Demand      { get; } = TimeSeriesTypeType.SingleDemand;

        /// <summary>
        /// What the energy guard allows - scenario 2, the one writeable series,
        /// in watts.
        /// </summary>
        public static TimeSeriesTypeType Constraints { get; } = TimeSeriesTypeType.Constraints;

        /// <summary>
        /// What the car intends to draw - scenario 4, written by nobody but the
        /// car, in watts.
        /// </summary>
        public static TimeSeriesTypeType Plan        { get; } = TimeSeriesTypeType.Plan;

        #endregion

        #region The scenarios as the framework needs them

        /// <summary>
        /// The scenarios of the EV, which is the server of all four data
        /// scenarios and the client of the four diagnosis ones.
        /// </summary>
        /// <param name="WithBroker">Whether this car talks to an energy broker as well as to an energy guard.</param>
        public static IEnumerable<UseCaseScenario> ElectricVehicleScenarios(Boolean WithBroker)
        {

            var scenarios = new List<UseCaseScenario> {
                                new (ScenarioEnergyDemand,   [ ], "EV sends charging energy demand")          { Mandatory = true },
                                new (ScenarioPowerLimits,    [ ], "Energy Guard sends maximum power limitation curve") { Mandatory = true },
                                new (ScenarioChargingPlan,   [ ], "EV sends charging plan curve")             { Mandatory = true },
                                new (ScenarioGuardHeartbeat, [ FeatureTypeType.DeviceDiagnosis ], "Energy Guard heartbeat")   { Mandatory = true },
                                new (ScenarioGuardState,     [ FeatureTypeType.DeviceDiagnosis ], "Energy Guard error state") { Mandatory = true }
                            };

            if (WithBroker)
            {
                scenarios.Add(new (ScenarioIncentiveTable,  [ ], "Energy Broker sends incentive table")  { Mandatory = true });
                scenarios.Add(new (ScenarioBrokerHeartbeat, [ FeatureTypeType.DeviceDiagnosis ], "Energy Broker heartbeat")   { Mandatory = true });
                scenarios.Add(new (ScenarioBrokerState,     [ FeatureTypeType.DeviceDiagnosis ], "Energy Broker error state") { Mandatory = true });
            }

            return scenarios;

        }


        /// <summary>
        /// The scenarios of the energy guard.
        ///
        /// It reads the demand and the plan and writes the power limitation
        /// curve, so it needs the car's TimeSeries feature - and the incentive
        /// table is not its business at all (Table 1 marks scenario 3 as "-" for
        /// the energy guard).
        /// </summary>
        public static IEnumerable<UseCaseScenario> EnergyGuardScenarios()

            => [
                   new (ScenarioEnergyDemand,   [ FeatureTypeType.TimeSeries ], "EV sends charging energy demand")                     { Mandatory = true },
                   new (ScenarioPowerLimits,    [ FeatureTypeType.TimeSeries ], "Energy Guard sends maximum power limitation curve")   { Mandatory = true },
                   new (ScenarioChargingPlan,   [ FeatureTypeType.TimeSeries ], "EV sends charging plan curve")                        { Mandatory = true },
                   new (ScenarioGuardHeartbeat, [ ],                            "Energy Guard heartbeat")                              { Mandatory = true },
                   new (ScenarioGuardState,     [ ],                            "Energy Guard error state")                            { Mandatory = true }
               ];


        /// <summary>
        /// The scenarios of the energy broker.
        ///
        /// It reads the demand and the plan and writes the incentive table, so
        /// it needs both of the car's features - and the power limitation curve
        /// is not its business (Table 1 marks scenario 2 as optional for the
        /// broker; we leave it out rather than announce something we do not do).
        /// </summary>
        public static IEnumerable<UseCaseScenario> EnergyBrokerScenarios()

            => [
                   new (ScenarioEnergyDemand,    [ FeatureTypeType.TimeSeries ],     "EV sends charging energy demand")      { Mandatory = true },
                   new (ScenarioIncentiveTable,  [ FeatureTypeType.IncentiveTable ], "Energy Broker sends incentive table")  { Mandatory = true },
                   new (ScenarioChargingPlan,    [ FeatureTypeType.TimeSeries ],     "EV sends charging plan curve")         { Mandatory = true },
                   new (ScenarioBrokerHeartbeat, [ ],                                "Energy Broker heartbeat")              { Mandatory = true },
                   new (ScenarioBrokerState,     [ ],                                "Energy Broker error state")            { Mandatory = true }
               ];

        #endregion

        #region Slots(Data) / Slots(Slots)

        /// <summary>
        /// The slots of a time series, as durations and powers.
        ///
        /// A slot without a duration is the last one and runs until further
        /// notice; the specification allows that only where the series states an
        /// end time, so a reader which needs a duration and finds none has
        /// reached the end of what it was told.
        /// </summary>
        /// <param name="Data">A time series.</param>
        public static IReadOnlyList<PowerSlot> Slots(TimeSeriesDataType? Data)

            => [.. (Data?.TimeSeriesSlot ?? []).
                       OrderBy(slot => slot.TimeSeriesSlotId ?? 0).
                       Where  (slot => slot.Duration?.AsTimeSpan is not null).
                       Select (slot => new PowerSlot(slot.Duration!.Value.AsTimeSpan!.Value,
                                                      slot.Value?.Value,
                                                      slot.MinValue?.Value,
                                                      slot.MaxValue?.Value))];


        /// <summary>
        /// Those slots as SPINE carries them.
        /// </summary>
        /// <param name="Slots">The slots.</param>
        public static List<TimeSeriesSlotType> Slots(IEnumerable<PowerSlot> Slots)

            => [.. Slots.Select((slot, index) =>
                       new TimeSeriesSlotType {
                           TimeSeriesSlotId  = (UInt32) index + 1,
                           Duration          = DurationType.Parse(slot.Duration),
                           Value             = slot.Value    is Decimal value ? ScaledNumberType.FromValue(value)    : null,
                           MinValue          = slot.MinValue is Decimal min   ? ScaledNumberType.FromValue(min)      : null,
                           MaxValue          = slot.MaxValue is Decimal max   ? ScaledNumberType.FromValue(max)      : null
                       })];

        #endregion

    }

}
