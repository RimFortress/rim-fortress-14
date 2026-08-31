using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.Social.Systems;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the user's mood level.
/// </summary>
public sealed partial class MoodLevel : BaseMathCurve<MoodLevel>
{
    /// <summary>
    /// If true, the value will be normalized relative to the maximum mood level.
    /// </summary>
    [DataField]
    public bool Normalize = true;
}

public sealed class MoodLevelMathCurveSystem : MathCurveSystem<MoodLevel>
{
    [Dependency] private readonly SocialSystem _social = default!;

    protected override float Curve(MoodLevel curve, float input, MathCurveContext ctx)
    {
        if (ctx.User == null)
            return 0f;

        var mood = _social.GetMood(ctx.User.Value);

        if (!curve.Normalize)
            return mood;

        return (float)(mood - _social.MinMood) / (_social.MaxMood - _social.MinMood);
    }
}
