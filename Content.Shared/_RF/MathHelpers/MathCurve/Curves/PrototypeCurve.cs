using Content.Shared._RF.MathHelpers.MathCurve.Prototypes;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the result of the mathematical curve preset calculation.
/// </summary>
public sealed partial class PrototypeCurve : BaseMathCurve<PrototypeCurve>
{
    /// <summary>
    /// Math curve preset prototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<MathCurvePrototype> Preset;

    /// <summary>
    /// Variables that will be passed when calculating the formula.
    /// </summary>
    [DataField]
    public Dictionary<string, List<MathCurve>> Variables = new();
}

public sealed class PrototypeCurveSystem : MathCurveSystem<PrototypeCurve>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MathCurvesSystem _mathCurves = default!;

    protected override float Curve(PrototypeCurve curve, float input, MathCurveContext ctx)
    {
        var newCtx = ctx with { Variables = new() };
        var proto = _proto.Index(curve.Preset);

        foreach (var (id, defaults) in proto.Variables)
        {
            newCtx.Variables[id] = _mathCurves.Get(curve.Variables.GetValueOrDefault(id, defaults), input, ctx);
        }

        return _mathCurves.Get(proto.Curves, input, newCtx);
    }
}
