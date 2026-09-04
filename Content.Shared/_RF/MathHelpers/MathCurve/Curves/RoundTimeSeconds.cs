using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.GameTicking;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the number of seconds that have elapsed since the start of the round
/// </summary>
public sealed partial class RoundTimeSeconds : BaseMathCurve<RoundTimeSeconds>;

public sealed partial class RoundTimeSecondsCurveSystem : MathCurveSystem<RoundTimeSeconds>
{
    [Dependency] private SharedGameTicker _ticker = default!;

    protected override float Curve(RoundTimeSeconds curve, float input, MathCurveContext ctx)
        => _ticker.RoundDuration().Seconds;
}
