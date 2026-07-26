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

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.Monitoring
{

    /// <summary>
    /// One thing a monitored device measures.
    ///
    /// Every measurement of every monitoring use case is the same four facts -
    /// what kind of quantity, in which unit, at which scope, and on which phase
    /// - and the scope is what tells "the total active power" from "the active
    /// power of phase B". The phase is not in the measurement description at
    /// all: it comes from the electrical connection parameter description,
    /// joined by the measurement identifier.
    /// </summary>
    /// <param name="Scenario">Which scenario of its use case it belongs to.</param>
    /// <param name="Type">Which kind of quantity it is.</param>
    /// <param name="Unit">In which unit it is measured.</param>
    /// <param name="Scope">What exactly it is a measurement of.</param>
    /// <param name="Phase">Which phase it is on, where it is on one.</param>
    public sealed record MonitoringQuantity(UInt32                              Scenario,
                                            MeasurementTypeType                 Type,
                                            UnitOfMeasurementType               Unit,
                                            ScopeTypeType                       Scope,
                                            ElectricalConnectionPhaseNameType?  Phase   = null)
    {

        /// <summary>Return a text representation of this quantity.</summary>
        public override String ToString()

            => $"{Scope}{(Phase is not null ? $" ({Phase})" : "")} in {Unit}";

    }


    /// <summary>
    /// One measured value of a monitored device, with everything needed to know
    /// what it is.
    /// </summary>
    /// <param name="Quantity">What was measured.</param>
    /// <param name="Value">Its value, in the unit of the quantity.</param>
    /// <param name="Timestamp">When it was measured, where the device said so.</param>
    public sealed record MonitoringReading(MonitoringQuantity  Quantity,
                                           Decimal             Value,
                                           DateTimeOffset?     Timestamp)
    {

        /// <summary>Return a text representation of this reading.</summary>
        public override String ToString()

            => $"{Quantity}: {Value}";

    }


    /// <summary>
    /// One scenario of a monitoring use case.
    /// </summary>
    /// <param name="Number">Its number in the use case document.</param>
    /// <param name="Name">What it is called there.</param>
    /// <param name="Mandatory">Whether a device implementing the use case has to support it.</param>
    /// <param name="ServerFeatures">Which server features the client side needs for it.</param>
    public sealed record MonitoringScenario(UInt32                        Number,
                                            String                        Name,
                                            Boolean                       Mandatory,
                                            IEnumerable<FeatureTypeType>  ServerFeatures)
    {

        /// <summary>Return a text representation of this scenario.</summary>
        public override String ToString()

            => $"{Number}: {Name}";

    }


    /// <summary>
    /// What tells one monitoring use case from another.
    ///
    /// The monitoring use cases are the same use case pointed at different
    /// things. A monitored device publishes measurements; a monitoring appliance
    /// reads the descriptions, joins them by measurement identifier and
    /// subscribes. Nothing is written, nothing has a state, nothing falls back.
    /// What differs between them is the name, who the actors are, which
    /// scenarios exist and which scopes belong to which scenario - which is what
    /// this record carries.
    /// </summary>
    /// <param name="UseCaseName">The name of the use case.</param>
    /// <param name="Version">The version this implementation follows.</param>
    /// <param name="DocumentSubRevision">The sub revision of the use case document.</param>
    /// <param name="ServerActor">What the side which is measured is called.</param>
    /// <param name="ClientActor">What the side which watches is called.</param>
    /// <param name="ClientEntityTypes">Which entity types the watching side may be, where the document says.</param>
    /// <param name="Scenarios">The scenarios of the use case.</param>
    /// <param name="ScenarioOfScope">Which scenario a measured scope belongs to.</param>
    public sealed record MonitoringProfile(String                                      UseCaseName,
                                           UseCaseVersion                              Version,
                                           String                                      DocumentSubRevision,
                                           String                                      ServerActor,
                                           String                                      ClientActor,
                                           IEnumerable<EntityTypeType>?                ClientEntityTypes,
                                           IReadOnlyList<MonitoringScenario>           Scenarios,
                                           IReadOnlyDictionary<ScopeTypeType, UInt32>  ScenarioOfScope)
    {

        #region MandatoryScenarios

        /// <summary>
        /// The scenarios which every device implementing this use case supports.
        /// </summary>
        public IEnumerable<UInt32> MandatoryScenarios

            => Scenarios.Where (scenario => scenario.Mandatory).
                         Select(scenario => scenario.Number);

        #endregion

        #region ScenarioOf(Scope)

        /// <summary>
        /// Which scenario of this use case a measured scope belongs to, or zero
        /// when the use case does not carry that scope at all.
        /// </summary>
        /// <param name="Scope">What a measurement is a measurement of.</param>
        public UInt32 ScenarioOf(ScopeTypeType Scope)

            => ScenarioOfScope.GetValueOrDefault(Scope, 0u);

        #endregion

        #region SupportedScenarios(ForClient, Scenarios = null)

        /// <summary>
        /// The scenarios of this use case which the given side supports, as the
        /// framework needs them.
        ///
        /// The mandatory ones are always there, whatever the caller asks for: a
        /// device which does not support them is not implementing this use case.
        /// The two sides differ only in direction - the appliance looks for the
        /// server features at the monitored device, and the monitored device
        /// needs nothing at all from the appliance.
        /// </summary>
        /// <param name="ForClient">Whether the list is for the watching side.</param>
        /// <param name="Scenarios">Which optional scenarios are supported.</param>
        public IEnumerable<UseCaseScenario> SupportedScenarios(Boolean               ForClient,
                                                               IEnumerable<UInt32>?  Scenarios   = null)
        {

            var supported = new SortedSet<UInt32>(Scenarios ?? []);

            foreach (var mandatory in MandatoryScenarios)
                supported.Add(mandatory);

            return [.. this.Scenarios.
                          Where (scenario => supported.Contains(scenario.Number)).
                          Select(scenario => new UseCaseScenario(scenario.Number,
                                                                 ForClient ? scenario.ServerFeatures : [],
                                                                 scenario.Name))];

        }

        #endregion


        /// <summary>Return a text representation of this profile.</summary>
        public override String ToString()

            => $"{UseCaseName} v{Version}";

    }


    /// <summary>
    /// What every monitoring use case has in common on the wire.
    /// </summary>
    public static class MonitoringFunctions
    {

        /// <summary>The function carrying the measured values.</summary>
        public const String MeasurementListData             = "measurementListData";

        /// <summary>The function describing what they are.</summary>
        public const String MeasurementDescriptionListData  = "measurementDescriptionListData";

        /// <summary>The function saying which measurement is on which phase.</summary>
        public const String ParameterDescriptionListData    = "electricalConnectionParameterDescriptionListData";

        /// <summary>The function describing the electrical connection itself.</summary>
        public const String ElectricalDescriptionListData   = "electricalConnectionDescriptionListData";

    }

}
