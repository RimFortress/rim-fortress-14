using JetBrains.Annotations;

namespace Content.Shared._RF.MathHelpers.MathCurve.Systems;

/// <summary>
/// A system that provides an API for retrieving data from mathematical curves.
/// </summary>
public sealed class MathCurvesSystem : EntitySystem, IMathCurveHandler
{
    public float Get<T>(T curve, float input) where T : BaseMathCurve<T>
    {
        var ev = new MathCurveResult<T>(curve, input, 0f);
        RaiseLocalEvent(ref ev);
        return ev.Result;
    }

    /// <summary>
    /// Returns the input value curved by a mathematical function.
    /// </summary>
    /// <param name="curve">Mathematical curve.</param>
    /// <param name="input">Input value.</param>
    [PublicAPI, Pure]
    public float Get(MathCurve curve, float input = 0) => curve.Curve(input, this);

    /// <summary>
    /// Returns the input data sequentially curved by a set of mathematical functions.
    /// </summary>
    /// <param name="curves">Mathematical curves.</param>
    /// <param name="input">Input value.</param>
    [PublicAPI, Pure]
    public float Get(IEnumerable<MathCurve> curves, float input = 0)
    {
        var result = input;

        foreach (var curve in curves)
        {
            result = curve.Curve(result, this);
        }

        return result;
    }
}

public interface IMathCurveHandler
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="curve"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    float Get<T>(T curve, float input) where T : BaseMathCurve<T>;
}

/// <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="Curve"></param>
/// <param name="Input"></param>
/// <param name="Result"></param>
[PublicAPI, ByRefEvent]
public record struct MathCurveResult<T>(T Curve, float Input, float Result) where T : BaseMathCurve<T>;