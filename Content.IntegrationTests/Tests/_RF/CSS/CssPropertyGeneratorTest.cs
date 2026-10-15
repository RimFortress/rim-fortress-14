using System.Linq;
using Content.Client._RF.CSS;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;

namespace Content.IntegrationTests.Tests._RF.CSS;

[TestFixture]
public sealed class CssPropertyGeneratorTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilManager _css = null!;

    [Test]
    [RunOnSide(Side.Client)]
    public void Every_Public_Member_Is_A_Property()
    {
        var control = new TestCssControl();

        Assert.That(control.TestDefault, Is.EqualTo(15f));
        Assert.That(control.Test, Is.Zero);

        // [SuilProperty] is not needed for a member to be stylable, only for a custom name
        Assert.That(_css.TryGetPropertyAccessor(typeof(TestCssControl), "test", out var test), Is.True);
        Assert.That(_css.TryGetPropertyAccessor(typeof(TestCssControl), "test-default", out _), Is.True);
        Assert.That(_css.TryGetPropertyAccessor(typeof(TestCssControl), "plain", out var plain), Is.True);
        Assert.That(_css.TryGetPropertyAccessor(typeof(TestCssControl), "renamed-member", out _), Is.False);
        Assert.That(_css.TryGetPropertyAccessor(typeof(TestCssControl), "custom", out var custom), Is.True);
        Assert.That(_css.TryGetPropertyAccessor(typeof(TestCssControl), "min-height", out _), Is.True, "a member of the base class");

        test!.Value.Setter(control, 2.3f);
        plain!.Value.Setter(control, 4f);
        custom!.Value.Setter(control, 6f);
        Assert.That(control.Test, Is.EqualTo(2.3f));
        Assert.That(control.Plain, Is.EqualTo(4f));
        Assert.That(control.RenamedMember, Is.EqualTo(6f));
        Assert.That(test.Value.Getter!(control), Is.EqualTo(2.3f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void GeneratorGetterTest()
    {
        _css.ClearTestStylesheet();
        _css.LoadSuilString("""
            test-css-control {
                test: 1337px;
                min-height: 11px;
            }
            """);

        var control = new TestCssControl();
        _css.UpdateTestStyle(control);

        Assert.That(control.Test, Is.EqualTo(1337f));
        Assert.That(control.MinHeight, Is.EqualTo(11f));
    }
}

public sealed partial class TestCssControl : CssControl
{
    [SuilProperty]
    public float Test { get; set; }

    [SuilProperty]
    public float TestDefault { get; set; } = 15f;

    public float Plain { get; set; }

    [SuilProperty("custom")]
    public float RenamedMember { get; set; }
}
