namespace Content.Shared._RF.MathHelpers.MathCurve.Systems;

/// <summary>
/// A system for calculating the results of complex mathematical curves.
/// </summary>
/// <typeparam name="T">Math curve type.</typeparam>
public abstract class MathCurveSystem<T> : EntitySystem where T : BaseMathCurve<T>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MathCurveResult<T>>(OnMathCurveResult);
    }

    private void OnMathCurveResult(ref MathCurveResult<T> ev)
    {
        ev.Result = Curve(ev.Curve, ev.Input, ev.Ctx);
    }

    /// <summary>
    /// Returns the input value curved by a mathematical function.
    /// </summary>
    /// <param name="curve">Mathematical curve.</param>
    /// <param name="input">Input value.</param>
    /// <param name="ctx">The context in which this mathematical curve is calculated.</param>
    protected abstract float Curve(T curve, float input, MathCurveContext ctx);
}
