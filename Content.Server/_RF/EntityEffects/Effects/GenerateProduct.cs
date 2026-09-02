using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Random;

namespace Content.Server._RF.EntityEffects.Effects;

/// <summary>
/// Causes the plant holder to spawn the product of the current plant
/// </summary>
public sealed partial class GenerateProduct : EntityEffectBase<GenerateProduct>
{
    /// <summary>
    /// Modifier of the quantity of the generated product
    /// </summary>
    [DataField]
    public int YieldMod = 1;
}

public sealed partial class GenerateProductEntityEffectSystem : EntityEffectSystem<PlantTrayComponent, GenerateProduct>
{
    [Dependency] private PlantTraySystem _plantTray = default!;
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private RandomHelperSystem _randomHelper = default!;

    protected override void Effect(Entity<PlantTrayComponent> entity, ref EntityEffectEvent<GenerateProduct> args)
    {
        if (!_plantTray.TryGetPlant(entity.AsNullable(), out var plant))
            return;

        _botany.SpawnProduce(plant.Value, Transform(entity).Coordinates, args.Effect.YieldMod);
    }
}
