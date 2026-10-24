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

    // ---- Text ----

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
