using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.EntityEffects.Effects;

/// <summary>
/// Changes the parameters of the plant holder
/// </summary>
public sealed partial class ChangePlanHolder : EntityEffect
{
    /// <summary>
    /// How much should the health of the plant be changed by
    /// </summary>
    [DataField]
    public int Health;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        null;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent(args.TargetEntity, out PlantHolderComponent? comp) || comp.Seed == null)
            return;

        comp.Health += Health;

        args.EntityManager.System<PlantHolderSystem>().CheckLevelSanity(args.TargetEntity, comp);
    }
}
