using Content.Server.Hands.Systems;
using Content.Server.Weapons.Melee;
using Content.Shared.EntityEffects;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.EntityEffects.Effects;

/// <summary>
/// Forces the entity to attack itself with whatever it has in its hand.
/// </summary>
public sealed partial class SelfAttack : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        null;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent(args.TargetEntity, out MeleeWeaponComponent? comp))
            return;

        var melee = args.EntityManager.System<MeleeWeaponSystem>();
        var hands = args.EntityManager.System<HandsSystem>();

        if (hands.TryGetActiveItem(args.TargetEntity, out var weapon))
            melee.AttemptLightAttack(args.TargetEntity, weapon.Value, comp, args.TargetEntity);
        else
            melee.AttemptLightAttack(args.TargetEntity, args.TargetEntity, comp, args.TargetEntity);
    }
}
