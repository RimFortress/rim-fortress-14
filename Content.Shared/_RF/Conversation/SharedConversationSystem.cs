using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.Conversation.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation;

/// <summary>
/// A helper system for easily implementing advanced random conversations between NPCs
/// </summary>
public abstract class SharedConversationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConversationActorComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ConversationActorComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ConversationActorComponent, AttackedEvent>(OnAttacked);
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

    /// <summary>
    /// Starts a conversation between entities
    /// </summary>
    /// <param name="protoId">Conversation script prototype</param>
    /// <param name="uids">List of entities that will participate in the conversation</param>
    /// <returns>True, if the conversation has been successfully initiated</returns>
    public bool TryStartConversation(ProtoId<ConversationScriptPrototype> protoId, List<EntityUid> uids)
        => TryStartConversation(protoId, uids, out _);

    /// <summary>
    /// Starts a conversation between entities
    /// </summary>
    /// <param name="protoId">Conversation script prototype</param>
    /// <param name="uids">List of entities that will participate in the conversation</param>
    /// <param name="actors">Dictionary mapping roleId -> EntityUid, or null if no valid assignment exists</param>
    /// <returns>True, if the conversation has been successfully initiated</returns>
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

        if (!TryFindRoles(script, uids, out actors))
            return false;

        var firstActor = actors.GetValueOrDefault(script.Actors.FirstOrDefault()?.Id ?? string.Empty);
        var coords = FindConversationCoords(actors.Values.ToList());

        if (!firstActor.IsValid())
            return false;

        foreach (var (id, uid) in actors)
        {
            var comp = EnsureComp<ConversationActorComponent>(uid);
            comp.Script = protoId;
            comp.Actors = actors;
            comp.ActorId = id;
            comp.NextActor = firstActor;
            comp.NextMessage = script.Lines.First(x => x.ActorId == id).Message;
            comp.ConversationCoords = coords;
        }

        return true;
    }

    private bool ValidateActor(EntityUid uid)
    {
        if (HasComp<ConversationActorComponent>(uid) || HasComp<SleepingComponent>(uid))
            return false;

        if (TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState != MobState.Alive)
            return false;

        return true;
    }

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

    /// <summary>
    /// Returns the line of conversation that the entity should say.
    /// </summary>
    public bool TryGetLine(Entity<ConversationActorComponent?> ent, [NotNullWhen(true)] out string? line)
    {
        line = null;
        return Resolve(ent, ref ent.Comp) && Loc.TryGetString(ent.Comp.NextMessage ?? string.Empty, out line);
    }

    /// <summary>
    /// Moves to the next line of conversation in which the entity participates.
    /// </summary>
    protected void ContinueConversation(Entity<ConversationActorComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.NextActor != ent.Owner
            || !_prototype.Resolve(ent.Comp.Script, out var script))
            return;

        // Find current conversation line
        var index = script.Lines.FindIndex(x =>
            x.ActorId == ent.Comp.ActorId && x.Message == ent.Comp.NextMessage);

        if (index == -1 || index + 1 >= script.Lines.Count)
        {
            EndConversation(ent, true);
            return;
        }

        // Find next message
        ent.Comp.NextMessage = null;

        for (var i = index + 1; i < script.Lines.Count; i++)
        {
            if (script.Lines[i].ActorId != ent.Comp.ActorId)
                continue;

            ent.Comp.NextMessage = script.Lines[i].Message;
            break;
        }

        var nextLine = script.Lines[index + 1];
        var nextActor = ent.Comp.Actors[nextLine.ActorId];

        // Update next line
        foreach (var (_, uid) in ent.Comp.Actors)
        {
            if (TryComp(uid, out ConversationActorComponent? comp))
                comp.NextActor = nextActor;
        }
    }

    /// <summary>
    /// Ends the conversation in which the entity participates
    /// </summary>
    public void EndConversation(Entity<ConversationActorComponent?> ent, bool applyEffects = false)
    {
        if (!Resolve(ent, ref ent.Comp) || !_prototype.TryIndex(ent.Comp.Script, out var proto))
            return;

        RemCompDeferred<ConversationActorComponent>(ent);

        if (!applyEffects)
            return;

        foreach (var (id, uid) in ent.Comp.Actors)
        {
            if (!proto.Effects.TryGetValue(id, out var effects))
                continue;

            // apply conversation completion effects
            var args = new EntityEffectConversationArgs(uid, ent.Comp.Actors, EntityManager);

            foreach (var effect in effects)
            {
                if (effect.ShouldApply(args))
                    effect.Effect(args);
            }
        }
    }

    /// <summary>
    /// Checks whether the entity is next in line in the conversation
    /// </summary>
    public bool IsNextInConversation(Entity<ConversationActorComponent?> ent)
        => Resolve(ent, ref ent.Comp) && ent.Comp.NextActor == ent.Owner;

    /// <summary>
    /// Attempts to assign conversation roles to entities according to all actor requirements
    /// </summary>
    /// <param name="script"></param>
    /// <param name="uids"></param>
    /// <param name="roles">Dictionary mapping roleId -> EntityUid, or null if no valid assignment exists</param>
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

            if (data == null)
                return false;

            foreach (var req in data.Requirements)
            {
                var result = req.Check(uid, null, EntityManager);

                if (!req.Invert && result || req.Invert && !result)
                    return false;
            }

            return true;
        }

        // Checks the requirements defined by 'role' toward other already assigned roles.
        // For example: RoleA.Requirements[RoleB] must hold for (uid -> RoleA, assigned[RoleB]).
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
                if (!assigned.TryGetValue(otherRole, out var otherUid))
                    continue;

                foreach (var req in reqList)
                {
                    var result = req.Check(uid, otherUid, EntityManager);

                    if (!req.Invert && !result || req.Invert && result)
                        return false;
                }
            }

            return true;
        }

        // Checks the requirements that already assigned roles have toward the new role.
        // For example: If RoleB requires RoleA, then when assigning RoleA = uid,
        // we must validate RoleB.Requirements[RoleA].
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

                foreach (var req in reqList)
                {
                    var result = req.Check(otherUid, uid, EntityManager);

                    if (!req.Invert && !result || req.Invert && result)
                        return false;
                }
            }

            return true;
        }

        // Recursively assigns each role to one entity using DFS backtracking.
        // Ensures all requirement constraints remain satisfied at each step.
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
}
