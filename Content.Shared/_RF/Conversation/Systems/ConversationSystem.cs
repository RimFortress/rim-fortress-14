using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared.Bed.Sleep;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.Systems;

/// <summary>
/// A helper system for easily implementing advanced random conversations between NPCs.
/// </summary>
public sealed class ConversationSystem : EntitySystem, IConversationConditionChecker
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;
    [Dependency] private readonly EntityQuery<ConversationActorComponent> _actorQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConversationActorComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ConversationActorComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ConversationActorComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<ConversationActorComponent, GoapPlanFinished>(OnGoapPlanFinished);
    }

    private void OnRemove(EntityUid uid, ConversationActorComponent component, ComponentRemove args)
    {
        foreach (var (_, actor) in component.Actors)
        {
            if (actor != uid)
                RemComp<ConversationActorComponent>(actor);
        }
    }

    private void OnMobStateChanged(EntityUid uid, ConversationActorComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            EndConversation(new(uid, component));
    }

    private void OnAttacked(EntityUid uid, ConversationActorComponent component, AttackedEvent args)
    {
        EndConversation(new(uid, component));
    }

    private void OnGoapPlanFinished(EntityUid uid, ConversationActorComponent component, GoapPlanFinished args)
    {
        EndConversation(new(uid, component));
    }

    // TODO: destroy this
    private EntityCoordinates FindConversationCoords(List<EntityUid> uids)
    {
        var map = MapId.Nullspace;
        var pos = Vector2.Zero;

        foreach (var uid in uids)
        {
            map = _xform.GetMapId(uid);
            pos += _xform.GetMapCoordinates(uid).Position;
        }

        var coords = _xform.ToCoordinates(new MapCoordinates(pos / uids.Count, map));
        return coords;
    }

    private static (int Index, string Actor)? GetNextMessage(ConversationScriptPrototype script, int current = -1)
    {
        switch (script.Order)
        {
            case ConversationSequentialOrderType seq:
                if (current >= seq.Lines - 1)
                    return null;

                var next = current + 1;
                return (next, script.Actors[script.Actors.Count % next].Id);
            case ConversationCustomOrderType custom:
                if (current < custom.Custom.Count - 1)
                    return (current + 1, custom.Custom[current + 1]);

                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(ConversationScriptPrototype.Order), script.Order, null);
        }
    }

    /// <summary>
    /// Starts a conversation between entities.
    /// </summary>
    /// <param name="protoId">Conversation script prototype.</param>
    /// <param name="uids">List of entities that will participate in the conversation.</param>
    /// <returns>True, if the conversation has been successfully initiated.</returns>
    [PublicAPI]
    public bool TryStartConversation(ProtoId<ConversationScriptPrototype> protoId, List<EntityUid> uids)
        => TryStartConversation(protoId, uids, out _);

    /// <summary>
    /// Starts a conversation between entities.
    /// </summary>
    /// <param name="protoId">Conversation script prototype.</param>
    /// <param name="uids">List of entities that will participate in the conversation.</param>
    /// <param name="actors">Dictionary mapping roleId -> EntityUid, or null if no valid assignment exists.</param>
    /// <returns>True, if the conversation has been successfully initiated.</returns>
    [PublicAPI]
    public bool TryStartConversation(
        ProtoId<ConversationScriptPrototype> protoId,
        List<EntityUid> uids,
        [NotNullWhen(true)] out Dictionary<string, EntityUid>? actors)
    {
        actors = null;

        if (!_prototype.TryIndex(protoId, out var script))
            return false;

        uids = uids.Where(ValidateActor).ToList();

        if (uids.Count < script.Actors.Count)
            return false;

        if (!TryFindRoles(script, uids, out actors)
            || GetNextMessage(script) is not { } first)
            return false;

        foreach (var (id, uid) in actors)
        {
            var comp = EnsureComp<ConversationActorComponent>(uid);
            comp.Script = protoId;
            comp.Actors = actors;
            comp.ActorId = id;
            comp.NextActor = actors[first.Actor];
            comp.NextMessage = first.Index;
            comp.ConversationCoords = FindConversationCoords(actors.Values.ToList());
        }

        return true;

        bool ValidateActor(EntityUid uid)
        {
            if (_actorQuery.HasComp(uid) || HasComp<SleepingComponent>(uid))
                return false;

            return TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState == MobState.Alive;
        }
    }

    /// <summary>
    /// Returns the line of conversation that the entity should say.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetLine(Entity<ConversationActorComponent?> ent, [NotNullWhen(true)] out string? line)
    {
        line = null;
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.NextMessage < 0)
            return false;

        line = Loc.GetString($"conversation-{ent.Comp.Script.Id.ToLowerInvariant()}-line-{ent.Comp.NextMessage}");
        return true;
    }

    /// <summary>
    /// Moves to the next line of conversation in which the entity participates.
    /// </summary>
    [PublicAPI]
    public void ContinueConversation(Entity<ConversationActorComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.NextActor != ent.Owner
            || !_prototype.Resolve(ent.Comp.Script, out var script))
            return;

        if (GetNextMessage(script, ent.Comp.NextMessage) is not { } next)
        {
            EndConversation(ent, true);
            return;
        }

        ent.Comp.NextMessage = next.Index;
        var nextActor = ent.Comp.Actors[next.Actor];

        // Update next line
        foreach (var (_, uid) in ent.Comp.Actors)
        {
            if (_actorQuery.TryComp(uid, out var comp))
                comp.NextActor = nextActor;
        }
    }

    /// <summary>
    /// Ends the conversation in which the entity participates.
    /// </summary>
    [PublicAPI]
    public void EndConversation(Entity<ConversationActorComponent?> ent, bool applyEffects = false)
    {
        if (!Resolve(ent, ref ent.Comp) || !_prototype.TryIndex(ent.Comp.Script, out var proto))
            return;

        RemCompDeferred<ConversationActorComponent>(ent);

        if (!applyEffects)
            return;

        foreach (var (id, uid) in ent.Comp.Actors)
        {
            // Apply conversation completion effects
            if (proto.Effects.TryGetValue(id, out var effects))
                _entityEffects.ApplyEffects(uid, effects);
        }
    }

    /// <summary>
    /// Checks whether the entity is next in line in the conversation.
    /// </summary>
    [PublicAPI, Pure]
    public bool IsNextInConversation(Entity<ConversationActorComponent?> ent)
        => Resolve(ent, ref ent.Comp, false) && ent.Comp.NextActor == ent.Owner;

    /// <summary>
    /// Attempts to assign conversation roles to entities according to all actor requirements.
    /// </summary>
    /// <param name="script">Conversation script prototype.</param>
    /// <param name="uids">Potential participants in the conversation.</param>
    /// <param name="roles">Dictionary mapping roleId -> EntityUid, or null if no valid assignment exists.</param>
    [PublicAPI, Pure]
    public bool TryFindRoles(
        ConversationScriptPrototype script,
        List<EntityUid> uids,
        [NotNullWhen(true)] out Dictionary<string, EntityUid>? roles)
    {
        roles = null;

        var roleList = script.Actors.Select(a => a.Id).ToList();
        var actors = new Dictionary<string, EntityUid>();
        var used = new HashSet<EntityUid>();

        if (!Assign(0))
            return false;

        roles = actors;
        return true;

        bool CheckCommonRequirements(string role, EntityUid uid)
        {
            var data = script.Actors.FirstOrDefault(a => a.Id == role);
            return data != null && CheckCondition(uid, null, data.Requirements);
        }

        // Checks the requirements defined by 'role' toward other already assigned roles.
        // For example: RoleA.Requirements[RoleB] must hold for (uid -> RoleA, assigned[RoleB])
        bool CheckRoleRequirements(
            string role,
            EntityUid uid,
            Dictionary<string, EntityUid> assigned)
        {
            var roleData = script.Actors.FirstOrDefault(a => a.Id == role);

            if (roleData == null)
                return false;

            foreach (var (otherRole, reqList) in roleData.RequirementsFor)
            {
                if (assigned.TryGetValue(otherRole, out var otherUid))
                    return CheckCondition(uid, otherUid, reqList);
            }

            return true;
        }

        // Checks the requirements that already assigned roles have toward the new role.
        // For example: If RoleB requires RoleA, then when assigning RoleA = uid,
        // we must validate RoleB.Requirements[RoleA]
        bool CheckReverseRequirements(
            string role,
            EntityUid uid,
            Dictionary<string, EntityUid> assigned)
        {
            foreach (var (otherRole, otherUid) in assigned)
            {
                var otherData = script.Actors.FirstOrDefault(a => a.Id == otherRole);

                if (otherData == null || !otherData.RequirementsFor.TryGetValue(role, out var reqList))
                    continue;

                return CheckCondition(otherUid, uid, reqList);
            }

            return true;
        }

        // Recursively assigns each role to one entity using DFS backtracking.
        // Ensures all requirement constraints remain satisfied at each step
        bool Assign(int index)
        {
            if (index >= roleList.Count)
                return true;

            var role = roleList[index];

            foreach (var uid in uids)
            {
                if (used.Contains(uid)
                    || !CheckCommonRequirements(role, uid)
                    || !CheckRoleRequirements(role, uid, actors)
                    || !CheckReverseRequirements(role, uid, actors))
                    continue;

                used.Add(uid);
                actors[role] = uid;

                if (Assign(index + 1))
                    return true;

                used.Remove(uid);
                actors.Remove(role);
            }

            return false;
        }
    }

    public bool CheckCondition<T>(EntityUid target, EntityUid? other, T condition) where T : BaseConversationCondition<T>
    {
        var ev = new ConversationConditionCheckEvent<T>(target, other, condition, false);
        RaiseLocalEvent(target, ref ev);
        return ev.Result;
    }

    [PublicAPI]
    public bool CheckCondition(EntityUid target, EntityUid? other, ConversationCondition condition)
        => condition.Check(target, other, this);

    [PublicAPI]
    public bool CheckCondition(EntityUid target, EntityUid? other, IEnumerable<ConversationCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (!CheckCondition(target, other, condition))
                return false;
        }

        return true;
    }
}

public interface IConversationConditionChecker
{
    bool CheckCondition<T>(EntityUid target, EntityUid? other, T condition) where T : BaseConversationCondition<T>;
}
