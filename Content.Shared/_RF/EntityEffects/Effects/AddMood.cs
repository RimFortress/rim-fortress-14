using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Adds an effect on the entity's mood.
/// </summary>
public sealed partial class AddMood : EntityEffectBase<AddMood>
{
    /// <summary>
    /// Prototype of the effect.
    /// </summary>
    [DataField]
    public ProtoId<SocialEffectPrototype> Proto;
}

public sealed partial class AddMoodEntityEffectsSystem : EntityEffectSystem<SocialComponent, AddMood>
{
    [Dependency] private SocialSystem _social = default!;

    protected override void Effect(Entity<SocialComponent> ent, ref EntityEffectEvent<AddMood> args)
    {
        _social.AddMoodEffect(ent.AsNullable(), args.Effect.Proto);
    }
}
