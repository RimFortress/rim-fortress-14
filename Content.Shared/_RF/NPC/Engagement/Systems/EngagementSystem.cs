using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using System.Linq;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.Engagement.Systems;

/// <summary>
/// A system that provides an API for engaging multiple AI agents in a single situation.
/// </summary>
/// <remarks>
/// This system only tracks membership, consent and the GoapState side-effects of joining/leaving
/// a role. It never decides how an agent should behave while engaged — that is entirely up to the
/// agent's own GOAP/UAI configuration reacting to the state keys written by <see cref="EngagementRolePrototype.OnStart"/>.
/// </remarks>
public sealed partial class EngagementSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;
    [Dependency] private readonly SharedGoapSystem _goap = default!;

    [Dependency] private readonly EntityQuery<EngagementComponent> _engagementQuery = default!;
    [Dependency] private readonly EntityQuery<EngagementParticipantComponent> _participantQuery = default!;
    [Dependency] private readonly EntityQuery<GoapComponent> _goapQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EngagementParticipantComponent, ComponentRemove>(OnParticipantRemove);
        SubscribeLocalEvent<EngagementParticipantComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EngagementComponent, ComponentRemove>(OnEngagementShutdown);
    }

    private void OnParticipantRemove(Entity<EngagementParticipantComponent> ent, ref ComponentRemove args)
    {
        foreach (var (engagementUid, role) in ent.Comp.Membership)
        {
            if (_engagementQuery.TryComp(engagementUid, out var comp))
                LeaveEngagementInternal((engagementUid, comp), ent.Owner, role, EngagementEndReason.Interrupted, cleanupParticipant: false);
        }
    }

    private void OnMobStateChanged(Entity<EngagementParticipantComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        foreach (var (engagementUid, _) in ent.Comp.Membership.ToArray())
        {
            if (_engagementQuery.TryComp(engagementUid, out var comp))
                LeaveEngagement((engagementUid, comp), ent.Owner, EngagementEndReason.Interrupted);
        }
    }

    private void OnEngagementShutdown(Entity<EngagementComponent> ent, ref ComponentRemove args)
    {
        foreach (var (_, actors) in ent.Comp.Actors)
        {
            foreach (var actor in actors)
            {
                if (!_participantQuery.TryComp(actor, out var participant))
                    continue;

                participant.Membership.Remove(ent.Owner);
            }
        }

        foreach (var invite in ent.Comp.Invites)
        {
            if (!_participantQuery.TryComp(invite.Uid, out var participant))
                continue;

            participant.Invites.RemoveWhere(x => x.EngageUid == ent.Owner);
        }
    }

    private void InviteToEngagementInternal(
        Entity<EngagementComponent> engagement,
        ProtoId<EngagementRolePrototype> role,
        EntityUid actor,
        EntityUid inviter)
    {
        if (!_prototype.Resolve(engagement.Comp.Kind, out var proto))
            return;

        var validUntil = _timing.CurTime + proto.InviteTime;
        engagement.Comp.Invites.Add((role, actor, validUntil));

        var participant = EnsureComp<EngagementParticipantComponent>(actor);
        participant.Invites.Add((engagement.Owner, inviter, role));

        var ev = new EngagementInviteSent(engagement.Owner, inviter, actor, role);
        RaiseLocalEvent(actor, ev);
        RaiseLocalEvent(engagement, ev);
        RaiseLocalEvent(inviter, ev);
    }

    /// <summary>
    /// Finds a role assignment for <paramref name="candidates"/> that fills every non-<see cref="EngagementRolePrototype.InitiatorOnly"/>
    /// role up to its <see cref="EngagementRolePrototype.MinCount"/>, satisfying every role's <see cref="EngagementRolePrototype.Conditions"/>
    /// and the mutual <see cref="EngagementRolePrototype.ConditionsFor"/> constraints between roles in both
    /// directions, via DFS backtracking over the whole candidate pool at once.
    /// </summary>
    private bool TryAssignRoles(
        EngagementPrototype proto,
        EntityUid initiator,
        IEnumerable<EntityUid> candidates,
        [NotNullWhen(true)] out Dictionary<ProtoId<EngagementRolePrototype>, List<EntityUid>>? assignment)
    {
        assignment = null;

        // Expand each role into one "slot" per required participant, e.g. a MinCount == 2 role
        // gets two slots. Roles with MinCount == 0 (grown dynamically later, e.g. combat's
        // Attacked role) and InitiatorOnly roles contribute no slots here.
        var slots = new List<EngagementRolePrototype>();

        foreach (var roleId in proto.Roles)
        {
            if (!_prototype.Resolve(roleId, out var role))
                continue;

            var count = Math.Min(role.MinCount, role.MaxCount);

            for (var i = 0; i < count; i++)
            {
                slots.Add(role);
            }
        }

        var pool = candidates.ToArray();

        if (slots.Count == 0 || pool.Length < slots.Count)
            return false;

        var used = new HashSet<EntityUid>();
        var result = new Dictionary<ProtoId<EngagementRolePrototype>, List<EntityUid>>();

        if (!Assign(0))
            return false;

        assignment = result;
        return true;

        // Recursively assigns each slot to one candidate via DFS backtracking, checking every
        // constraint (forward and reverse) against the in-progress assignment at each step.
        bool Assign(int index)
        {
            if (index >= slots.Count)
                return true;

            var roleData = slots[index];

            foreach (var candidate in pool)
            {
                if (used.Contains(candidate))
                    continue;

                if (!roleData.Force)
                {
                    if (_participantQuery.TryComp(candidate, out var participant) && participant.Membership.Count > 0)
                        continue;

                    if (roleData.InitiatorOnly && candidate != initiator)
                        continue;

                    if (!MeetsForwardRequirements(
                            roleData,
                            candidate,
                            result.GetValueOrDefault)
                        || !MeetsReverseRequirements(
                            roleData,
                            candidate,
                            result.SelectMany(x => x.Value.Select(u => (x.Key, u)))))
                        continue;
                }

                used.Add(candidate);

                if (!result.TryGetValue(roleData, out var assigned))
                    result[roleData] = assigned = new();

                assigned.Add(candidate);

                if (Assign(index + 1))
                    return true;

                assigned.RemoveAt(assigned.Count - 1);

                if (assigned.Count == 0)
                    result.Remove(roleData);

                used.Remove(candidate);
            }

            return false;
        }
    }

    /// <summary>
    /// Common capacity/InitiatorOnly/duplicate-membership checks shared by
    /// <see cref="TryJoinEngagement"/> and <see cref="InviteToEngagement"/>.
    /// </summary>
    private bool CanSeat(Entity<EngagementComponent> engagement, EngagementRolePrototype role, EntityUid actor)
    {
        if (role.InitiatorOnly && actor != engagement.Comp.Initiator)
            return false;

        var current = engagement.Comp.Actors.TryGetValue(role, out var set) ? set.Count : 0;

        if (current >= role.MaxCount)
            return false;

        // Already a participant of this exact situation (in this or another role).
        if (_participantQuery.TryComp(actor, out var participant) && participant.Membership.ContainsKey(engagement.Owner))
            return false;

        return true;
    }

    private void LeaveEngagementInternal(
        Entity<EngagementComponent> engagement,
        EntityUid actor,
        ProtoId<EngagementRolePrototype> roleId,
        EngagementEndReason reason,
        bool cleanupParticipant)
    {
        if (!_prototype.Resolve(roleId, out var role)
            || !_prototype.Resolve(engagement.Comp.Kind, out var proto)
            || !proto.Roles.Contains(role))
            return;

        if (!engagement.Comp.Actors.TryGetValue(roleId, out var set) || !set.Remove(actor))
            return;

        if (set.Count == 0)
            engagement.Comp.Actors.Remove(roleId);

        engagement.Comp.NextConditionCheck.Remove(actor);

        if (cleanupParticipant && _participantQuery.TryComp(actor, out var participant))
        {
            participant.Membership.Remove(engagement.Owner);

            if (participant.Membership.Count == 0)
                RemComp<EngagementParticipantComponent>(actor);
        }

        if (_goapQuery.TryComp(actor, out var goap))
        {
            goap.State.OverwriteFrom(role.OnFinish);

            foreach (var key in role.OnFinishRemove)
            {
                goap.State.Remove(key);
            }
        }

        var ev = new EngagementRoleLeft(engagement.Owner, actor, role, reason);
        RaiseLocalEvent(actor, ev);
        RaiseLocalEvent(engagement.Owner, ev);

        if (!engagement.Comp.Started || !proto.DissolveInvalid)
            return;

        var remaining = engagement.Comp.Actors.TryGetValue(role, out var left) ? left.Count : 0;

        if (remaining < role.MinCount)
            EndEngagement(engagement.AsNullable(), EngagementEndReason.Dissolved);
    }

    /// <summary>
    /// Checks <see cref="EngagementRolePrototype.Conditions"/> and forward <see cref="EngagementRolePrototype.ConditionsFor"/>
    /// (this role's requirements toward roles that already have occupants) for a candidate trying
    /// to take a non-forced role. <paramref name="getAssigned"/> abstracts over where "already
    /// occupied" comes from — the live <see cref="EngagementComponent.Actors"/>, or an in-progress
    /// batch assignment being built by <c>TryAssignRoles</c>.
    /// </summary>
    private bool MeetsForwardRequirements(
        EngagementRolePrototype roleData,
        EntityUid actor,
        Func<ProtoId<EngagementRolePrototype>, IEnumerable<EntityUid>?> getAssigned)
    {
        if (!_goapQuery.TryComp(actor, out var goap))
            return roleData.Conditions.Count == 0 && roleData.ConditionsFor.Count == 0;

        if (!_goap.CheckCondition(actor, goap.State, roleData.Conditions))
            return false;

        foreach (var (otherRole, conditions) in roleData.ConditionsFor)
        {
            var others = getAssigned(otherRole);

            if (others == null)
                continue;

            foreach (var other in others)
            {
                // Temporarily expose the counterpart being evaluated so ConditionsFor
                // conditions can reference it via GoapState.EngagementParticipant.
                _goap.SetValue(goap.State, GoapState.EngagementParticipant, other);
                var met = _goap.CheckCondition(actor, goap.State, conditions);
                _goap.RemoveKey(goap.State, GoapState.EngagementParticipant);

                if (!met)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Overload of <see cref="MeetsForwardRequirements(EngagementRolePrototype, EntityUid, Func{ProtoId{EngagementRolePrototype}, IEnumerable{EntityUid}?})"/>
    /// that reads "already occupied" from the live situation.
    /// </summary>
    private bool MeetsForwardRequirements(
        Entity<EngagementComponent> engagement,
        EngagementRolePrototype roleData,
        EntityUid actor)
        => MeetsForwardRequirements(roleData, actor, r => engagement.Comp.Actors.GetValueOrDefault(r));

    /// <summary>
    /// Checks whether roles that already have occupants declare <see cref="EngagementRolePrototype.ConditionsFor"/>
    /// entries pointing at <paramref name="role"/> — the reverse direction of
    /// <see cref="MeetsForwardRequirements(EngagementRolePrototype, EntityUid, Func{ProtoId{EngagementRolePrototype}, IEnumerable{EntityUid}?})"/>.
    /// For example, if Doctor.ConditionsFor["Patient"] is set, seating a new Patient must satisfy
    /// it against every current Doctor, even though Patient's own role might not reference Doctor at all.
    /// This was entirely missing before and let invalid one-sided assignments through.
    /// </summary>
    private bool MeetsReverseRequirements(
        ProtoId<EngagementRolePrototype> role,
        EntityUid candidate,
        IEnumerable<(ProtoId<EngagementRolePrototype> Role, EntityUid Uid)> assignedPairs)
    {
        foreach (var (otherRole, otherUid) in assignedPairs)
        {
            if (!_prototype.Resolve(otherRole, out var otherRoleData)
                || !otherRoleData.ConditionsFor.TryGetValue(role, out var conditions))
                continue;

            if (!_goapQuery.TryComp(otherUid, out var otherGoap))
                return false;

            _goap.SetValue(otherGoap.State, GoapState.EngagementParticipant, candidate);
            var met = _goap.CheckCondition(otherUid, otherGoap.State, conditions);
            _goap.RemoveKey(otherGoap.State, GoapState.EngagementParticipant);

            if (!met)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Overload of <see cref="MeetsReverseRequirements(ProtoId{EngagementRolePrototype}, EntityUid, IEnumerable{ValueTuple{ProtoId{EngagementRolePrototype}, EntityUid}})"/>
    /// that reads "already occupied" from the live situation.
    /// </summary>
    private bool MeetsReverseRequirements(
        Entity<EngagementComponent> engagement,
        ProtoId<EngagementRolePrototype> role,
        EntityUid candidate)
        => MeetsReverseRequirements(
            role,
            candidate,
            engagement.Comp.Actors.SelectMany(x => x.Value.Select(u => (x.Key, u))));

    /// <summary>
    /// Records the actor as occupying the role, applies <see cref="EngagementRolePrototype.OnStart"/>
    /// to its GoapState, fires <see cref="EngagementRoleJoined"/>, and checks whether the
    /// situation has now met every role's <see cref="EngagementRolePrototype.MinCount"/>.
    /// </summary>
    private void SeatActor(Entity<EngagementComponent> engagement, EngagementRolePrototype role, EntityUid actor)
    {
        if (!engagement.Comp.Actors.TryGetValue(role, out var set))
            engagement.Comp.Actors[role] = set = new();

        set.Add(actor);

        var participant = EnsureComp<EngagementParticipantComponent>(actor);
        participant.Membership[engagement.Owner] = role;

        if (engagement.Comp.Started && _goapQuery.TryComp(actor, out var goap))
            goap.State.OverwriteFrom(role.OnStart);

        var ev = new EngagementRoleJoined(engagement.Owner, actor, role);
        RaiseLocalEvent(actor, ev);
        RaiseLocalEvent(engagement.Owner, ev);

        CheckStarted(engagement);
    }

    /// <summary>
    /// Marks the situation as <see cref="EngagementComponent.Started"/> once every role has
    /// reached its <see cref="EngagementRolePrototype.MinCount"/>, and fires <see cref="EngagementStarted"/>.
    /// No-ops if already started.
    /// </summary>
    private void CheckStarted(Entity<EngagementComponent> engagement)
    {
        if (engagement.Comp.Started || !_prototype.TryIndex(engagement.Comp.Kind, out var proto))
            return;

        foreach (var roleId in proto.Roles)
        {
            if (!_prototype.Resolve(roleId, out var role))
                continue;

            var count = engagement.Comp.Actors.TryGetValue(roleId, out var set) ? set.Count : 0;

            if (count < role.MinCount)
                return;
        }

        engagement.Comp.Started = true;
        var ev = new EngagementStarted(engagement.Owner, engagement.Comp.Kind);
        RaiseLocalEvent(engagement.Owner, ev);

        foreach (var (roleId, actors) in engagement.Comp.Actors)
        {
            if (!_prototype.Resolve(roleId, out var role))
                continue;

            foreach (var actor in actors)
            {
                if (_goapQuery.TryComp(actor, out var goap))
                    goap.State.OverwriteFrom(role.OnStart);

                RaiseLocalEvent(actor, ev);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<EngagementComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            // Expire invites nobody accepted in time.
            if (comp.Invites.Count > 0)
            {
                foreach (var invite in comp.Invites.ToArray())
                {
                    if (invite.ValidUntil > _timing.CurTime)
                        continue;

                    RemoveInvite(new(uid, comp), invite.Uid);
                }
            }

            // Re-check "stay-in" requirements for roles that opted into continuous validation.
            foreach (var (roleId, actors) in comp.Actors.ToArray())
            {
                if (!_prototype.Resolve(roleId, out var role)
                    || !role.AlwaysConditionCheck)
                    continue;

                foreach (var actor in actors.ToArray())
                {
                    if (comp.NextConditionCheck.TryGetValue(actor, out var next) && next > _timing.CurTime)
                        continue;

                    comp.NextConditionCheck[actor] = _timing.CurTime + role.ConditionsCheckRate;

                    if (!MeetsForwardRequirements((uid, comp), role, actor)
                        || !MeetsReverseRequirements(new(uid, comp), role, actor))
                        LeaveEngagement((uid, comp), actor, EngagementEndReason.Dissolved);
                }
            }
        }
    }
}

/// <summary>
/// Reasons a participant or an entire situation stopped being active.
/// </summary>
[Serializable, NetSerializable]
public enum EngagementEndReason : byte
{
    /// <summary>
    /// The role or situation concluded normally (e.g. a scripted outcome was reached).
    /// </summary>
    Finished,

    /// <summary>
    /// The role or situation was cut short because a role's required conditions stopped
    /// holding, or a role's participant count dropped below its <see cref="EngagementRolePrototype.MinCount"/>.
    /// </summary>
    Dissolved,

    /// <summary>
    /// The participant left involuntarily — death, entity removal, or another external interruption.
    /// </summary>
    Interrupted,
}
