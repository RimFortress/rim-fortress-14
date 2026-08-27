using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.UtilityAi.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Executable.Systems;

public partial class SharedExecutableGoalSystem
{
    /// <summary>
    /// Adds a goal to the agent's execution queue.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="protoId">Goal prototype.</param>
    /// <param name="user">A user attempting to add a goal to the queue.</param>
    /// <param name="target">Goal target, if any.</param>
    /// <param name="targetCoords">Goal target coordinates, if any.</param>
    /// <returns>True, if the task was successfully added to the queue.</returns>
    [PublicAPI]
    public bool TryAddToQueue(
        Entity<UtilityAiComponent?, ControllableNpcComponent?> ent,
        ProtoId<ExecutableGoalPrototype> protoId,
        Entity<NpcControllerComponent?> user,
        EntityUid? target = null,
        EntityCoordinates? targetCoords = null)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2)
            || !Resolve(user, ref user.Comp)
            || !Proto.HasIndex(protoId)
            || !ent.Comp2.Goals.Contains(protoId)
            || !user.Comp.Goals.Contains(protoId)
            || ent.Comp2.Queue.Count >= ent.Comp2.QueueMaxCapacity)
            return false;

        var entry = new ExecutableGoalQueueEntry(protoId,
            GetNetEntity(user),
            GetNetEntity(target),
            GetNetCoordinates(targetCoords));

        if (ent.Comp2.Queue.Contains(entry))
            return false;

        ent.Comp2.Queue.Enqueue(entry);
        DirtyField(ent, ent.Comp2, nameof(ControllableNpcComponent.Queue));
        return true;
    }

    /// <summary>
    /// Clears the entity's goals queue.
    /// </summary>
    [PublicAPI]
    public void ClearQueue(Entity<ControllableNpcComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Queue.Clear();
        DirtyField(ent, nameof(ControllableNpcComponent.Queue));
    }


    /// <summary>
    /// Tries to set a new goal for an NPC
    /// </summary>
    /// <returns>True, if the goal is successfully set</returns>
    [PublicAPI]
    public bool TrySetGoal(
        Entity<GoapComponent?, UtilityAiComponent?, ControllableNpcComponent?> ent,
        ExecutableGoalQueueEntry entry)
        => TrySetGoal(ent, entry.Goal, GetEntity(entry.Target), GetCoordinates(entry.TargetCoordinates));

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

        if (!proto.GoalType.HasFlag(ExecutableGoalType.Place))
        {
            if (target == null || !CheckGoalStart(new(ent, ent.Comp1, ent.Comp3), proto, target.Value))
                return false;
        }
        else
        {
            // Place goals have no target entity to validate via CheckGoalStart, but the NPC
            // still needs to actually be allowed to perform this specific goal - previously
            // this check was skipped entirely for place goals.
            if (targetCoords == null || !ent.Comp3.Goals.Contains(proto))
                return false;
        }

        SetGoal(
            new(ent, ent.Comp1, ent.Comp2, ent.Comp3),
            proto,
            target,
            targetCoords,
            additionalKeys: additionalKeys);
        return true;
    }

    /// <summary>
    /// Checks if the user can control this NPC
    /// </summary>
    /// <param name="user">NPC controller entity.</param>
    /// <param name="entity">NPC entity.</param>
    [PublicAPI]
    public bool CanControl(EntityUid user, EntityUid entity)
        => ControllableQuery.TryComp(entity, out var controllable)
           && ControllerQuery.TryComp(user, out var controller)
           && _activeQuery.HasComp(entity)
           && controllable.CanControl.Contains(user)
           && controller.CanControl.Contains(entity);

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
        var enumerator = EntityQueryEnumerator<UtilityAiComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (comp.CurrentGoal != null
                && (comp.CurrentGoal == proto.Goal || proto.UnionPerformersWith.Contains(comp.CurrentGoal.Value))
                && TryGetTarget(new(uid, comp), out var t)
                && t == target)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Give the user access to control this NPC
    /// </summary>
    [PublicAPI]
    public void AddController(EntityUid user, EntityUid uid)
    {
        var control = EnsureComp<NpcControllerComponent>(user);
        var comp = EnsureComp<ControllableNpcComponent>(uid);
        control.CanControl.Add(uid);
        comp.CanControl.Add(user);
        RaiseLocalEvent(uid, new NpcControllerAdded(user));
        DirtyField(user, control, nameof(NpcControllerComponent.CanControl));
        DirtyField(uid, comp, nameof(ControllableNpcComponent.CanControl));
    }

    /// <summary>
    /// Remove the user access to control this NPC.
    /// </summary>
    [PublicAPI]
    public bool RemoveController(Entity<NpcControllerComponent?> user, Entity<ControllableNpcComponent?> uid)
    {
        if (!Resolve(user, ref user.Comp)
            ||!Resolve(uid, ref uid.Comp)
            || !user.Comp.CanControl.Remove(uid)
            || !uid.Comp.CanControl.Remove(user))
            return false;

        DirtyField(uid, nameof(ControllableNpcComponent.CanControl));
        DirtyField(user, nameof(NpcControllerComponent.CanControl));
        return true;
    }

    /// <summary>
    /// Allows the player to issue the given goal.
    /// </summary>
    [PublicAPI]
    public void AddAllowedGoal(Entity<NpcControllerComponent?> user, ProtoId<ExecutableGoalPrototype> proto)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        user.Comp.Goals.Add(proto);
        DirtyField(user, nameof(NpcControllerComponent.Goals));
    }

    /// <summary>
    /// Forbids the player from issuing the given goal.
    /// </summary>
    [PublicAPI]
    public void RemoveAllowedGoal(Entity<NpcControllerComponent?> user, ProtoId<ExecutableGoalPrototype> proto)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        user.Comp.Goals.Remove(proto);
        DirtyField(user, nameof(NpcControllerComponent.Goals));
    }

    /// <summary>
    /// Creates a passive target for a Utility AI goal.
    /// </summary>
    /// <param name="user">The user who issues the target.</param>
    /// <param name="protoId">Goal prototype.</param>
    /// <param name="uid">An entity that will become a passive target.</param>
    [PublicAPI]
    public void SetPassiveTarget(
        Entity<NpcControllerComponent?> user,
        ProtoId<ExecutableGoalPrototype> protoId,
        EntityUid uid)
        => SetPassiveTarget(user, protoId, new List<EntityUid> { uid });

    // It's fucking long
    /// <inheritdoc cref="SetPassiveTarget(Robust.Shared.GameObjects.Entity{NpcControllerComponent?},Robust.Shared.Prototypes.ProtoId{ExecutableGoalPrototype},Robust.Shared.GameObjects.EntityUid)"/>
    [PublicAPI]
    public void SetPassiveTarget(
        Entity<NpcControllerComponent?> user,
        ProtoId<ExecutableGoalPrototype> protoId,
        List<EntityUid> entities)
    {
        if (!Resolve(user, ref user.Comp)
            || !user.Comp.Goals.Contains(protoId)
            || !Proto.Resolve(protoId, out var proto)
            || !proto.GoalType.HasFlag(ExecutableGoalType.Passive))
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

            var ev = new NpcPassiveGoalSet(protoId, uid, user);
            RaiseLocalEvent(user, ev);
            RaiseLocalEvent(uid, ev, broadcast: true);
        }
    }

    /// <summary>
    /// Removes a passive target for a Utility AI goal.
    /// </summary>
    /// <param name="uid">Passive target to remove.</param>
    [PublicAPI]
    public void RemovePassiveTarget(EntityUid uid) => RemovePassiveTarget(new List<EntityUid> { uid });

    /// <summary>
    /// Removes a passive target for a Utility AI goal.
    /// </summary>
    /// <param name="entities">Passive targets to remove.</param>
    [PublicAPI]
    public void RemovePassiveTarget(List<EntityUid> entities)
    {
        foreach (var uid in entities)
        {
            if (!PassiveGoalQuery.TryComp(uid, out var comp))
                continue;

            RaiseLocalEvent(uid, new NpcPassiveGoalRemoved(comp.Goal, uid, comp.User), broadcast: true);
            RemComp<PassiveGoalTargetComponent>(uid);
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
                || !Goap.TryGetValue(goap.State, proto.TargetKey, out var uid))
                continue;

            target = uid;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the current target coordinates of the Utility AI goal, if any.
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

            if (proto.GoalType.HasFlag(ExecutableGoalType.Place))
            {
                if (Goap.TryGetValue(goap.State, proto.TargetCoordinatesKey, out var result))
                {
                    coords = result;
                    return true;
                }

                continue;
            }

            if (Goap.TryGetValue(goap.State, proto.TargetKey, out var uid))
            {
                coords = Transform(uid).Coordinates;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Changes the combat mode of controlled entities.
    /// </summary>
    [PublicAPI]
    public bool TrySetCombatMode(
        Entity<NpcControllerComponent?> player,
        Entity<ControllableNpcComponent?> ent,
        bool mode)
    {
        if (!Resolve(player, ref player.Comp, false)
            || !Resolve(ent, ref ent.Comp, false)
            || !CanControl(player, ent)
            || !Goap.CheckCondition(ent.Owner, ent.Comp.CombatConditions))
            return false;

        _combatMode.SetInCombatMode(ent, mode);
        return false;
    }

    /// <summary>
    /// Changes the combat mode of controlled entities.
    /// </summary>
    [PublicAPI]
    public void SetCombatMode(
        Entity<NpcControllerComponent?> player,
        IReadOnlyList<EntityUid> entities,
        bool mode)
    {
        foreach (var uid in entities)
        {
            TrySetCombatMode(player, uid, mode);
        }

        if (!_net.IsClient)
            return;

        RaiseNetworkEvent(new SetCombatModeMessage(GetNetEntityList(entities), mode));
    }
}
