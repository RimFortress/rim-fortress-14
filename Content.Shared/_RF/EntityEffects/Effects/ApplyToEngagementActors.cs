using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Groups.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Applies effects to the participants of the situations of a kind that the target owns,
/// see <see cref="OwnershipSystem"/>: the participants are the targets of the effects,
/// and the target of this effect is their user.
/// </summary>
public sealed partial class ApplyToEngagementActors : EntityEffectBase<ApplyToEngagementActors>
{
    /// <summary>
    /// The kind of the situations.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Engagement;

    /// <summary>
    /// The role of those that the effects are applied to, all of them if it is not set.
    /// </summary>
    [DataField]
    public ProtoId<EngagementRolePrototype>? Role;

    /// <summary>
    /// The effects.
    /// </summary>
    [DataField(required: true)]
    public EntityEffect[] Effects = [];
}

public sealed partial class ApplyToEngagementActorsEntityEffectsSystem
    : EntityEffectSystem<MetaDataComponent, ApplyToEngagementActors>
{
    [Dependency] private GroupsSystem _groups = null!;
    [Dependency] private SharedEntityEffectsSystem _effects = null!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<ApplyToEngagementActors> args)
    {
        // The effects can change the situations, so the participants are taken first.
        var actors = _groups.GetMembers(ent, args.Effect.Engagement, args.Effect.Role);

        foreach (var actor in actors)
        {
            _effects.ApplyEffects(actor, args.Effect.Effects, 1f, ent);
        }
    }
}
