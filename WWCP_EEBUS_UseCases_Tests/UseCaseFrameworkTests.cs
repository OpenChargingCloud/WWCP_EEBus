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

using NUnit.Framework;

using cloud.charging.open.protocols.EEBUS.SPINE;
using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases.tests
{

    /// <summary>
    /// The use case framework, with the two actors of "Limitation of Power
    /// Consumption" playing against each other.
    ///
    /// LPC is the right example because it uses every part of the framework at
    /// once: two actors, one of which is the client, four scenarios with
    /// different required features, an availability which changes, and a
    /// heartbeat whose direction is the reverse of the use case's - which per
    /// the general implementation guideline § 2.1.3 does not make the energy
    /// guard a server actor.
    /// </summary>
    [TestFixture]
    public class UseCaseFrameworkTests
    {

        #region (class) The two actors of LPC

        /// <summary>
        /// The one which limits.
        /// </summary>
        private sealed class EnergyGuard : AUseCase
        {

            public EnergyGuard(SPINELocalEntity Entity)

                : base(Entity,
                       UseCaseActors.EnergyGuard,
                       UseCaseNames.LimitationOfPowerConsumption,
                       UseCaseVersion.Parse("1.0.0"),
                       LPCScenarios,
                       [ UseCaseActors.ControllableSystem ],
                       [ EntityTypeType.EVSE, EntityTypeType.CEM, EntityTypeType.Generic ])

            { }

        }

        /// <summary>
        /// The one which is limited.
        /// </summary>
        private sealed class ControllableSystem : AUseCase
        {

            public ControllableSystem(SPINELocalEntity Entity)

                : base(Entity,
                       UseCaseActors.ControllableSystem,
                       UseCaseNames.LimitationOfPowerConsumption,
                       UseCaseVersion.Parse("1.0.0"),
                       LPCScenarios,
                       [ UseCaseActors.EnergyGuard ])

            { }

        }

        /// <summary>
        /// The four scenarios of LPC 1.0.0, with the server features each of
        /// them needs at the partner.
        /// </summary>
        private static readonly UseCaseScenario[] LPCScenarios = [

            new (1, [ FeatureTypeType.LoadControl ],
                 "Control the power consumption limit"),

            new (2, [ FeatureTypeType.DeviceConfiguration ],
                 "Failsafe values"),

            new (3, [ FeatureTypeType.DeviceDiagnosis ],
                 "Heartbeat"),

            new (4, [ FeatureTypeType.ElectricalConnection ],
                 "Constraints of the power consumption limit")

        ];

        #endregion

        #region Data

        private SPINELoopback        loopback   = null!;
        private EnergyGuard          guard      = null!;
        private ControllableSystem   system     = null!;
        private SPINELocalEntity     evseEntity = null!;

        #endregion

        #region Setup()

        [SetUp]
        public void Setup()
        {

            var hems = new SPINELocalDevice("d:_i:19667_HEMS", DeviceTypeType.EnergyManagementSystem);
            var evse = new SPINELocalDevice("d:_i:19667_EVSE", DeviceTypeType.ChargingStation);

            var cem  = hems.AddEntity(EntityTypeType.CEM);
            cem.AddFeature(FeatureTypeType.LoadControl,          RoleType.Client);
            cem.AddFeature(FeatureTypeType.DeviceConfiguration,  RoleType.Client);
            cem.AddFeature(FeatureTypeType.DeviceDiagnosis,      RoleType.Server);  // the heartbeat of the guard

            evseEntity = evse.AddEntity(EntityTypeType.EVSE);

            var limits = evseEntity.AddFeature(FeatureTypeType.LoadControl, RoleType.Server);
            limits.AddFunction("loadControlLimitListData",             Read: true, Write: true, PartialRead: true, PartialWrite: true);
            limits.AddFunction("loadControlLimitDescriptionListData");

            evseEntity.AddFeature(FeatureTypeType.DeviceConfiguration,  RoleType.Server);
            evseEntity.AddFeature(FeatureTypeType.DeviceDiagnosis,      RoleType.Server);

            guard   = new EnergyGuard       (cem);
            system  = new ControllableSystem(evseEntity);

            loopback = new SPINELoopback(hems, evse);

        }

        #endregion

        #region (private) Discover()

        /// <summary>
        /// What a device does on a new connection: ask what the partner is, and
        /// which use cases it plays.
        /// </summary>
        private async Task Discover()
        {
            await loopback.A.NodeManagement.RequestDetailedDiscovery(loopback.BAsSeenByA);
            await loopback.B.NodeManagement.RequestDetailedDiscovery(loopback.AAsSeenByB);
            await loopback.A.NodeManagement.RequestUseCaseData      (loopback.BAsSeenByA);
            await loopback.B.NodeManagement.RequestUseCaseData      (loopback.AAsSeenByB);
        }

        private SPINERemoteEntity EVSEAsSeenByHEMS
            => loopback.BAsSeenByA.Entity([ 1 ])!;

        #endregion


        #region RegisteringAUseCaseAnnouncesIt()

        /// <summary>
        /// SPINE 1.3.0, 7.3: a use case is announced in the node management use
        /// case data, with its actor, its name, its version and its scenarios.
        /// </summary>
        [Test]
        public async Task RegisteringAUseCaseAnnouncesIt()
        {

            await system.Register();

            var information = loopback.B.NodeManagement.UseCases.ToList();

            Assert.Multiple(() => {

                Assert.That(information,                          Has.Count.EqualTo(1));
                Assert.That(information[0].Actor,                 Is.EqualTo("ControllableSystem"));
                Assert.That(information[0].Address?.ToString(),    Is.EqualTo("d:_i:19667_EVSE:[1]:1"));

                var support = information[0].UseCaseSupport?[0];

                Assert.That(support?.UseCaseName,                 Is.EqualTo("limitationOfPowerConsumption"));
                Assert.That(support?.UseCaseVersion,              Is.EqualTo("1.0.0"));
                Assert.That(support?.UseCaseAvailable,            Is.True);
                Assert.That(support?.ScenarioSupport,             Is.EqualTo(new UInt32[] { 1, 2, 3, 4 }));

                Assert.That(system.IsRegistered,                  Is.True);

            });

        }

        #endregion

        #region TwoUseCasesOfTheSameActorShareOneEntry()

        /// <summary>
        /// The use case discovery is grouped by actor: one entry per address and
        /// actor, holding every use case that entity plays in that role.
        /// </summary>
        [Test]
        public async Task TwoUseCasesOfTheSameActorShareOneEntry()
        {

            await system.Register();

            await loopback.B.NodeManagement.AddUseCaseSupport(
                      evseEntity.Features.First().Address,
                      UseCaseActors.ControllableSystem,
                      UseCaseNames.LimitationOfPowerProduction,
                      "1.0.0",
                      [ 1, 2 ]
                  );

            var information = loopback.B.NodeManagement.UseCases.ToList();

            Assert.Multiple(() => {
                Assert.That(information,                        Has.Count.EqualTo(1));
                Assert.That(information[0].UseCaseSupport,      Has.Count.EqualTo(2));
                Assert.That(information[0].UseCaseSupport?.Select(support => support.UseCaseName),
                            Is.EquivalentTo(new[] { "limitationOfPowerConsumption", "limitationOfPowerProduction" }));
            });

        }

        #endregion

        #region UnregisteringTakesItBack()

        [Test]
        public async Task UnregisteringTakesItBack()
        {

            await system.Register();
            await system.Unregister();

            Assert.Multiple(() => {
                Assert.That(loopback.B.NodeManagement.UseCases, Is.Empty,
                            "An actor which plays no use case any more still says something.");
                Assert.That(system.IsRegistered,                Is.False);
            });

        }

        #endregion


        #region ThePartnersOfAUseCaseAreFoundByDiscovery()

        /// <summary>
        /// Nothing is arranged by hand: both sides register their actor, ask
        /// each other what they are, and then know which of the other's entities
        /// can play which scenario.
        /// </summary>
        [Test]
        public async Task ThePartnersOfAUseCaseAreFoundByDiscovery()
        {

            await guard. Register();
            await system.Register();

            await Discover();

            var partner = guard.PartnerFor(EVSEAsSeenByHEMS);

            Assert.Multiple(() => {

                Assert.That(partner,                   Is.Not.Null,
                            "The charging station was not recognised as a controllable system.");
                Assert.That(partner?.Version.ToString(), Is.EqualTo("1.0.0"));
                Assert.That(partner?.SameMajorVersion, Is.True);
                Assert.That(partner?.Available,        Is.True);

                // Scenarios 1 to 4: load control, device configuration, device
                // diagnosis and electrical connection are the features they
                // need - and the charging station has the first three.
                Assert.That(partner?.Scenarios,        Is.EquivalentTo(new UInt32[] { 1, 2, 3 }));

                Assert.That(guard.Supports(EVSEAsSeenByHEMS, 1), Is.True);
                Assert.That(guard.Supports(EVSEAsSeenByHEMS, 4), Is.False,
                            "A scenario was accepted although the partner has no feature for it.");

            });

        }

        #endregion

        #region AScenarioNeedsTheFeaturesItSaysItNeeds()

        /// <summary>
        /// A device may announce a scenario it has no feature for. Believing it
        /// would mean sending a read to a feature which is not there; this is a
        /// finding rather than something to trust.
        /// </summary>
        [Test]
        public async Task AScenarioNeedsTheFeaturesItSaysItNeeds()
        {

            await guard. Register();
            await system.Register();

            await Discover();

            Assert.That(guard.Supports(EVSEAsSeenByHEMS, 4), Is.False);

            // The charging station grows the missing feature and says so.
            evseEntity.AddFeature(FeatureTypeType.ElectricalConnection, RoleType.Server);

            await Discover();

            Assert.That(guard.Supports(EVSEAsSeenByHEMS, 4), Is.True,
                        "The scenario is still refused although the feature is there now.");

        }

        #endregion

        #region APartnerOfTheWrongActorIsIgnored()

        /// <summary>
        /// An energy guard talks to a controllable system, not to another energy
        /// guard.
        /// </summary>
        [Test]
        public async Task APartnerOfTheWrongActorIsIgnored()
        {

            await guard.Register();

            await loopback.B.NodeManagement.AddUseCaseSupport(
                      evseEntity.Features.First().Address,
                      UseCaseActors.EnergyGuard,
                      UseCaseNames.LimitationOfPowerConsumption,
                      "1.0.0",
                      [ 1, 2, 3, 4 ]
                  );

            await Discover();

            Assert.That(guard.PartnerFor(EVSEAsSeenByHEMS), Is.Null);

        }

        #endregion

        #region APartnerOfTheWrongEntityTypeIsIgnored()

        /// <summary>
        /// Each use case specification lists the entity types its actors may
        /// live on ("List of permitted entityTypes for Actor ...").
        /// </summary>
        [Test]
        public async Task APartnerOfTheWrongEntityTypeIsIgnored()
        {

            var dhw = loopback.B.AddEntity(EntityTypeType.DHWCircuit);
            dhw.AddFeature(FeatureTypeType.LoadControl, RoleType.Server);

            await guard.Register();

            await loopback.B.NodeManagement.AddUseCaseSupport(
                      dhw.Features.First().Address,
                      UseCaseActors.ControllableSystem,
                      UseCaseNames.LimitationOfPowerConsumption,
                      "1.0.0",
                      [ 1 ]
                  );

            await Discover();

            Assert.Multiple(() => {
                Assert.That(guard.IsCompatible(loopback.BAsSeenByA.Entity([ 2 ])), Is.False);
                Assert.That(guard.PartnerFor  (loopback.BAsSeenByA.Entity([ 2 ])), Is.Null);
            });

        }

        #endregion

        #region AnUnavailableUseCaseIsKnownButNotUsable()

        /// <summary>
        /// A charging station with no car plugged in still supports the use case
        /// and cannot currently do anything with it. Both halves of that are
        /// worth knowing: the partner stays a partner, and its scenarios stop
        /// being playable.
        /// </summary>
        [Test]
        public async Task AnUnavailableUseCaseIsKnownButNotUsable()
        {

            await guard. Register();
            await system.Register();

            await Discover();

            await system.SetAvailable(false);

            await Discover();

            var partner = guard.PartnerFor(EVSEAsSeenByHEMS);

            Assert.Multiple(() => {
                Assert.That(partner,                             Is.Not.Null);
                Assert.That(partner?.Available,                  Is.False);
                Assert.That(partner?.Scenarios,                  Is.Not.Empty,
                            "What it could do was forgotten instead of being marked unavailable.");
                Assert.That(guard.Supports(EVSEAsSeenByHEMS, 1), Is.False);
            });

        }

        #endregion

        #region AChangeOfSupportIsAnnouncedAsAnEvent()

        /// <summary>
        /// A use case tells the device when its partners change, so that an
        /// application does not have to poll for it.
        /// </summary>
        [Test]
        public async Task AChangeOfSupportIsAnnouncedAsAnEvent()
        {

            var changes = new List<UseCaseSupportChanged>();

            loopback.A.Events.Subscribe<UseCaseSupportChanged>(changes.Add);

            await guard. Register();
            await system.Register();

            await Discover();

            Assert.Multiple(() => {
                Assert.That(changes,                    Is.Not.Empty);
                Assert.That(changes[0].UseCase,         Is.SameAs(guard));
                Assert.That(changes[0].Partner?.Scenarios, Is.EquivalentTo(new UInt32[] { 1, 2, 3 }));
            });

            // ... and the same discovery again says nothing new.
            var before = changes.Count;

            await Discover();

            Assert.That(changes, Has.Count.EqualTo(before),
                        "The same state was announced twice.");

        }

        #endregion

        #region AnEntityWhichDisappearsIsNoLongerAPartner()

        [Test]
        public async Task AnEntityWhichDisappearsIsNoLongerAPartner()
        {

            await guard. Register();
            await system.Register();

            await Discover();

            Assert.That(guard.PartnerFor(EVSEAsSeenByHEMS), Is.Not.Null);

            loopback.B.Subscriptions.Add(loopback.A.NodeManagement.Address,
                                         loopback.B.NodeManagement.Address);

            var gone = EVSEAsSeenByHEMS;

            await loopback.B.NodeManagement.NotifyEntityRemoved(evseEntity);

            Assert.That(guard.PartnerFor(gone), Is.Null,
                        "An entity which is gone is still a partner.");

        }

        #endregion


        #region AFeaturePairKnowsWhatThePartnerOffers()

        /// <summary>
        /// The pair of our client feature and the partner's server feature is
        /// what a use case works with, and it will not ask for something the
        /// partner never announced.
        /// </summary>
        [Test]
        public async Task AFeaturePairKnowsWhatThePartnerOffers()
        {

            await guard. Register();
            await system.Register();

            await Discover();

            var loadControl = new UseCaseFeature(FeatureTypeType.LoadControl,
                                                 loopback.A.Entities.First(entity => entity.EntityId is [ 1 ]),
                                                 EVSEAsSeenByHEMS);

            Assert.Multiple(() => {

                Assert.That(loadControl.Supports("loadControlLimitListData"),                    Is.True);
                Assert.That(loadControl.Supports("loadControlLimitListData",  ForWriting: true), Is.True);
                Assert.That(loadControl.Supports("loadControlLimitDescriptionListData", ForWriting: true), Is.False);
                Assert.That(loadControl.Supports("loadControlNodeData"),                         Is.False);

                Assert.That(() => loadControl.RequestData("loadControlNodeData"),
                            Throws.InvalidOperationException,
                            "A read was sent for a function the partner never announced.");

                Assert.That(() => loadControl.WriteData("loadControlLimitListData", new LoadControlLimitListDataType()),
                            Throws.InvalidOperationException,
                            "A write was sent without a binding (SPINE 1.3.0, 7.6).");

            });

        }

        #endregion

        #region AFeaturePairSubscribesBindsAndWrites()

        /// <summary>
        /// The whole sequence of a use case, in the order the general
        /// implementation guideline puts it: subscribe first (§ 3.2.2), bind
        /// before writing (§ 7.6), and write partially (§ 3.1).
        /// </summary>
        [Test]
        public async Task AFeaturePairSubscribesBindsAndWrites()
        {

            await guard. Register();
            await system.Register();

            await Discover();

            var loadControl = new UseCaseFeature(FeatureTypeType.LoadControl,
                                                 loopback.A.Entities.First(entity => entity.EntityId is [ 1 ]),
                                                 EVSEAsSeenByHEMS);

            var limits      = evseEntity.Feature(FeatureTypeType.LoadControl, RoleType.Server)!;

            await limits.SetData("loadControlLimitListData",
                                 new LoadControlLimitListDataType {
                                     LoadControlLimitData = [
                                         new LoadControlLimitDataType {
                                             LimitId            = 1,
                                             IsLimitChangeable  = true,
                                             Value              = new ScaledNumberType { Number = 1600 }
                                         }
                                     ]
                                 });

            var subscribed = await loadControl.Subscribe();
            var bound      = await loadControl.Bind();

            var written    = await loadControl.WriteData(
                                       "loadControlLimitListData",
                                       new LoadControlLimitListDataType {
                                           LoadControlLimitData = [
                                               new LoadControlLimitDataType {
                                                   LimitId  = 1,
                                                   Value    = new ScaledNumberType { Number = 800 }
                                               }
                                           ]
                                       }
                                   );

            Assert.Multiple(() => {

                Assert.That(subscribed.IsError,          Is.False, subscribed.Result?.Description);
                Assert.That(bound.     IsError,          Is.False, bound.     Result?.Description);
                Assert.That(written.   IsError,          Is.False, written.   Result?.Description);

                Assert.That(loadControl.HasSubscription, Is.True);
                Assert.That(loadControl.HasBinding,      Is.True);

                // The write went through, partially: what it did not mention
                // stayed.
                var data = limits.DataCopy<LoadControlLimitListDataType>("loadControlLimitListData");

                Assert.That(data?.LoadControlLimitData?[0].Value?.Number,     Is.EqualTo(800));
                Assert.That(data?.LoadControlLimitData?[0].IsLimitChangeable, Is.True);

                // ... and the subscription brought the new state back by itself.
                Assert.That(loadControl.Data<LoadControlLimitListDataType>("loadControlLimitListData")?.
                                LoadControlLimitData?[0].Value?.Number,
                            Is.EqualTo(800));

                // Which is exactly why reading it again now would be the
                // anti-pattern of § 3.2.3.
                Assert.That(loadControl.IsRedundantPolling("loadControlLimitListData"), Is.True);

            });

        }

        #endregion

        #region APartialReadIsNotAskedOfAPartnerWhichCannotAnswerIt()

        /// <summary>
        /// A partner which did not announce a partial read will answer in full
        /// whatever we ask (SPINE 1.3.0, 5.3.4.5), so the selectors are dropped
        /// rather than sent as a filter it has to ignore.
        ///
        /// The Go reference implementation drops them when the partner cannot do
        /// partial reads **or** partial writes; see docs/spec-deviations.md, S8.
        /// </summary>
        [Test]
        public async Task APartialReadIsNotAskedOfAPartnerWhichCannotAnswerIt()
        {

            var descriptions = "loadControlLimitDescriptionListData";

            await guard. Register();
            await system.Register();

            await Discover();

            var loadControl = new UseCaseFeature(FeatureTypeType.LoadControl,
                                                 loopback.A.Entities.First(entity => entity.EntityId is [ 1 ]),
                                                 EVSEAsSeenByHEMS);

            await loadControl.RequestData(descriptions,
                                          Selectors: new LoadControlLimitDescriptionListDataSelectorsType { LimitId = 1 });

            var read = loopback.AToB.Datagrams.Last(datagram => datagram.Header?.CmdClassifier == CmdClassifierType.Read);

            Assert.That(read.Payload?.Cmd?[0].Filter, Is.Null,
                        "A partial read was sent to a partner which never announced one.");

        }

        #endregion

    }

}
