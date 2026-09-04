using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Adds an effect on the opinion of an entity to any of the participants in the engagement.
/// </summary>
public sealed partial class AddOpinion : EntityEffectBase<AddOpinion>
{
    /// <summary>
    /// IDs of engagement participants, effect on opinion to which should be added.
    /// </summary>
    [DataField]
    public List<ProtoId<EngagementRolePrototype>> Actors = new();

    /// <summary>
    /// Prototype of the effect.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SocialEffectPrototype> Proto;
}

public sealed partial class AddOpinionEntityEffectsSystem : EntityEffectSystem<EngagementParticipantComponent, AddOpinion>
{
    [Dependency] private EngagementSystem _engagement = default!;
    [Dependency] private SocialSystem _social = default!;

    protected override void Effect(Entity<EngagementParticipantComponent> ent, ref EntityEffectEvent<AddOpinion> args)
    {
        foreach (var (engage, _) in ent.Comp.Membership)
        {
            foreach (var actor in args.Effect.Actors)
            {
                if (!_engagement.TryGetActors(engage, actor, out var uids))
                    continue;

                foreach (var uid in uids)
                {
                    _social.AddOpinionEffect(ent.Owner, uid, args.Effect.Proto);
                }
            }
        }
    }
}
