#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.Client._RF.CSS;
using Content.Client._RF.CSS.Parsers;
using Content.Client._RF.CSS.Streams;
using Content.Suil;
using Content.Client._RF.Graphics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.CSS;

/// <summary>
/// <c>calc()</c>, <c>min()</c>, <c>max()</c>, <c>clamp()</c>, the units <c>rlh</c>, <c>cap</c>, <c>ic</c>,
/// <c>dvh</c>, <c>svh</c>, <c>lvh</c>, and the parsers that take them.
/// </summary>
[TestFixture]
public sealed class CssCalcTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ICssManager _css = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

    // em = 16, rem = 16, ex = 8, ch = 9, lh = 20, viewport 800x600, cap = 11, ic = 16, rlh = 24
    private static readonly LengthContext Context =
        new(16f, 16f, 8f, 9f, 20f, new Vector2(800f, 600f), 11f, 16f, 24f);

    private static List<ICssComponentValue> Tokens(string css)
        => ((CssDeclaration) CssParser.ParseListOfDeclarations($"x: {css};")[0]).Value;

    private bool TryParse<T>(string css, out T? value)
    {
        var stream = new CssValueStream(Tokens(css));
        return _css.TryParseValue(ref stream, out value) && stream.AtEnd;
    }

    private static bool TryParseNode(string css, out CalcNode? node)
    {
        node = null;
        return CssValueConverter.Convert(Tokens(css)) is [SuilFunction function] && CssCalc.TryParse(function, out node);
    }

    private LengthUnit Length(string css)
    {
        Assert.That(TryParse<LengthUnit>(css, out var value), Is.True, css);
        return value;
    }

    private float Px(string css, float percentBase = 0f) => Length(css).Resolve(Context, percentBase);

    #region Lengths

    [Test]
    [RunOnSide(Side.Client)]
    public void Calc_Of_Absolute_Units_Folds_To_Pixels()
    {
        Assert.That(Length("calc(1in + 4px)"), Is.EqualTo(LengthUnit.Px(100f)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Calc_Of_A_Single_Dimension_Is_That_Dimension()
    {
        Assert.That(Length("calc(2em)"), Is.EqualTo(LengthUnit.Em(2f)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Calc_With_Relative_Units_Is_Kept_And_Resolved_Later()
    {
        var length = Length("calc(100% - 2em)");

        Assert.Multiple(() =>
        {
            Assert.That(length.Type, Is.EqualTo(LengthUnitType.Calc));
            Assert.That(length.DependsOnPercent, Is.True);
            Assert.That(length.Resolve(Context, 400f), Is.EqualTo(368f));
            Assert.That(length.Resolve(Context, 1000f), Is.EqualTo(968f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Calc_Without_Percent_Does_Not_Depend_On_Percent()
    {
        Assert.That(Length("calc(2em + 1vw)").DependsOnPercent, Is.False);
    }

    [TestCase("calc(1px + 2px * 3)", 7f)]
    [TestCase("calc((1px + 2px) * 3)", 9f)]
    [TestCase("calc(10px / 4)", 2.5f)]
    [TestCase("calc(3 * 2px)", 6f)]
    [TestCase("calc(10px - 4px - 3px)", 3f)]
    [TestCase("calc(20px / 2 / 5)", 2f)]
    [TestCase("calc(2px * pi)", 2f * MathF.PI)]
    [TestCase("CALC(1px + 1px)", 2f)]
    [RunOnSide(Side.Client)]
    public void Calc_Follows_Operator_Precedence(string css, float expected)
    {
        Assert.That(Px(css), Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase("calc(1em + 1ex + 1ch)", 33f)]
    [TestCase("calc(1lh + 1rlh)", 44f)]
    [TestCase("calc(1cap + 1ic)", 27f)]
    [TestCase("calc(1vw + 1vh)", 14f)]
    [TestCase("calc(10% + 1rem)", 56f)]
    [RunOnSide(Side.Client)]
    public void Calc_Resolves_Every_Relative_Unit(string css, float expected)
    {
        Assert.That(Px(css, 400f), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Nested_Math_Functions_Are_Evaluated()
    {
        Assert.That(Px("calc(min(10px, 2em) + max(1px, 3px))"), Is.EqualTo(13f));
        Assert.That(Px("calc(clamp(0px, 50vw, 100px) * 2)"), Is.EqualTo(200f));
    }

    [TestCase("min(50%, 300px)", 200f)]
    [TestCase("min(1em, 20px, 5px)", 5f)]
    [TestCase("max(1em, 20px)", 20f)]
    [TestCase("max(50%, 100px, 1em)", 200f)]
    [TestCase("clamp(10px, 50vw, 300px)", 300f)]
    [TestCase("clamp(10px, 1px, 300px)", 10f)]
    [TestCase("clamp(10px, 100px, 300px)", 100f)]
    [TestCase("clamp(100px, 0px, 50px)", 100f)] // the minimum wins
    [TestCase("min(5px)", 5f)]
    [RunOnSide(Side.Client)]
    public void Min_Max_Clamp_Pick_The_Right_Argument(string css, float expected)
    {
        Assert.That(Px(css, 400f), Is.EqualTo(expected));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Min_Of_Absolute_Units_Folds_To_Pixels()
    {
        Assert.That(Length("min(1in, 50px)"), Is.EqualTo(LengthUnit.Px(50f)));
    }

    [TestCase("calc(1px + 2)")] // a length and a number can't be added
    [TestCase("calc(1px * 2px)")]
    [TestCase("calc(1px / 2px)")]
    [TestCase("calc(1px / 2px)")]
    [TestCase("calc(1px 2px)")] // no operator
    [TestCase("calc(1px+2px)")] // +2px is a number token
    [TestCase("calc(1px +)")]
    [TestCase("calc(* 2px)")]
    [TestCase("calc()")]
    [TestCase("calc(2 * 3)")] // a number is not a length
    [TestCase("calc(foo)")]
    [TestCase("min()")]
    [TestCase("min(1px, 2)")]
    [TestCase("max(1px,)")]
    [TestCase("clamp(1px, 2px)")]
    [TestCase("clamp(1px, 2px, 3px, 4px)")]
    [TestCase("foo(1px)")]
    [RunOnSide(Side.Client)]
    public void Invalid_Math_Is_Rejected_As_A_Length(string css)
    {
        Assert.That(TryParse<LengthUnit>(css, out _), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Unclosed_Math_Function_Is_A_Parse_Error()
    {
        Assert.Throws<CssParserError>(() => Tokens("calc((1px)"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Math_Function_Is_Consumed_As_One_Value()
    {
        var stream = new CssValueStream(Tokens("calc(1px + 1px) 3px"));

        Assert.Multiple(() =>
        {
            Assert.That(_css.TryParseValue<LengthUnit>(ref stream, out var first), Is.True);
            Assert.That(first, Is.EqualTo(LengthUnit.Px(2f)));
            Assert.That(_css.TryParseValue<LengthUnit>(ref stream, out var second), Is.True);
            Assert.That(second, Is.EqualTo(LengthUnit.Px(3f)));
            Assert.That(stream.AtEnd, Is.True);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Failed_Math_Function_Is_Not_Consumed()
    {
        var stream = new CssValueStream(Tokens("calc(1px + 2)"));

        Assert.That(_css.TryParseValue<LengthUnit>(ref stream, out _), Is.False);
        Assert.That(stream.AtEnd, Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Calc_Length_Keeps_Its_Meaning_When_Scaled_Or_Negated()
    {
        var length = Length("calc(100% - 2em)");

        Assert.Multiple(() =>
        {
            Assert.That((length * 2f).Resolve(Context, 400f), Is.EqualTo(736f));
            Assert.That((-length).Resolve(Context, 400f), Is.EqualTo(-368f));
            Assert.That(length.IsZero, Is.False);
        });
    }

    #endregion

    #region Number expressions

    [TestCase("calc(2 * 3)", 6f)]
    [TestCase("calc(1 + 2 * 3)", 7f)]
    [TestCase("calc(pi)", MathF.PI)]
    [TestCase("calc(e * 2)", MathF.E * 2f)]
    [TestCase("max(1, 5, 3)", 5f)]
    [TestCase("clamp(0, -3, 1)", 0f)]
    [RunOnSide(Side.Client)]
    public void Number_Expressions_Evaluate_To_A_Number(string css, float expected)
    {
        Assert.That(TryParseNode(css, out var node), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(node!.Kind, Is.EqualTo(CalcKind.Number));
            Assert.That(node.IsConstant, Is.True);
            Assert.That(node.Eval(default), Is.EqualTo(expected).Within(0.001f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Number_Divided_By_Length_Is_Invalid()
    {
        Assert.That(TryParseNode("calc(2 / 1px)", out _), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Length_Divided_By_Number_Is_A_Length()
    {
        Assert.That(TryParseNode("calc(10px / 2)", out var node), Is.True);
        Assert.That(node!.Kind, Is.EqualTo(CalcKind.Length));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Bind_Resolves_Everything_But_Percentages()
    {
        Assert.That(TryParseNode("calc(100% - 2em + 1vw)", out var node), Is.True);

        var bound = node!.Bind(Context);

        Assert.Multiple(() =>
        {
            Assert.That(bound.HasPercent, Is.True);
            Assert.That(bound.Eval(default, 400f), Is.EqualTo(400f - 32f + 8f));
            Assert.That(bound.Eval(default, 0f), Is.EqualTo(-24f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Bind_Without_Percent_Is_Constant()
    {
        Assert.That(TryParseNode("min(2em, 1vh)", out var node), Is.True);
        Assert.That(node!.Bind(Context).IsConstant, Is.True);
        Assert.That(node.Bind(Context).EvalConstant(), Is.EqualTo(6f));
    }

    #endregion

    #region New units

    [TestCase("1rlh", LengthUnitType.Rlh, 24f)]
    [TestCase("2cap", LengthUnitType.Cap, 22f)]
    [TestCase("3ic", LengthUnitType.Ic, 48f)]
    [TestCase("10dvh", LengthUnitType.Dvh, 60f)]
    [TestCase("10svh", LengthUnitType.Svh, 60f)]
    [TestCase("10lvh", LengthUnitType.Lvh, 60f)]
    [TestCase("0.5RLH", LengthUnitType.Rlh, 12f)]
    [RunOnSide(Side.Client)]
    public void New_Units_Are_Parsed_And_Resolved(string css, LengthUnitType type, float expected)
    {
        var length = Length(css);

        Assert.Multiple(() =>
        {
            Assert.That(length.Type, Is.EqualTo(type));
            Assert.That(length.Resolve(Context), Is.EqualTo(expected));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void New_Units_Are_Classified()
    {
        Assert.Multiple(() =>
        {
            foreach (var unit in new[] { LengthUnit.Rlh(1), LengthUnit.Cap(1), LengthUnit.Ic(1) })
            {
                Assert.That(unit.IsFontRelative, Is.True, unit.ToString());
                Assert.That(unit.IsViewportRelative, Is.False, unit.ToString());
                Assert.That(unit.IsAbsolute, Is.False, unit.ToString());
            }

            foreach (var unit in new[] { LengthUnit.Dvh(1), LengthUnit.Svh(1), LengthUnit.Lvh(1) })
            {
                Assert.That(unit.IsViewportRelative, Is.True, unit.ToString());
                Assert.That(unit.IsFontRelative, Is.False, unit.ToString());
                Assert.That(unit.ViewportToPx(new Vector2(800, 600)), Is.EqualTo(LengthUnit.Px(6f)), unit.ToString());
            }

            Assert.That(LengthUnitType.FontRelativeUnits, Does.Contain(LengthUnitType.Rlh));
            Assert.That(LengthUnitType.ViewportUnits, Does.Contain(LengthUnitType.Dvh));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Initial_Context_Has_Metrics_For_The_New_Units()
    {
        var context = LengthContext.Initial(10f, new Vector2(100, 100));

        Assert.Multiple(() =>
        {
            Assert.That(LengthUnit.Rlh(1).Resolve(context), Is.GreaterThan(0f));
            Assert.That(LengthUnit.Cap(1).Resolve(context), Is.GreaterThan(0f));
            Assert.That(LengthUnit.Ic(1).Resolve(context), Is.GreaterThan(0f));
        });
    }

    #endregion

    #region Float and thickness

    [TestCase("4px", 4f)]
    [TestCase("1in", 96f)] // used to be divided by the ratio: 1/96
    [TestCase("2.54cm", 96f)]
    [TestCase("12pt", 16f)]
    [TestCase("0", 0f)]
    [TestCase("calc(1in + 4px)", 100f)]
    [TestCase("calc(2 * 3px)", 6f)]
    [TestCase("min(1in, 50px)", 50f)]
    [TestCase("clamp(0px, 200px, 100px)", 100f)]
    [RunOnSide(Side.Client)]
    public void Float_Is_A_Context_Free_Length_In_Pixels(string css, float expected)
    {
        Assert.That(TryParse<float>(css, out var value), Is.True);
        Assert.That(value, Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase("1em")]
    [TestCase("50%")]
    [TestCase("10vw")]
    [TestCase("calc(1em + 1px)")]
    [TestCase("calc(100% - 1px)")]
    [TestCase("calc(2 * 3)")] // a number is not a length
    [TestCase("4")]
    [RunOnSide(Side.Client)]
    public void Float_Rejects_What_Needs_A_Context_Or_Has_No_Unit(string css)
    {
        Assert.That(TryParse<float>(css, out _), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Thickness_Uses_The_Css_Value_Order()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TryParse<Thickness>("1px", out var one), Is.True);
            Assert.That(one, Is.EqualTo(new Thickness(1f)));

            // vertical horizontal
            Assert.That(TryParse<Thickness>("1px 2px", out var two), Is.True);
            Assert.That(two, Is.EqualTo(new Thickness(2f, 1f, 2f, 1f)));

            // top horizontal bottom
            Assert.That(TryParse<Thickness>("1px 2px 3px", out var three), Is.True);
            Assert.That(three, Is.EqualTo(new Thickness(2f, 1f, 2f, 3f)));

            // top right bottom left
            Assert.That(TryParse<Thickness>("1px 2px 3px 4px", out var four), Is.True);
            Assert.That(four, Is.EqualTo(new Thickness(4f, 1f, 2f, 3f)));

            Assert.That(TryParse<Thickness>("calc(1px + 1px) 1px", out var math), Is.True);
            Assert.That(math, Is.EqualTo(new Thickness(1f, 2f, 1f, 2f)));
        });
    }

    #endregion

    #region Background

    private static bool TryParseSize(string css, out RfStyleBoxTexture.StyleBoxSize size)
    {
        var stream = CssValueConverter.Stream(Tokens(css));
        return new StyleBoxSizeParser().TryParse(ref stream, out size) && stream.AtEnd;
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Background_Size_Takes_Math_Functions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TryParseSize("calc(100% - 20px) 50px", out var size), Is.True);
            Assert.That(size.WidthCalc, Is.Not.Null);
            Assert.That(size.WidthCalc!.Eval(default, 200f), Is.EqualTo(180f));
            Assert.That(size.HeightCalc, Is.Null);
            Assert.That(size.Height, Is.EqualTo(50f));

            // no context needed: a plain pixel width
            Assert.That(TryParseSize("calc(1in + 10px) auto", out var folded), Is.True);
            Assert.That(folded.WidthCalc, Is.Null);
            Assert.That(folded.Width, Is.EqualTo(106f));
            Assert.That(folded.HeightAuto, Is.True);

            Assert.That(TryParseSize("auto min(50%, 2em)", out var height), Is.True);
            Assert.That(height.HeightCalc, Is.Not.Null);

            Assert.That(TryParseSize("calc(2 * 3)", out _), Is.False);
            Assert.That(TryParseSize("calc(1px + 2)", out _), Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Background_Position_Takes_Math_Functions()
    {
        var stream = CssValueConverter.Stream(Tokens("calc(50% + 10px)"));
        Assert.That(BackgroundPositionParsing.TryParseComponent(ref stream, out _, out var abs, out var calc, true), Is.True);
        Assert.That(abs, Is.False);
        Assert.That(calc!.Eval(default, 100f), Is.EqualTo(60f));

        stream = CssValueConverter.Stream(Tokens("calc(1in + 4px)"));
        Assert.That(BackgroundPositionParsing.TryParseComponent(ref stream, out var value, out abs, out calc, false), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(calc, Is.Null);
            Assert.That(abs, Is.True);
            Assert.That(value, Is.EqualTo(100f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Background_Position_Takes_Any_Absolute_Unit()
    {
        var stream = CssValueConverter.Stream(Tokens("1in"));

        Assert.That(BackgroundPositionParsing.TryParseComponent(ref stream, out var value, out var abs, out _, true), Is.True);
        Assert.That(abs, Is.True);
        Assert.That(value, Is.EqualTo(96f));

        stream = CssValueConverter.Stream(Tokens("1em"));
        Assert.That(BackgroundPositionParsing.TryParseComponent(ref stream, out _, out _, out _, true), Is.False);
    }

    #endregion

    #region Controls

    private TestUnitsControl Styled(string rules, params string[] classes)
    {
        _css.ClearTestStylesheet();
        _css.LoadString(rules);
        _css.BuildTestStylesheet();

        var control = new TestUnitsControl();

        foreach (var name in classes)
        {
            control.AddCssStyleClass(name);
        }

        _css.UpdateTestStyle(control);
        return control;
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Property_Takes_Calc_From_A_Stylesheet()
    {
        var control = Styled(".a { padding-left: calc(1in + 4px); padding-top: calc(100% - 2em); }", "a");

        Assert.Multiple(() =>
        {
            Assert.That(control.PaddingLeftValue, Is.EqualTo(LengthUnit.Px(100f)));
            Assert.That(control.PaddingTopValue.Type, Is.EqualTo(LengthUnitType.Calc));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Calc_Em_Resolves_Against_The_Font_Size_Of_The_Control()
    {
        var control = Styled(".a { font-size: 20px; }", "a");

        Assert.That(control.Px(Length("calc(2em + 1px)")), Is.EqualTo(41f));
        Assert.That(control.Px(Length("clamp(10px, 1em, 15px)")), Is.EqualTo(15f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void New_Font_Units_Resolve_Against_The_Control()
    {
        var control = Styled(".a { font-size: 20px; }", "a");

        Assert.Multiple(() =>
        {
            Assert.That(control.Px(Length("1cap")), Is.GreaterThan(0f).And.LessThan(20f));
            Assert.That(control.Px(Length("1ic")), Is.GreaterThan(0f));
            Assert.That(control.Px(Length("1rlh")), Is.GreaterThan(0f));
            Assert.That(control.Px(Length("1rlh")), Is.EqualTo(control.RootLineHeight));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Viewport_Units_Follow_The_Resizing_Of_The_Root()
    {
        // The viewport is the topmost control of the tree, i.e. the root of the UI.
        var window = _ui.RootControl;
        var original = new UIBox2(Vector2.Zero, window.Size);
        var control = new TestUnitsControl();
        window.AddChild(control);

        try
        {
            window.Arrange(new UIBox2(0, 0, 800, 600));

            Assert.That(control.Px(Length("10vw")), Is.EqualTo(80f));
            Assert.That(control.Px(Length("10dvh")), Is.EqualTo(60f));

            window.Arrange(new UIBox2(0, 0, 400, 200));

            Assert.That(control.Px(Length("10vw")), Is.EqualTo(40f));
            Assert.That(control.Px(Length("10svh")), Is.EqualTo(20f));
        }
        finally
        {
            control.Orphan();
            window.Arrange(original);
        }
    }

    #endregion
}

/// <summary>
/// Exposes the length resolution of <see cref="CssControl"/> to the tests.
/// </summary>
public sealed class TestUnitsControl : CssControl
{
    public LengthUnit PaddingLeftValue => PaddingLeft;
    public LengthUnit PaddingTopValue => PaddingTop;

    public float RootLineHeight => LengthUnit.Rlh(1).Resolve(LengthContext);

    public float Px(LengthUnit length, float percentBase = 0f) => ToPx(length, percentBase);
}
