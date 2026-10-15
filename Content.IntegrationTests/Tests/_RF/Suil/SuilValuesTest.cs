#nullable enable
using System.Linq;
using System.Numerics;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.Parsers;
using Content.Client._RF.Suil.Text;
using Content.Client._RF.Graphics;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The value parsers of the game fed with the values written in SUIL, not in SUIL: the text is parsed by
/// <see cref="SuilParser"/> and the parsers read its values.
/// </summary>
[TestFixture]
public sealed class SuilValuesTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;

    // em = 16, rem = 16, viewport 800x600
    private static readonly LengthContext Context =
        new(16f, 16f, 8f, 9f, 20f, new Vector2(800f, 600f), 11f, 16f, 24f);

    private static SuilValueStream Stream(string value)
    {
        var rule = (SuilRule) SuilParser.ParseFile($".a {{ v: {value}; }}").Items.Single();
        return new SuilValueStream(rule.Declarations.Single().Value);
    }

    private bool TryParse<T>(string value, out T? result)
    {
        var stream = Stream(value);
        return _suil.TryParse(ref stream, out result) && stream.AtEnd;
    }

    private T Parse<T>(string value)
    {
        Assert.That(TryParse<T>(value, out var result), Is.True, value);
        return result!;
    }

    // ---- Colors ----

    [TestCase("#B07D2B", "#B07D2B")]
    [TestCase("#5E4B3A59", "#5E4B3A59")]
    [TestCase("#abc", "#aabbcc")]
    [TestCase("rgb(255, 0, 0)", "#FF0000")]
    [TestCase("rgb(255 0 0)", "#FF0000")]
    [TestCase("rgba(0, 0, 255, 0.5)", "#0000FF7F")]
    [RunOnSide(Side.Client)]
    public void Colors(string text, string expectedHex)
    {
        Assert.That(Parse<Color>(text), Is.EqualTo(Color.FromHex(expectedHex)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Named_Color()
    {
        Assert.That(Parse<Color>("red"), Is.EqualTo(Color.Red));
        Assert.That(TryParse<Color>("nonsense", out _), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Color_Mix_Of_Two_Colors()
    {
        var mixed = Parse<Color>("color-mix(in srgb, #ff0000 50%, #0000ff)");

        Assert.Multiple(() =>
        {
            Assert.That(mixed.R, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(mixed.G, Is.EqualTo(0f).Within(0.01f));
            Assert.That(mixed.B, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(mixed.A, Is.EqualTo(1f).Within(0.01f));
        });
    }

    // ---- Lengths ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Lengths()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse<LengthUnit>("12px"), Is.EqualTo(LengthUnit.Px(12f)));
            Assert.That(Parse<LengthUnit>("-4px"), Is.EqualTo(LengthUnit.Px(-4f)));
            Assert.That(Parse<LengthUnit>("50%"), Is.EqualTo(new LengthUnit(50f, LengthUnitType.Percent)));
            Assert.That(Parse<LengthUnit>("1.5em"), Is.EqualTo(new LengthUnit(1.5f, LengthUnitType.Em)));
            Assert.That(Parse<LengthUnit>("0"), Is.EqualTo(LengthUnit.Zero));
            Assert.That(TryParse<LengthUnit>("12", out _), Is.False, "a number is not a length");
            Assert.That(TryParse<LengthUnit>("12 px", out _), Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Absolute_Lengths_Become_Pixels()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse<float>("4px"), Is.EqualTo(4f));
            Assert.That(Parse<float>("1in"), Is.EqualTo(96f));
            Assert.That(TryParse<float>("1em", out _), Is.False, "em needs an element");
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Calc_Is_Written_With_Operators()
    {
        var relative = Parse<LengthUnit>("calc(100% - 10px)");

        Assert.Multiple(() =>
        {
            Assert.That(relative.Type, Is.EqualTo(LengthUnitType.Calc));
            Assert.That(relative.Resolve(Context, 200f), Is.EqualTo(190f));

            Assert.That(Parse<LengthUnit>("calc(1in + 4px)"), Is.EqualTo(LengthUnit.Px(100f)), "folded at once");
            Assert.That(Parse<LengthUnit>("calc((1px + 2px) * 3)"), Is.EqualTo(LengthUnit.Px(9f)));
            Assert.That(Parse<LengthUnit>("min(10px, 2em)").Resolve(Context), Is.EqualTo(10f));
            Assert.That(Parse<LengthUnit>("clamp(5px, 1em, 8px)").Resolve(Context), Is.EqualTo(8f));
            Assert.That(TryParse<LengthUnit>("calc(1px + 2)", out _), Is.False, "a length and a number");
        });
    }

    // ---- Boxes ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Thickness_Follows_The_Suil_Order()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse<Thickness>("1px"), Is.EqualTo(new Thickness(1f)));
            Assert.That(Parse<Thickness>("1px 2px"), Is.EqualTo(new Thickness(2f, 1f, 2f, 1f)));
            Assert.That(Parse<Thickness>("1px 2px 3px 4px"), Is.EqualTo(new Thickness(4f, 1f, 2f, 3f)));
            Assert.That(TryParse<Thickness>("1px 2px 3px 4px 5px", out _), Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Border_Image_Slice()
    {
        var stream = Stream("30 fill");

        Assert.That(BorderImageParsing.TryParseSlice(ref stream, out var slice) && stream.AtEnd, Is.True);
        Assert.That(slice.Fill, Is.True);
        Assert.That(slice.Edges.Top, Is.EqualTo(BorderImageEdge.Number(30)));
    }

    // ---- Text ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Strings_And_Idents()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse<string>("\"a b\""), Is.EqualTo("a b"));
            Assert.That(Parse<string>("word"), Is.EqualTo("word"));
            Assert.That(Parse<bool>("true"), Is.True);
            Assert.That(TryParse<bool>("maybe", out _), Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Font_Family_Is_A_List_With_Commas()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse<SuilFontFamily>("\"Noto Sans\", Arial, sans-serif").Names,
                Is.EqualTo(new[] { "Noto Sans", "Arial", "sans-serif" }));
            Assert.That(Parse<SuilFontFamily>("Source Code Pro").Names, Is.EqualTo(new[] { "Source Code Pro" }));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Line_Height()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse<SuilLineHeight>("normal"), Is.EqualTo(SuilLineHeight.Normal));
            Assert.That(Parse<SuilLineHeight>("1.5"), Is.EqualTo(SuilLineHeight.FromMultiplier(1.5f)));
            Assert.That(Parse<SuilLineHeight>("120%"), Is.EqualTo(SuilLineHeight.FromMultiplier(1.2f)));
            Assert.That(Parse<SuilLineHeight>("20px"), Is.EqualTo(SuilLineHeight.FromLength(LengthUnit.Px(20f))));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Text_Shadow_Layers_Are_Separated_By_Commas()
    {
        var shadows = Parse<SuilTextShadow[]>("1px 2px 3px red, -1px -2px blue");

        Assert.Multiple(() =>
        {
            Assert.That(shadows, Has.Length.EqualTo(2));
            Assert.That(shadows[0].Offset, Is.EqualTo(new UnitVector2(LengthUnit.Px(1f), LengthUnit.Px(2f))));
            Assert.That(shadows[0].Blur, Is.EqualTo(LengthUnit.Px(3f)));
            Assert.That(shadows[0].Color, Is.EqualTo(Color.Red));
            Assert.That(shadows[1].Offset, Is.EqualTo(new UnitVector2(LengthUnit.Px(-1f), LengthUnit.Px(-2f))));
            Assert.That(shadows[1].Color, Is.EqualTo(Color.Blue));
            Assert.That(Parse<SuilTextShadow[]>("none"), Is.Null, "none is the null of a reference type, as it always was");
        });
    }

    // ---- Resources and the rest ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Texture_Function_Takes_A_String()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse<Texture>("texture(\"/Textures/_RF/Interface/Style/cross.png\")"), Is.Not.Null);
            Assert.That(Parse<Texture>("none"), Is.Null, "none is the null of a reference type");
            Assert.That(TryParse<Texture>("texture(1)", out _), Is.False);
            Assert.That(TryParse<Texture>("texture(\"a\", \"b\")", out _), Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Enums_Are_Written_In_Kebab_Case()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse<SuilJustifyContent>("flex-start"), Is.EqualTo(SuilJustifyContent.FlexStart));
            Assert.That(Parse<SuilJustifyContent>("space-between"), Is.EqualTo(SuilJustifyContent.SpaceBetween));
            Assert.That(Parse<SuilFlexDirection>("column"), Is.EqualTo(SuilFlexDirection.Column));
            Assert.That(TryParse<SuilFlexDirection>("diagonal", out _), Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Custom_Parsers_Are_Named_By_Type()
    {
        var bold = Stream("bold");
        Assert.That(_suil.TryParseWith(typeof(SuilFontWeightParser), ref bold, out var weight), Is.True);
        Assert.That(weight, Is.EqualTo(FontWeight.Bold));

        var number = Stream("600");
        Assert.That(_suil.TryParseWith(typeof(SuilFontWeightParser), ref number, out weight), Is.True);
        Assert.That(weight, Is.EqualTo((FontWeight) 600));

        var even = Stream("0");
        Assert.That(_suil.TryParseWith(typeof(SuilNonNegativeNumberParser), ref even, out var zero), Is.True);
        Assert.That(zero, Is.EqualTo(0f));

        var negative = Stream("-1");
        Assert.That(_suil.TryParseWith(typeof(SuilNonNegativeNumberParser), ref negative, out _), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Let_Constants_Reach_The_Parsers()
    {
        var file = SuilExpander.Expand("""
            let gold = #B07D2B;
            let pad = 3px 10px;
            .a { color: <gold>; padding: <pad>; }
            """);
        var rule = (SuilRule) file.Items.Single();

        var color = new SuilValueStream(rule.Declarations[0].Value);
        var padding = new SuilValueStream(rule.Declarations[1].Value);

        Assert.Multiple(() =>
        {
            Assert.That(_suil.TryParse<Color>(ref color, out var c) && color.AtEnd, Is.True);
            Assert.That(c, Is.EqualTo(Color.FromHex("#B07D2B")));
            Assert.That(_suil.TryParse<Thickness>(ref padding, out var t) && padding.AtEnd, Is.True);
            Assert.That(t, Is.EqualTo(new Thickness(10f, 3f, 10f, 3f)));
        });
    }
}
