using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Prototypes;

/// <summary>
/// A prototype preset for mathematical curves.
/// </summary>
[Prototype]
public sealed partial class MathCurvePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<MathCurve> Curves = new();
}
