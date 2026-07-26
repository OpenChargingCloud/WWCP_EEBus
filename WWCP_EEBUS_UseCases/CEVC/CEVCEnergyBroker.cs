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
    /// The energy broker of "Coordinated EV Charging" - the side which knows
    /// what energy costs.
    ///
    /// It writes one thing: the incentive table (scenario 3), a price per
    /// stretch of time. In exchange it reads the demand and the plan, and it
    /// reads the plan for a very concrete reason - a broker which sends prices
    /// and never looks at what the car decided to do with them has no way of
    /// knowing whether its prices did anything.
    ///
    /// The power limitation curve is not its business: Table 1 marks scenario 2
    /// as optional for the broker, and this implementation leaves it out rather
    /// than announce something it does not do.
    /// </summary>
    public class CEVCEnergyBroker : ACEVCCoordinator
    {

        #region Events

        /// <summary>
        /// A car asked for a fresh incentive table ([CEVC-030]).
        /// </summary>
        public event Action<CEVCEnergyBroker, SPINERemoteEntity>? OnIncentivesRequested;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the energy broker of CEVC to an entity.
        /// </summary>
        /// <param name="Entity">The entity which brokers energy.</param>
        public CEVCEnergyBroker(SPINELocalEntity Entity)

            : base(Entity,
                   UseCaseActors.EnergyBroker,
                   CoordinatedEVCharging.EnergyBrokerScenarios(),
                   [ FeatureTypeType.TimeSeries, FeatureTypeType.IncentiveTable ])

        {

            Device.Events.Subscribe<SPINEDataChanged>(Watch, SPINEEventLevel.Core);

        }

        #endregion


        #region IncentiveTableOf(Partner) / (override) Subscribe(Partner, ...)

        /// <summary>
        /// The incentive table of a car, paired with our client feature.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        public UseCaseFeature IncentiveTableOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.IncentiveTable, Entity, Partner);


        /// <summary>
        /// Read what a car publishes and ask to be told when it changes - the
        /// time series as every coordinator does, and the incentive table
        /// description as well.
        ///
        /// The description matters more here than anywhere else in this use
        /// case: it is the car saying what shape of tariff it can understand,
        /// and a broker which writes prices into a table it did not read the
        /// shape of is guessing.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public override async Task Subscribe(SPINERemoteEntity  Partner,
                                             CancellationToken  CancellationToken   = default)
        {

            await base.Subscribe(Partner, CancellationToken);

            var incentives = IncentiveTableOf(Partner);

            await incentives.Subscribe(CancellationToken);
            await incentives.Bind     (CancellationToken);

            await incentives.RequestData(CoordinatedEVCharging.IncentiveTableDescriptionData, CancellationToken: CancellationToken);
            await incentives.RequestData(CoordinatedEVCharging.IncentiveTableData,            CancellationToken: CancellationToken);

        }

        #endregion

        #region WriteIncentives(Partner, Slots, ...)

        /// <summary>
        /// Tell a car what energy will cost over time ([CEVC-028]).
        ///
        /// One incentive slot per stretch of time, each with the cost of the one
        /// tier the car described. The slots "SHALL NOT overlap in time", and
        /// the last one has to state an end - a price with no end is a promise
        /// nobody made.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        /// <param name="Slots">The prices, slot by slot.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="ArgumentException">When the slots overlap or the last one has no end.</exception>
        public async Task<SPINEResponse> WriteIncentives(SPINERemoteEntity          Partner,
                                                         IEnumerable<IncentiveSlot>  Slots,
                                                         CancellationToken           CancellationToken   = default)
        {

            var slots = Slots.OrderBy(slot => slot.Start).ToList();

            if (slots.Count == 0)
                throw new ArgumentException("An incentive table with no slots says nothing.",
                                            nameof(Slots));

            if (slots[^1].End is null)
                throw new ArgumentException("The last incentive slot \"SHALL\" state an end time.",
                                            nameof(Slots));

            for (var index = 1; index < slots.Count; index++)
                if (slots[index - 1].End is TimeSpan end && end > slots[index].Start)
                    throw new ArgumentException("The time intervals of different incentive slots \"SHALL NOT overlap in time\".",
                                                nameof(Slots));

            var tariffId = TariffIdOf(Partner)
                               ?? throw new InvalidOperationException($"{Partner.Address} describes no writeable tariff.");

            return await IncentiveTableOf(Partner).WriteData(
                             CoordinatedEVCharging.IncentiveTableData,
                             new IncentiveTableDataType {
                                 IncentiveTable = [
                                     new IncentiveTableType {

                                         Tariff = new TariffDataType { TariffId = tariffId },

                                         IncentiveSlot = [.. slots.Select(slot =>
                                             new IncentiveTableIncentiveSlotType {

                                                 // Relative rather than absolute: "Only relative
                                                 // times SHALL be used", because neither side can
                                                 // rely on the other's clock.
                                                 TimeInterval = new TimeTableDataType {
                                                                    StartTime  = new AbsoluteOrRecurringTimeType {
                                                                                     Relative = DurationType.Parse(slot.Start)
                                                                                 },
                                                                    EndTime    = slot.End.HasValue
                                                                                     ? new AbsoluteOrRecurringTimeType {
                                                                                           Relative = DurationType.Parse(slot.End.Value)
                                                                                       }
                                                                                     : null
                                                                },

                                                 Tier = [
                                                     new IncentiveTableTierType {
                                                         Tier       = new TierDataType { TierId = 1 },
                                                         Incentive  = [
                                                             new IncentiveDataType {
                                                                 IncentiveId  = 1,
                                                                 ValueType    = IncentiveValueTypeType.Value,
                                                                 Value        = ScaledNumberType.FromValue(slot.Cost)
                                                             }
                                                         ]
                                                     }
                                                 ]

                                             })]

                                     }
                                 ]
                             },

                             // In full, not partially. `incentiveTable` has no
                             // primary key of its own, and SPINE 1.3.0, 5.3.4.1
                             // allows only the exchange of a complete list for
                             // entries which cannot be addressed - so a partial
                             // write of it is refused, correctly.
                             Partial: false,
                             CancellationToken: CancellationToken
                         );

        }

        #endregion

        #region TariffIdOf(Partner) / IncentivesRequestedBy(Partner)

        /// <summary>
        /// Which tariff a car offers a broker to fill in.
        ///
        /// The writeable one: a car may describe a tariff it maintains itself,
        /// and writing prices into that would be answering a question nobody
        /// asked.
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        public UInt32? TariffIdOf(SPINERemoteEntity Partner)

            => IncentiveTableOf(Partner).
                   Data<IncentiveTableDescriptionDataType>(CoordinatedEVCharging.IncentiveTableDescriptionData)?.
                   IncentiveTableDescription?.
                   FirstOrDefault(table => table.TariffDescription?.TariffWriteable == true)?.
                   TariffDescription?.TariffId;


        /// <summary>
        /// Whether a car is currently asking for a new incentive table
        /// ([CEVC-030]).
        /// </summary>
        /// <param name="Partner">An entity of an EV.</param>
        public Boolean IncentivesRequestedBy(SPINERemoteEntity Partner)

            => IncentiveTableOf(Partner).
                   Data<IncentiveTableDescriptionDataType>(CoordinatedEVCharging.IncentiveTableDescriptionData)?.
                   IncentiveTableDescription?.
                   Any(table => table.TariffDescription?.UpdateRequired == true) == true;

        #endregion


        #region (private) Watch(Event)

        /// <summary>
        /// A car changed its incentive table description. If the update flag
        /// went up, it wants new prices.
        /// </summary>
        private void Watch(SPINEDataChanged Event)
        {

            if (Event.Change.Function != CoordinatedEVCharging.IncentiveTableDescriptionData ||
                Event.Change.RemoteFeature.Role != RoleType.Server)
                return;

            var partner = Event.Change.RemoteFeature.Entity;

            if (PartnerFor(partner) is null)
                return;

            if ((Event.Change.Data as IncentiveTableDescriptionDataType)?.
                    IncentiveTableDescription?.
                    Any(table => table.TariffDescription?.UpdateRequired == true) == true)
                OnIncentivesRequested?.Invoke(this, partner);

        }

        #endregion

    }

}
