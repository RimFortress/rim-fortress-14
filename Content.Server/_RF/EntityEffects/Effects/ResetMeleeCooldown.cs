using Content.Shared.EntityEffects;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RF.EntityEffects.Effects;

/// <summary>
/// Resets the cooldown of melee weapon use
/// </summary>
public sealed partial class ResetMeleeCooldown : EntityEffectBase<ResetMeleeCooldown>;

public sealed partial class ResetMeleeCooldownEntityEffectSystem : EntityEffectSystem<MeleeWeaponComponent, GenerateProduct>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<MeleeWeaponComponent> entity, ref EntityEffectEvent<GenerateProduct> args)
    {
        entity.Comp.NextAttack = _timing.CurTime;
    }
}
