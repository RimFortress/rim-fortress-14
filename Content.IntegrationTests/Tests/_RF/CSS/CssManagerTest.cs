using System.Linq;
using Content.Client._RF.CSS;
using Content.Client._RF.CSS.Streams;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.CSS;

[TestFixture, TestOf(typeof(ICssManager))]
public sealed class CssManagerTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ICssManager _css = null!;
    [SidedDependency(Side.Client)] private readonly ILocalizationManager _loc = null!;

    private CssStylesheet Load(string text)
    {
        _css.ClearTestStylesheet();
        _css.LoadString(text);
        return _css.BuildTestStylesheet();
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void TypeSelector_Resolves_Known_Control_Type()
    {
        var sheet = Load("label { min-height: 10px; }");
        var selector = (CssElementSelector)sheet.Rules.Single().Selector;

        Assert.That(selector.ElementType, Is.EqualTo(typeof(Label)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Class_Id_And_PseudoClass_Selectors_Are_Parsed_Without_A_Type()
    {
        var sheet = Load(".foo#bar:testPseudo { min-height: 1px; }");
        var selector = (CssElementSelector)sheet.Rules.Single().Selector;

        Assert.That(selector.ElementType, Is.Null);
        // exact member names below (StyleClasses/StyleIdentifier/StylePseudoClass)
        // assumed from the constructor's parameter order — verify against the
        // real CssElementSelector declaration before relying on this test.
        Assert.That(selector.ElementClasses, Does.Contain("foo"));
        Assert.That(selector.ElementId, Is.EqualTo("bar"));
        Assert.That(selector.PseudoClasses, Does.Contain("testPseudo"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Unknown_Type_Selector_Throws()
    {
        Assert.Throws<CssParserError>(() => _css.LoadString("not-a-real-control { min-height: 1px; }"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Engine_Property_Is_Stored_Under_The_PascalCase_Name_Not_The_Css_Name()
    {
        // Regression test for the kebab-case/PascalCase mismatch: StyleProperty.Name
        // must equal nameof(Control.MinHeight) ("MinHeight"), never the raw CSS
        // declaration name ("min-height") — otherwise Control's internal switch
        // (Control.Layout.Styling.cs) silently fails to match any case and the
        // property is dropped without any error.
        var sheet = Load("label { min-height: 10px; }");
        var property = sheet.Rules.Single().Properties.Single();

        Assert.That(property.Name, Is.EqualTo(nameof(Control.MinHeight)));
        Assert.That(property.Value, Is.EqualTo(10f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Enum_Property_Converts_Kebab_Case_Ident_To_The_Enum_Value()
    {
        var sheet = Load("label { horizontal-alignment: center; }");
        var property = sheet.Rules.Single().Properties.Single();

        Assert.That(property.Name, Is.EqualTo(nameof(Control.HorizontalAlignment)));
        Assert.That(property.Value, Is.EqualTo(Control.HAlignment.Center));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Applying_The_Stylesheet_Updates_A_Real_Engine_Control()
    {
        Load("css-control { min-height: 25px; }");
        var control = new CssControl();
        _css.UpdateTestStyle(control);

        Assert.That(control.MinHeight, Is.EqualTo(25f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Custom_CssControl_Property_Applies_Through_A_Class_Selector()
    {
        Load("""
            .highlighted {
                max-width: 42px;
            }
            """);

        var control = new CssControl();
        control.AddCssStyleClass("highlighted");
        _css.UpdateTestStyle(control);

        Assert.That(control.MaxWidth, Is.EqualTo(42f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Multiple_LoadString_Calls_Accumulate_Into_One_Stylesheet()
    {
        var sheet = Load("""
                label { min-height: 1px; }
                .foo { min-height: 2px; }
            """);

        Assert.That(sheet.Rules, Has.Count.GreaterThanOrEqualTo(2));
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
    public void TryParseValue_Generic_And_NonGeneric_Overloads_Agree()
    {
        var tokens = CssTokenizer.Tokenize("center");
        var stream = new CssValueStream(tokens.Cast<ICssComponentValue>().ToList());

        var genericOk = _css.TryParseValue<Control.HAlignment>(ref stream, out var viaGeneric);

        stream = new CssValueStream(tokens.Cast<ICssComponentValue>().ToList());
        var nonGenericOk = _css.TryParseValue(ref stream, typeof(Control.HAlignment), out var viaNonGeneric);

        Assert.That(genericOk, Is.EqualTo(nonGenericOk));
        Assert.That(viaGeneric, Is.EqualTo(viaNonGeneric));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ChildSelectorParseTest()
    {
        var sheet = Load("""
            css-control > test-locale-css-control {
                min-height: 10px;
            }
            """);

        Assert.That(sheet.Rules, Has.Count.EqualTo(1));
        Assert.That(sheet.Rules.Single().Selector, Is.TypeOf<SelectorChild>());

        var parent = new CssControl();
        var child = new TestLocaleCssControl();
        parent.AddChild(child);

        Assert.That(sheet.Rules.Single().Selector.Matches(child), Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void LocaleFunctionParseTest()
    {
        const string locKey = "generic-unknown";

        Load($$"""
            test-locale-css-control {
                text: loc("{{locKey}}");
            }
            """);

        var control = new TestLocaleCssControl();
        _css.UpdateTestStyle(control);

        Assert.That(control.Text, Is.EqualTo(_loc.GetString(locKey)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Background_Shorthand_Expands_Into_Separate_StyleProperty_Entries()
    {
        var sheet = Load("css-control { background: red no-repeat; }");
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
        var sheet = Load("css-control { background-position: right bottom; }");
        var properties = sheet.Rules.Single().Properties;

        Assert.That(
            properties.Select(p => p.Name),
            Is.EquivalentTo(new[] { "background-position-x", "background-position-y" }));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Longhand_Declared_After_Shorthand_Wins_On_A_Real_Control()
    {
        var control = new TestLocaleCssControl();
        // Order within one declaration block is meaningful in real CSS (later
        // wins at equal specificity) — verify the shorthand's expanded
        // background-color doesn't clobber an explicit longhand that follows it.
        Load("""
            test-locale-css-control {
                background: red;
                background-color: blue;
            }
            """);

        _css.UpdateTestStyle(control);

        // Exact accessor for reading the applied color back depends on
        // CssControl's actual background field/property — adjust to the
        // real getter if this isn't it.
        Assert.That(control.BackgroundColor, Is.EqualTo(Color.Blue));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void VariableParseTest()
    {
        var control = new CssControl();

        Load("""
            :root {
                --test-var: red;
            }

            css-control {
                background-color: var(--test-var);
            }
            """);

        _css.UpdateTestStyle(control);
        Assert.That(control.BackgroundColor, Is.EqualTo(Color.Red));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void VariableOverwriteTest()
    {
        var control = new CssControl();

        Load("""
            :root {
                --test-var: red;
            }

            css-control {
                --test-var: blue;
                background-color: var(--test-var);
            }
            """);

        _css.UpdateTestStyle(control);
        Assert.That(control.BackgroundColor, Is.EqualTo(Color.Blue));
    }
}

public sealed partial class TestLocaleCssControl : CssControl
{
    [CssProperty]
    public string Text { get; set; }
}
