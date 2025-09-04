using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.EntityEffects.Effects;

/// <summary>
/// Causes the plant holder to spawn the product of the current plant
/// </summary>
public sealed partial class GenerateProduct : EntityEffect
{
    /// <summary>
    /// Modifier of the quantity of the generated product
    /// </summary>
    [DataField]
    public int YieldMod = 1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        null;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent(args.TargetEntity, out PlantHolderComponent? comp) || comp.Seed == null)
            return;

        var botany = args.EntityManager.System<BotanySystem>();
        var random = args.EntityManager.System<RandomHelperSystem>();
        var coords = args.EntityManager.GetComponent<TransformComponent>(args.TargetEntity).Coordinates;

        foreach (var uid in botany.GenerateProduct(comp.Seed, coords, YieldMod))
        {
            random.RandomOffset(uid, 0.25f);
        }
    }
}
