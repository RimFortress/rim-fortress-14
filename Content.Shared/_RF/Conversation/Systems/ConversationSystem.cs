using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Conversation.Systems;

/// <summary>
/// A helper system for easily implementing advanced random conversations between NPCs.
/// </summary>
public sealed class ConversationSystem : EntitySystem, IConversationConditionChecker
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;

    [Dependency] private readonly EntityQuery<ConversationActorComponent> _actorQuery = default!;

    private readonly Dictionary<int, HashSet<ProtoId<ConversationScriptPrototype>>> _scriptsByActors = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConversationActorComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ConversationActorComponent, MobStateChangedEvent>(OnMobStateChanged);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<ConversationScriptPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    private void OnRemove(Entity<ConversationActorComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp(ent.Comp.Conversation, out ConversationComponent? conv))
            return;

        foreach (var (_, actor) in conv.Actors)
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

    private void ReloadPrototypes()
    {
        _scriptsByActors.Clear();

        foreach (var proto in _prototype.EnumeratePrototypes<ConversationScriptPrototype>())
        {
            if (!_scriptsByActors.TryAdd(proto.Actors.Count, new() { proto }))
                _scriptsByActors[proto.Actors.Count].Add(proto);
        }
    }

    private (int Index, string Actor, TimeSpan Delay, InGameICChatType SpeakType, bool Speak)? GetNextMessage(
        ConversationScriptPrototype script,
        int current = -1)
    {
        var next = current + 1;

        switch (script.Order)
        {
            case ConversationBasicOrderType seq:
                if (current >= seq.Lines - 1)
                    return null;

                var actor = script.Actors[next % script.Actors.Count].Id;
                var delay = TimeSpan.FromSeconds(_random.NextFloat(seq.Delay.Min, seq.Delay.Max));
                return (next, actor, delay, seq.SpeakType, true);
            case ConversationCustomOrderType custom:
                if (current >= custom.Custom.Count - 1)
                    return null;

                var nextLine = custom.Custom[next];
                delay = TimeSpan.FromSeconds(nextLine.Delay?.Next(_random) ?? custom.Delay.Next(_random));
                return (next, nextLine.Id, delay, nextLine.SpeakType, nextLine.Speak);
            default:
                throw new ArgumentOutOfRangeException(nameof(ConversationScriptPrototype.Order), script.Order, null);
        }
    }

    private Vector2 GetRotatePosition(ConversationComponent conv)
    {
        var script = _prototype.Index(conv.Script);

        switch (script.Order)
        {
            case ConversationBasicOrderType:
                return ConversationCenter(conv);
            case ConversationCustomOrderType custom:
                var msg = custom.Custom[conv.NextMessage];

                if (msg.FaceDir != null)
                    return Transform(conv.NextActor).Coordinates.Position + msg.FaceDir.Value;

                if (msg.FaceTo == null)
                    return ConversationCenter(conv);

                return Transform(conv.Actors[msg.FaceTo]).Coordinates.Position;
            default:
                throw new ArgumentOutOfRangeException(nameof(ConversationScriptPrototype.Order), script.Order, null);
        }
    }

    private Vector2 ConversationCenter(ConversationComponent conv)
    {
        var pos = Vector2.Zero;

        foreach (var (_, uid) in conv.Actors)
        {
            pos += Transform(uid).Coordinates.Position;
        }

        pos /= conv.Actors.Count;
        return pos;
    }

    /// <summary>
    /// Start the conversation with the entities to whom the agent sent invites.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="actors">Dictionary mapping roleId -> EntityUid, or null if no valid assignment exists.</param>
    /// <returns>True, if the conversation has been successfully initiated.</returns>
    /// <remarks>
    /// A conversation scenario is selected at random, in descending order of the number of actors in it
    /// (first, random scenarios with N actors are selected; if none are suitable, then N - 1, and so on down to 1).
    /// The actors for the conversation are taken from invites sent by the agent.
    /// </remarks>
    /// <seealso cref="InviteInConversation"/>
    /// <seealso cref="GoapState.ConversationInvitesToOtherKey"/>
    [PublicAPI]
    public bool TryStartConversation(Entity<GoapComponent?> ent,
        [NotNullWhen(true)] out Dictionary<string, EntityUid>? actors)
    {
        actors = null;

        if (!Resolve(ent, ref ent.Comp)
            || !ent.Comp.State.TryGetValue(GoapState.ConversationInvitesToOtherKey, out var invites))
            return false;

        var scripts = _prototype
            .EnumeratePrototypes<ConversationScriptPrototype>()
            .ToList();
        var entities = invites
            .Where(x => x.Value.ValidUntil >= _timing.CurTime && x.Value.Accespted)
            .Select(x => x.Key)
            .ToHashSet();
        entities.Add(ent);

        while (scripts.Count > 0)
        {
            if (TryStartConversation(_random.PickAndTake(scripts), entities, out actors))
                return true;
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

        var convEnt = Spawn();
        var convComp = EnsureComp<ConversationComponent>(convEnt);
        convComp.Script = protoId;
        convComp.Actors = actors;
        convComp.NextActor = actors[first.Actor];
        convComp.NextMessage = first.Index;
        convComp.NextDelay = first.Delay;
        convComp.NextSpeakType = first.SpeakType;
        convComp.NextSpeak = first.Speak;
        // TODO: I think the starting location for the conversation should be determined using a more advanced method
        convComp.StartPosition = Transform(_random.Pick(uids)).Coordinates;

        foreach (var (_, uid) in actors)
        {
            var comp = EnsureComp<ConversationActorComponent>(uid);
            comp.Conversation = convEnt;
            comp.Ready = false;
            comp.TargetPos = convComp.StartPosition;
            comp.TargetRangeKey = GoapState.ConversationRange;
            comp.TargetFaceTo = uid == convComp.NextActor
                ? GetRotatePosition(convComp)
                : ConversationCenter(convComp);
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
        [NotNullWhen(true)] out TimeSpan? delay,
        [NotNullWhen(true)] out InGameICChatType? speakType)
    {
        line = null;
        delay = null;
        speakType = null;

        if (!TryGetConversation(ent, out var conv)
            || conv.NextMessage < 0
            || !conv.NextSpeak)
            return false;

        line = Loc.GetString($"conversation-{conv.Script.Id.ToLowerInvariant()}-line-{conv.NextMessage + 1}");
        delay = conv.NextDelay;
        speakType = conv.NextSpeakType;
        return true;
    }

    /// <summary>
    /// Moves to the next line of conversation in which the entity participates.
    /// </summary>
    [PublicAPI]
    public void ContinueConversation(Entity<ConversationActorComponent?> ent)
    {
        if (!TryGetConversation(ent, out var conv)
            || conv.NextActor != ent.Owner
            || !_prototype.Resolve(conv.Script, out var script))
            return;

        if (GetNextMessage(script, conv.NextMessage) is not { } next)
        {
            EndConversation(ent, true);
            return;
        }

        var nextActor = conv.Actors[next.Actor];
        conv.NextActor = nextActor;
        conv.NextDelay = next.Delay;
        conv.NextMessage = next.Index;
        conv.NextSpeakType = next.SpeakType;
        conv.NextSpeak = next.Speak;

        if (!_actorQuery.TryComp(conv.NextActor, out var actor))
            return;

        if (script.Order is ConversationCustomOrderType custom
            && custom.Custom[conv.NextMessage].PosOffset is { } offset)
        {
            actor.TargetPos = new EntityCoordinates(actor.TargetPos.EntityId, actor.TargetPos.Position + offset);
            actor.TargetRangeKey = GoapState.MovementRange;
        }

        actor.TargetFaceTo = GetRotatePosition(conv);
    }

    /// <summary>
    /// Ends the conversation in which the entity participates.
    /// </summary>
    [PublicAPI]
    public void EndConversation(Entity<ConversationActorComponent?> ent, bool applyEffects = false)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || !TryGetConversation(ent, out var conv)
            || !_prototype.TryIndex(conv.Script, out var proto))
            return;

        RemCompDeferred<ConversationActorComponent>(ent);
        RemoveAllInvites(ent.Owner);

        if (!applyEffects)
            return;

        foreach (var (id, uid) in conv.Actors)
        {
            // Apply conversation completion effects
            if (proto.Effects.TryGetValue(id, out var effects))
                _entityEffects.ApplyEffects(uid, effects);
        }
    }

    /// <summary>
    /// Updates the actor's readiness to engage in conversation.
    /// </summary>
    [PublicAPI]
    public void SetReady(Entity<ConversationActorComponent?> ent, bool ready)
    {
        if (Resolve(ent, ref ent.Comp))
            ent.Comp.Ready = ready;
    }

    /// <summary>
    /// Returns true if all actors have indicated their readiness to engage in a conversation.
    /// </summary>
    [PublicAPI]
    public bool AllReady(Entity<ConversationActorComponent?> ent)
    {
        if (!TryGetConversation(ent, out var conv))
            return false;

        foreach (var (_, uid) in conv.Actors)
        {
            if (!_actorQuery.TryComp(uid, out var actor) || !actor.Ready)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether the entity is next in line in the conversation.
    /// </summary>
    [PublicAPI, Pure]
    public bool IsNextInConversation(Entity<ConversationActorComponent?> ent)
        => TryGetConversation(ent, out var conv) && conv.NextActor == ent.Owner;

    /// <summary>
    /// Returns the conversation component in which the entity is participating.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetConversation(
        Entity<ConversationActorComponent?> ent,
        [NotNullWhen(true)] out ConversationComponent? conversation)
    {
        conversation = null;
        return Resolve(ent, ref ent.Comp) && TryComp(ent.Comp.Conversation, out conversation);
    }

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

        if (uids.Count < script.Actors.Count)
            return false;

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

        DebugTools.AssertNotEqual(inviter, invited);

        var inviterState = inviter.Comp.State;
        var invitedState = invited.Comp.State;
        var invite = (_timing.CurTime + inviterState.GetValue(GoapState.ConversationInviteValidTimeKey), false);

        if (inviterState.TryGetValue(GoapState.ConversationInvitesToOtherKey, out var invitesToOthers))
        {
            invitesToOthers[invited] = invite;
            inviterState.SetValue(GoapState.ConversationInvitesToOtherKey, invitesToOthers);
        }
        else
            inviterState.SetValue(GoapState.ConversationInvitesToOtherKey, new() { { invited, invite } });

        if (invitedState.TryGetValue(GoapState.ConversationInvitesKey, out var invites))
        {
            invites[inviter] = invite;
            invitedState.SetValue(GoapState.ConversationInvitesKey, invites);
        }
        else
            invitedState.SetValue(GoapState.ConversationInvitesKey, new() { { inviter, invite } });
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

        DebugTools.AssertNotEqual(inviter, invited);

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
    /// Accepts an invitation to a conversation from another agent.
    /// </summary>
    /// <param name="invited">An agent invited to join the conversation.</param>
    /// <param name="inviter">An agent who initiates a conversation.</param>
    [PublicAPI]
    public bool AcceptInvite(Entity<GoapComponent?> invited, Entity<GoapComponent?> inviter)
    {
        if (!Resolve(inviter, ref inviter.Comp) || !Resolve(invited, ref invited.Comp))
            return false;

        DebugTools.AssertNotEqual(inviter, invited);

        var inviterState = inviter.Comp.State;
        var invitedState = invited.Comp.State;

        if (!inviterState.TryGetValue(GoapState.ConversationInvitesToOtherKey, out var invitesToOthers)
            || !invitesToOthers.ContainsKey(invited))
            return false;

        if (!invitedState.TryGetValue(GoapState.ConversationInvitesKey, out var invites)
            || !invites.ContainsKey(inviter))
            return false;

        var newInvite = (_timing.CurTime + inviterState.GetValue(GoapState.ConversationInviteValidTimeKey), true);
        invitesToOthers[invited] = newInvite;
        invites[inviter] = newInvite;
        inviterState.SetValue(GoapState.ConversationInvitesToOtherKey, invitesToOthers);
        invitedState.SetValue(GoapState.ConversationInvitesKey, invites);
        return true;
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
            ? invited.Comp.State.GetValueOrDefault(GoapState.ConversationInvitesKey, new())
                .Count(x => x.Value.ValidUntil >= _timing.CurTime)
            : 0;

    /// <summary>
    /// Checks whether all participants in the conversation are within a specified radius of the target location.
    /// </summary>
    /// <param name="ent">One of the participants in the conversation.</param>
    /// <param name="targetCoords">Target coordinates.</param>
    /// <param name="range">Maximum radius.</param>
    [PublicAPI]
    public bool ActorsInRange(
        Entity<ConversationActorComponent?> ent,
        EntityCoordinates targetCoords,
        float range)
    {
        if (!TryGetConversation(ent, out var conv))
            return false;

        foreach (var (_, uid) in conv.Actors)
        {
            if (!_transform.InRange(Transform(uid).Coordinates, targetCoords, range))
                return false;
        }

        return true;
    }
}

public interface IConversationConditionChecker
{
    bool CheckCondition<T>(EntityUid target, EntityUid? other, T condition) where T : BaseConversationCondition<T>;
}
