using System.Linq;
using Content.Client._RF.CSS;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;

namespace Content.IntegrationTests.Tests._RF.CSS;

[TestFixture]
public sealed class CssPropertyGeneratorTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ICssManager _css = null!;

    [Test]
    [RunOnSide(Side.Client)]
    public void GeneratorTest()
    {
        var control = new TestCssControl();
        var css = control.AutoCssProperties();

        Assert.That(css, Has.Count.EqualTo(2));
        Assert.That(control.TestDefault, Is.EqualTo(15f));
        Assert.That(control.Test, Is.Zero);
        css["test"].Setter.Invoke(control, 2.3f);
        Assert.That(control.Test, Is.EqualTo(2.3f));
        Assert.That(css.Last().Key, Is.EqualTo("test-default"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void GeneratorGetterTest()
    {
        _css.ClearTestStylesheet();
        _css.LoadString("""
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
    [CssProperty]
    public float Test { get; set; }

    [CssProperty]
    public float TestDefault { get; set; } = 15f;
}
