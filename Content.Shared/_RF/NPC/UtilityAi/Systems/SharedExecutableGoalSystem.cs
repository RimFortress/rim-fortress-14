using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.Selection.Systems;
using Content.Shared.Maps;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.UtilityAi.Systems;

/// <summary>
/// A system that allows the player to set Utility AI goals for NPCs.
/// </summary>
public abstract class SharedExecutableGoalSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly EntityWhitelistSystem Whitelist = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedUtilityAiSystem _utilityAi = default!;
    [Dependency] private readonly SharedSelectionSystem _selection = default!;
    [Dependency] private readonly SharedGoapSystem _goap = default!;

    [Dependency] protected readonly EntityQuery<GoapComponent> GoapQuery = default!;
    [Dependency] protected readonly EntityQuery<ControllableNpcComponent> ControllableQuery = default!;
    [Dependency] protected readonly EntityQuery<NpcControlComponent> ControlQuery = default!;
    [Dependency] protected readonly EntityQuery<PassiveGoalTargetComponent> PassiveGoalQuery = default!;

    protected readonly Dictionary<ProtoId<UtilityAiGoalPrototype>, HashSet<ProtoId<ExecutableGoalPrototype>>> Executables = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeNetworkEvent<SetGoalRequest>(OnGoalRequest);
        SubscribeNetworkEvent<PassiveGoalRequest>(OnPassiveGoalRequest);
        SubscribeNetworkEvent<PassiveGoalRemoveRequest>(OnPassiveGoalRemoveRequest);
        SubscribeNetworkEvent<SetGoalMessage>(OnSetGoalMessage);

        Proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<ExecutableGoalPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    #region Events Handle

    private void ReloadPrototypes()
    {
        Executables.Clear();

        foreach (var proto in Proto.EnumeratePrototypes<ExecutableGoalPrototype>())
        {
            if (!Executables.TryAdd(proto.Goal, new() { proto }))
                Executables[proto.Goal].Add(proto);
        }
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!TryComp(ev.User, out NpcControlComponent? control))
            return;

        var tasks = new Dictionary<ExecutableGoalPrototype, List<EntityUid>>();
        var prototypes = control.Goals.Select(Proto.Index).ToList();

        foreach (var entity in _selection.SelectedEntities(ev.User))
        {
            if (!CanControl(ev.User, entity)
                || FindSatisfiedGoals(entity, ev.Target, prototypes, ExecutableGoalType.Verb) is not { } suitable)
                continue;

            foreach (var task in suitable)
            {
                if (!tasks.TryAdd(task, new()))
                    tasks[task].Add(entity);
                else
                    tasks[task] = new() { entity };
            }
        }

        foreach (var (goal, entities) in tasks)
        {
            ev.Verbs.Add(new()
            {
                Text = Loc.GetString(Proto.Index(goal.Goal).Name),
                Icon = goal.VerbIcon is { } icon ? new SpriteSpecifier.Texture(icon) : null,
                Category = VerbCategory.NpcTask,
                CloseMenu = true,
                Act = () =>
                {
                    foreach (var uid in entities)
                    {
                        TrySetGoal(uid, goal, target: ev.Target);
                    }
                },
            });
        }
    }

    private void OnGoalRequest(SetGoalRequest request, EntitySessionEventArgs args)
    {
        if (!Timing.IsFirstTimePredicted
            || args.SenderSession.AttachedEntity is not { } requester
            || !ControlQuery.TryComp(requester, out var control))
            return;

        var entities = request.Entities
            .Select(GetEntity)
            .Where(x => CanControl(requester, x))
            .ToList();
        var targetCoords = GetCoordinates(request.TargetCoordinates);
        var allGoals = control.Goals.Select(t => Proto.Index(t)).ToList();
        var previousTargets = new List<TileRef>();
        ExecutableGoalPrototype? goal = null;

        foreach (var entity in entities)
        {
            if (FindSatisfiedGoals(entity, null, allGoals, ExecutableGoalType.Place) is not { } satisfied)
                continue;

            goal = satisfied[0];
            break;
        }

        if (goal == null)
            return;

        foreach (var uid in entities)
        {
            if (previousTargets.Count == 0)
            {
                if (!TryComp(targetCoords.EntityId, out MapGridComponent? grid)
                    || !_map.TryGetTileRef(targetCoords.EntityId, grid, targetCoords, out var tileRef)
                    || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                    break;

                previousTargets.Add(tileRef);
                TrySetGoal(uid, goal, targetCoords: _turf.GetTileCenter(tileRef));
                continue;
            }

            if (GetNeighborTile(previousTargets) is not { } tile)
                continue;

            previousTargets.Add(tile);

            var tileCenter = _turf.GetTileCenter(tile);
            TrySetGoal(uid, goal, targetCoords: tileCenter);
        }
    }

    private void OnPassiveGoalRequest(PassiveGoalRequest request, EntitySessionEventArgs args)
    {
        if (!Timing.IsFirstTimePredicted || args.SenderSession.AttachedEntity is not { } uid)
            return;

        SetPassiveTarget(uid, request.GoalId, GetEntityList(request.Entities));
    }

    private void OnPassiveGoalRemoveRequest(PassiveGoalRemoveRequest request, EntitySessionEventArgs args)
    {
        if (!Timing.IsFirstTimePredicted
            || !ControlQuery.HasComp(args.SenderSession.AttachedEntity))
            return;

        foreach (var uid in GetEntityList(request.Entities))
        {
            if (!PassiveGoalQuery.TryComp(uid, out var comp)
                || comp.User != args.SenderSession.AttachedEntity)
                continue;

            RemComp(uid, comp);
        }
    }

    private void OnSetGoalMessage(SetGoalMessage msg, EntitySessionEventArgs args)
    {
        if (!GoapQuery.TryComp(GetEntity(msg.Agent), out var comp)
            || !Proto.Resolve(msg.Goal, out var goal))
            return;

        if (GetEntity(msg.Target) is { } uid)
            comp.State.SetValue(goal.TargetKey, uid);

        if (GetCoordinates(msg.TargetCoordinates) is { } coords)
            comp.State.SetValue(goal.TargetCoordinatesKey, coords);
    }

    #endregion

    /// <summary>
    /// Finds the suitable goals for the target from the goals list.
    /// </summary>
    protected List<ExecutableGoalPrototype>? FindSatisfiedGoals(
        Entity<GoapComponent?> ent,
        EntityUid? target,
        List<ExecutableGoalPrototype> goals,
        ExecutableGoalType? type = null)
    {
        List<ExecutableGoalPrototype>? zeroGoals = null;
        List<ExecutableGoalPrototype>? satisfied = null;

        foreach (var proto in goals)
        {
            if (type != null && !proto.TaskType.HasFlag(type))
                continue;

            if (proto.TaskType.HasFlag(ExecutableGoalType.Place))
            {
                zeroGoals ??= new();
                zeroGoals.Add(proto);
            }

            if (target == null
                || ent == target && !proto.SelfPerform
                || !CheckGoalStart(ent, proto, target.Value))
                continue;

            satisfied ??= new();
            satisfied.Add(proto);
        }

        return satisfied ?? zeroGoals;
    }

    private bool CheckGoalStart(Entity<GoapComponent?> ent, ExecutableGoalPrototype goal, EntityUid target)
    {
        if (!Whitelist.IsWhitelistPassOrNull(goal.TargetWhitelist, target)
            || GoalPerformersCount(goal, target) >= goal.MaxPerformers
            || !GoapQuery.Resolve(ent, ref ent.Comp))
            return false;

        // Set a temporary variables in GoapState to check conditions.
        ent.Comp.State.SetValue(goal.TargetKey, target);
        var result = _utilityAi.ConditionsMet(ent, goal.Goal);
        ent.Comp.State.Remove(goal.TargetKey);
        return result;
    }

    /// <summary>
    /// Tries to set a new goal for an NPC
    /// </summary>
    /// <returns>True, if the goal is successfully set</returns>
    [PublicAPI]
    public bool TrySetGoal(
        Entity<GoapComponent?, UtilityAiComponent?, ControllableNpcComponent?> ent,
        ProtoId<ExecutableGoalPrototype> protoId,
        EntityUid? target = null,
        EntityCoordinates? targetCoords = null,
        Dictionary<string, object>? additionalKeys = null)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, ref ent.Comp3)
            || !Proto.TryIndex(protoId, out var proto))
            return false;

        if (!proto.TaskType.HasFlag(ExecutableGoalType.Place)
            && (target == null || !CheckGoalStart(new(ent, ent.Comp1), proto, target.Value)))
            return false;

        if (proto.TaskType.HasFlag(ExecutableGoalType.Place) && targetCoords == null)
            return false;

        SetGoal(
            new(ent, ent.Comp1, ent.Comp2, ent.Comp3),
            proto,
            target,
            targetCoords,
            additionalKeys: additionalKeys);
        return true;
    }

    /// <summary>
    /// Creates a new goal for the NPC.
    /// </summary>
    private void SetGoal(
        Entity<GoapComponent, UtilityAiComponent, ControllableNpcComponent> ent,
        ExecutableGoalPrototype proto,
        EntityUid? target = null,
        EntityCoordinates? coords = null,
        Dictionary<string, object>? additionalKeys = null)
    {
        var goap = ent.Comp1;
        var utilityAi = ent.Comp2;

        if (goap.Plan != null)
            _goap.PlanShutdown(new(ent, goap), GoapPlanFinishReason.Interrupted);

        DebugTools.Assert(proto.TaskType.HasFlag(ExecutableGoalType.Place) || coords == null);
        DebugTools.Assert(!proto.TaskType.HasFlag(ExecutableGoalType.Place) || target == null);

        if (target != null)
            goap.State.SetValue(proto.TargetKey, target.Value);
        if (coords != null)
            goap.State.SetValue(proto.TargetCoordinatesKey, coords.Value);

        if (_net.IsServer)
        {
            RaiseNetworkEvent(new SetGoalMessage
            {
                Agent = GetNetEntity(ent),
                Goal = proto,
                Target = GetNetEntity(target),
                TargetCoordinates = GetNetCoordinates(coords),
            });
        }

        if (additionalKeys != null)
        {
            foreach (var (id, obj) in additionalKeys)
            {
                goap.State.SetValue(id, obj);
            }
        }

        _utilityAi.SetGoal((ent, utilityAi, goap), proto.Goal);
    }

    /// <summary>
    /// Returns the first free neighboring tile for the tile list
    /// </summary>
    protected TileRef? GetNeighborTile(List<TileRef> tiles)
    {
        var directions = new[] { Vector2i.Left, Vector2i.Right, Vector2i.Up, Vector2i.Down };
        var indicates = tiles.Select(tile => tile.GridIndices).ToList();

        foreach (var tile in tiles)
        {
            foreach (var direction in directions)
            {
                var offsetCoords = tile.GridIndices + direction;

                if (indicates.Contains(offsetCoords))
                    continue;

                if (TryComp(tile.GridUid, out MapGridComponent? grid)
                    && _map.TryGetTileRef(tile.GridUid, grid, offsetCoords, out var tileRef)
                    && !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
                    && !_turf.IsSpace(tileRef))
                    return tileRef;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if the user can control this NPC
    /// </summary>
    [PublicAPI]
    public bool CanControl(EntityUid user, EntityUid entity)
        => ControllableQuery.TryComp(entity, out var control)
            && HasComp<ActiveNPCComponent>(entity)
            && control.CanControl.Contains(user);

    /// <inheritdoc cref="CanControl(EntityUid, EntityUid)"/>
    [PublicAPI]
    public bool CanControl(ICommonSession user, EntityUid entity)
        => user.AttachedEntity is { } uid && CanControl(uid, entity);

    /// <summary>
    /// Returns a list of all entities that can be controlled by this user
    /// </summary>
    [PublicAPI]
    public List<EntityUid> ControllableEntities(EntityUid user)
    {
        var uids = new List<EntityUid>();
        var query = EntityQueryEnumerator<ControllableNpcComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.CanControl.Contains(user))
                uids.Add(uid);
        }

        return uids;
    }

    /// <summary>
    /// Counts the number of performers of goal on the given target.
    /// </summary>
    [PublicAPI]
    public int GoalPerformersCount(ProtoId<ExecutableGoalPrototype> goal, EntityUid target)
    {
        if (!Proto.Resolve(goal, out var proto))
            return 0;

        var count = 0;
        var enumerator = EntityQueryEnumerator<UtilityAiComponent, GoapComponent>();

        while (enumerator.MoveNext(out var comp, out var goap))
        {
            if (comp.CurrentGoal == proto.Goal
                && goap.State.TryGetValue(proto.TargetKey, out var uid)
                && uid == target)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Give the user access to control this NPC
    /// </summary>
    [PublicAPI]
    public void AddControl(EntityUid user, EntityUid uid)
    {
        var control = EnsureComp<NpcControlComponent>(user);
        var comp = EnsureComp<ControllableNpcComponent>(uid);
        comp.CanControl.Add(user);
        RaiseLocalEvent(uid, new NpcControllerAdded(user));
        Dirty(user, control);
        Dirty(uid, comp);
    }

    /// <summary>
    /// Remove the user access to control this NPC.
    /// </summary>
    [PublicAPI]
    public bool RemoveControl(EntityUid user, Entity<ControllableNpcComponent?> uid)
    {
        if (!Resolve(uid, ref uid.Comp) || !uid.Comp.CanControl.Remove(user))
            return false;

        Dirty(uid);
        return true;
    }

    /// <summary>
    /// Allows the player to issue the given goal.
    /// </summary>
    [PublicAPI]
    public void AddAllowedGoal(Entity<NpcControlComponent?> user, ProtoId<ExecutableGoalPrototype> proto)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        user.Comp.Goals.Add(proto);
        Dirty(user);
    }

    /// <summary>
    /// Forbids the player from issuing the given goal.
    /// </summary>
    [PublicAPI]
    public void RemoveAllowedGoal(Entity<NpcControlComponent?> user, ProtoId<ExecutableGoalPrototype> proto)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        user.Comp.Goals.Remove(proto);
        Dirty(user);
    }

    /// <summary>
    /// Creates a passive target for a Utility AI goal.
    /// </summary>
    /// <param name="user">The user who issues the target.</param>
    /// <param name="protoId">Goal prototype.</param>
    /// <param name="uid">An entity that will become a passive target.</param>
    [PublicAPI]
    public void SetPassiveTarget(
        Entity<NpcControlComponent?> user,
        ProtoId<ExecutableGoalPrototype> protoId,
        EntityUid uid)
        => SetPassiveTarget(user, protoId, new List<EntityUid> { uid });

    // It's fucking long
    /// <inheritdoc cref="SetPassiveTarget(Robust.Shared.GameObjects.Entity{Content.Shared._RF.NPC.Components.NpcControlComponent?},Robust.Shared.Prototypes.ProtoId{Content.Shared._RF.NPC.UtilityAi.Prototypes.ExecutableGoalPrototype},Robust.Shared.GameObjects.EntityUid)"/>
    [PublicAPI]
    public void SetPassiveTarget(
        Entity<NpcControlComponent?> user,
        ProtoId<ExecutableGoalPrototype> protoId,
        List<EntityUid> entities)
    {
        if (!Resolve(user, ref user.Comp)
            || !user.Comp.Goals.Contains(protoId)
            || !Proto.Resolve(protoId, out var proto)
            || !proto.TaskType.HasFlag(ExecutableGoalType.Passive))
            return;

        foreach (var uid in entities)
        {
            if (PassiveGoalQuery.TryComp(uid, out var task) && task.Goal == proto)
                continue;

            if (!Whitelist.IsWhitelistPassOrNull(proto.TargetWhitelist, uid)
                || GoalPerformersCount(proto, uid) >= proto.MaxPerformers)
                continue;

            var comp = EnsureComp<PassiveGoalTargetComponent>(uid);
            comp.Goal = proto.ID;
            comp.User = user;
            Dirty(uid, comp);
        }
    }

    /// <summary>
    /// Returns the current target of the Utility AI goal, if any.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetTarget(Entity<UtilityAiComponent?> ent, [NotNullWhen(true)] out EntityUid? target)
    {
        target = null;

        if (!_utilityAi.TryGetCurrentGoal(ent, out var current)
            || !Executables.TryGetValue(current.Value, out var goals)
            || !GoapQuery.TryComp(ent, out var goap))
            return false;

        foreach (var goal in goals)
        {
            if (!Proto.Resolve(goal, out var proto)
                || !goap.State.TryGetValue(proto.TargetKey, out var uid))
                continue;

            target = uid;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the current target coorinates of the Utility AI goal, if any.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetTargetCoordinates(
        Entity<UtilityAiComponent?> ent,
        [NotNullWhen(true)] out EntityCoordinates? coords)
    {
        coords = null;

        if (!_utilityAi.TryGetCurrentGoal(ent, out var current)
            || !Executables.TryGetValue(current.Value, out var goals)
            || !GoapQuery.TryComp(ent, out var goap))
            return false;

        foreach (var goal in goals)
        {
            if (!Proto.Resolve(goal, out var proto))
                continue;

            if (proto.TaskType.HasFlag(ExecutableGoalType.Place)
                && goap.State.TryGetValue(proto.TargetCoordinatesKey, out var result))
            {
                coords = result;
                return true;
            }

            if (goap.State.TryGetValue(proto.TargetKey, out var uid))
            {
                coords = Transform(uid).Coordinates;
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Invoked when a user who can control this entity is added.
/// </summary>
/// <param name="User">User entity.</param>
[PublicAPI]
public record struct NpcControllerAdded(EntityUid User);

[Serializable, NetSerializable]
public sealed class SetGoalRequest : EntityEventArgs
{
    public List<NetEntity> Entities { get; set; } = new();
    public NetCoordinates TargetCoordinates;
}

[Serializable, NetSerializable]
public sealed class SetGoalMessage : EntityEventArgs
{
    public NetEntity Agent;
    public ProtoId<ExecutableGoalPrototype>? Goal;
    public NetEntity? Target;
    public NetCoordinates? TargetCoordinates;
}

[Serializable, NetSerializable]
public sealed class PassiveGoalRequest(ProtoId<ExecutableGoalPrototype> goalId, List<NetEntity> entities) : EntityEventArgs
{
    public ProtoId<ExecutableGoalPrototype> GoalId { get; set; } = goalId;
    public List<NetEntity> Entities { get; set; } = entities;
}

[Serializable, NetSerializable]
public sealed class PassiveGoalRemoveRequest(List<NetEntity> entities) : EntityEventArgs
{
    public List<NetEntity> Entities { get; set; } = entities;
}
