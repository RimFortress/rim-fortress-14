using JetBrains.Annotations;

namespace Content.Shared._RF.MathHelpers.MathCurve.Systems;

/// <summary>
/// A system that provides an API for retrieving data from mathematical curves.
/// </summary>
public sealed class MathCurvesSystem : EntitySystem, IMathCurveHandler
{
    public float Get<T>(T curve, float input, EntityUid? user = null) where T : BaseMathCurve<T>
    {
        var ev = new MathCurveResult<T>(curve, input, 0f, user);
        RaiseLocalEvent(ref ev);
        return ev.Result;
    }

    /// <summary>
    /// Returns the input value curved by a mathematical function.
    /// </summary>
    /// <param name="curve">Mathematical curve.</param>
    /// <param name="input">Input value.</param>
    /// <param name="user">Entity for which the calculation is performed.</param>
    [PublicAPI, Pure]
    public float Get(MathCurve curve, float input = 0, EntityUid? user = null)
        => curve.Curve(input, this, user);

    /// <summary>
    /// Returns the input data sequentially curved by a set of mathematical functions.
    /// </summary>
    /// <param name="curves">Mathematical curves.</param>
    /// <param name="input">Input value.</param>
    /// <param name="user">Entity for which the calculation is performed.</param>
    [PublicAPI, Pure]
    public float Get(IEnumerable<MathCurve> curves, float input = 0, EntityUid? user = null)
    {
        var result = input;

        foreach (var curve in curves)
        {
            result = curve.Curve(result, this, user);
        }

        return result;
    }
}

public interface IMathCurveHandler
{
    /// <summary>
    /// Returns the input value curved by a mathematical function.
    /// </summary>
    /// <typeparam name="T">Math curve type.</typeparam>
    /// <param name="curve">Mathematical curve.</param>
    /// <param name="input">Input value.</param>
    /// <param name="user">Entity for which the calculation is performed.</param>
    float Get<T>(T curve, float input, EntityUid? user = null) where T : BaseMathCurve<T>;
}

/// <summary>
/// An event raised to calculate the result of a mathematical curve.
/// </summary>
/// <typeparam name="T">Math curve type.</typeparam>
/// <param name="Curve">Mathematical curve.</param>
/// <param name="Input">Input value.</param>
/// <param name="Result">Result of the curve calculation.</param>
/// <param name="User">Entity for which the calculation is performed.</param>
[PublicAPI, ByRefEvent]
public record struct MathCurveResult<T>(T Curve, float Input, float Result, EntityUid? User) where T : BaseMathCurve<T>;