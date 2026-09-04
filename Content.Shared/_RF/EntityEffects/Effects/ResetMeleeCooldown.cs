using Content.Shared.EntityEffects;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Timing;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Resets the cooldown of melee weapon use
/// </summary>
public sealed partial class ResetMeleeCooldown : EntityEffectBase<ResetMeleeCooldown>;

public sealed partial class ResetMeleeCooldownEntityEffectSystem : EntityEffectSystem<MeleeWeaponComponent, ResetMeleeCooldown>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<MeleeWeaponComponent> entity, ref EntityEffectEvent<ResetMeleeCooldown> args)
    {
        entity.Comp.NextAttack = _timing.CurTime;
    }
}
