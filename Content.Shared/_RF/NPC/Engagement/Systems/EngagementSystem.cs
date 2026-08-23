using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using System.Linq;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RF.NPC.Engagement.Systems;

/// <summary>
/// A system that provides an API for engaging multiple AI agents in a single situation.
/// </summary>
/// <remarks>
/// This system only tracks membership, consent and the GoapState side-effects of joining/leaving
/// a role. It never decides how an agent should behave while engaged — that is entirely up to the
/// agent's own GOAP/UAI configuration reacting to the state keys written by <see cref="EngagementRole.OnStart"/>.
/// </remarks>
public sealed partial class EngagementSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedGoapSystem _goap = default!;

    [Dependency] private readonly EntityQuery<EngagementComponent> _engagementQuery = default!;
    [Dependency] private readonly EntityQuery<EngagementParticipantComponent> _participantQuery = default!;
    [Dependency] private readonly EntityQuery<GoapComponent> _goapQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EngagementParticipantComponent, ComponentRemove>(OnParticipantRemove);
        SubscribeLocalEvent<EngagementParticipantComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EngagementComponent, ComponentShutdown>(OnEngagementShutdown);
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

    private void OnEngagementShutdown(Entity<EngagementComponent> ent, ref ComponentShutdown args)
    {
        // Defensive cleanup in case the situation entity gets deleted directly instead
        // of going through EndEngagement (e.g. map cleanup).
        foreach (var (_, actors) in ent.Comp.Actors)
        {
            foreach (var actor in actors)
            {
                if (_participantQuery.TryComp(actor, out var participant))
                    participant.Membership.Remove(ent.Owner);
            }
        }
    }

    #region Join / Invite

    /// <summary>
    /// Common capacity/InitiatorOnly/duplicate-membership checks shared by
    /// <see cref="TryJoinEngagement"/> and <see cref="InviteToEngagement"/>.
    /// </summary>
    private bool CanSeat(Entity<EngagementComponent> engagement, string role, EngagementRole roleData, EntityUid actor)
    {
        if (roleData.InitiatorOnly && actor != engagement.Comp.Initiator)
            return false;

        var current = engagement.Comp.Actors.TryGetValue(role, out var set) ? set.Count : 0;

        if (current >= roleData.MaxCount)
            return false;

        // Already a participant of this exact situation (in this or another role).
        if (_participantQuery.TryComp(actor, out var participant) && participant.Membership.ContainsKey(engagement.Owner))
            return false;

        return true;
    }

    /// <summary>
    /// Checks <see cref="EngagementRole.Conditions"/> and <see cref="EngagementRole.ConditionsFor"/>
    /// for a candidate trying to take a non-forced role.
    /// </summary>
    private bool MeetsRoleRequirements(Entity<EngagementComponent> engagement, EngagementRole roleData, EntityUid actor)
    {
        if (!_goapQuery.TryComp(actor, out var goap))
            return roleData.Conditions.Count == 0 && roleData.ConditionsFor.Count == 0;

        if (!_goap.CheckCondition(actor, goap.State, roleData.Conditions))
            return false;

        foreach (var (otherRole, conditions) in roleData.ConditionsFor)
        {
            if (!engagement.Comp.Actors.TryGetValue(otherRole, out var others))
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
    /// Records the actor as occupying the role, applies <see cref="EngagementRole.OnStart"/>
    /// to its GoapState, fires <see cref="EngagementRoleJoined"/>, and checks whether the
    /// situation has now met every role's <see cref="EngagementRole.MinCount"/>.
    /// </summary>
    private void SeatActor(Entity<EngagementComponent> engagement, string role, EngagementRole roleData, EntityUid actor)
    {
        if (!engagement.Comp.Actors.TryGetValue(role, out var set))
            engagement.Comp.Actors[role] = set = new();

        set.Add(actor);

        var participant = EnsureComp<EngagementParticipantComponent>(actor);
        participant.Membership[engagement.Owner] = role;

        if (_goapQuery.TryComp(actor, out var goap))
            goap.State.OverwriteFrom(roleData.OnStart);

        var ev = new EngagementRoleJoined(engagement.Owner, actor, role);
        RaiseLocalEvent(actor, ev);
        RaiseLocalEvent(engagement.Owner, ev);

        CheckStarted(engagement);
    }

    /// <summary>
    /// Marks the situation as <see cref="EngagementComponent.Started"/> once every role has
    /// reached its <see cref="EngagementRole.MinCount"/>, and fires <see cref="EngagementStarted"/>.
    /// No-ops if already started.
    /// </summary>
    private void CheckStarted(Entity<EngagementComponent> engagement)
    {
        if (engagement.Comp.Started || !_prototype.TryIndex(engagement.Comp.Kind, out var proto))
            return;

        foreach (var (roleId, roleData) in proto.Roles)
        {
            var count = engagement.Comp.Actors.TryGetValue(roleId, out var set) ? set.Count : 0;

            if (count < roleData.MinCount)
                return;
        }

        engagement.Comp.Started = true;
        var ev = new EngagementStarted(engagement.Owner, engagement.Comp.Kind);
        RaiseLocalEvent(engagement.Owner, ev);

        foreach (var (_, actors) in engagement.Comp.Actors)
        {
            foreach (var actor in actors)
            {
                RaiseLocalEvent(actor, ev);
            }
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<EngagementComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (!_prototype.TryIndex(comp.Kind, out var proto))
                continue;

            // Expire invites nobody accepted in time.
            if (comp.Invites.Count > 0)
            {
                foreach (var invite in comp.Invites.ToArray())
                {
                    if (invite.ValidUntil > _timing.CurTime)
                        continue;

                    RemoveInvite((uid, comp), invite.Uid);
                }
            }

            // Re-check "stay-in" requirements for roles that opted into continuous validation.
            foreach (var (role, actors) in comp.Actors.ToArray())
            {
                if (!proto.Roles.TryGetValue(role, out var roleData) || !roleData.AlwaysConditionCheck)
                    continue;

                foreach (var actor in actors.ToArray())
                {
                    if (comp.NextConditionCheck.TryGetValue(actor, out var next) && next > _timing.CurTime)
                        continue;

                    comp.NextConditionCheck[actor] = _timing.CurTime + roleData.ConditionsCheckRate;

                    if (!MeetsRoleRequirements((uid, comp), roleData, actor))
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
    /// holding, or a role's participant count dropped below its <see cref="EngagementRole.MinCount"/>.
    /// </summary>
    Dissolved,

    /// <summary>
    /// The participant left involuntarily — death, entity removal, or another external interruption.
    /// </summary>
    Interrupted,
}
