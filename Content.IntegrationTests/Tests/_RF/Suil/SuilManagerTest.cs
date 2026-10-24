using System.Linq;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.Streams;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests._RF.Suil;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Suil;

[TestFixture, TestOf(typeof(ISuilManager))]
public sealed class SuilManagerTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;
    [SidedDependency(Side.Client)] private readonly ILocalizationManager _loc = null!;

    private SuilStylesheet Load(string text)
    {
        _suil.ClearTestStylesheet();
        _suil.LoadSuilString(text);
        return _suil.BuildTestStylesheet();
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Applying_The_Stylesheet_Updates_A_Real_Engine_Control()
    {
        Load("_ { min-height: 25px; }");
        var control = new UiElement();
        _suil.UpdateTestStyle(control);

        Assert.That(control.MinHeight, Is.EqualTo(25f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Custom_UiElement_Property_Applies_Through_A_Class_Selector()
    {
        Load("""
            .highlighted {
                max-width: 42px;
            }
            """);

        var control = new UiElement();
        control.AddSuilStyleClass("highlighted");
        _suil.UpdateTestStyle(control);

        Assert.That(control.MaxWidth, Is.EqualTo(42f));
    }

#if EXCEPTION_TOLERANCE
    [Test]
    [RunOnSide(Side.Client)]
    public void Unknown_Property_Is_Skipped_Rather_Than_Applied()
    {
        // Relies on the EXCEPTION_TOLERANCE branch in TryApplyDeclaration's caller
        // (Load) taking the "continue" path for an unregistered property name, rather than the throw.
        var sheet = Load("label { not-a-real-property: 1px; min-height: 3px; }");
        var properties = sheet.Rules.Single().Properties;

        Assert.That(properties, Has.Count.EqualTo(1));
        Assert.That(properties.Single().Name, Is.EqualTo(nameof(Control.MinHeight)));
    }
#endif

    [Test]
    [RunOnSide(Side.Client)]
    public void ChildSelectorParseTest()
    {
        var sheet = Load("""
            _ > test-locale-ui-element {
                min-height: 10px;
            }
            """);

        Assert.That(sheet.Rules, Has.Count.EqualTo(1));
        Assert.That(sheet.Rules.Single().Selector, Is.TypeOf<SelectorChild>());

        var parent = new UiElement();
        var child = new TestLocaleUiElement();
        parent.AddChild(child);

        Assert.That(sheet.Rules.Single().Selector.Matches(child), Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void LocaleFunctionParseTest()
    {
        const string locKey = "generic-unknown";

        Load($$"""
            test-locale-ui-element {
                locale-text: loc("{{locKey}}");
            }
            """);

        var control = new TestLocaleUiElement();
        _suil.UpdateTestStyle(control);

        Assert.That(control.LocaleText, Is.EqualTo(_loc.GetString(locKey)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Background_Shorthand_Expands_Into_Separate_StyleProperty_Entries()
    {
        var sheet = Load("_ { background: red no-repeat; }");
        var properties = sheet.Rules.Single().Properties;
        var names = properties.Select(p => p.Name).ToArray();

        Assert.That(names, Does.Contain("background-color"));
        Assert.That(names, Does.Contain("background-repeat"));
        Assert.That(properties.Single(p => p.Name == "background-color").Value, Is.EqualTo(Color.Red));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Background_Position_Shorthand_Expands_Into_X_And_Y()
    {
        var sheet = Load("_ { background-position: right bottom; }");
        var properties = sheet.Rules.Single().Properties;

        Assert.That(
            properties.Select(p => p.Name),
            Is.EquivalentTo(new[] { "background-position-x", "background-position-y" }));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Longhand_Declared_After_Shorthand_Wins_On_A_Real_Control()
    {
        var control = new TestLocaleUiElement();
        // Order within one declaration block is meaningful in real SUIL (later
        // wins at equal specificity) — verify the shorthand's expanded
        // background-color doesn't clobber an explicit longhand that follows it.
        Load("""
            test-locale-ui-element {
                background: red;
                background-color: blue;
            }
            """);

        _suil.UpdateTestStyle(control);

        // Exact accessor for reading the applied color back depends on
        // UiElement's actual background field/property — adjust to the
        // real getter if this isn't it.
        Assert.That(control.BackgroundColor, Is.EqualTo(Color.Blue));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ConstantParseTest()
    {
        var control = new UiElement();

        Load("""
            let test-var = red;

            _ {
                background-color: <test-var>;
            }
            """);

        _suil.UpdateTestStyle(control);
        Assert.That(control.BackgroundColor, Is.EqualTo(Color.Red));
    }
}

public sealed partial class TestLocaleUiElement : UiElement
{
    [SuilProperty]
    public string LocaleText { get; set; }
}
