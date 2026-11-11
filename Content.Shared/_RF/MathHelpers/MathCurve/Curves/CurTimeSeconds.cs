using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the time of the game in seconds.
/// </summary>
public sealed partial class CurTimeSeconds : BaseMathCurve<CurTimeSeconds>;

public sealed partial class CurTimeSecondsCurveSystem : MathCurveSystem<CurTimeSeconds>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override float Curve(CurTimeSeconds curve, float input, MathCurveContext ctx)
        => (float) _timing.CurTime.TotalSeconds;
}
