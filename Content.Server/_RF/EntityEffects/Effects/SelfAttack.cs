using Content.Server.Hands.Systems;
using Content.Server.Weapons.Melee;
using Content.Shared.EntityEffects;
using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.EntityEffects.Effects;

/// <summary>
/// Forces the entity to attack itself with whatever it has in its hand.
/// </summary>
public sealed partial class SelfAttack : EntityEffectBase<SelfAttack>;

public sealed class SelfAttackEntityEffectSystem : EntityEffectSystem<HandsComponent, SelfAttack>
{
    [Dependency] private readonly MeleeWeaponSystem _melee = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    protected override void Effect(Entity<HandsComponent> entity, ref EntityEffectEvent<SelfAttack> args)
    {
        if (_hands.TryGetActiveItem(entity.AsNullable(), out var weapon)
            && TryComp(weapon, out MeleeWeaponComponent? melee))
            _melee.AttemptLightAttack(entity, weapon.Value, melee, entity);
        else if (TryComp(entity, out melee))
            _melee.AttemptLightAttack(entity, entity, melee, entity);
    }
}
