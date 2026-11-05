using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.Damage.Systems;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns how much damage the user has taken, of any kind: a mob that is dead or down has taken it too. It is for telling that
/// someone has been hurt, such as the raiders that sit in their camp until somebody attacks them.
/// </summary>
public sealed partial class MobDamage : BaseMathCurve<MobDamage>;

public sealed partial class MobDamageCurveSystem : MathCurveSystem<MobDamage>
{
    [Dependency] private DamageableSystem _damageable = default!;

    protected override float Curve(MobDamage curve, float input, MathCurveContext ctx)
        => ctx.User is { } user ? _damageable.GetTotalDamage(user).Float() : 0f;
}
