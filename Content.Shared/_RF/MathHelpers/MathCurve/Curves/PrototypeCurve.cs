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
}

public sealed class PrototypeCurveSystem : MathCurveSystem<PrototypeCurve>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MathCurvesSystem _mathCurves = default!;

    protected override float Curve(PrototypeCurve curve, float input, EntityUid? user)
        => _mathCurves.Get(_proto.Index(curve.Preset).Curves, input, user);
}
