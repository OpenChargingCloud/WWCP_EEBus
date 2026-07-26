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

using System.Collections.Concurrent;

using cloud.charging.open.protocols.EEBUS.SPINE;
using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.UseCases
{

    /// <summary>
    /// One scenario of a use case, and what a partner needs in order to play it.
    /// </summary>
    /// <param name="Number">The number of the scenario within its use case.</param>
    /// <param name="ServerFeatures">Which server features the partner has to have for it.</param>
    /// <param name="Description">What the scenario is about, for humans.</param>
    public sealed record UseCaseScenario(UInt32                        Number,
                                         IEnumerable<FeatureTypeType>  ServerFeatures,
                                         String?                       Description   = null)
    {

        /// <summary>
        /// Whether a device implementing the use case at all has to support this
        /// scenario.
        ///
        /// Only meaningful where a use case is described as a whole - a profile
        /// listing every scenario the document has. The list an actor hands to
        /// <see cref="AUseCase"/> is what it actually supports, and there the
        /// question no longer arises.
        /// </summary>
        public Boolean  Mandatory    { get; init; }


        /// <summary>Return a text representation of this scenario.</summary>
        public override String ToString()

            => $"{Number}{(Description is not null ? $" ({Description})" : "")}: " +
               $"{String.Join(", ", ServerFeatures)}";

    }


    /// <summary>
    /// What a partner can do for one use case.
    /// </summary>
    /// <param name="Entity">The entity of the partner.</param>
    /// <param name="Version">The version of the use case it was matched at.</param>
    /// <param name="SameMajorVersion">Whether that version shares our major number.</param>
    /// <param name="Scenarios">The scenarios which can be played with it.</param>
    /// <param name="Available">Whether the partner says the use case can be used right now.</param>
    public sealed record UseCasePartner(SPINERemoteEntity     Entity,
                                        UseCaseVersion        Version,
                                        Boolean               SameMajorVersion,
                                        IReadOnlySet<UInt32>  Scenarios,
                                        Boolean               Available)
    {

        /// <summary>Whether the given scenario can be played with this partner.</summary>
        /// <param name="Scenario">The number of a scenario.</param>
        public Boolean Supports(UInt32 Scenario)

            => Available && Scenarios.Contains(Scenario);

        /// <summary>Return a text representation of this partner.</summary>
        public override String ToString()

            => $"{Entity.Address} {Version}{(SameMajorVersion ? "" : " (other major version)")}" +
               $"{(Available ? "" : ", not available")}: scenarios {String.Join(", ", Scenarios.Order())}";

    }


    /// <summary>
    /// The partners of a use case changed.
    /// </summary>
    /// <param name="Timestamp">When it happened.</param>
    /// <param name="UseCase">The use case.</param>
    /// <param name="Entity">The entity whose support changed, or null when a whole device went away.</param>
    /// <param name="Partner">What it can do now, or null when it can no longer do anything.</param>
    public sealed record UseCaseSupportChanged(DateTimeOffset      Timestamp,
                                               AUseCase            UseCase,
                                               SPINERemoteEntity?  Entity,
                                               UseCasePartner?     Partner)

        : SPINEEvent(Timestamp)

    {

        /// <summary>Return a text representation of this event.</summary>
        public override String ToString()

            => Partner is not null
                   ? $"{UseCase.Name}: {Partner}"
                   : $"{UseCase.Name}: {Entity?.Address.ToString() ?? "a device"} is gone";

    }


    /// <summary>
    /// One use case, as this device plays one of its actors.
    ///
    /// A use case is two things at once: a declaration - "this entity is the
    /// energy guard of the limitation of power consumption, version 1.0.0,
    /// scenarios 1 to 4" - which goes into the node management use case data and
    /// which every partner reads; and a watcher, which looks at what the
    /// partners declare and works out which of them can actually play which
    /// scenario.
    ///
    /// The second half is where the rules live, and they are the same for every
    /// use case, which is why they are here and not in each of them:
    ///
    /// * the actor of the partner has to be one this use case can work with;
    /// * the entity type of the partner has to be one the use case allows;
    /// * the version rules of section 3.1.2 (see <see cref="UseCaseVersion"/>);
    /// * a scenario counts as playable only when the partner announces it **and**
    ///   has every server feature that scenario needs. A device which claims a
    ///   scenario it has no feature for is a device to report, not to believe.
    /// </summary>
    public abstract class AUseCase
    {

        #region Data

        private readonly ConcurrentDictionary<String, UseCasePartner>  partners   = new (StringComparer.Ordinal);

        private readonly Action<SPINEEvent>                            handler;

        private readonly SortedSet<UInt32>                             announced  = [];

        #endregion

        #region Properties

        /// <summary>
        /// The entity of this device which plays the actor.
        /// </summary>
        public SPINELocalEntity                 Entity                 { get; }

        /// <summary>
        /// The device this use case belongs to.
        /// </summary>
        public SPINELocalDevice                 Device
            => Entity.Device;

        /// <summary>
        /// Which actor of the use case this entity plays.
        /// </summary>
        public String                           Actor                  { get; }

        /// <summary>
        /// The name of the use case.
        /// </summary>
        public String                           Name                   { get; }

        /// <summary>
        /// The version of the use case this implementation follows.
        /// </summary>
        public UseCaseVersion                   Version                { get; }

        /// <summary>
        /// The sub revision of the use case document, where it has one.
        /// </summary>
        public String?                          DocumentSubRevision    { get; }

        /// <summary>
        /// The scenarios this implementation supports.
        /// </summary>
        public IReadOnlyList<UseCaseScenario>   Scenarios              { get; }

        /// <summary>
        /// Which actors of the use case this one talks to.
        /// </summary>
        public IReadOnlySet<String>             PartnerActors          { get; }

        /// <summary>
        /// Which entity types a partner may be. An empty set means any.
        /// </summary>
        public IReadOnlySet<EntityTypeType>     PartnerEntityTypes     { get; }

        /// <summary>
        /// Whether this device says the use case can be used right now.
        /// </summary>
        public Boolean                          IsAvailable            { get; private set; } = true;

        /// <summary>
        /// Whether this use case is announced in the node management use case
        /// data.
        /// </summary>
        public Boolean                          IsRegistered           { get; private set; }

        /// <summary>
        /// The partners which can play this use case, as they last announced.
        /// </summary>
        public IEnumerable<UseCasePartner>      Partners
            => partners.Values;

        /// <summary>
        /// The scenarios this device is announcing right now.
        ///
        /// Usually every scenario it implements, and not always: a use case may
        /// ask an actor to stop supporting one of its scenarios while a
        /// condition holds. An EV which has reached its maximum energy capacity
        /// "SHALL stop to support this scenario" of the optimisation of self
        /// consumption ([OSCEV-009]) - it still implements it, it just cannot do
        /// anything with it until the battery has room again.
        /// </summary>
        public IEnumerable<UInt32>              AnnouncedScenarios
            => announced;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a use case an entity of this device plays.
        /// </summary>
        /// <param name="Entity">The entity which plays it.</param>
        /// <param name="Actor">Which actor of the use case it plays.</param>
        /// <param name="Name">The name of the use case.</param>
        /// <param name="Version">The version this implementation follows.</param>
        /// <param name="Scenarios">The scenarios it supports.</param>
        /// <param name="PartnerActors">Which actors it talks to.</param>
        /// <param name="PartnerEntityTypes">Which entity types a partner may be. Null or empty means any.</param>
        /// <param name="DocumentSubRevision">The sub revision of the use case document.</param>
        protected AUseCase(SPINELocalEntity               Entity,
                           String                         Actor,
                           String                         Name,
                           UseCaseVersion                 Version,
                           IEnumerable<UseCaseScenario>   Scenarios,
                           IEnumerable<String>            PartnerActors,
                           IEnumerable<EntityTypeType>?   PartnerEntityTypes    = null,
                           String?                        DocumentSubRevision   = null)
        {

            this.Entity               = Entity;
            this.Actor                = Actor;
            this.Name                 = Name;
            this.Version              = Version;
            this.Scenarios            = [.. Scenarios];
            this.PartnerActors        = PartnerActors.     ToHashSet(StringComparer.Ordinal);
            this.PartnerEntityTypes   = (PartnerEntityTypes ?? []).ToHashSet();
            this.DocumentSubRevision  = DocumentSubRevision;

            foreach (var scenario in this.Scenarios)
                announced.Add(scenario.Number);

            // At the core level: a use case has to have caught up with what a
            // device announced before the application is told about it.
            this.handler              = Device.Events.Subscribe<SPINEEvent>(Handle,
                                                                            SPINEEventLevel.Core);

        }

        #endregion


        #region Register(CancellationToken = default) / Unregister(...)

        /// <summary>
        /// Announce this use case, so that every partner reading our node
        /// management use case data finds it.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task Register(CancellationToken CancellationToken = default)
        {

            await Device.NodeManagement.AddUseCaseSupport(
                      Feature().Address,
                      Actor,
                      Name,
                      Version.ToString(),
                      announced,
                      DocumentSubRevision,
                      IsAvailable,
                      CancellationToken: CancellationToken
                  );

            IsRegistered = true;

            // Whatever the partners already told us was ignored until now.
            Reevaluate();

        }


        /// <summary>
        /// Stop announcing this use case.
        /// </summary>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task Unregister(CancellationToken CancellationToken = default)
        {

            await Device.NodeManagement.RemoveUseCaseSupport(Actor,
                                                             Name,
                                                             CancellationToken: CancellationToken);

            IsRegistered = false;

            Device.Events.Unsubscribe(handler);

            partners.Clear();

        }

        #endregion

        #region SetAvailable(Available, CancellationToken = default)

        /// <summary>
        /// Say whether this device can carry out the use case right now.
        ///
        /// A charging station with no car plugged in still supports the use case
        /// and cannot currently do anything with it. The partners are told,
        /// because the alternative is that they keep asking.
        /// </summary>
        /// <param name="Available">Whether it can be used right now.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public async Task SetAvailable(Boolean            Available,
                                       CancellationToken  CancellationToken   = default)
        {

            IsAvailable = Available;

            if (IsRegistered)
                await Device.NodeManagement.SetUseCaseAvailability(Actor,
                                                                   Name,
                                                                   Available,
                                                                   CancellationToken: CancellationToken);

        }

        #endregion

        #region SetScenarioSupported(Scenario, Supported, CancellationToken = default)

        /// <summary>
        /// Start or stop announcing one scenario of this use case.
        ///
        /// The use case itself stays available - it is one scenario which is
        /// withdrawn, not the whole thing. A partner reading the use case data
        /// then sees a device which implements this use case and cannot
        /// currently play that scenario, which is exactly what
        /// [OSCEV-009] asks for.
        /// </summary>
        /// <param name="Scenario">The number of a scenario of this use case.</param>
        /// <param name="Supported">Whether it is announced.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        /// <exception cref="ArgumentException">When this actor does not implement that scenario at all.</exception>
        public async Task SetScenarioSupported(UInt32             Scenario,
                                               Boolean            Supported,
                                               CancellationToken  CancellationToken   = default)
        {

            if (Supported && !Scenarios.Any(scenario => scenario.Number == Scenario))
                throw new ArgumentException($"{Name} as {Actor} does not implement scenario {Scenario}.",
                                            nameof(Scenario));

            if (!(Supported ? announced.Add(Scenario) : announced.Remove(Scenario)))
                return;

            if (IsRegistered)
                await Register(CancellationToken);

        }

        #endregion


        #region Feature()

        /// <summary>
        /// The feature of this entity which the use case is announced at.
        ///
        /// The use case discovery names a feature address, and the one which
        /// makes sense is the entity's own node management address - the use
        /// case belongs to the entity, not to one of its features.
        /// </summary>
        protected virtual SPINEFeature Feature()

            => Entity.Features.FirstOrDefault()
                   ?? Device.NodeManagement;

        #endregion

        #region PartnerFor(Entity) / Supports(Entity, Scenario)

        /// <summary>
        /// What the given partner can do for this use case, or null when it
        /// cannot do anything.
        /// </summary>
        /// <param name="Entity">An entity of another device.</param>
        public UseCasePartner? PartnerFor(SPINERemoteEntity? Entity)

            => Entity is null
                   ? null
                   : partners.GetValueOrDefault(KeyOf(Entity));


        /// <summary>
        /// Whether the given partner can play the given scenario.
        /// </summary>
        /// <param name="Entity">An entity of another device.</param>
        /// <param name="Scenario">The number of a scenario.</param>
        public Boolean Supports(SPINERemoteEntity? Entity, UInt32 Scenario)

            => PartnerFor(Entity)?.Supports(Scenario) == true;

        #endregion

        #region IsCompatible(Entity)

        /// <summary>
        /// Whether an entity of another device is of a type this use case can
        /// work with.
        /// </summary>
        /// <param name="Entity">An entity of another device.</param>
        public Boolean IsCompatible(SPINERemoteEntity? Entity)

            => Entity is not null &&
               (PartnerEntityTypes.Count == 0 ||
                PartnerEntityTypes.Contains(Entity.EntityType));

        #endregion


        #region (private) Handle(Event)

        /// <summary>
        /// Something changed at a partner: it was discovered, it announced its
        /// use cases, or an entity of it came or went.
        /// </summary>
        private void Handle(SPINEEvent Event)
        {

            if (!IsRegistered)
                return;

            switch (Event)
            {

                case SPINEDeviceDiscovered discovered:
                    Evaluate(discovered.RemoteDevice);
                    break;

                case SPINEDataChanged changed
                    when changed.Change.Function == SPINENodeManagement.UseCaseData ||
                         changed.Change.Function == SPINENodeManagement.DetailedDiscoveryData:
                    Evaluate(changed.Change.RemoteFeature.Device);
                    break;

                case SPINEEntityChanged { Added: false } entityChanged:
                    Forget(entityChanged.RemoteEntity);
                    break;

                case SPINEEntityChanged { Added: true } entityChanged:
                    Evaluate(entityChanged.RemoteEntity.Device);
                    break;

            }

        }

        #endregion

        #region (private) Reevaluate() / Evaluate(RemoteDevice)

        /// <summary>
        /// Look at every partner again.
        /// </summary>
        private void Reevaluate()
        {
            foreach (var remoteDevice in Device.RemoteDevices)
                Evaluate(remoteDevice);
        }


        /// <summary>
        /// Work out what one partner can do for this use case.
        ///
        /// Accumulated per entity across the actors it announces, rather than
        /// decided by whichever entry comes last. One entity regularly plays two
        /// actors of the same use case - the energy manager of the coordinated
        /// EV charging is the energy guard *and* the energy broker, and a car
        /// facing it can play scenarios 2, 5 and 7 with the one and 3, 6 and 8
        /// with the other. Taking the last entry seen would have hidden half of
        /// them, and which half would depend on the order the partner listed its
        /// actors in.
        /// </summary>
        private void Evaluate(SPINERemoteDevice RemoteDevice)
        {

            var seen   = new HashSet<String>(StringComparer.Ordinal);
            var found  = new Dictionary<String, UseCasePartner>(StringComparer.Ordinal);

            foreach (var information in RemoteDevice.UseCases)
            {

                if (information.Actor is null ||
                    !PartnerActors.Contains(information.Actor))
                    continue;

                var supports = (information.UseCaseSupport ?? []).
                                   Where(support => String.Equals(support.UseCaseName, Name, StringComparison.Ordinal)).
                                   ToList();

                if (supports.Count == 0)
                    continue;

                if (!UseCaseVersion.Best(Version,
                                         supports.Select(support => support.UseCaseVersion),
                                         out var version,
                                         out var sameMajor))
                    continue;

                // The entry the chosen version came from decides the scenarios
                // and the availability.
                var chosen = supports.FirstOrDefault(support => UseCaseVersion.TryParse(support.UseCaseVersion, out var parsed) &&
                                                                parsed == version)
                                 ?? supports[0];

                // SPINE 1.3.0 lets the use case information name the entity it
                // belongs to. Where it does, that entity is the partner; where it
                // does not, every entity of the device is a candidate.
                var candidates = information.Address?.Entity is List<UInt32> entityId &&
                                 RemoteDevice.Entity(entityId) is SPINERemoteEntity named
                                     ? [ named ]
                                     : RemoteDevice.Entities.ToList();

                foreach (var entity in candidates)
                {

                    if (!IsCompatible(entity))
                        continue;

                    var key        = KeyOf(entity);
                    var scenarios  = Playable(entity, chosen);
                    var available  = chosen.UseCaseAvailable != false;

                    seen.Add(key);

                    // A second actor of the same use case at the same entity
                    // adds what it can play; it does not replace what the first
                    // one could. An entity is unavailable only when every actor
                    // of it says so.
                    if (found.TryGetValue(key, out var already))
                        found[key] = new UseCasePartner(entity,
                                                        already.Version,
                                                        already.SameMajorVersion,
                                                        already.Scenarios.Union(scenarios).ToHashSet(),
                                                        already.Available || available);

                    else
                        found[key] = new UseCasePartner(entity,
                                                        version,
                                                        sameMajor,
                                                        scenarios,
                                                        available);

                }

            }

            foreach (var (_, partner) in found)
                Remember(partner.Entity, partner);

            // Whatever this device no longer announces, it no longer plays.
            foreach (var entity in RemoteDevice.Entities)
                if (!seen.Contains(KeyOf(entity)))
                    Forget(entity);

        }

        #endregion

        #region (private) Playable(Entity, Support)

        /// <summary>
        /// Which of the scenarios the partner announces it can actually play.
        ///
        /// A scenario needs every server feature this implementation says it
        /// needs. A partner which announces a scenario without having the
        /// features for it has announced something it cannot do - and a test
        /// bench wants to see that rather than to trust it.
        /// </summary>
        private IReadOnlySet<UInt32> Playable(SPINERemoteEntity   Entity,
                                              UseCaseSupportType  Support)
        {

            var announced  = (Support.ScenarioSupport ?? []).ToHashSet();

            var available  = Entity.Features.
                                 Where (feature => feature.Role == RoleType.Server).
                                 Select(feature => feature.FeatureType).
                                 ToHashSet();

            return Scenarios.
                       Where (scenario => announced.Contains(scenario.Number) &&
                                          scenario.ServerFeatures.All(available.Contains)).
                       Select(scenario => scenario.Number).
                       ToHashSet();

        }

        #endregion

        #region (private) Remember(Entity, Partner) / Forget(Entity)

        private void Remember(SPINERemoteEntity Entity, UseCasePartner Partner)
        {

            var key = KeyOf(Entity);

            if (partners.TryGetValue(key, out var existing) &&
                existing.Version          == Partner.Version &&
                existing.Available        == Partner.Available &&
                existing.SameMajorVersion == Partner.SameMajorVersion &&
                existing.Scenarios.SetEquals(Partner.Scenarios))
                return;

            partners[key] = Partner;

            Device.Events.Publish(timestamp => new UseCaseSupportChanged(timestamp, this, Entity, Partner));

        }


        private void Forget(SPINERemoteEntity Entity)
        {

            if (!partners.TryRemove(KeyOf(Entity), out _))
                return;

            Device.Events.Publish(timestamp => new UseCaseSupportChanged(timestamp, this, Entity, null));

        }


        private static String KeyOf(SPINERemoteEntity Entity)

            => $"{Entity.Address.Device?.ToLowerInvariant()}:[{String.Join(',', Entity.EntityId)}]";

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this use case.
        /// </summary>
        public override String ToString()

            => $"{Name} {Version} as {Actor} at {Entity.Address}" +
               $"{(IsAvailable ? "" : " (not available)")}, " +
               $"{partners.Count} partner(s)";

        #endregion

    }

}
