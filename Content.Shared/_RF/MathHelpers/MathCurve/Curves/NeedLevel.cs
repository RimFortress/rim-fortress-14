using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.Needs;
using Content.Shared._RF.Needs.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the user's need value.
/// </summary>
public sealed partial class NeedLevel : BaseMathCurve<NeedLevel>
{
    /// <summary>
    /// Need prototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NeedPrototype> Need;

    /// <summary>
    /// Should be output value normalized.
    /// </summary>
    [DataField]
    public bool Normalize = true;
}

public sealed partial class NeedLevelSystem : MathCurveSystem<NeedLevel>
{
    [Dependency] private readonly NeedsSystem _needs = default!;

    protected override float Curve(NeedLevel curve, float input, MathCurveContext ctx)
    {
        if (ctx.User == null)
            return float.NaN;

        var value = _needs.GetValue(ctx.User.Value, curve.Need);

        if (curve.Normalize)
            return value != 0 ? value / _needs.MaxValue(curve.Need) : 0f;

        return value;
    }
}
