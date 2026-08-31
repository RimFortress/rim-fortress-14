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

    /// <summary>
    /// Variables and their default values that can be passed to a formula.
    /// </summary>
    [DataField]
    public Dictionary<string, List<MathCurve>> Variables = new();

    [DataField(required: true)]
    public List<MathCurve> Curves = new();
}
