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

namespace cloud.charging.open.protocols.EEBUS.UseCases.ChargingCurrent
{

    /// <summary>
    /// One phase of an electric vehicle, as the side writing the currents sees
    /// it.
    /// </summary>
    /// <param name="Phase">Which phase it is.</param>
    /// <param name="LimitId">The load control limit which sets its current.</param>
    /// <param name="MinimumCurrent">The lowest current the EV can charge that phase with, in ampere.</param>
    /// <param name="MaximumCurrent">The highest, in ampere.</param>
    public sealed record ChargingCurrentPhase(ElectricalConnectionPhaseNameType  Phase,
                                              UInt32                             LimitId,
                                              Decimal?                           MinimumCurrent,
                                              Decimal?                           MaximumCurrent)
    {

        /// <summary>Return a text representation of this phase.</summary>
        public override String ToString()

            => $"{Phase}: limit {LimitId}, {MinimumCurrent}..{MaximumCurrent} A";

    }


    /// <summary>
    /// Whether an electric vehicle is currently doing what the other side last
    /// told it.
    /// </summary>
    public enum ChargingCurrentTrust
    {

        /// <summary>
        /// The other side is there and healthy, so what it wrote applies.
        /// </summary>
        Following,

        /// <summary>
        /// No heartbeat for longer than the timeout ([OPEV-005], [OSCEV-005]).
        /// </summary>
        HeartbeatMissing,

        /// <summary>
        /// The other side announced a failure ([OPEV-007], [OSCEV-007]).
        /// </summary>
        PartnerFailed

    }


    /// <summary>
    /// The trust of an electric vehicle in the side writing its currents
    /// changed.
    /// </summary>
    /// <param name="Timestamp">When it happened.</param>
    /// <param name="UseCaseName">Which use case it is about.</param>
    /// <param name="From">What it was.</param>
    /// <param name="To">What it is now.</param>
    /// <param name="Reason">Which rule of the specification caused it.</param>
    public sealed record ChargingCurrentTrustChanged(DateTimeOffset        Timestamp,
                                                     String                UseCaseName,
                                                     ChargingCurrentTrust  From,
                                                     ChargingCurrentTrust  To,
                                                     String                Reason)

        : SPINEEvent(Timestamp)

    {

        /// <summary>Return a text representation of this event.</summary>
        public override String ToString()

            => $"{UseCaseName}: {From} -> {To} ({Reason})";

    }


    /// <summary>
    /// What tells one charging current use case from the other.
    ///
    /// Two use cases write a **current per phase** into the load control feature
    /// of an electric vehicle, and structurally they are one use case:
    ///
    /// * the **overload protection** (OPEV) writes an *obligation* with the
    ///   scope "overloadProtection". The EV has to keep it, because what is
    ///   behind it is a fuse.
    /// * the **optimisation of self consumption** (OSCEV) writes a
    ///   *recommendation* with the scope "selfConsumption". The EV may follow
    ///   it, and nothing breaks if it does not - what is behind it is a
    ///   photovoltaic system and a tariff.
    ///
    /// Everything else matches: three scenarios with the same numbers and the
    /// same meanings, a four second heartbeat, one limit per phase joined to the
    /// electrical connection through a measurement identifier, and a client
    /// which hosts its own diagnosis as a server feature.
    ///
    /// The one place where the difference is behaviour rather than vocabulary is
    /// what an EV does when it stops trusting the other side, and it follows
    /// from obligation versus recommendation: see
    /// <see cref="FallsBackToSafeCurrent"/>.
    /// </summary>
    /// <param name="UseCaseName">The name of the use case.</param>
    /// <param name="Version">The version this implementation follows.</param>
    /// <param name="DocumentSubRevision">The sub revision of the use case document.</param>
    /// <param name="RulePrefix">How the document numbers its rules, so that a message quotes the right one.</param>
    /// <param name="ClientActor">What the side writing the currents is called.</param>
    /// <param name="LimitCategory">Whether the EV has to keep what is written or may follow it.</param>
    /// <param name="Scope">What the limit is a limit of.</param>
    public sealed record ChargingCurrentProfile(String                   UseCaseName,
                                                UseCaseVersion           Version,
                                                String                   DocumentSubRevision,
                                                String                   RulePrefix,
                                                String                   ClientActor,
                                                LoadControlCategoryType  LimitCategory,
                                                ScopeTypeType            Scope)
    {

        #region Properties

        /// <summary>
        /// A second name the writing side goes by, where the field does not
        /// agree with the document.
        /// </summary>
        public String?   AlsoKnownAsClientActor    { get; init; }

        /// <summary>
        /// Whether an EV which has stopped trusting the other side falls back to
        /// a current of its own choosing, rather than simply stopping to follow.
        ///
        /// This is the difference between an obligation and a recommendation,
        /// and it matters. Under the overload protection the EV was keeping a
        /// fuse from tripping, and the energy guard going quiet does not make
        /// the fuse bigger - so it "should switch to a safe current setting that
        /// guarantees that no overload occurs during absence of the Energy
        /// Guard" ([OPEV-005]).
        ///
        /// Under the optimisation of self consumption it was following advice
        /// about the sun. Advice from a source which has gone quiet is simply
        /// not advice any more: the EV stops applying it and charges as it
        /// otherwise would, which is what "the EV should no longer rely on the
        /// self-consumption current information" means. Falling back to a low
        /// safe current here would be actively wrong - it would slow a charging
        /// session down because a photovoltaic forecast stopped arriving.
        /// </summary>
        public Boolean   FallsBackToSafeCurrent    { get; init; }

        /// <summary>
        /// After this long without a heartbeat, the EV stops trusting the other
        /// side. Four seconds in both use cases.
        /// </summary>
        public TimeSpan  HeartbeatTimeout          { get; init; } = TimeSpan.FromSeconds(4);

        /// <summary>
        /// How often the writing side sends its heartbeat. A little more often
        /// than the timeout, because "deviceDiagnosisHeartbeatData SHALL be sent
        /// at least each heartbeatTimeout period".
        /// </summary>
        public TimeSpan  HeartbeatInterval         { get; init; } = TimeSpan.FromSeconds(2);


        /// <summary>
        /// Every name the writing side may announce itself as.
        /// </summary>
        public IEnumerable<String> ClientActors

            => AlsoKnownAsClientActor is not null
                   ? [ ClientActor, AlsoKnownAsClientActor ]
                   : [ ClientActor ];

        #endregion

        #region The scenarios

        /// <summary>Scenario 1: the other side writes the charging current of the EV.</summary>
        public const UInt32 ScenarioCurrents      = 1;

        /// <summary>Scenario 2: the EV checks whether the other side is still there.</summary>
        public const UInt32 ScenarioAvailability  = 2;

        /// <summary>Scenario 3: the other side says that it has a problem.</summary>
        public const UInt32 ScenarioErrorState    = 3;


        /// <summary>
        /// The three scenarios, with the server features the partner needs.
        ///
        /// The split is unusually clean in both use cases: scenario 1 lives
        /// entirely at the EV, scenarios 2 and 3 entirely at the other side.
        /// </summary>
        /// <param name="ForClient">Whether the list is for the side writing the currents.</param>
        /// <param name="What">What scenario 1 is called in this use case.</param>
        public static IEnumerable<UseCaseScenario> ScenariosOf(Boolean  ForClient,
                                                                String   What)

            => ForClient

                   ? [
                         new (ScenarioCurrents,     [ FeatureTypeType.LoadControl,
                                                      FeatureTypeType.ElectricalConnection ], What)                              { Mandatory = true },
                         new (ScenarioAvailability, [ ],                                      "EV checks partner availability")  { Mandatory = true },
                         new (ScenarioErrorState,   [ ],                                      "Partner sends error state")       { Mandatory = true }
                     ]

                   : [
                         new (ScenarioCurrents,     [ ],                                      What)                              { Mandatory = true },
                         new (ScenarioAvailability, [ FeatureTypeType.DeviceDiagnosis      ], "EV checks partner availability")  { Mandatory = true },
                         new (ScenarioErrorState,   [ FeatureTypeType.DeviceDiagnosis      ], "Partner sends error state")       { Mandatory = true }
                     ];

        #endregion

        #region LimitDescription(LimitId, MeasurementId) / IsALimit(Description)

        /// <summary>
        /// The description which makes a load control limit one of **this** use
        /// case.
        ///
        /// Which phase it is about is not in here: it links to a measurement,
        /// and the electrical connection parameter description says which phase
        /// that measurement is on. That indirection is the whole reason
        /// scenario 1 needs two features - and it is also what lets one EV run
        /// both use cases on one load control feature, because a limit is told
        /// from a limit by its category and its scope rather than by its number.
        /// </summary>
        public LoadControlLimitDescriptionDataType LimitDescription(UInt32  LimitId,
                                                                    UInt32  MeasurementId)

            => new () {
                   LimitId         = LimitId,
                   LimitType       = LoadControlLimitTypeType.MaxValueLimit,
                   LimitCategory   = LimitCategory,
                   LimitDirection  = EnergyDirectionType.Consume,
                   MeasurementId   = MeasurementId,
                   Unit            = UnitOfMeasurementType.A,
                   ScopeType       = Scope
               };


        /// <summary>
        /// Whether the given limit description is one of this use case.
        ///
        /// Three of the four fields, not four: the certified Go implementation
        /// matches on the limit type, the category and the scope, and a device
        /// which leaves the direction out is a device we still want to talk to.
        /// </summary>
        /// <param name="Description">A load control limit description.</param>
        public Boolean IsALimit(LoadControlLimitDescriptionDataType? Description)

            => Description is not null                                              &&
               Description.LimitType     == LoadControlLimitTypeType.MaxValueLimit  &&
               Description.LimitCategory == LimitCategory                           &&
               Description.ScopeType     == Scope;

        #endregion


        /// <summary>Return a text representation of this profile.</summary>
        public override String ToString()

            => $"{UseCaseName} v{Version}";

    }


    /// <summary>
    /// What both charging current use cases have in common on the wire.
    /// </summary>
    public static class ChargingCurrentFunctions
    {

        /// <summary>The function carrying the limits.</summary>
        public const String LimitListData                = "loadControlLimitListData";

        /// <summary>The function describing them.</summary>
        public const String LimitDescriptionListData     = "loadControlLimitDescriptionListData";

        /// <summary>The function describing which measurement belongs to which phase.</summary>
        public const String ParameterDescriptionListData = "electricalConnectionParameterDescriptionListData";

        /// <summary>The function carrying the currents the EV can charge with.</summary>
        public const String PermittedValueSetListData    = "electricalConnectionPermittedValueSetListData";

        /// <summary>The function carrying the heartbeat.</summary>
        public const String HeartbeatData                = "deviceDiagnosisHeartbeatData";

        /// <summary>The function carrying the state of the writing side.</summary>
        public const String StateData                    = "deviceDiagnosisStateData";

    }

}
