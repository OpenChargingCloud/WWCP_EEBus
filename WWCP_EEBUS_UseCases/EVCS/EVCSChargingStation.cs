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
    /// The charging station of "EV Charging Summary" - the side which asks what
    /// the session cost and shows the answer to a person.
    ///
    /// It is the **server**, which is the other way round from what the use case
    /// name suggests: the energy broker knows the prices, but the charging
    /// station is where somebody is standing, so it holds the Bill feature and
    /// the broker writes into it.
    ///
    /// Being the server is also why <see cref="RequestSummary"/> looks the way
    /// it does. A SPINE server answers rather than requests, so [EVCS-009] - the
    /// charging station asking for a summary when charging finishes - is a flag
    /// on the bill description which the broker is subscribed to. The flag goes
    /// down by itself when the summary arrives.
    /// </summary>
    public class EVCSChargingStation : AUseCase
    {

        #region Data

        private readonly UInt32  billId;

        #endregion

        #region Properties

        /// <summary>
        /// The bill server feature, which holds the charging summary.
        /// </summary>
        public SPINELocalFeature  Bill    { get; }

        #endregion

        #region Events

        /// <summary>
        /// The energy broker wrote a charging summary.
        /// </summary>
        public event Action<EVCSChargingStation, ChargingSummary>? OnSummaryWritten;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Add the EVSE of EVCS to an entity.
        /// </summary>
        /// <param name="Entity">The entity of the charging station.</param>
        public EVCSChargingStation(SPINELocalEntity Entity)

            : base(Entity,
                   UseCaseActors.EVSE,
                   EVChargingSummary.Name,
                   EVChargingSummary.Version,
                   EVChargingSummary.Scenarios(ForBroker: false),
                   [ UseCaseActors.EnergyBroker, UseCaseActors.CEM ],
                   [ EntityTypeType.CEM ],
                   EVChargingSummary.DocumentSubRevision)

        {

            Bill = Entity.Feature(FeatureTypeType.Bill, RoleType.Server)
                       ?? Entity.AddFeature(FeatureTypeType.Bill, RoleType.Server);

            Bill.AddFunction(EVChargingSummary.BillDescriptionListData);
            Bill.AddFunction(EVChargingSummary.BillConstraintsListData);

            Bill.AddFunction(EVChargingSummary.BillListData,
                             Read:          true,
                             Write:         true,
                             PartialRead:   true,
                             PartialWrite:  true);

            var descriptions = Bill.DataCopy<BillDescriptionListDataType>(EVChargingSummary.BillDescriptionListData)?.
                                   BillDescriptionData?.ToList() ?? [];

            var constraints  = Bill.DataCopy<BillConstraintsListDataType>(EVChargingSummary.BillConstraintsListData)?.
                                   BillConstraintsData?.ToList() ?? [];

            billId = UseCaseIds.NextFree(descriptions.Select(description => description.BillId));

            descriptions.Add(new BillDescriptionDataType {
                                 BillId             = billId,
                                 BillWriteable      = true,
                                 UpdateRequired     = false,
                                 SupportedBillType  = [ BillTypeType.ChargingSummary ]
                             });

            // Exactly two positions and no more: the grid share and the
            // self-produced share (Table 7). A broker which sent a third would
            // be inventing a category this use case does not have.
            constraints.Add(new BillConstraintsDataType {
                                BillId            = billId,
                                PositionCountMin  = 0,
                                PositionCountMax  = EVChargingSummary.PositionCount
                            });

            Bill.FunctionData(EVChargingSummary.BillDescriptionListData)!.SetData(
                new BillDescriptionListDataType { BillDescriptionData = descriptions }
            );

            Bill.FunctionData(EVChargingSummary.BillConstraintsListData)!.SetData(
                new BillConstraintsListDataType { BillConstraintsData = constraints }
            );

            var approvedBySomeoneElse = Bill.WriteApproval;
            Bill.WriteApproval        = async (message, cancellationToken) =>
                await ApproveBill(message, cancellationToken)
                    ?? (approvedBySomeoneElse is not null
                            ? await approvedBySomeoneElse(message, cancellationToken)
                            : null);

            Device.Events.Subscribe<SPINEDataChanged>(Watch, SPINEEventLevel.Core);

        }

        #endregion


        #region Summary / SummaryRequested / RequestSummary(...)

        /// <summary>
        /// What the energy broker last said the session cost, or null when it
        /// has said nothing yet.
        /// </summary>
        public ChargingSummary? Summary

            => EVChargingSummary.FromSPINE(
                   Bill.DataCopy<BillListDataType>(EVChargingSummary.BillListData)?.
                       BillData?.FirstOrDefault(bill => bill.BillId == billId)
               );


        /// <summary>
        /// Whether this charging station is currently asking for a summary.
        /// </summary>
        public Boolean SummaryRequested

            => Description()?.UpdateRequired == true;


        /// <summary>
        /// Ask the energy broker for a charging summary ([EVCS-009]).
        ///
        /// Typically when charging finishes, and it may be asked during charging
        /// too - "e.g. to visualize the charging process". A server cannot make
        /// a request, so this raises the update flag and waits.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public Task RequestSummary(CancellationToken CancellationToken = default)

            => SetUpdateRequired(true, CancellationToken);

        #endregion


        #region (private) Description() / SetUpdateRequired(...) / ApproveBill(...) / Watch(...)

        /// <summary>
        /// The description of our bill.
        /// </summary>
        private BillDescriptionDataType? Description()

            => Bill.DataCopy<BillDescriptionListDataType>(EVChargingSummary.BillDescriptionListData)?.
                   BillDescriptionData?.FirstOrDefault(description => description.BillId == billId);


        /// <summary>
        /// Raise or lower the update request.
        /// </summary>
        private async Task SetUpdateRequired(Boolean            Required,
                                             CancellationToken  CancellationToken)
        {

            var data = Bill.DataCopy<BillDescriptionListDataType>(EVChargingSummary.BillDescriptionListData);

            var mine = data?.BillDescriptionData?.FirstOrDefault(description => description.BillId == billId);

            if (mine is null || mine.UpdateRequired == Required)
                return;

            mine.UpdateRequired = Required;

            await Bill.SetData(EVChargingSummary.BillDescriptionListData,
                               data,
                               CancellationToken: CancellationToken);

        }


        /// <summary>
        /// The energy broker wrote a bill.
        ///
        /// Two things are checked, and both are the constraints this station
        /// published rather than opinions of its own: the bill has to be a
        /// charging summary, and it may not have more positions than were
        /// declared. A broker which sends a third position is describing a
        /// category this use case does not have.
        /// </summary>
        private Task<ResultDataType?> ApproveBill(SPINEMessage       Message,
                                                  CancellationToken  CancellationToken)
        {

            if (Message.Data is not BillListDataType data)
                return Task.FromResult<ResultDataType?>(null);

            foreach (var bill in data.BillData ?? [])
            {

                if (bill.BillId != billId)
                    continue;

                if (bill.BillType is not null &&
                    bill.BillType != BillTypeType.ChargingSummary)
                    return Task.FromResult<ResultDataType?>(
                               ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                    $"This EVSE offers a '{BillTypeType.ChargingSummary}' bill; " +
                                                    $"'{bill.BillType}' is not one.")
                           );

                if (bill.Position?.Count > EVChargingSummary.PositionCount)
                    return Task.FromResult<ResultDataType?>(
                               ResultDataType.Error(SPINEErrorNumbers.CommandRejected,
                                                    $"A charging summary has at most {EVChargingSummary.PositionCount} positions; " +
                                                    $"{bill.Position.Count} were written.")
                           );

            }

            return Task.FromResult<ResultDataType?>(null);

        }


        /// <summary>
        /// A summary arrived. Lower the update request and tell the application.
        /// </summary>
        private void Watch(SPINEDataChanged Event)
        {

            if (Event.Change.CmdClassifier != CmdClassifierType.Write ||
                Event.Change.LocalFeature  != Bill                     ||
                Event.Change.Function      != EVChargingSummary.BillListData)
                return;

            _ = SetUpdateRequired(false, CancellationToken.None);

            if (Summary is ChargingSummary summary)
                OnSummaryWritten?.Invoke(this, summary);

        }

        #endregion

        #region (override) Feature()

        /// <summary>
        /// The use case is announced at the bill feature.
        /// </summary>
        protected override SPINEFeature Feature()

            => Bill;

        #endregion

    }

}
