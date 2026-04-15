using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Random;
using Robust.Shared.Prototypes;

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

public sealed class GenerateProductEntityEffectSystem : EntityEffectSystem<PlantHolderComponent, GenerateProduct>
{
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly RandomHelperSystem _randomHelper = default!;

    protected override void Effect(Entity<PlantHolderComponent> entity, ref EntityEffectEvent<GenerateProduct> args)
    {
        if (entity.Comp.Seed == null)
            return;

        foreach (var uid in _botany.GenerateProduct(entity.Comp.Seed, Transform(entity).Coordinates, args.Effect.YieldMod))
        {
            _randomHelper.RandomOffset(uid, 0.25f);
        }
    }
}
