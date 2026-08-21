using Content.Shared._RF.MathHelpers.MathCurve.Systems;

namespace Content.Shared._RF.MathHelpers.MathCurve;

/// <summary>
/// A mathematical curve that modifies the input data.
/// Used to easily create complex mathematical formulas in YAML.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class MathCurve
{
    public abstract float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx);

    public static float ListValue(
        IEnumerable<MathCurve> curves,
        IMathCurveHandler handler,
        float value = 0,
        MathCurveContext ctx = default)
    {
        var result = value;

        foreach (var curve in curves)
        {
            result = curve.Curve(result, handler, ctx);
        }

        return result;
    }
}

/// <summary>
/// A mathematical curve using entity systems in its calculation.
/// <seealso cref="IMathCurveHandler"/>
/// </summary>
/// <typeparam name="T">Math curve type.</typeparam>
public abstract partial class BaseMathCurve<T> : MathCurve where T : BaseMathCurve<T>
{
    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
        => handler.Get((T)this, value, ctx);
}

/// <summary>
/// The context in which this mathematical curve is calculated.
/// </summary>
/// <param name="User">Entity for which the calculation is performed.</param>
/// <param name="Variables"></param>
public record struct MathCurveContext(EntityUid? User, Dictionary<string, float> Variables);
