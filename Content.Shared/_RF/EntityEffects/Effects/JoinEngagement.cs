using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Makes the user of the effect take a role in a situation (<see cref="EngagementSystem"/>) that the target owns,
/// see <see cref="OwnershipSystem"/>. If the target owns no such situation yet, it is started,
/// and the target becomes its owner. The role has to be one that can be taken by force.
/// </summary>
public sealed partial class JoinEngagement : EntityEffectBase<JoinEngagement>
{
    /// <summary>
    /// The kind of the situation.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EngagementPrototype> Engagement;

    /// <summary>
    /// The role that is taken.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EngagementRolePrototype> Role;
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

        Entity<EngagementComponent>? situation = null;

        foreach (var owned in _ownership.GetOwned(ent))
        {
            if (!_engagementQuery.TryComp(owned, out var comp) || comp.Kind != args.Effect.Engagement)
                continue;

            situation = (owned, comp);
            break;
        }

        if (situation == null)
        {
            if (!_engagement.TryStartEngagement(args.Effect.Engagement, user, out situation))
                return;

            _ownership.AddOwnership(situation.Value, owner: ent);
        }

        _engagement.TryJoinEngagement(situation.Value.AsNullable(), args.Effect.Role, user);
    }
}
