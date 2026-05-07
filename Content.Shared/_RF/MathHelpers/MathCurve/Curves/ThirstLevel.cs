using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.Nutrition.Components;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Gets the current user thirst value.
/// </summary>
public sealed partial class ThirstLevel : BaseMathCurve<ThirstLevel>
{
    [DataField]
    public bool Normalize = true;
}

public sealed class ThirstLevelSystem : MathCurveSystem<ThirstLevel>
{
    protected override float Curve(ThirstLevel curve, float input, EntityUid? user)
    {
        if (!TryComp(user, out ThirstComponent? thirst))
            return 0f;

        if (!curve.Normalize)
            return thirst.CurrentThirst;

        return thirst.CurrentThirst / thirst.ThirstThresholds[ThirstThreshold.OverHydrated];
    }
}
