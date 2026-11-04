using System.Linq;
using System.Numerics;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.Groups.Components;
using Content.Shared._RF.NPC.Systems;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Groups.Systems;

/// <summary>
/// Works with the groups of an owner: the situations of one kind (<see cref="EngagementSystem"/>) that the entity owns
/// (<see cref="OwnershipSystem"/>), such as the squads of a raid, the guards of a caravan, a brigade of workers. A kind of a group is
/// an entity prototype with the <c>Engagement</c> component, so its roles, their weights and their conditions are the ones to set up in
/// YAML, and the system does what is common for any set of groups: finds them and their participants, divides the members into groups,
/// puts a member in a group that has a place, moves members from group to group, joins groups, fills the roles that are empty, and
/// puts an end to the groups that nobody is in. The roles are always given by the engagement system, according to the roles of the
/// kind. How strong a group is, which groups are weak, and what the groups do, is up to the effects, the curves and the
/// <c>MathVariables</c> of the groups.
/// </summary>
public sealed partial class GroupsSystem : EntitySystem
{
    [Dependency] private EngagementSystem _engagement = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private SharedTransformSystem _transform = null!;

    [Dependency] private EntityQuery<EngagementComponent> _engagementQuery;
    [Dependency] private EntityQuery<GroupDissolvingComponent> _dissolvingQuery;

    #region Queries

    /// <summary>
    /// The groups of a kind that the entity owns.
    /// </summary>
    [PublicAPI]
    public List<Entity<EngagementComponent>> GetGroups(EntityUid owner, EntProtoId<EngagementComponent> kind)
    {
        var groups = new List<Entity<EngagementComponent>>();

        foreach (var owned in _ownership.GetOwned(owner))
        {
            if (_engagementQuery.TryComp(owned, out var comp) && Prototype(owned)?.ID == kind.Id)
                groups.Add((owned, comp));
        }

        return groups;
    }

    /// <summary>
    /// The participants of a group, in a role or in any.
    /// </summary>
    [PublicAPI]
    public HashSet<EntityUid> GetMembers(EngagementComponent group, ProtoId<EngagementRolePrototype>? role = null)
    {
        var members = new HashSet<EntityUid>();

        foreach (var (actorRole, actors) in group.Actors)
        {
            if (role == null || role == actorRole)
                members.UnionWith(actors);
        }

        return members;
    }

    /// <summary>
    /// The participants of all the groups of a kind that the entity owns, in a role or in any.
    /// </summary>
    [PublicAPI]
    public HashSet<EntityUid> GetMembers(
        EntityUid owner,
        EntProtoId<EngagementComponent> kind,
        ProtoId<EngagementRolePrototype>? role = null)
    {
        var members = new HashSet<EntityUid>();

        foreach (var group in GetGroups(owner, kind))
        {
            members.UnionWith(GetMembers(group.Comp, role));
        }

        return members;
    }

    /// <summary>
    /// The group of a kind that the entity owns and the member is in.
    /// </summary>
    [PublicAPI]
    public bool TryGetGroup(
        EntityUid owner,
        EntityUid member,
        EntProtoId<EngagementComponent> kind,
        out Entity<EngagementComponent> group)
    {
        foreach (var candidate in GetGroups(owner, kind))
        {
            if (!_engagement.IsMember(candidate.AsNullable(), member))
                continue;

            group = candidate;
            return true;
        }

        group = default;
        return false;
    }

    /// <summary>
    /// Is nobody in the group.
    /// </summary>
    [PublicAPI]
    public bool IsEmpty(EngagementComponent group)
        => group.Actors.Values.All(x => x.Count == 0);

    /// <summary>
    /// Is the group being taken apart, see <see cref="GroupDissolvingComponent"/>.
    /// </summary>
    [PublicAPI]
    public bool IsDissolving(EntityUid group)
        => _dissolvingQuery.HasComp(group);

    /// <summary>
    /// The middle of the participants of the group in the world, zero if there is nobody.
    /// </summary>
    [PublicAPI]
    public Vector2 Center(EngagementComponent group)
    {
        var sum = Vector2.Zero;
        var count = 0;

        foreach (var member in GetMembers(group))
        {
            sum += _transform.GetWorldPosition(member);
            count++;
        }

        return count == 0 ? Vector2.Zero : sum / count;
    }

    #endregion

    #region Operations

    /// <summary>
    /// Puts an end to the groups of a kind that the entity owns and nobody is in.
    /// </summary>
    /// <returns>The groups that are left.</returns>
    [PublicAPI]
    public List<Entity<EngagementComponent>> Prune(EntityUid owner, EntProtoId<EngagementComponent> kind)
    {
        var groups = GetGroups(owner, kind);

        foreach (var group in groups.ToArray())
        {
            if (!IsEmpty(group.Comp))
                continue;

            groups.Remove(group);
            Disband(group);
        }

        return groups;
    }

    /// <summary>
    /// Takes a group apart: its participants leave it, and it is over. It is not a loss for the owner's reactions to the leaving.
    /// </summary>
    [PublicAPI]
    public void Disband(Entity<EngagementComponent> group, EngagementEndReason reason = EngagementEndReason.Dissolved)
    {
        EnsureComp<GroupDissolvingComponent>(group);
        _engagement.EndEngagement(group.AsNullable(), reason);
    }

    /// <summary>
    /// Divides the items into the given number of lists so that the weights of the lists are about the same. The strongest
    /// of the items are the first of the lists, the others go to the list that is the weakest at the time.
    /// </summary>
    [PublicAPI]
    public static List<List<T>> Partition<T>(IEnumerable<(T Item, float Weight)> items, int count)
    {
        var sorted = items.OrderByDescending(x => x.Weight).ToList();
        count = Math.Clamp(count, 1, Math.Max(1, sorted.Count));
        var lists = new List<(List<T> Items, float Weight)>();

        for (var i = 0; i < Math.Min(count, sorted.Count); i++)
        {
            lists.Add((new List<T> { sorted[i].Item }, sorted[i].Weight));
        }

        for (var i = lists.Count; i < sorted.Count; i++)
        {
            var weakest = 0;

            for (var j = 1; j < lists.Count; j++)
            {
                if (lists[j].Weight < lists[weakest].Weight)
                    weakest = j;
            }

            lists[weakest].Items.Add(sorted[i].Item);
            lists[weakest] = (lists[weakest].Items, lists[weakest].Weight + sorted[i].Weight);
        }

        return lists.Select(x => x.Items).ToList();
    }

    /// <summary>
    /// Are the groups there already what <see cref="Form"/> would make: as many as needed, and every member is in one.
    /// </summary>
    [PublicAPI]
    public bool IsFormed(List<Entity<EngagementComponent>> groups, IEnumerable<EntityUid> members, int count)
    {
        var inGroups = new HashSet<EntityUid>();
        var used = 0;

        foreach (var group in groups)
        {
            var any = GetMembers(group.Comp);
            inGroups.UnionWith(any);

            if (any.Count > 0)
                used++;
        }

        return used == count && members.All(inGroups.Contains);
    }

    /// <summary>
    /// Divides the members into the given number of groups of a kind that the entity owns, the strength of the groups being
    /// as even as it can be by the weights. The groups of the kind that are there are taken apart first, so the members
    /// change places. The strongest are the first of each group, so they take the roles that have to be filled: the engagement
    /// system seats them, and the others go where there is a place.
    /// </summary>
    /// <returns>The groups that are made.</returns>
    [PublicAPI]
    public List<Entity<EngagementComponent>> Form(
        EntityUid owner,
        EntProtoId<EngagementComponent> kind,
        IEnumerable<(EntityUid Member, float Weight)> members,
        int count)
    {
        foreach (var old in GetGroups(owner, kind))
        {
            Disband(old);
        }

        var formed = new List<Entity<EngagementComponent>>();

        foreach (var part in Partition(members, count))
        {
            if (!_engagement.TryStartEngagement(kind, part[0], part, out var group)
                && !_engagement.TryStartEngagement(kind, part[0], out group))
                continue;

            _ownership.AddOwnership(group.Value, owner: owner);

            foreach (var member in part)
            {
                if (!_engagement.IsMember(group.Value.AsNullable(), member))
                    _engagement.TryJoinAnyRole(group.Value.AsNullable(), member);
            }

            formed.Add(group.Value);
            RaiseLocalEvent(group.Value.Owner, new GroupFormed(group.Value.Owner, owner));
        }

        return formed;
    }

    /// <summary>
    /// Puts the member in a group of a kind that the entity owns: the first one that has a place in one of the roles, and if
    /// there is none, in a new group that the entity owns.
    /// </summary>
    /// <param name="roles">The roles to take, which have to be ones that can be taken by force. Any role if empty.</param>
    [PublicAPI]
    public bool Place(
        EntityUid owner,
        EntProtoId<EngagementComponent> kind,
        EntityUid member,
        IReadOnlyCollection<ProtoId<EngagementRolePrototype>>? roles = null)
    {
        foreach (var group in GetGroups(owner, kind))
        {
            if (TryJoin(group, member, roles))
                return true;
        }

        if (!_engagement.TryStartEngagement(kind, member, out var created))
            return false;

        _ownership.AddOwnership(created.Value, owner: owner);

        // Nothing to take at all, such as the roles that are not forced: the group is for nothing.
        if (TryJoin(created.Value, member, roles))
        {
            RaiseLocalEvent(created.Value.Owner, new GroupFormed(created.Value.Owner, owner));
            return true;
        }

        QueueDel(created.Value);
        return false;
    }

    private bool TryJoin(
        Entity<EngagementComponent> group,
        EntityUid member,
        IReadOnlyCollection<ProtoId<EngagementRolePrototype>>? roles)
    {
        if (roles == null || roles.Count == 0)
            return _engagement.TryJoinAnyRole(group.AsNullable(), member);

        return roles.Any(role => _engagement.TryJoinEngagement(group.AsNullable(), role, member));
    }

    /// <summary>
    /// Moves a member from a group to another. The role in the new group is chosen by the engagement system. If there is no place
    /// for the member there, it is left without a group.
    /// </summary>
    [PublicAPI]
    public bool Transfer(Entity<EngagementComponent> from, Entity<EngagementComponent> to, EntityUid member)
    {
        if (!_engagement.LeaveEngagement(from.AsNullable(), member, EngagementEndReason.Finished))
            return false;

        return _engagement.TryJoinAnyRole(to.AsNullable(), member);
    }

    /// <summary>
    /// Joins a group to another: all of its participants are moved, and then it is over. It is not a loss for the reactions
    /// to the leaving of a leader of the group that is gone.
    /// </summary>
    [PublicAPI]
    public void Merge(Entity<EngagementComponent> from, Entity<EngagementComponent> to)
    {
        EnsureComp<GroupDissolvingComponent>(from);

        // A group may be closed by itself when a role has too few, then the rest are moved all the same.
        foreach (var member in GetMembers(from.Comp).ToArray())
        {
            if (Exists(from) && _engagement.IsMember(from.AsNullable(), member))
                _engagement.LeaveEngagement(from.AsNullable(), member, EngagementEndReason.Finished);

            _engagement.TryJoinAnyRole(to.AsNullable(), member);
        }

        if (Exists(from))
            _engagement.EndEngagement(from.AsNullable(), EngagementEndReason.Dissolved);

        RaiseLocalEvent(to.Owner, new GroupMerged(to.Owner, from.Owner));
    }

    /// <summary>
    /// Fills a role that nobody has with one of the participants of the group who has another role, the one who is best by the rank.
    /// </summary>
    /// <returns>True, if the role has been filled.</returns>
    [PublicAPI]
    public bool FillRole(
        Entity<EngagementComponent> group,
        ProtoId<EngagementRolePrototype> role,
        Func<EntityUid, float>? rank = null)
    {
        if (_engagement.TryGetActors(group.AsNullable(), role, out _))
            return false;

        var candidates = GetMembers(group.Comp);

        if (candidates.Count == 0)
            return false;

        var best = rank == null ? candidates.First() : candidates.MaxBy(rank);

        return _engagement.LeaveEngagement(group.AsNullable(), best, EngagementEndReason.Finished)
               && _engagement.TryJoinEngagement(group.AsNullable(), role, best);
    }

    #endregion
}
