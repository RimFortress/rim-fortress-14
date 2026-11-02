using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.Systems;
using System.Linq;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Makes the user of the effect take a role in a situation (<see cref="EngagementSystem"/>) that the target owns,
/// see <see cref="OwnershipSystem"/>. The situations of the kind that the target owns are tried one by one for a place in one of
/// the roles, and if there is none in any, a new one is started, and the target becomes its owner. It is how the squads are
/// made: a squad takes as many as its roles can have, and the next one goes to another. The roles have to be ones that can be
/// taken by force.
/// </summary>
public sealed partial class JoinEngagement : EntityEffectBase<JoinEngagement>
{
    /// <summary>
    /// The kind of the situation.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EngagementPrototype> Engagement;

    /// <summary>
    /// The roles that are taken, the first that has a place.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<EngagementRolePrototype>> Roles = new();
}

public sealed partial class JoinEngagementEntityEffectsSystem : EntityEffectSystem<MetaDataComponent, JoinEngagement>
{
    [Dependency] private EngagementSystem _engagement = null!;
    [Dependency] private OwnershipSystem _ownership = null!;

    [Dependency] private EntityQuery<EngagementComponent> _engagementQuery;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<JoinEngagement> args)
    {
        if (args.User is not { } user)
            return;

        foreach (var owned in _ownership.GetOwned(ent).ToArray())
        {
            if (_engagementQuery.TryComp(owned, out var comp)
                && comp.Kind == args.Effect.Engagement
                && TryJoin((owned, comp), args.Effect.Roles, user))
                return;
        }

        if (!_engagement.TryStartEngagement(args.Effect.Engagement, user, out var situation))
            return;

        _ownership.AddOwnership(situation.Value, owner: ent);

        // Nothing to take at all, such as the roles that are not forced: the situation is for nothing.
        if (!TryJoin(situation.Value, args.Effect.Roles, user))
            QueueDel(situation.Value);
    }

    private bool TryJoin(Entity<EngagementComponent> situation, List<ProtoId<EngagementRolePrototype>> roles, EntityUid user)
    {
        foreach (var role in roles)
        {
            if (_engagement.TryJoinEngagement(situation.AsNullable(), role, user))
                return true;
        }

        return false;
    }
}
