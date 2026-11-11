using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Domains;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Engagement.Systems;

/// <summary>
/// The keys of the domains <c>Engagement/...</c> (the situation in which the agent is engaged, its participants, its state and the state
/// of its owner, the invitations) and <c>OwnedEngagement/...</c> (the participants of the situations that the agent owns), see
/// <see cref="GoapState"/>. It finds their values, and it tells <see cref="GoapDomainSystem"/> for whom the values may have changed
/// when a situation changes: who joins it or leaves it, who is invited, who owns it, and when the state of the situation or
/// of its owner changes, so that those who depend on the keys (the search queries, for one) are up to date.
/// The variables of the situations (<c>Engagement/ProtoId/Var/Name</c>) are numbers that are changed all the time and are not
/// followed: they are looked at when they are needed.
/// </summary>
public sealed partial class EngagementGoapDomains : EntitySystem, IGoapDomainProvider
{
    public const string EngagementFamily = "Engagement";
    public const string OwnedFamily = "OwnedEngagement";

    [Dependency] private GoapDomainSystem _domains = null!;
    [Dependency] private SharedGoapSystem _goap = null!;
    [Dependency] private EngagementSystem _engagement = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private MathVariablesSystem _variables = null!;
    [Dependency] private IPrototypeManager _proto = null!;

    [Dependency] private EntityQuery<EngagementComponent> _engagementQuery;
    [Dependency] private EntityQuery<GoapComponent> _goapQuery;
    [Dependency] private EntityQuery<MathVariablesComponent> _variablesQuery;
    [Dependency] private EntityQuery<OwnershipComponent> _ownershipQuery;

    public override void Initialize()
    {
        base.Initialize();

        _domains.Register(this, EngagementFamily, OwnedFamily);
    }

    #region Values

    public bool TryResolve<T>(
        GoapState state,
        EntityUid owner,
        StateKey<T>[] domains,
        [NotNullWhen(true)] out object? value) where T : notnull
    {
        value = null;

        if (GoapState.InAnyEngagementDomain.Equals(domains))
        {
            value = TryComp(owner, out EngagementParticipantComponent? participant) && participant.Membership.Count > 0;
            return true;
        }

        if (GoapState.InEngagementDomain.TryGetParams(domains, out EntProtoId<EngagementComponent>? engagement))
        {
            if (!_proto.HasIndex(engagement))
                return false;

            value = _engagement.TryGetEngagement(owner, engagement.Value, out _, out _);
            return true;
        }

        if (GoapState.EngagementDomain.TryGetParams(domains, out engagement))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _))
                return false;

            value = ent.Value.Owner;
            return true;
        }

        if (GoapState.EngagementOwnerDomain.TryGetParams(domains, out engagement, out string? ownerKey))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _))
                return false;

            var ownerKeyId = new StateKey<T>(ownerKey);

            foreach (var situationOwner in _ownership.GetOwners(ent.Value.Owner))
            {
                if (!_goapQuery.TryComp(situationOwner, out var ownerGoap)
                    || !_goap.TryGetValue(ownerGoap.State, ownerKeyId, out var found))
                    continue;

                value = found;
                return true;
            }

            return false;
        }

        if (GoapState.EngagementStateDomain.TryGetParams(domains, out engagement, out string? stateKey))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _)
                || !_goapQuery.TryComp(ent.Value.Owner, out var situationGoap)
                || !_goap.TryGetValue(situationGoap.State, new StateKey<T>(stateKey), out var found))
                return false;

            value = found;
            return true;
        }

        if (GoapState.EngagementVarDomain.TryGetParams(domains, out engagement, out string? varName))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _)
                || !_variablesQuery.HasComp(ent.Value.Owner))
                return false;

            value = _variables.Get(ent.Value.Owner, varName);
            return true;
        }

        if (GoapState.EngagementRoleAllDomain.TryGetParams(domains, out engagement, out ProtoId<EngagementRolePrototype>? allRole))
        {
            if (!_proto.HasIndex(engagement)
                || !_proto.HasIndex(allRole)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _)
                || !_engagement.TryGetActors(ent.Value.AsNullable(), allRole.Value, out var actors))
                return false;

            value = new HashSet<EntityUid>(actors);
            return true;
        }

        if (GoapState.OwnedEngagementAllDomain.TryGetParams(domains, out engagement))
        {
            if (!_proto.HasIndex(engagement))
                return false;

            value = GetOwnedActors(owner, engagement.Value, null);
            return true;
        }

        if (GoapState.OwnedEngagementRoleAllDomain.TryGetParams(domains, out engagement, out allRole))
        {
            if (!_proto.HasIndex(engagement) || !_proto.HasIndex(allRole))
                return false;

            value = GetOwnedActors(owner, engagement.Value, allRole.Value);
            return true;
        }

        if (GoapState.EngagementStartedDomain.TryGetParams(domains, out engagement))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _)
                || !ent.Value.Comp.Started)
                return false;

            value = ent.Value.Owner;
            return true;
        }

        if (GoapState.InEngagementRoleDomain.TryGetParams(domains, out engagement, out ProtoId<EngagementRolePrototype>? role))
        {
            if (!_proto.HasIndex(engagement) || !_proto.HasIndex(role))
                return false;

            value = _engagement.TryFindEngagement(owner, role.Value, engagement.Value, out _);
            return true;
        }

        if (GoapState.EngagementRoleDomain.TryGetParams(domains, out engagement, out role))
        {
            if (!_proto.HasIndex(engagement)
                || !_proto.HasIndex(role)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _)
                || !_engagement.TryGetActors(ent.Value.AsNullable(), role.Value, out var actors)
                || actors.Count == 0)
                return false;

            value = actors.First();
            return true;
        }

        if (GoapState.EngagementInvitedDomain.TryGetParams(domains, out engagement))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetInviteEngagement(owner, engagement.Value, out var ent))
                return false;

            value = ent.Value.Owner;
            return true;
        }

        if (GoapState.EngagementInvitedInviterDomain.TryGetParams(domains, out engagement))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetInviteInviter(owner, engagement.Value, out var ent))
                return false;

            value = ent.Value;
            return true;
        }

        if (GoapState.EngagementInvitedRoleDomain.TryGetParams(domains, out engagement, out role))
        {
            if (!_proto.HasIndex(engagement)
                || !_proto.HasIndex(role)
                || !_engagement.TryGetInviteEngagement(owner, engagement.Value, out var ent, role))
                return false;

            value = ent.Value.Owner;
            return true;
        }

        if (GoapState.EngagementInvitedRoleInviterDomain.TryGetParams(domains, out engagement, out role))
        {
            if (!_proto.HasIndex(engagement)
                || !_proto.HasIndex(role)
                || !_engagement.TryGetInviteInviter(owner, engagement.Value, out var ent, role))
                return false;

            value = ent.Value;
            return true;
        }

        if (GoapState.EngagementInvitesRoleInvitedDomain.TryGetParams(domains, out engagement, out role))
        {
            if (!_proto.HasIndex(engagement)
                || !_proto.HasIndex(role)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _))
                return false;

            foreach (var invite in ent.Value.Comp.Invites)
            {
                if (invite.Role != role)
                    continue;

                value = invite.Uid;
                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    /// Returns the participants of the situations of a kind that the entity owns, in a role or in any.
    /// </summary>
    private HashSet<EntityUid> GetOwnedActors(
        EntityUid owner,
        Robust.Shared.Prototypes.EntProtoId<EngagementComponent> kind,
        ProtoId<EngagementRolePrototype>? role)
    {
        var result = new HashSet<EntityUid>();

        foreach (var owned in _ownership.GetOwned(owner))
        {
            if (!_engagementQuery.TryComp(owned, out var comp) || Prototype(owned)?.ID != kind.Id)
                continue;

            foreach (var (actorRole, actors) in comp.Actors)
            {
                if (role == null || role == actorRole)
                    result.UnionWith(actors);
            }
        }

        return result;
    }

    #endregion

    #region Changes

    [SubscribeLocalEvent]
    private void OnChanged(ref EngagementChanged args)
    {
        if (_engagementQuery.TryComp(args.Engagement, out var situation))
            Changed((args.Engagement, situation), args.Extra1, args.Extra2);
    }

    [SubscribeLocalEvent]
    private void OnOwnershipAdded(Entity<EngagementComponent> ent, ref OwnershipAddedEvent args)
        => Changed(ent, owner: args.Owner);

    [SubscribeLocalEvent]
    private void OnOwnershipRemoved(Entity<EngagementComponent> ent, ref OwnershipRemovedEvent args)
        => Changed(ent, owner: args.Owner);

    /// <summary>
    /// Something in the state of an agent has been set or removed: if the agent is a situation, or owns some, those who are in them
    /// have it as their <c>Engagement/ProtoId/State/Key</c> or <c>Engagement/ProtoId/Owner/Key</c>.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnStateKeyChanged(ref GoapStateKeyChanged args)
    {
        if (!_domains.IsWatched(EngagementFamily))
            return;

        if (_engagementQuery.TryComp(args.Agent, out var situation))
            Changed((args.Agent, situation));

        if (!_ownershipQuery.TryComp(args.Agent, out var ownership))
            return;

        foreach (var owned in ownership.Owned)
        {
            if (_engagementQuery.TryComp(owned, out var owned_situation))
                Changed((owned, owned_situation), owner: args.Agent);
        }
    }

    /// <summary>
    /// Tells that the keys may have changed for the participants of the situation, those who are invited to it, and those who own
    /// it.
    /// </summary>
    /// <param name="ent">The situation.</param>
    /// <param name="extra">Others that it is about, such as the one who has just left.</param>
    private void Changed(Entity<EngagementComponent> ent, EntityUid? extra1 = null, EntityUid? extra2 = null, EntityUid? owner = null)
    {
        if (!_domains.IsWatched(EngagementFamily) && !_domains.IsWatched(OwnedFamily))
            return;

        foreach (var actors in ent.Comp.Actors.Values)
        {
            foreach (var actor in actors)
            {
                _domains.Invalidate(actor, EngagementFamily);
            }
        }

        foreach (var invite in ent.Comp.Invites)
        {
            _domains.Invalidate(invite.Uid, EngagementFamily);
        }

        foreach (var other in new[] { extra1, extra2 })
        {
            if (other is { } uid)
                _domains.Invalidate(uid, EngagementFamily);
        }

        if (owner is { } changedOwner)
            _domains.Invalidate(changedOwner, OwnedFamily);

        if (!_ownershipQuery.TryComp(ent, out var ownership))
            return;

        foreach (var situationOwner in ownership.Owners)
        {
            _domains.Invalidate(situationOwner, OwnedFamily);
        }
    }

    #endregion
}
