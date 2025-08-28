using Content.Shared.EntityEffects;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RF.EntityEffects.Effects;

/// <summary>
/// Resets the cooldown of melee weapon use
/// </summary>
public sealed partial class ResetMeleeCooldown : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        null;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent(args.TargetEntity, out MeleeWeaponComponent? comp))
            return;

        comp.NextAttack = IoCManager.Resolve<IGameTiming>().CurTime;
    }
}
