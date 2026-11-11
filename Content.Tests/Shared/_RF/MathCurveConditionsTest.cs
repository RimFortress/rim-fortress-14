using System.Collections.Generic;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Curves;
using NUnit.Framework;

namespace Content.Tests.Shared._RF;

[TestFixture]
public sealed class MathCurveConditionsTest
{
    private static readonly MathCurveContext Context = new(null, new Dictionary<string, float>());

    private static float Run(float input, params MathCurve[] curves)
        => MathCurve.ListValue(curves, null!, input, Context);

    [Test]
    public void TestThreshold()
    {
        Assert.That(Run(5f, new Threshold { MoreThan = 3f }), Is.EqualTo(1f));
        Assert.That(Run(3f, new Threshold { MoreThan = 3f }), Is.EqualTo(0f), "greater, not equal");
        Assert.That(Run(5f, new Threshold { LessThan = 3f }), Is.EqualTo(0f));
        Assert.That(Run(5f, new Threshold { MoreThan = 3f, LessThan = 8f }), Is.EqualTo(1f));
        Assert.That(Run(9f, new Threshold { MoreThan = 3f, LessThan = 8f }), Is.EqualTo(0f));
        Assert.That(Run(2f, new Threshold { EqualTo = 2f }), Is.EqualTo(1f));
        Assert.That(Run(2.1f, new Threshold { EqualTo = 2f }), Is.EqualTo(0f));
        Assert.That(Run(float.NaN, new Threshold { MoreThan = 0f }), Is.EqualTo(0f), "nothing is greater than that");
    }

    [Test]
    public void TestBothHold()
    {
        // Both of them: the lesser of the two.
        var both = new MinCurve { Min = new List<MathCurve> { new FloatCurve { Float = 1f } } };

        Assert.That(Run(1f, both), Is.EqualTo(1f));
        Assert.That(Run(0f, both), Is.EqualTo(0f));
    }

    [Test]
    public void TestOneOfThemHolds()
    {
        var either = new MaxCurve { Max = new List<MathCurve> { new FloatCurve { Float = 0f } } };

        Assert.That(Run(1f, either), Is.EqualTo(1f));
        Assert.That(Run(0f, either), Is.EqualTo(0f));
    }
}
