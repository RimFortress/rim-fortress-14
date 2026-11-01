using Content.Shared.EntityEffects;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Deletes the target.
/// </summary>
public sealed partial class DespawnEntity : EntityEffectBase<DespawnEntity>;

public sealed class DespawnEntityEntityEffectsSystem : EntityEffectSystem<MetaDataComponent, DespawnEntity>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<DespawnEntity> args)
        => PredictedQueueDel(ent.Owner);
}
