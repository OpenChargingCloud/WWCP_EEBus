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
using cloud.charging.open.protocols.EEBUS.UseCases.ChargingCurrent;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.OSCEV
{

    /// <summary>
    /// The EV of "Optimization of Self-Consumption During EV Charging".
    ///
    /// The shared work is in <see cref="AChargingCurrentVehicle"/>; what this
    /// adds is the two rules which make this use case advice rather than an
    /// obligation.
    ///
    /// The first is what happens when the energy manager goes quiet or announces
    /// a failure: nothing dramatic. The car stops applying the recommendation
    /// and charges as it otherwise would - see <see cref="RecommendedCurrents"/>,
    /// which is then null rather than a low safe current. That is the whole
    /// difference between this use case and the overload protection, and getting
    /// it the other way round would slow a charging session down because a
    /// photovoltaic forecast stopped arriving.
    ///
    /// The second is [OSCEV-009]: a car which has no flexibility left - a full
    /// battery, most obviously - "SHALL stop to support this scenario". Not the
    /// use case, the scenario: the car still implements the optimisation of self
    /// consumption, it just cannot do anything with self-produced current until
    /// there is room again, and an energy manager reading the use case data
    /// should see that rather than keep sending advice into a full battery.
    /// </summary>
    public class OSCEVElectricVehicle : AChargingCurrentVehicle
    {

        #region Properties

        /// <summary>
        /// Whether this EV can currently do anything with self-produced current
        /// ([OSCEV-009]).
        /// </summary>
        public Boolean HasFlexibility

            => AnnouncedScenarios.Contains(SelfConsumptionOptimization.ScenarioSelfProducedCurrent);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the EV of OSCEV to an entity.
        /// </summary>
        /// <param name="Entity">The entity of the electric vehicle.</param>
        /// <param name="PhaseCount">How many phases it charges on. Three by default.</param>
        public OSCEVElectricVehicle(SPINELocalEntity  Entity,
                                    UInt32            PhaseCount   = 3)

            : base(Entity,
                   SelfConsumptionOptimization.Profile,
                   SelfConsumptionOptimization.ScenarioName,
                   PhaseCount)

        { }

        #endregion


        #region RecommendedCurrents

        /// <summary>
        /// How much self-produced current this EV is currently being advised to
        /// charge with, per phase, in ampere - or null for a phase where there
        /// is no advice to act on.
        ///
        /// Null in three different situations, and all three mean the same thing
        /// to a charging car: charge as you otherwise would. The energy manager
        /// has gone quiet ([OSCEV-005]), it has announced a failure
        /// ([OSCEV-007]), or it is there and healthy and has set the
        /// recommendation inactive because there is no self-produced current to
        /// speak of.
        /// </summary>
        public IReadOnlyList<Decimal?> RecommendedCurrents

            => Currents;

        #endregion

        #region SetFlexibility(HasFlexibility, CancellationToken = default)

        /// <summary>
        /// Say whether this EV can still do anything with self-produced current
        /// ([OSCEV-009]).
        ///
        /// A car which has reached its maximum energy capacity stops supporting
        /// scenario 1 and starts again when there is room. The use case stays
        /// announced throughout - scenarios 2 and 3 are still true, because the
        /// car is still watching whether the energy manager is there - which is
        /// exactly what "stop to support this scenario" says and what
        /// withdrawing the whole use case would get wrong.
        /// </summary>
        /// <param name="HasFlexibility">Whether there is room for self-produced current.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task SetFlexibility(Boolean            HasFlexibility,
                                   CancellationToken  CancellationToken   = default)

            => SetScenarioSupported(SelfConsumptionOptimization.ScenarioSelfProducedCurrent,
                                    HasFlexibility,
                                    CancellationToken);

        #endregion

    }

}
