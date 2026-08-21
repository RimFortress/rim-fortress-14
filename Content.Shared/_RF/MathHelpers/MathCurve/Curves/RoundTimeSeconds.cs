using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.GameTicking;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the number of seconds that have elapsed since the start of the round
/// </summary>
public sealed partial class RoundTimeSeconds : BaseMathCurve<RoundTimeSeconds>;

public sealed class RoundTimeSecondsCurveSystem : MathCurveSystem<RoundTimeSeconds>
{
    [Dependency] private readonly SharedGameTicker _ticker = default!;

    protected override float Curve(RoundTimeSeconds curve, float input, MathCurveContext ctx)
        => _ticker.RoundDuration().Seconds;
}
