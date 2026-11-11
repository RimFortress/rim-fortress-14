using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns how much damage the user can take before it is critical.
/// </summary>
public sealed partial class MobHealth : BaseMathCurve<MobHealth>
{
    /// <summary>
    /// Is a mob that is not alive, down or dead, left with no health at all.
    /// </summary>
    [DataField]
    public bool OnlyAlive = true;
}

public sealed partial class MobHealthCurveSystem : MathCurveSystem<MobHealth>
{
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery;

    protected override float Curve(MobHealth curve, float input, MathCurveContext ctx)
    {
        if (ctx.User is not { } user
            || !_mobStateQuery.TryComp(user, out var state)
            || curve.OnlyAlive && state.CurrentState != MobState.Alive
            || !_thresholds.TryGetThresholdForState(user, MobState.Critical, out var critical))
            return 0f;

        return Math.Max(0f, critical.Value.Float() - _damageable.GetTotalDamage(user).Float());
    }
}
