using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Engagement.Systems;

public partial class EngagementSystem
{
    #region Start

    /// <summary>
    /// Creates a new, empty situation with the given initiator. No roles are assigned yet —
    /// use <see cref="TryJoinEngagement"/> or <see cref="InviteToEngagement"/> to seat participants,
    /// including the initiator themselves if they need to occupy a role.
    /// </summary>
    /// <param name="protoId">The kind of situation to create.</param>
    /// <param name="initiator">
    /// The entity that caused this situation to exist. Used for <see cref="EngagementRole.InitiatorOnly"/> checks.
    /// </param>
    /// <param name="engagement">The created situation entity, if successful.</param>
    /// <returns>True, if the situation was created.</returns>
    [PublicAPI]
    public bool TryStartEngagement(
        ProtoId<EngagementPrototype> protoId,
        EntityUid initiator,
        [NotNullWhen(true)] out Entity<EngagementComponent>? engagement)
    {
        engagement = null;

        if (!_prototype.HasIndex(protoId))
            return false;

        var uid = Spawn();
        var comp = EnsureComp<EngagementComponent>(uid);
        comp.Kind = protoId;
        comp.Initiator = initiator;

        engagement = (uid, comp);
        return true;
    }

    /// <summary>
    /// Creates a new situation and attempts to seat or invite every entity in <paramref name="invited"/>
    /// into the first role they qualify for (skipping <see cref="EngagementRole.InitiatorOnly"/> roles
    /// and roles that are already full).
    /// </summary>
    /// <param name="protoId">The kind of situation to create.</param>
    /// <param name="initiator">The entity that caused this situation to exist.</param>
    /// <param name="invited">Candidate entities to seat/invite into the new situation.</param>
    /// <param name="engagement">The created situation entity, if successful.</param>
    /// <returns>True, if the situation was created. Individual candidates may still fail to be seated.</returns>
    [PublicAPI]
    public bool TryStartEngagement(
        ProtoId<EngagementPrototype> protoId,
        EntityUid initiator,
        IEnumerable<EntityUid> invited,
        [NotNullWhen(true)] out Entity<EngagementComponent>? engagement)
    {
        if (!TryStartEngagement(protoId, initiator, out engagement)
            || !_prototype.TryIndex(protoId, out var proto))
            return engagement != null;

        foreach (var actor in invited)
        {
            if (!TryFindRole(proto, engagement.Value, actor, out var role))
                continue;

            if (proto.Roles[role].Force)
                TryJoinEngagement(engagement.Value.AsNullable(), role, actor);
            else
                InviteToEngagement(engagement.Value.AsNullable(), role, actor);
        }

        return true;
    }

    /// <summary>
    /// Picks the first role in <paramref name="proto"/> that <paramref name="actor"/> could take:
    /// not <see cref="EngagementRole.InitiatorOnly"/>, has free capacity, and (for non-forced roles)
    /// satisfies <see cref="EngagementRole.Conditions"/>/<see cref="EngagementRole.ConditionsFor"/>.
    /// </summary>
    private bool TryFindRole(
        EngagementPrototype proto,
        Entity<EngagementComponent> engagement,
        EntityUid actor,
        [NotNullWhen(true)] out string? role)
    {
        foreach (var (roleId, roleData) in proto.Roles)
        {
            if (roleData.InitiatorOnly)
                continue;

            var current = engagement.Comp.Actors.TryGetValue(roleId, out var set) ? set.Count : 0;

            if (current >= roleData.MaxCount)
                continue;

            if (!roleData.Force && !MeetsRoleRequirements(engagement, roleData, actor))
                continue;

            role = roleId;
            return true;
        }

        role = null;
        return false;
    }

    #endregion

    #region Join / Invite

    /// <summary>
    /// Directly seats an actor into a <see cref="EngagementRole.Force"/> role, bypassing consent
    /// and the role's conditions entirely. Use this for situations the actor is dragged into
    /// regardless of what it wants — e.g. the victim of an attack.
    /// </summary>
    /// <param name="engagement">The situation to join.</param>
    /// <param name="role">The role to occupy. Must have <see cref="EngagementRole.Force"/> set to true.</param>
    /// <param name="actor">The entity to seat.</param>
    /// <returns>True, if the actor was seated.</returns>
    [PublicAPI]
    public bool TryJoinEngagement(Entity<EngagementComponent?> engagement, string role, EntityUid actor)
    {
        if (!Resolve(engagement, ref engagement.Comp, false)
            || !_prototype.TryIndex(engagement.Comp.Kind, out var proto)
            || !proto.Roles.TryGetValue(role, out var roleData)
            || !roleData.Force)
            return false;

        if (!CanSeat(engagement!, role, roleData, actor))
            return false;

        SeatActor(engagement!, role, roleData, actor);
        return true;
    }

    /// <summary>
    /// Sends an invitation for a non-forced role. The actor must call <see cref="AcceptInvite"/>
    /// before <see cref="EngagementRole.InviteTime"/> elapses, or the invite expires on its own
    /// during <see cref="Update"/>.
    /// </summary>
    /// <param name="engagement">The situation to invite into.</param>
    /// <param name="role">The role being offered. Must have <see cref="EngagementRole.Force"/> set to false.</param>
    /// <param name="actor">The entity being invited.</param>
    /// <returns>True, if the invite was sent.</returns>
    [PublicAPI]
    public bool InviteToEngagement(Entity<EngagementComponent?> engagement, string role, EntityUid actor)
    {
        if (!Resolve(engagement, ref engagement.Comp, false)
            || !_prototype.TryIndex(engagement.Comp.Kind, out var proto)
            || !proto.Roles.TryGetValue(role, out var roleData)
            || roleData.Force)
            return false;

        if (!CanSeat(engagement!, role, roleData, actor) || !MeetsRoleRequirements(engagement!, roleData, actor))
            return false;

        var validUntil = _timing.CurTime + roleData.InviteTime;
        engagement.Comp.Invites.Add((role, actor, validUntil));

        var participant = EnsureComp<EngagementParticipantComponent>(actor);
        participant.Invites.Add((engagement.Owner, engagement.Comp.Initiator, role));

        var ev = new EngagementInviteSent(engagement.Owner, actor, role);
        RaiseLocalEvent(actor, ev);
        RaiseLocalEvent(engagement.Owner, ev);
        return true;
    }

    /// <summary>
    /// Accepts a pending invite to a situation, seating the actor into the offered role.
    /// </summary>
    /// <param name="actor">The entity accepting the invite.</param>
    /// <param name="engagement">The situation being joined.</param>
    /// <returns>True, if a valid, non-expired invite was found and accepted.</returns>
    [PublicAPI]
    public bool AcceptInvite(EntityUid actor, Entity<EngagementComponent?> engagement)
    {
        if (!Resolve(engagement, ref engagement.Comp, false)
            || !_participantQuery.TryComp(actor, out var participant))
            return false;

        (EntityUid EngageUid, EntityUid Inviter, string Role)? ownInvite = null;

        foreach (var entry in participant.Invites)
        {
            if (entry.EngageUid != engagement.Owner)
                continue;

            ownInvite = entry;
            break;
        }

        if (ownInvite == null)
            return false;

        (string Role, EntityUid Uid, TimeSpan ValidUntil)? matched = null;

        foreach (var entry in engagement.Comp.Invites)
        {
            if (entry.Uid != actor || entry.Role != ownInvite.Value.Role)
                continue;

            matched = entry;
            break;
        }

        if (matched == null || matched.Value.ValidUntil < _timing.CurTime
            || !_prototype.TryIndex(engagement.Comp.Kind, out var proto)
            || !proto.Roles.TryGetValue(matched.Value.Role, out var roleData))
            return false;

        engagement.Comp.Invites.Remove(matched.Value);
        participant.Invites.Remove(ownInvite.Value);

        SeatActor(engagement!, matched.Value.Role, roleData, actor);
        return true;
    }

    /// <summary>
    /// Withdraws a pending invite without waiting for it to expire or be accepted.
    /// </summary>
    /// <param name="engagement">The situation the invite belongs to.</param>
    /// <param name="actor">The invited entity whose invite should be withdrawn.</param>
    [PublicAPI]
    public void RemoveInvite(Entity<EngagementComponent?> engagement, EntityUid actor)
    {
        if (!Resolve(engagement, ref engagement.Comp, false))
            return;

        engagement.Comp.Invites.RemoveWhere(x => x.Uid == actor);

        if (_participantQuery.TryComp(actor, out var participant))
            participant.Invites.RemoveWhere(x => x.EngageUid == engagement.Owner);

        var ev = new EngagementInviteRemoved(engagement.Owner, actor);
        RaiseLocalEvent(actor, ev);
        RaiseLocalEvent(engagement.Owner, ev);
    }

    #endregion

    #region Leave / End

    /// <summary>
    /// Removes a single actor from the situation, applying <see cref="EngagementRole.OnFinish"/>
    /// and <see cref="EngagementRole.OnFinishRemove"/> to its GoapState. If this drops the actor's
    /// role below <see cref="EngagementRole.MinCount"/> and <see cref="EngagementPrototype.DissolveInvalid"/>
    /// is set, the whole situation is dissolved for every remaining participant.
    /// </summary>
    /// <param name="engagement">The situation to leave.</param>
    /// <param name="actor">The entity leaving.</param>
    /// <param name="reason">Why the actor is leaving.</param>
    [PublicAPI]
    public void LeaveEngagement(Entity<EngagementComponent?> engagement, EntityUid actor, EngagementEndReason reason)
    {
        if (!Resolve(engagement, ref engagement.Comp, false)
            || !_participantQuery.TryComp(actor, out var participant)
            || !participant.Membership.TryGetValue(engagement.Owner, out var role))
            return;

        LeaveEngagementInternal(engagement!, actor, role, reason, cleanupParticipant: true);
    }

    private void LeaveEngagementInternal(
        Entity<EngagementComponent> engagement,
        EntityUid actor,
        string role,
        EngagementEndReason reason,
        bool cleanupParticipant)
    {
        if (!engagement.Comp.Actors.TryGetValue(role, out var set) || !set.Remove(actor))
            return;

        if (set.Count == 0)
            engagement.Comp.Actors.Remove(role);

        engagement.Comp.NextConditionCheck.Remove(actor);

        if (cleanupParticipant && _participantQuery.TryComp(actor, out var participant))
        {
            participant.Membership.Remove(engagement.Owner);

            if (participant.Membership.Count == 0)
                RemCompDeferred<EngagementParticipantComponent>(actor);
        }

        _prototype.TryIndex(engagement.Comp.Kind, out var proto);

        if (proto != null && proto.Roles.TryGetValue(role, out var roleData) && _goapQuery.TryComp(actor, out var goap))
        {
            goap.State.OverwriteFrom(roleData.OnFinish);

            foreach (var key in roleData.OnFinishRemove)
            {
                goap.State.Remove(key);
            }
        }

        var ev = new EngagementRoleLeft(engagement.Owner, actor, role, reason);
        RaiseLocalEvent(actor, ev);
        RaiseLocalEvent(engagement.Owner, ev);

        if (!engagement.Comp.Started || proto is not { DissolveInvalid: true } || !proto.Roles.TryGetValue(role, out var rd))
            return;

        var remaining = engagement.Comp.Actors.TryGetValue(role, out var left) ? left.Count : 0;

        if (remaining < rd.MinCount)
            EndEngagement(engagement.AsNullable(), EngagementEndReason.Dissolved);
    }

    /// <summary>
    /// Dissolves the whole situation, removing every participant and applying each of their
    /// roles' <see cref="EngagementRole.OnFinish"/>/<see cref="EngagementRole.OnFinishRemove"/>.
    /// </summary>
    /// <param name="engagement">The situation to end.</param>
    /// <param name="reason">Why the situation ended.</param>
    [PublicAPI]
    public void EndEngagement(Entity<EngagementComponent?> engagement, EngagementEndReason reason)
    {
        if (!Resolve(engagement, ref engagement.Comp, false))
            return;

        var copy = engagement.Comp.Actors
            .ToDictionary(x => x.Key, x => x.Value.ToArray());

        foreach (var (role, actors) in copy)
        {
            foreach (var actor in actors)
            {
                LeaveEngagementInternal(engagement!, actor, role, reason, cleanupParticipant: true);
            }
        }

        var ev = new EngagementEnded(engagement.Owner, reason);
        RaiseLocalEvent(engagement.Owner, ev);

        QueueDel(engagement.Owner);
    }

    #endregion

    #region Queries

    /// <summary>
    /// Finds an active situation of the given kind that the entity is a participant of.
    /// </summary>
    /// <param name="ent">The candidate participant.</param>
    /// <param name="protoId">The kind of situation to look for.</param>
    /// <param name="engagement">The found situation, if any.</param>
    /// <param name="roleId">The role the entity occupies in that situation, if found.</param>
    [PublicAPI, Pure]
    public bool TryGetEngagement(
        Entity<EngagementParticipantComponent?> ent,
        ProtoId<EngagementPrototype> protoId,
        [NotNullWhen(true)] out Entity<EngagementComponent>? engagement,
        [NotNullWhen(true)] out string? roleId)
    {
        engagement = null;
        roleId = null;

        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        foreach (var (uid, id) in ent.Comp.Membership)
        {
            if (!_engagementQuery.TryComp(uid, out var comp) || comp.Kind != protoId)
                continue;

            engagement = new(uid, comp);
            roleId = id;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds an active situation of the given kind in which <paramref name="actor"/> already
    /// occupies <paramref name="role"/>. Use this to fold new participants into an ongoing
    /// situation instead of starting a parallel one — e.g. the same attacker adding a second
    /// victim to its existing Combat situation rather than creating a new one per hit.
    /// </summary>
    /// <param name="actor">The entity whose existing role is being looked up.</param>
    /// <param name="role">The role the entity is expected to already occupy.</param>
    /// <param name="protoId">The kind of situation to look for.</param>
    /// <param name="engagement">The found situation, if any.</param>
    [PublicAPI, Pure]
    public bool TryFindEngagement(
        EntityUid actor,
        string role,
        ProtoId<EngagementPrototype> protoId,
        [NotNullWhen(true)] out Entity<EngagementComponent>? engagement)
    {
        engagement = null;

        if (!_participantQuery.TryComp(actor, out var participant))
            return false;

        foreach (var (uid, actorRole) in participant.Membership)
        {
            if (actorRole != role || !_engagementQuery.TryComp(uid, out var comp) || comp.Kind != protoId)
                continue;

            engagement = (uid, comp);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the role an entity occupies in a specific situation, if any.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetRole(
        Entity<EngagementParticipantComponent?> ent,
        EntityUid engagement,
        [NotNullWhen(true)] out string? role)
    {
        role = null;
        return Resolve(ent, ref ent.Comp, false) && ent.Comp.Membership.TryGetValue(engagement, out role);
    }

    #endregion
}
