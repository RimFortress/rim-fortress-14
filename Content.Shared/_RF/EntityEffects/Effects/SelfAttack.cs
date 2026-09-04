using Content.Shared.EntityEffects;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Forces the entity to attack itself with whatever it has in its hand.
/// </summary>
public sealed partial class SelfAttack : EntityEffectBase<SelfAttack>;

public sealed partial class SelfAttackEntityEffectSystem : EntityEffectSystem<HandsComponent, SelfAttack>
{
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    protected override void Effect(Entity<HandsComponent> entity, ref EntityEffectEvent<SelfAttack> args)
    {
        if (_hands.TryGetActiveItem(entity.AsNullable(), out var weapon)
            && TryComp(weapon, out MeleeWeaponComponent? melee))
            _melee.AttemptLightAttack(entity, weapon.Value, melee, entity);
        else if (TryComp(entity, out melee))
            _melee.AttemptLightAttack(entity, entity, melee, entity);
    }
}
