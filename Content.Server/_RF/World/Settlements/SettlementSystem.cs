using System.Numerics;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.World.Components;
using Content.Server.GameTicking;
using Content.Shared._RF.World.Settlements;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RF.World.Settlements;

/// <summary>
/// Finds the settlements of the players and keeps them the same entities while their groups change.
/// </summary>
/// <remarks>
/// Every so often the pops of a player, and the things it owns, are divided into groups of the ones that are close to each
/// other (see <see cref="SettlementClustering"/>). A group with enough pops is a settlement. The groups are matched to the
/// settlements that were found before by the pops they have in common: the settlement and the group that have the most pops
/// in common are one, so a settlement does not change when pops come and go or move around. What is left over is new
/// settlements, the settlements that have been divided or have become one, and the ones that are gone.
/// <para>
/// A player owns its settlements and nothing else, and a settlement owns the things that are in it: its residents, with what they
/// carry, and whatever the residents take or make. So what the pops of a settlement share (see the <c>Owned</c> search query) is
/// what the settlement owns. A pop that has been away from its settlement for longer than <c>LeaveDelay</c> makes a settlement of its
/// own, however few the pops are: how many residents a settlement needs is up to what is done to it (the events say how many they
/// need), not to the settlements. What is outside the radius of every settlement (a zone or a ghost that is marked out far from them,
/// the things of a settlement that is gone) is owned by the player for the time being, and the settlement that comes near adopts it,
/// see <see cref="Adopt"/>. The pops are all in a settlement, so they never share what the other settlements have.
/// </para>
/// </remarks>
public sealed partial class SettlementSystem : SharedSettlementSystem
{
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private MobStateSystem _mobState = null!;
    [Dependency] private EntityWhitelistSystem _whitelist = null!;
    [Dependency] private SharedTransformSystem _xform = null!;
    [Dependency] private GameTicker _ticker = null!;

    [Dependency] private EntityQuery<SettlementComponent> _settlements;
    [Dependency] private EntityQuery<SettlementResidentComponent> _residents;
    [Dependency] private EntityQuery<SettlementTrackerComponent> _trackers;
    [Dependency] private EntityQuery<RimFortressPlayerComponent> _players;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery;

    private static readonly EntProtoId SettlementPrototype = "RimFortressSettlement";

    // The buffers that are reused by the updates.
    private readonly List<Vector2> _points = new();
    private readonly List<EntityUid> _pointEntities = new();
    private readonly List<bool> _pointIsPop = new();
    private readonly Dictionary<EntityUid, Vector2> _popPositions = new();
    private readonly HashSet<EntityUid> _alivePops = new();
    private readonly HashSet<EntityUid> _clusteredPops = new();

    /// <summary>
    /// A group of the things of a player that are close to each other.
    /// </summary>
    private sealed class Group
    {
        public readonly HashSet<EntityUid> Pops = new();
        public int Owned;
        public Vector2 Center;
        public float Radius;
        public Entity<SettlementComponent>? Settlement;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var trackers = EntityQueryEnumerator<SettlementTrackerComponent, RimFortressPlayerComponent>();

        while (trackers.MoveNext(out var uid, out var tracker, out var player))
        {
            if (tracker.SettlementsDirty || now >= tracker.NextUpdate)
                UpdatePlayer((uid, tracker, player));
        }

        // The settlements of a player that is gone have nothing to wait for.
        var settlements = EntityQueryEnumerator<SettlementComponent>();

        while (settlements.MoveNext(out var uid, out var settlement))
        {
            if (!Terminating(uid) && TerminatingOrDeleted(settlement.Player))
                Dissolve((uid, settlement));
        }
    }

    [SubscribeLocalEvent]
    private void OnOwnedStateChanged(Entity<OwnershipComponent> ent, ref MobStateChangedEvent args)
    {
        // The pops that die or come back to life change the groups.
        if (args.NewMobState != MobState.Dead && args.OldMobState != MobState.Dead)
            return;

        foreach (var owner in ent.Comp.Owners)
        {
            // The pops are owned by their settlements, which are the player's.
            MarkDirty(_settlements.TryComp(owner, out var settlement) ? settlement.Player : owner);
        }
    }

    [SubscribeLocalEvent]
    private void OnResidentStateChanged(Entity<SettlementResidentComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            RemoveResident(ent, true);
    }

    [SubscribeLocalEvent]
    private void OnResidentTerminating(Entity<SettlementResidentComponent> ent, ref EntityTerminatingEvent args)
    {
        RemoveResident(ent, false);
    }

    [SubscribeLocalEvent]
    private void OnSettlementShutdown(Entity<SettlementComponent> ent, ref ComponentShutdown args)
    {
        foreach (var resident in ent.Comp.Residents)
        {
            if (!TerminatingOrDeleted(resident)
                && _residents.TryComp(resident, out var comp)
                && comp.Settlement == ent.Owner)
                RemComp<SettlementResidentComponent>(resident);
        }
    }

    /// <inheritdoc/>
    public override void Refresh(EntityUid player)
    {
        if (!_trackers.TryComp(player, out var tracker) || !_players.TryComp(player, out var comp))
            return;

        if (tracker.SettlementsDirty || _timing.CurTime >= tracker.NextUpdate)
            UpdatePlayer((player, tracker, comp));
    }

    /// <inheritdoc/>
    public override void MarkDirty(EntityUid player)
    {
        if (_trackers.TryComp(player, out var tracker))
            tracker.SettlementsDirty = true;
    }

    /// <summary>
    /// Takes a pop out of the settlement it lives in at once.
    /// </summary>
    private void RemoveResident(Entity<SettlementResidentComponent> pop, bool removeComponent)
    {
        if (!_settlements.TryComp(pop.Comp.Settlement, out var settlement))
            return;

        var uid = pop.Comp.Settlement;

        if (!settlement.Residents.Remove(pop))
            return;

        settlement.Away.Remove(pop);
        settlement.ResidentCount = settlement.Residents.Count;
        Dirty(uid, settlement);
        _ownership.RemoveOwnership(pop.Owner, owner: uid);

        if (removeComponent)
            RemComp<SettlementResidentComponent>(pop);

        var removed = new[] { pop.Owner };
        RaiseLocalEvent(uid, new SettlementResidentsChangedEvent(uid, Array.Empty<EntityUid>(), removed), true);
        MarkDirty(settlement.Player);
    }

    private void UpdatePlayer(Entity<SettlementTrackerComponent, RimFortressPlayerComponent> player)
    {
        var (uid, tracker, comp) = player;
        var now = _timing.CurTime;

        tracker.NextUpdate = now + tracker.UpdateInterval;
        tracker.SettlementsDirty = false;

        if (!TryGetGrid(uid, out var grid))
            return;

        var radius = tracker.Radius;

        CollectPoints(uid, comp, tracker, grid);

        var groups = BuildGroups(tracker, radius);
        var old = FindSettlements(uid, true);

        Match(old, groups, tracker);

        // Where a group has been cut off from is found before the settlements change.
        var origins = new Dictionary<Group, EntityUid?>();

        foreach (var group in groups)
        {
            if (group.Settlement == null)
                origins[group] = FindOrigin(old, group);
        }

        // The pops that are in the group of a settlement are its (or another's) residents at once. The pops of a group that has no
        // settlement yet are not: they stay in the settlement that they are leaving for LeaveDelay, and only then they are free to
        // make a settlement of their own.
        _clusteredPops.Clear();

        foreach (var group in groups)
        {
            if (group.Settlement != null)
                _clusteredPops.UnionWith(group.Pops);
        }

        // The settlements that found their groups.
        foreach (var group in groups)
        {
            if (group.Settlement is not { } settlement)
                continue;

            ApplyGroup(settlement, group, tracker, radius, grid, now);
        }

        // The groups that did not find a settlement, such as the ones that are cut off from the others.
        foreach (var group in groups)
        {
            if (group.Settlement != null || !IsFree(group))
                continue;

            var origin = origins[group];
            var created = Create(uid, group, tracker, radius, grid, now);
            group.Settlement = created;

            if (origin != null)
                RaiseLocalEvent(origin.Value, new SettlementSplitEvent(origin.Value, created.Owner), true);
        }

        // The settlements that did not find a group: they were absorbed, or they wait to see if their group returns.
        foreach (var settlement in old)
        {
            if (Terminating(settlement.Owner) || groups.Exists(g => g.Settlement?.Owner == settlement.Owner))
                continue;

            var survivor = FindSurvivor(settlement, groups);

            if (survivor != null)
            {
                MergeInto(survivor.Value, settlement);
                continue;
            }

            Wait(settlement, tracker, now);
        }

        Adopt(uid, tracker);
        OwnLoners(comp, uid, tracker);
    }

    /// <summary>
    /// The pops that live in no settlement are the player's, with what they carry: they are left without an owner otherwise, and
    /// what the pops search for is what is owned by their owners.
    /// </summary>
    /// <summary>
    /// A group can make a settlement if none of its pops is a resident of a settlement any more: they have not been here before, or
    /// they have been away from their settlement for longer than <see cref="SettlementTrackerComponent.LeaveDelay"/>.
    /// </summary>
    private bool IsFree(Group group)
    {
        foreach (var pop in group.Pops)
        {
            if (_residents.TryComp(pop, out var resident) && _settlements.HasComp(resident.Settlement))
                return false;
        }

        return true;
    }

    private void OwnLoners(RimFortressPlayerComponent comp, EntityUid player, SettlementTrackerComponent tracker)
    {
        foreach (var pop in comp.Pops)
        {
            if (TerminatingOrDeleted(pop)
                || _mobState.IsDead(pop)
                || _residents.HasComp(pop)
                || _ownership.HasOwner(pop, player))
                continue;

            _ownership.AddOwnershipRecursively(player, pop, tracker.ResidentItems);
        }
    }

    /// <summary>
    /// Gives what waits with the player (a zone that was marked out where there was no settlement, the things of a settlement that
    /// is gone) to the settlement that is near it, if there is one.
    /// </summary>
    private void Adopt(EntityUid player, SettlementTrackerComponent tracker)
    {
        var waiting = _ownership.GetOwned(player);

        if (waiting.Count == 0)
            return;

        var settlements = FindSettlements(player, false);

        if (settlements.Count == 0)
            return;

        foreach (var uid in waiting.ToArray())
        {
            // Pops and what they carry are not adopted by the place they are at: they belong to a settlement by being its
            // residents. What is adopted lies on the ground of the world.
            if (_settlements.HasComp(uid)
                || TerminatingOrDeleted(uid)
                || HasComp<MobStateComponent>(uid)
                || !_xformQuery.TryComp(uid, out var xform)
                || !HasComp<MapGridComponent>(xform.ParentUid))
                continue;

            if (FindSettlementAt(settlements, uid, tracker.Radius) is not { } settlement)
                continue;

            _ownership.RemoveOwnership(uid, owner: player);
            _ownership.AddOwnership(uid, owner: settlement);
        }
    }

    private Entity<SettlementComponent>? FindSettlementAt(
        List<Entity<SettlementComponent>> settlements,
        EntityUid uid,
        float radius)
        => _xformQuery.TryComp(uid, out var xform)
            ? FindSettlementAt(settlements, xform.Coordinates, radius)
            : null;

    private Entity<SettlementComponent>? FindSettlementAt(
        List<Entity<SettlementComponent>> settlements,
        EntityCoordinates coordinates,
        float radius)
    {
        Entity<SettlementComponent>? best = null;
        var bestDistance = radius;

        foreach (var settlement in settlements)
        {
            if (!coordinates.TryDistance(EntityManager, Transform(settlement.Owner).Coordinates, out var distance)
                || distance > bestDistance)
                continue;

            best = settlement;
            bestDistance = distance;
        }

        return best;
    }

    /// <inheritdoc/>
    public override Entity<SettlementComponent>? Claim(EntityUid player, EntityUid uid, EntityCoordinates coordinates)
    {
        var radius = _trackers.TryComp(player, out var tracker) ? tracker.Radius : 0f;
        var settlement = FindSettlementAt(GetPlayerSettlements(player), coordinates, radius);

        _ownership.AddOwnership(uid, owner: settlement?.Owner ?? player);
        return settlement;
    }

    /// <summary>
    /// Finds the pops of the player that are alive, and the things it owns that join the groups.
    /// </summary>
    private void CollectPoints(
        EntityUid player,
        RimFortressPlayerComponent comp,
        SettlementTrackerComponent tracker,
        EntityUid grid)
    {
        _points.Clear();
        _pointEntities.Clear();
        _pointIsPop.Clear();
        _popPositions.Clear();
        _alivePops.Clear();

        var allPops = new HashSet<EntityUid>(comp.Pops);

        foreach (var pop in allPops)
        {
            if (TerminatingOrDeleted(pop) || _mobState.IsDead(pop))
                continue;

            _alivePops.Add(pop);

            if (!_xformQuery.TryComp(pop, out var xform) || xform.ParentUid != grid)
                continue;

            _popPositions[pop] = xform.LocalPosition;
            AddPoint(pop, xform.LocalPosition, true);
        }

        if (!tracker.CountOwned)
            return;

        // What the player's settlements own, and what waits with the player for a settlement.
        var candidates = new HashSet<EntityUid>(_ownership.GetOwned(player));

        foreach (var settlement in FindSettlements(player, true))
        {
            candidates.UnionWith(_ownership.GetOwned(settlement));
        }

        foreach (var owned in candidates)
        {
            if (allPops.Contains(owned)
                || _settlements.HasComp(owned)
                || TerminatingOrDeleted(owned)
                || !_xformQuery.TryComp(owned, out var xform)
                || xform.ParentUid != grid
                || !_whitelist.CheckBoth(owned, tracker.OwnedBlacklist, tracker.OwnedWhitelist))
                continue;

            AddPoint(owned, xform.LocalPosition, false);
        }
    }

    private void AddPoint(EntityUid uid, Vector2 position, bool pop)
    {
        _points.Add(position);
        _pointEntities.Add(uid);
        _pointIsPop.Add(pop);
    }

    /// <summary>
    /// Divides the points into groups, and keeps the ones that have enough pops.
    /// </summary>
    private List<Group> BuildGroups(SettlementTrackerComponent tracker, float radius)
    {
        var groups = new List<Group>();

        foreach (var indices in SettlementClustering.Cluster(_points, radius))
        {
            var group = new Group();
            var center = Vector2.Zero;
            var weight = 0f;
            var popCenter = Vector2.Zero;

            foreach (var i in indices)
            {
                if (_pointIsPop[i])
                {
                    group.Pops.Add(_pointEntities[i]);
                    popCenter += _points[i];
                    center += _points[i] * tracker.PopWeight;
                    weight += tracker.PopWeight;
                }
                else
                {
                    group.Owned++;
                    center += _points[i] * tracker.OwnedWeight;
                    weight += tracker.OwnedWeight;
                }
            }

            if (group.Pops.Count == 0)
                continue;

            // If nothing weighs anything, the pops are the ones that matter.
            group.Center = weight > 0f ? center / weight : popCenter / group.Pops.Count;

            foreach (var i in indices)
            {
                group.Radius = MathF.Max(group.Radius, Vector2.Distance(_points[i], group.Center));
            }

            groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// Matches the settlements to the groups: the pairs with the most pops in common go first.
    /// </summary>
    private static void Match(List<Entity<SettlementComponent>> old, List<Group> groups, SettlementTrackerComponent tracker)
    {
        var pairs = new List<(Entity<SettlementComponent> Settlement, Group Group, int Common)>();

        foreach (var settlement in old)
        {
            foreach (var group in groups)
            {
                var common = CountCommon(settlement.Comp.Residents, group.Pops);

                if (common == 0)
                    continue;

                var needed = Math.Max(
                    1,
                    (int) MathF.Ceiling(tracker.MatchThreshold * Math.Min(settlement.Comp.Residents.Count, group.Pops.Count)));

                if (common >= needed)
                    pairs.Add((settlement, group, common));
            }
        }

        // The older settlement wins when it is a tie.
        pairs.Sort((a, b) =>
        {
            var byCommon = b.Common.CompareTo(a.Common);
            return byCommon != 0 ? byCommon : a.Settlement.Comp.Created.CompareTo(b.Settlement.Comp.Created);
        });

        var taken = new HashSet<EntityUid>();

        foreach (var (settlement, group, _) in pairs)
        {
            if (group.Settlement != null || !taken.Add(settlement.Owner))
                continue;

            group.Settlement = settlement;
        }
    }

    private static int CountCommon(HashSet<EntityUid> residents, HashSet<EntityUid> pops)
    {
        var common = 0;

        foreach (var resident in residents)
        {
            if (pops.Contains(resident))
                common++;
        }

        return common;
    }

    /// <summary>
    /// Finds the settlement a group has been divided from: the one that has the most of its pops, if it lives in another
    /// group now.
    /// </summary>
    private static EntityUid? FindOrigin(List<Entity<SettlementComponent>> old, Group group)
    {
        EntityUid? origin = null;
        var best = 0;

        foreach (var settlement in old)
        {
            var common = CountCommon(settlement.Comp.Residents, group.Pops);

            if (common > best)
            {
                best = common;
                origin = settlement.Owner;
            }
        }

        return origin;
    }

    /// <summary>
    /// Finds the settlement that has absorbed this one: the one of the group that has the most of its pops.
    /// </summary>
    private static Entity<SettlementComponent>? FindSurvivor(Entity<SettlementComponent> settlement, List<Group> groups)
    {
        Entity<SettlementComponent>? survivor = null;
        var best = 0;

        foreach (var group in groups)
        {
            if (group.Settlement is not { } other)
                continue;

            var common = CountCommon(settlement.Comp.Residents, group.Pops);

            if (common > best)
            {
                best = common;
                survivor = other;
            }
        }

        return survivor;
    }

    private Entity<SettlementComponent> Create(
        EntityUid player,
        Group group,
        SettlementTrackerComponent tracker,
        float radius,
        EntityUid grid,
        TimeSpan now)
    {
        var uid = Spawn(SettlementPrototype, new EntityCoordinates(grid, group.Center));
        var settlement = Comp<SettlementComponent>(uid);

        settlement.Player = player;
        settlement.Created = now;
        Dirty(uid, settlement);

        // The player owns its settlements, and the settlement owns what is in it.
        _ownership.AddOwnership(uid, owner: player);

        // The events of a settlement are waited for from the moment it is there.
        if (TryComp(uid, out SettlementEventsComponent? events))
            events.LastEventTime = _ticker.RoundDuration();

        ApplyGroup((uid, settlement), group, tracker, radius, grid, now);
        RaiseLocalEvent(uid, new SettlementCreatedEvent(uid, player), true);

        return (uid, settlement);
    }

    /// <summary>
    /// Makes a settlement what its group is: the residents, the size and the place.
    /// </summary>
    private void ApplyGroup(
        Entity<SettlementComponent> settlement,
        Group group,
        SettlementTrackerComponent tracker,
        float radius,
        EntityUid grid,
        TimeSpan now)
    {
        var comp = settlement.Comp;
        var residents = new HashSet<EntityUid>(group.Pops);

        // The residents that have left their group stay for a while.
        foreach (var resident in comp.Residents)
        {
            if (group.Pops.Contains(resident))
            {
                comp.Away.Remove(resident);
                continue;
            }

            if (!_alivePops.Contains(resident) || _clusteredPops.Contains(resident))
            {
                comp.Away.Remove(resident);
                continue;
            }

            if (!comp.Away.TryGetValue(resident, out var since))
                comp.Away[resident] = since = now;

            if (now - since > tracker.LeaveDelay)
            {
                comp.Away.Remove(resident);
                continue;
            }

            residents.Add(resident);
        }

        SetResidents(settlement, residents);

        var changed = false;
        changed |= Set(ref comp.OwnedCount, group.Owned);
        changed |= Set(ref comp.Radius, group.Radius);
        changed |= Set(ref comp.Dissolving, false);
        comp.DissolveAt = null;

        if (changed)
            Dirty(settlement.Owner, comp);

        var xform = Transform(settlement.Owner);

        if (xform.ParentUid != grid || Vector2.Distance(xform.LocalPosition, group.Center) > tracker.MoveThreshold)
            _xform.SetCoordinates(settlement.Owner, new EntityCoordinates(grid, group.Center));
    }

    /// <summary>
    /// Sets the pops that live in a settlement.
    /// </summary>
    private void SetResidents(Entity<SettlementComponent> settlement, HashSet<EntityUid> residents)
    {
        var comp = settlement.Comp;
        List<EntityUid>? added = null;
        List<EntityUid>? removed = null;

        foreach (var resident in comp.Residents)
        {
            if (!residents.Contains(resident))
                (removed ??= new List<EntityUid>()).Add(resident);
        }

        foreach (var resident in residents)
        {
            if (!comp.Residents.Contains(resident))
                (added ??= new List<EntityUid>()).Add(resident);
        }

        if (removed != null)
        {
            foreach (var resident in removed)
            {
                comp.Residents.Remove(resident);
                comp.Away.Remove(resident);

                // The pop goes with what it carries, to the player (see OwnLoners) if it is alive and is still the player's.
                if (!TerminatingOrDeleted(resident))
                    _ownership.RemoveOwnershipRecursively(settlement.Owner, resident);

                // A pop that has moved to another settlement is not to be taken from it.
                if (!TerminatingOrDeleted(resident)
                    && _residents.TryComp(resident, out var residentComp)
                    && residentComp.Settlement == settlement.Owner)
                    RemComp<SettlementResidentComponent>(resident);
            }
        }

        if (added != null)
        {
            var items = _trackers.TryComp(comp.Player, out var tracker) ? tracker.ResidentItems : null;

            foreach (var resident in added)
            {
                comp.Residents.Add(resident);
                EnsureComp<SettlementResidentComponent>(resident).Settlement = settlement.Owner;

                // The settlement owns the pop and what it carries, and the player does not any more.
                _ownership.RemoveOwnershipRecursively(comp.Player, resident);
                _ownership.AddOwnershipRecursively(settlement.Owner, resident, items);
            }
        }

        if (Set(ref comp.ResidentCount, comp.Residents.Count))
            Dirty(settlement.Owner, comp);

        if (added != null || removed != null)
        {
            RaiseLocalEvent(
                settlement.Owner,
                new SettlementResidentsChangedEvent(
                    settlement.Owner,
                    (IReadOnlyCollection<EntityUid>?) added ?? Array.Empty<EntityUid>(),
                    (IReadOnlyCollection<EntityUid>?) removed ?? Array.Empty<EntityUid>()),
                true);
        }
    }

    /// <summary>
    /// A settlement is absorbed by another one: its residents are the survivor's by now.
    /// </summary>
    private void MergeInto(Entity<SettlementComponent> survivor, Entity<SettlementComponent> absorbed)
    {
        RaiseLocalEvent(
            survivor.Owner,
            new SettlementsMergedEvent(survivor.Owner, new[] { absorbed.Owner }),
            true);

        // Whatever the absorbed settlement owned is the survivor's.
        foreach (var owned in _ownership.GetOwned(absorbed.Owner).ToArray())
        {
            if (!_settlements.HasComp(owned) && !TerminatingOrDeleted(owned))
                _ownership.AddOwnership(owned, owner: survivor.Owner);
        }

        Dissolve(absorbed);
    }

    /// <summary>
    /// A settlement has no group: it waits to see if the group returns, and is disbanded if it does not.
    /// </summary>
    private void Wait(Entity<SettlementComponent> settlement, SettlementTrackerComponent tracker, TimeSpan now)
    {
        var comp = settlement.Comp;

        // The pops that are dead or gone are not residents.
        var residents = new HashSet<EntityUid>(comp.Residents);
        residents.IntersectWith(_alivePops);
        SetResidents(settlement, residents);

        if (comp.DissolveAt == null)
        {
            comp.Dissolving = true;
            comp.DissolveAt = now + tracker.GracePeriod;
            Dirty(settlement.Owner, comp);
        }

        if (now >= comp.DissolveAt)
            Dissolve(settlement);
    }

    /// <summary>
    /// Disbands a settlement and deletes its entity.
    /// </summary>
    private void Dissolve(Entity<SettlementComponent> settlement)
    {
        var comp = settlement.Comp;

        RaiseLocalEvent(settlement.Owner, new SettlementDissolvedEvent(settlement.Owner, comp.Player), true);

        comp.Dissolving = true;
        Dirty(settlement.Owner, comp);

        SetResidents(settlement, new HashSet<EntityUid>());

        // What the settlement owned waits with the player for another settlement, see Adopt.
        if (!TerminatingOrDeleted(comp.Player))
        {
            foreach (var owned in _ownership.GetOwned(settlement.Owner).ToArray())
            {
                if (!_settlements.HasComp(owned) && !TerminatingOrDeleted(owned))
                    _ownership.AddOwnership(owned, owner: comp.Player);
            }
        }

        QueueDel(settlement.Owner);
    }

    private bool TryGetGrid(EntityUid player, out EntityUid grid)
    {
        var worlds = EntityQueryEnumerator<WorldMapComponent>();

        if (worlds.MoveNext(out var world, out _))
        {
            grid = world;
            return true;
        }

        grid = Transform(player).ParentUid;
        return grid.IsValid();
    }

    private static bool Set<T>(ref T field, T value) where T : IEquatable<T>
    {
        if (field.Equals(value))
            return false;

        field = value;
        return true;
    }
}
