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
    /// The entity that caused this situation to exist. Used for <see cref="EngagementRolePrototype.InitiatorOnly"/> checks.
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
    /// Creates a new situation and attempts to seat or invite entities from <paramref name="invited"/>
    /// into roles according to <see cref="EngagementRolePrototype.Conditions"/>/<see cref="EngagementRolePrototype.ConditionsFor"/>.
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
        engagement = null;

        if (!_prototype.Resolve(protoId, out var proto))
            return false;

        if (!TryAssignRoles(proto, initiator, invited, out var assignment))
            return false;

        if (!TryStartEngagement(protoId, initiator, out engagement))
            return false;

        foreach (var (roleId, actors) in assignment)
        {
            if (!_prototype.Resolve(roleId, out var role))
                continue;

            foreach (var actor in actors)
            {
                if (role.Force || actor == initiator)
                    TryJoinEngagement(engagement.Value.AsNullable(), roleId, actor);
                else
                    InviteToEngagementInternal(engagement.Value, roleId, actor, initiator);
            }
        }

        return true;
    }

    #endregion

    #region Join / Invite

    /// <summary>
    /// Directly seats an actor into a <see cref="EngagementRolePrototype.Force"/> role, bypassing consent
    /// and the role's conditions entirely. Use this for situations the actor is dragged into
    /// regardless of what it wants — e.g. the victim of an attack.
    /// </summary>
    /// <param name="engagement">The situation to join.</param>
    /// <param name="roleId">The role to occupy. Must have <see cref="EngagementRolePrototype.Force"/> set to true.</param>
    /// <param name="actor">The entity to seat.</param>
    /// <returns>True, if the actor was seated.</returns>
    [PublicAPI]
    public bool TryJoinEngagement(
        Entity<EngagementComponent?> engagement,
        ProtoId<EngagementRolePrototype> roleId,
        EntityUid actor)
    {
        if (!Resolve(engagement, ref engagement.Comp)
            || !_prototype.Resolve(roleId, out var role))
            return false;

        if (!role.Force && actor != engagement.Comp.Initiator)
            return false;

        if (!CanSeat(engagement!, role, actor))
            return false;

        SeatActor(engagement!, role, actor);
        return true;
    }

    /// <summary>
    /// Sends an invitation for a non-forced role. The actor must call <see cref="AcceptInvite(EntityUid, Entity{EngagementComponent?})"/>
    /// before <see cref="EngagementPrototype.InviteTime"/> elapses, or the invite expires on its own
    /// during <see cref="Update"/>.
    /// </summary>
    /// <param name="engagement">The situation to invite into.</param>
    /// <param name="roleId">The role being offered. Must have <see cref="EngagementRolePrototype.Force"/> set to false.</param>
    /// <param name="actor">The entity being invited.</param>
    /// <param name="inviter">The inviter entity.</param>
    /// <returns>True, if the invite was sent.</returns>
    [PublicAPI]
    public bool InviteToEngagement(
        Entity<EngagementComponent?> engagement,
        ProtoId<EngagementRolePrototype> roleId,
        EntityUid actor,
        EntityUid inviter)
    {
        if (!Resolve(engagement, ref engagement.Comp)
            || !_prototype.Resolve(roleId, out var role)
            || actor == inviter
            || role.Force)
            return false;

        if (!CanSeat(engagement!, role, actor)
            || !MeetsForwardRequirements(engagement!, role, actor)
            || !MeetsReverseRequirements(engagement!, role, actor))
            return false;

        InviteToEngagementInternal(engagement!, roleId, actor, inviter);
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
        if (!Resolve(engagement, ref engagement.Comp)
            || !_participantQuery.TryComp(actor, out var participant))
            return false;

        (EntityUid EngageUid, EntityUid Inviter, ProtoId<EngagementRolePrototype> Role)? ownInvite = null;

        foreach (var entry in participant.Invites)
        {
            if (entry.EngageUid != engagement.Owner)
                continue;

            ownInvite = entry;
            break;
        }

        if (ownInvite == null)
            return false;

        (ProtoId<EngagementRolePrototype> Role, EntityUid Uid, TimeSpan ValidUntil)? matched = null;

        foreach (var entry in engagement.Comp.Invites)
        {
            if (entry.Uid != actor || entry.Role != ownInvite.Value.Role)
                continue;

            matched = entry;
            break;
        }

        if (matched == null
            || matched.Value.ValidUntil < _timing.CurTime
            || !_prototype.Resolve(matched.Value.Role, out var role)
            || role.Force)
            return false;

        RemoveInvite(engagement, actor, ownInvite.Value.Role);
        SeatActor(engagement!, role, actor);
        return true;
    }

    /// <summary>
    /// Accepts a pending invite by the entity that sent it, without needing to know which situation
    /// entity the invite belongs to. Convenience overload for search-driven flows where it's more
    /// natural to look candidates up by inviter than by the (otherwise invisible) session entity.
    /// </summary>
    /// <param name="actor">The entity accepting the invite.</param>
    /// <param name="inviter">
    /// The entity that initiated the situation the invite belongs to (i.e. <see cref="EngagementComponent.Initiator"/>).
    /// </param>
    /// <returns>True, if a matching, non-expired invite was found and accepted.</returns>
    [PublicAPI]
    public bool AcceptInvite(EntityUid actor, EntityUid inviter)
    {
        if (!_participantQuery.TryComp(actor, out var participant))
            return false;

        foreach (var entry in participant.Invites)
        {
            if (entry.Inviter == inviter)
                return AcceptInvite(actor, new Entity<EngagementComponent?>(entry.EngageUid, null));
        }

        return false;
    }

    /// <summary>
    /// Withdraws a pending invite without waiting for it to expire or be accepted.
    /// </summary>
    /// <param name="engagement">The situation the invite belongs to.</param>
    /// <param name="actor">The invited entity whose invite should be withdrawn.</param>
    /// <param name="role"></param>
    [PublicAPI]
    public void RemoveInvite(
        Entity<EngagementComponent?> engagement,
        Entity<EngagementParticipantComponent?> actor,
        string? role = null)
    {
        if (!Resolve(engagement, ref engagement.Comp)
            || !Resolve(actor, ref actor.Comp))
            return;

        engagement.Comp.Invites.RemoveWhere(x => x.Uid == actor.Owner && (role == null || x.Role == role));

        foreach (var invite in actor.Comp.Invites.ToArray())
        {
            if (invite.EngageUid != engagement.Owner)
                continue;

            if (role != null && invite.Role != role)
                continue;

            actor.Comp.Invites.Remove(invite);
            var ev = new EngagementInviteRemoved(engagement.Owner, invite.Inviter, actor);
            RaiseLocalEvent(actor, ev);
            RaiseLocalEvent(engagement, ev);
            RaiseLocalEvent(invite.Inviter, ev);
        }
    }

    #endregion

    #region Leave / End

    /// <summary>
    /// Removes a single actor from the situation, applying <see cref="EngagementRolePrototype.OnFinish"/>
    /// and <see cref="EngagementRolePrototype.OnFinishRemove"/> to its GoapState. If this drops the actor's
    /// role below <see cref="EngagementRolePrototype.MinCount"/> and <see cref="EngagementPrototype.DissolveInvalid"/>
    /// is set, the whole situation is dissolved for every remaining participant.
    /// </summary>
    /// <param name="engagement">The situation to leave.</param>
    /// <param name="actor">The entity leaving.</param>
    /// <param name="reason">Why the actor is leaving.</param>
    [PublicAPI]
    public void LeaveEngagement(Entity<EngagementComponent?> engagement, EntityUid actor, EngagementEndReason reason)
    {
        if (!Resolve(engagement, ref engagement.Comp)
            || !_participantQuery.TryComp(actor, out var participant)
            || !participant.Membership.TryGetValue(engagement.Owner, out var role))
            return;

        LeaveEngagementInternal(engagement!, actor, role, reason, cleanupParticipant: true);
    }

    /// <summary>
    /// Dissolves the whole situation, removing every participant and applying each of their
    /// roles' <see cref="EngagementRolePrototype.OnFinish"/>/<see cref="EngagementRolePrototype.OnFinishRemove"/>.
    /// </summary>
    /// <param name="engagement">The situation to end.</param>
    /// <param name="reason">Why the situation ended.</param>
    [PublicAPI]
    public void EndEngagement(Entity<EngagementComponent?> engagement, EngagementEndReason reason)
    {
        if (!Resolve(engagement, ref engagement.Comp))
            return;

        if (reason == EngagementEndReason.Finished)
        {
            foreach (var (roleId, entities) in engagement.Comp.Actors)
            {
                if (!_prototype.Resolve(roleId, out var role))
                    continue;

                foreach (var uid in entities)
                {
                    _entityEffects.ApplyEffects(uid, role.Effects);
                }
            }
        }

        var copy = engagement.Comp.Actors
            .ToDictionary(x => x.Key, x => x.Value.ToArray());

        foreach (var (role, actors) in copy)
        {
            foreach (var actor in actors)
            {
                LeaveEngagementInternal(engagement!, actor, role, reason, cleanupParticipant: true);
            }
        }

        foreach (var invite in engagement.Comp.Invites.ToArray())
        {
            RemoveInvite(engagement, invite.Uid);
        }

        var ev = new EngagementEnded(engagement.Owner, reason);
        RaiseLocalEvent(engagement.Owner, ev);
        Del(engagement.Owner);
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
        [NotNullWhen(true)] out ProtoId<EngagementRolePrototype>? roleId)
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
        ProtoId<EngagementRolePrototype> role,
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
        [NotNullWhen(true)] out ProtoId<EngagementRolePrototype>? role)
    {
        role = null;

        if (!Resolve(ent, ref ent.Comp, false) || !ent.Comp.Membership.TryGetValue(engagement, out var roleId))
            return false;

        role = roleId;
        return true;
    }

    /// <summary>
    /// Returns the entities that play a specific role in the situation.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetActors(
        Entity<EngagementComponent?> ent,
        ProtoId<EngagementRolePrototype> role,
        [NotNullWhen(true)] out IReadOnlySet<EntityUid>? actors)
    {
        actors = null;

        if (!Resolve(ent, ref ent.Comp, false)
            || !ent.Comp.Actors.TryGetValue(role, out var entities)
            || entities.Count == 0)
            return false;

        actors = entities;
        return true;
    }

    #endregion
}
