using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Gets the current user hunger value.
/// </summary>
public sealed partial class HungerLevel : BaseMathCurve<HungerLevel>
{
    [DataField]
    public bool Normalize = true;
}

public sealed class HungerLevelSystem : MathCurveSystem<HungerLevel>
{
    [Dependency] private readonly HungerSystem _hunger = default!;

    protected override float Curve(HungerLevel curve, float input, MathCurveContext ctx)
    {
        if (!TryComp(ctx.User, out HungerComponent? hunger))
            return 0f;

        if (!curve.Normalize)
            return _hunger.GetHunger(hunger);

        return _hunger.GetHunger(hunger) / hunger.Thresholds[HungerThreshold.Overfed];
    }
}
