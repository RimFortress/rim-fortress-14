using Content.Shared._RF.MathHelpers.MathCurve.Systems;

namespace Content.Shared._RF.MathHelpers.MathCurve;

/// <summary>
/// A mathematical curve that modifies the input data.
/// Used to easily create complex mathematical formulas in YAML.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class MathCurve
{
    public abstract float Curve(float value, IMathCurveHandler handler, EntityUid? user = null);

    public static float ListValue(
        IEnumerable<MathCurve> curves,
        IMathCurveHandler handler,
        float value = 0,
        EntityUid? user = null)
    {
        var result = value;

        foreach (var curve in curves)
        {
            result = curve.Curve(result, handler, user);
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
    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
        => handler.Get((T)this, value, user);
}
