using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Shared.Bed.Sleep;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RF.Conversation.Systems;

/// <summary>
/// A helper system for easily implementing advanced random conversations between NPCs.
/// </summary>
public sealed class ConversationSystem : EntitySystem, IConversationConditionChecker
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;

    [Dependency] private readonly EntityQuery<ConversationActorComponent> _actorQuery = default!;

    private readonly Dictionary<int, HashSet<ProtoId<ConversationScriptPrototype>>> _scriptsByActors = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConversationActorComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ConversationActorComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ConversationActorComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<ConversationActorComponent, UtilityAiGoalFinished>(OnUtilityAiGoalFinished);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<ConversationScriptPrototype>())
                ReloadPrototypes();
        };
    }

    private void OnRemove(Entity<ConversationActorComponent> ent, ref ComponentRemove args)
    {
        foreach (var (_, actor) in ent.Comp.Actors)
        {
            if (actor != ent.Owner)
                RemComp<ConversationActorComponent>(actor);
        }
    }

    private void OnMobStateChanged(Entity<ConversationActorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            EndConversation(ent.AsNullable());
    }

    private void OnAttacked(Entity<ConversationActorComponent> ent, ref AttackedEvent args)
    {
        EndConversation(ent.AsNullable());
    }

    private void OnUtilityAiGoalFinished(Entity<ConversationActorComponent> ent, ref UtilityAiGoalFinished args)
    {
        EndConversation(ent.AsNullable());
    }

    private void ReloadPrototypes()
    {
        _scriptsByActors.Clear();

        foreach (var proto in _prototype.EnumeratePrototypes<ConversationScriptPrototype>())
        {
            if (!_scriptsByActors.TryAdd(proto.Actors.Count, new() { proto }))
                _scriptsByActors[proto.Actors.Count].Add(proto);
        }
    }

    private (int Index, string Actor, TimeSpan Delay)? GetNextMessage(ConversationScriptPrototype script, int current = -1)
    {
        var next = current + 1;

        switch (script.Order)
        {
            case ConversationSequentialOrderType seq:
                if (current >= seq.Lines - 1)
                    return null;

                var actor = script.Actors[script.Actors.Count % next].Id;
                var delay = TimeSpan.FromSeconds(_random.NextFloat(seq.Delay.Min, seq.Delay.Max));
                return (next, actor, delay);
            case ConversationCustomOrderType custom:
                if (current >= custom.Custom.Count - 1)
                    return null;

                var nextLine = custom.Custom[next];
                delay = TimeSpan.FromSeconds(_random.NextFloat(nextLine.Delay.Min, nextLine.Delay.Max));
                return (next, nextLine.Id, delay);
            default:
                throw new ArgumentOutOfRangeException(nameof(ConversationScriptPrototype.Order), script.Order, null);
        }
    }

    /// <summary>
    /// Start the conversation with the entities to whom the agent sent invites.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <returns>True, if the conversation has been successfully initiated.</returns>
    /// <remarks>
    /// A conversation scenario is selected at random, in descending order of the number of actors in it
    /// (first, random scenarios with N actors are selected; if none are suitable, then N - 1, and so on down to 1).
    /// The actors for the conversation are taken from invites sent by the agent.
    /// </remarks>
    /// <seealso cref="InviteInConversation"/>
    /// <seealso cref="GoapState.ConversationInvitesToOtherKey"/>
    [PublicAPI]
    public bool TryStartConversation(Entity<GoapComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !ent.Comp.State.TryGetValue(GoapState.ConversationInvitesToOtherKey, out var invites))
            return false;

        var actors = invites
            .Where(x => x.Value >= _timing.CurTime)
            .Select(x => x.Key)
            .ToHashSet();
        actors.Add(ent);

        for (var i = invites.Count + 1; i > 0; i--)
        {
            if (!_scriptsByActors.TryGetValue(i, out var scripts))
                continue;

            var scriptsList = scripts.ToList();

            while (scriptsList.Count > 0)
            {
                if (TryStartConversation(_random.PickAndTake(scriptsList), actors))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Starts a conversation between entities.
    /// </summary>
    /// <param name="protoId">Conversation script prototype.</param>
    /// <param name="uids">List of entities that will participate in the conversation.</param>
    /// <returns>True, if the conversation has been successfully initiated.</returns>
    [PublicAPI]
    public bool TryStartConversation(ProtoId<ConversationScriptPrototype> protoId, HashSet<EntityUid> uids)
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
        HashSet<EntityUid> uids,
        [NotNullWhen(true)] out Dictionary<string, EntityUid>? actors)
    {
        actors = null;

        if (!_prototype.TryIndex(protoId, out var script))
            return false;

        uids = uids.Where(ValidateActor).ToHashSet();

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
            comp.NextDelay = first.Delay;
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
    public bool TryGetLine(
        Entity<ConversationActorComponent?> ent,
        [NotNullWhen(true)] out string? line,
        [NotNullWhen(true)] out TimeSpan? delay)
    {
        line = null;
        delay = null;

        if (!Resolve(ent, ref ent.Comp) || ent.Comp.NextMessage < 0)
            return false;

        line = Loc.GetString($"conversation-{ent.Comp.Script.Id.ToLowerInvariant()}-line-{ent.Comp.NextMessage}");
        delay = ent.Comp.NextDelay;
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
        ent.Comp.NextDelay = next.Delay;
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
        if (!Resolve(ent, ref ent.Comp)
            || !_prototype.TryIndex(ent.Comp.Script, out var proto))
            return;

        RemCompDeferred<ConversationActorComponent>(ent);
        RemoveAllInvites(ent.Owner);

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
        HashSet<EntityUid> uids,
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

    /// <summary>
    /// Invites another agent to join the conversation.
    /// </summary>
    /// <remarks>
    /// The invitation is written to the GoapState of both the inviter and the invited,
    /// and then they must be processed by the Utility AI.
    /// </remarks>
    /// <param name="inviter">An agent who initiates a conversation.</param>
    /// <param name="invited">An agent invited to join the conversation.</param>
    [PublicAPI]
    public void InviteInConversation(Entity<GoapComponent?> inviter, Entity<GoapComponent?> invited)
    {
        if (!Resolve(inviter, ref inviter.Comp) || !Resolve(invited, ref invited.Comp))
            return;

        var inviterState = inviter.Comp.State;
        var invitedState = invited.Comp.State;
        var validTime = _timing.CurTime + inviterState.GetValue(GoapState.ConversationInviteValidTimeKey);

        if (inviterState.TryGetValue(GoapState.ConversationInvitesToOtherKey, out var invitesToOthers))
        {
            invitesToOthers[invited] = validTime;
            inviterState.SetValue(GoapState.ConversationInvitesToOtherKey, invitesToOthers);
        }
        else
            inviterState.SetValue(GoapState.ConversationInvitesToOtherKey, new { invitesToOthers });

        if (invitedState.TryGetValue(GoapState.ConversationInvitesKey, out var invites))
        {
            invites[inviter] = validTime;
            invitedState.SetValue(GoapState.ConversationInvitesKey, invites);
        }
        else
            invitedState.SetValue(GoapState.ConversationInvitesKey, new { inviter });
    }

    /// <summary>
    /// Withdraws the invitation to the conversation.
    /// </summary>
    /// <param name="inviter">An agent who initiates a conversation.</param>
    /// <param name="invited">An agent invited to join the conversation.</param>
    [PublicAPI]
    public void RemoveInvite(Entity<GoapComponent?> inviter, Entity<GoapComponent?> invited)
    {
        if (!Resolve(inviter, ref inviter.Comp) || !Resolve(invited, ref invited.Comp))
            return;

        var inviterState = inviter.Comp.State;
        var invitedState = invited.Comp.State;

        if (inviterState.TryGetValue(GoapState.ConversationInvitesToOtherKey, out var invitesToOthers))
        {
            invitesToOthers.Remove(invited);
            inviterState.SetValue(GoapState.ConversationInvitesToOtherKey, invitesToOthers);
        }

        if (invitedState.TryGetValue(GoapState.ConversationInvitesKey, out var invites))
        {
            invites.Remove(inviter);
            invitedState.SetValue(GoapState.ConversationInvitesKey, invites);
        }
    }

    /// <summary>
    /// Withdraws all invites to conversation from this agent.
    /// </summary>
    /// <param name="inviter">GOAP agent.</param>
    [PublicAPI]
    public void RemoveAllInvites(Entity<GoapComponent?> inviter)
    {
        if (!Resolve(inviter, ref inviter.Comp))
            return;

        var state = inviter.Comp.State;

        if (!state.TryGetValue(GoapState.ConversationInvitesToOtherKey, out var invites))
            return;

        foreach (var (invited, _) in invites)
        {
            RemoveInvite(inviter, invited);
        }
    }

    /// <summary>
    /// Returns the number of valid invites to conversation for this agent.
    /// </summary>
    /// <param name="invited">GOAP agent.</param>
    [PublicAPI]
    public int InvitesCount(Entity<GoapComponent?> invited)
        => Resolve(invited, ref invited.Comp)
            ? invited.Comp.State.GetValueOrDefault(GoapState.ConversationInvitesKey)
                .Count(x => x.Value >= _timing.CurTime)
            : 0;
}

public interface IConversationConditionChecker
{
    bool CheckCondition<T>(EntityUid target, EntityUid? other, T condition) where T : BaseConversationCondition<T>;
}
