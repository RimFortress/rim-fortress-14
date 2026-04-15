using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Shared.EntityEffects;

namespace Content.Server._RF.EntityEffects.Effects;

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

public sealed class ChangePlantHolderEntityEffectSystem : EntityEffectSystem<PlantHolderComponent, ChangePlantHolder>
{
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;

    protected override void Effect(Entity<PlantHolderComponent> entity, ref EntityEffectEvent<ChangePlantHolder> args)
    {
        entity.Comp.Health += args.Effect.Health;
        _plantHolder.CheckLevelSanity(entity, entity.Comp);
    }
}
