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
    /// The energy guard of "Coordinated EV Charging" - the side which knows what
    /// the connection can give.
    ///
    /// It writes one thing: the maximum power limitation curve (scenario 2),
    /// which is the only writeable time series a car offers. In exchange it
    /// reads the demand and the plan, and that exchange is the use case: the car
    /// says what it needs, the guard says what is available when, and the car
    /// answers with what it will therefore do.
    ///
    /// The incentive table is not its business - Table 1 marks scenario 3 as "-"
    /// for the energy guard, which is the specification keeping the question
    /// "how much power is there" apart from the question "what does it cost".
    /// </summary>
    public class CEVCEnergyGuard : ACEVCCoordinator
    {

        #region Events

        /// <summary>
        /// A car asked for a fresh power limitation curve ([CEVC-015]).
        /// </summary>
        public event Action<CEVCEnergyGuard, SPINERemoteEntity>? OnPowerLimitsRequested;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the energy guard of CEVC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which guards the connection.</param>
        public CEVCEnergyGuard(SPINELocalEntity Entity)

            : base(Entity,
                   UseCaseActors.EnergyGuard,
                   CoordinatedEVCharging.EnergyGuardScenarios(),
                   [ FeatureTypeType.TimeSeries ])

        {

            Device.Events.Subscribe<SPINEDataChanged>(Watch, SPINEEventLevel.Core);

        }

        #endregion


        #region WritePowerLimits(Partner, Slots, ...)

        /// <summary>
        /// Tell a car how much power it may draw over time ([CEVC-007]).
        ///
        /// A curve rather than a number, and that is the difference from the
        /// overload protection: this says what will be available in twenty
        /// minutes as well as what is available now, so the car can decide when
        /// to charge rather than only how hard.
        ///
        /// The durations are relative to now and each has to be greater than
        /// zero.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Slots">The curve, slot by slot, in watts.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">When the car offers no writeable constraints curve.</exception>
        public async Task<SPINEResponse> WritePowerLimits(SPINERemoteEntity       Partner,
                                                          IEnumerable<PowerSlot>  Slots,
                                                          CancellationToken       CancellationToken   = default)
        {

            var slots = Slots.ToList();

            if (slots.Any(slot => slot.Duration <= TimeSpan.Zero))
                throw new ArgumentException("A time series slot \"SHALL only contain values greater than zero seconds\".",
                                            nameof(Slots));

            var description = DescriptionOf(Partner, CoordinatedEVCharging.Constraints)
                                  ?? throw new InvalidOperationException($"{Partner.Address} offers no constraints time series.");

            if (description.TimeSeriesWriteable != true)
                throw new InvalidOperationException($"The constraints time series of {Partner.Address} is not writeable.");

            return await TimeSeriesOf(Partner).WriteData(
                             CoordinatedEVCharging.TimeSeriesListData,
                             new TimeSeriesListDataType {
                                 TimeSeriesData = [
                                     new TimeSeriesDataType {
                                         TimeSeriesId    = description.TimeSeriesId,
                                         TimePeriod      = new TimePeriodType {
                                                               StartTime = AbsoluteOrRelativeTimeType.Parse(TimeSpan.Zero)
                                                           },
                                         TimeSeriesSlot  = CoordinatedEVCharging.Slots(slots)
                                     }
                                 ]
                             },
                             Partial: true,
                             CancellationToken: CancellationToken
                         );

        }

        #endregion

        #region PowerLimitsRequestedBy(Partner)

        /// <summary>
        /// Whether a car is currently asking for a new power limitation curve
        /// ([CEVC-015]).
        ///
        /// The car cannot ask - it is the server - so it raises this flag on the
        /// description instead, and an energy guard which does not look at it
        /// will simply never be asked for anything.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        public Boolean PowerLimitsRequestedBy(SPINERemoteEntity Partner)

            => DescriptionOf(Partner, CoordinatedEVCharging.Constraints)?.UpdateRequired == true;

        #endregion


        #region (private) Watch(Event)

        /// <summary>
        /// A car changed its time series descriptions. If the update flag went
        /// up, it wants a new curve.
        /// </summary>
        private void Watch(SPINEDataChanged Event)
        {

            if (Event.Change.Function != CoordinatedEVCharging.TimeSeriesDescriptionListData ||
                Event.Change.RemoteFeature.Role != RoleType.Server)
                return;

            var partner = Event.Change.RemoteFeature.Entity;

            if (PartnerFor(partner) is null)
                return;

            if ((Event.Change.Data as TimeSeriesDescriptionListDataType)?.
                    TimeSeriesDescriptionData?.
                    Any(description => description.TimeSeriesType == CoordinatedEVCharging.Constraints &&
                                        description.UpdateRequired == true) == true)
                OnPowerLimitsRequested?.Invoke(this, partner);

        }

        #endregion

    }

}
