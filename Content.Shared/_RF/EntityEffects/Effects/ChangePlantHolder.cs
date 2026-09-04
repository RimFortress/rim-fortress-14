using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.EntityEffects;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Changes the parameters of the plant holder
/// </summary>
public sealed partial class ChangePlantHolder : EntityEffectBase<ChangePlantHolder>
{
    /// <summary>
    /// How much should the health of the plant be changed by
    /// </summary>
    [DataField]
    public int Health;
}

public sealed partial class ChangePlantHolderEntityEffectSystem : EntityEffectSystem<PlantHolderComponent, ChangePlantHolder>
{
    [Dependency] private PlantHolderSystem _plantHolder = default!;

    protected override void Effect(Entity<PlantHolderComponent> entity, ref EntityEffectEvent<ChangePlantHolder> args)
    {
        _plantHolder.AdjustsHealth(entity.AsNullable(), args.Effect.Health);
    }
}
