using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.Weapons.Melee;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns how much damage the blow of the user deals, with the weapon that it has in the hands, or with the hands.
/// </summary>
public sealed partial class MeleeBlow : BaseMathCurve<MeleeBlow>;

public sealed partial class MeleeBlowCurveSystem : MathCurveSystem<MeleeBlow>
{
    [Dependency] private SharedMeleeWeaponSystem _melee = null!;

    protected override float Curve(MeleeBlow curve, float input, MathCurveContext ctx)
        => ctx.User is { } user && _melee.TryGetWeapon(user, out var weapon, out var melee)
            ? _melee.GetDamage(weapon, user, melee).GetTotal().Float()
            : 0f;
}
