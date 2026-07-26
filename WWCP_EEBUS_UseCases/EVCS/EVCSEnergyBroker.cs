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
    /// The energy broker of "EV Charging Summary" - the side which knows what
    /// the electricity cost and where it came from.
    ///
    /// It is the client, and its whole job is to answer one question: what did
    /// this session cost, and how much of it was the sun. Section 2.1 is
    /// explicit about what the answer is for and what it is not for - "should
    /// not be used for billing purposes, as it may contain estimated values" -
    /// which is why the positions are percentages rather than metered amounts.
    ///
    /// The two rules about *when* it has to be able to answer are the ones a
    /// conformance test will ask about, and they are easy to fail by keeping the
    /// summary only while a car is plugged in: the answer has to be available
    /// while the car is connected ([EVCS-007]) **and for one minute after it is
    /// unplugged**, as long as no other car has started charging in the meantime
    /// ([EVCS-008]). A charging station whose screen says "session finished" is
    /// asking exactly then.
    /// </summary>
    public class EVCSEnergyBroker : AUseCase
    {

        #region Events

        /// <summary>
        /// A charging station asked for a summary ([EVCS-009]).
        /// </summary>
        public event Action<EVCSEnergyBroker, SPINERemoteEntity>? OnSummaryRequested;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the energy broker of EVCS to an entity.
        /// </summary>
        /// <param name="Entity">The entity which brokers energy.</param>
        public EVCSEnergyBroker(SPINELocalEntity Entity)

            : base(Entity,
                   UseCaseActors.EnergyBroker,
                   EVChargingSummary.Name,
                   EVChargingSummary.Version,
                   EVChargingSummary.Scenarios(ForBroker: true),
                   [ UseCaseActors.EVSE ],
                   [ EntityTypeType.EVSE ],
                   EVChargingSummary.DocumentSubRevision)

        {

            if (Entity.Feature(FeatureTypeType.Bill, RoleType.Client) is null)
                Entity.AddFeature(FeatureTypeType.Bill, RoleType.Client);

            Device.Events.Subscribe<SPINEDataChanged>(Watch, SPINEEventLevel.Core);

        }

        #endregion


        #region BillOf(Partner) / Subscribe(Partner, ...)

        /// <summary>
        /// The bill of a charging station, paired with our client feature.
        /// </summary>
        /// <param name="Partner">An entity of a charging station.</param>
        public UseCaseFeature BillOf(SPINERemoteEntity Partner)

            => new (FeatureTypeType.Bill, Entity, Partner);


        /// <summary>
        /// Read what a charging station offers and ask to be told when it
        /// changes.
        ///
        /// The description first, because it is where the update request will
        /// appear and the whole use case is driven by it, and a binding, because
        /// everything this side does is a write.
        /// </summary>
        /// <param name="Partner">An entity of a charging station.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task Subscribe(SPINERemoteEntity  Partner,
                                    CancellationToken  CancellationToken   = default)
        {

            var bill = BillOf(Partner);

            await bill.Subscribe(CancellationToken);
            await bill.Bind     (CancellationToken);

            await bill.RequestData(EVChargingSummary.BillDescriptionListData, CancellationToken: CancellationToken);
            await bill.RequestData(EVChargingSummary.BillConstraintsListData, CancellationToken: CancellationToken);

        }

        #endregion

        #region WriteSummary(Partner, Summary, ...)

        /// <summary>
        /// Tell a charging station what the session cost and where the
        /// electricity came from ([EVCS-001] to [EVCS-006]).
        /// </summary>
        /// <param name="Partner">An entity of a charging station.</param>
        /// <param name="Summary">The summary.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="InvalidOperationException">When the charging station offers no writeable charging summary.</exception>
        public async Task<SPINEResponse> WriteSummary(SPINERemoteEntity  Partner,
                                                      ChargingSummary    Summary,
                                                      CancellationToken  CancellationToken   = default)
        {

            var description = BillIdOf(Partner)
                                  ?? throw new InvalidOperationException($"{Partner.Address} offers no writeable charging summary.");

            var maximum     = BillOf(Partner).
                                  Data<BillConstraintsListDataType>(EVChargingSummary.BillConstraintsListData)?.
                                  BillConstraintsData?.
                                  FirstOrDefault(constraint => constraint.BillId == description)?.
                                  PositionCountMax;

            if (maximum < EVChargingSummary.PositionCount)
                throw new InvalidOperationException(
                          $"{Partner.Address} accepts at most {maximum} bill position(s); " +
                          $"a charging summary has {EVChargingSummary.PositionCount}.");

            return await BillOf(Partner).WriteData(
                             EVChargingSummary.BillListData,
                             new BillListDataType {
                                 BillData = [ EVChargingSummary.ToSPINE(description, Summary) ]
                             },
                             Partial: true,
                             CancellationToken: CancellationToken
                         );

        }

        #endregion

        #region BillIdOf(Partner) / SummaryRequestedBy(Partner)

        /// <summary>
        /// Which bill of a charging station this broker is meant to fill in.
        ///
        /// The writeable one whose supported type is a charging summary: a
        /// station may hold other bills, and writing a charging summary into one
        /// of those would be answering a question nobody asked.
        /// </summary>
        /// <param name="Partner">An entity of a charging station.</param>
        public UInt32? BillIdOf(SPINERemoteEntity Partner)

            => BillOf(Partner).
                   Data<BillDescriptionListDataType>(EVChargingSummary.BillDescriptionListData)?.
                   BillDescriptionData?.
                   FirstOrDefault(description => description.BillWriteable == true &&
                                                  description.SupportedBillType?.Contains(BillTypeType.ChargingSummary) == true)?.
                   BillId;


        /// <summary>
        /// Whether a charging station is currently asking for a summary
        /// ([EVCS-009]).
        /// </summary>
        /// <param name="Partner">An entity of a charging station.</param>
        public Boolean SummaryRequestedBy(SPINERemoteEntity Partner)

            => BillOf(Partner).
                   Data<BillDescriptionListDataType>(EVChargingSummary.BillDescriptionListData)?.
                   BillDescriptionData?.
                   Any(description => description.UpdateRequired == true) == true;

        #endregion


        #region (private) Watch(Event)

        /// <summary>
        /// A charging station changed its bill description. If the update flag
        /// went up, it wants a summary.
        /// </summary>
        private void Watch(SPINEDataChanged Event)
        {

            if (Event.Change.Function != EVChargingSummary.BillDescriptionListData ||
                Event.Change.RemoteFeature.Role != RoleType.Server)
                return;

            var partner = Event.Change.RemoteFeature.Entity;

            if (PartnerFor(partner) is null)
                return;

            if ((Event.Change.Data as BillDescriptionListDataType)?.
                    BillDescriptionData?.
                    Any(description => description.UpdateRequired == true) == true)
                OnSummaryRequested?.Invoke(this, partner);

        }

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the bill client feature.
        /// </summary>
        protected override SPINEFeature Feature()

            => Entity.Feature(FeatureTypeType.Bill, RoleType.Client)
                   ?? base.Feature();

        #endregion

    }

}
