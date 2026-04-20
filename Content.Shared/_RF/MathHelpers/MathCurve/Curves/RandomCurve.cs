using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Robust.Shared.Random;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns a random number
/// </summary>
public sealed partial class RandomCurve : BaseMathCurve<RandomCurve>
{
    [DataField]
    public MinMaxFloat Random;
}

public sealed class RandomCurveCurveSystem : MathCurveSystem<RandomCurve>
{
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override float Curve(RandomCurve curve, float input)
        => curve.Random.Next(_random);
}