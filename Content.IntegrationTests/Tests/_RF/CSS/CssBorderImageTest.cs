#nullable enable
using Content.Client._RF.CSS;
using Content.Client._RF.CSS.Parsers;
using Content.Client._RF.CSS.Streams;
using Content.Client._RF.Graphics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;

namespace Content.IntegrationTests.Tests._RF.CSS;

/// <summary>
/// <c>border-image-*</c> and the <c>border-image</c> shorthand.
/// </summary>
[TestFixture]
public sealed class CssBorderImageTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ICssManager _css = null!;

    private TestBorderImageControl Styled(string rules)
    {
        _css.ClearTestStylesheet();
        _css.LoadString(rules);
        _css.BuildTestStylesheet();

        var control = new TestBorderImageControl();
        control.AddCssStyleClass("a");
        _css.UpdateTestStyle(control);
        return control;
    }

    private static CssValueStream Stream(string css)
        => new(((CssDeclaration) CssParser.ParseListOfDeclarations($"x: {css};")[0]).Value);

    [TestCase("30", 30f, BorderImageEdgeKind.Number)]
    [TestCase("25%", 25f, BorderImageEdgeKind.Percent)]
    [RunOnSide(Side.Client)]
    public void Slice_With_One_Value_Applies_To_All_Sides(string css, float value, BorderImageEdgeKind kind)
    {
        var stream = Stream(css);

        Assert.That(BorderImageParsing.TryParseSlice(ref stream, out var slice), Is.True);
        Assert.Multiple(() =>
        {
            var edge = new BorderImageEdge(kind, value);
            Assert.That(slice.Edges, Is.EqualTo(new BorderImageEdges(edge)));
            Assert.That(slice.Fill, Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Slice_Takes_Four_Values_And_Fill_In_Any_Place()
    {
        var stream = Stream("fill 1 2 3 4");

        Assert.That(BorderImageParsing.TryParseSlice(ref stream, out var slice), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(slice.Fill, Is.True);
            Assert.That(slice.Edges.Top, Is.EqualTo(BorderImageEdge.Number(1)));
            Assert.That(slice.Edges.Right, Is.EqualTo(BorderImageEdge.Number(2)));
            Assert.That(slice.Edges.Bottom, Is.EqualTo(BorderImageEdge.Number(3)));
            Assert.That(slice.Edges.Left, Is.EqualTo(BorderImageEdge.Number(4)));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Slice_With_Two_Values_Is_Vertical_Then_Horizontal()
    {
        var stream = Stream("1 2");

        Assert.That(BorderImageParsing.TryParseSlice(ref stream, out var slice), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(slice.Edges.Top, Is.EqualTo(slice.Edges.Bottom));
            Assert.That(slice.Edges.Left, Is.EqualTo(slice.Edges.Right));
            Assert.That(slice.Edges.Top, Is.Not.EqualTo(slice.Edges.Left));
        });
    }

    [TestCase("-1")]
    [TestCase("10px")]
    [TestCase("fill")]
    [TestCase("1 2 3 4 5")]
    [RunOnSide(Side.Client)]
    public void Invalid_Slice_Is_Rejected(string css)
    {
        var stream = Stream(css);

        Assert.That(BorderImageParsing.TryParseSlice(ref stream, out _) && stream.AtEnd, Is.False);
    }

    [TestCase("stretch", BorderImageRepeatMode.Stretch, BorderImageRepeatMode.Stretch)]
    [TestCase("round", BorderImageRepeatMode.Round, BorderImageRepeatMode.Round)]
    [TestCase("repeat space", BorderImageRepeatMode.Repeat, BorderImageRepeatMode.Space)]
    [RunOnSide(Side.Client)]
    public void Repeat_Takes_One_Or_Two_Keywords(string css, BorderImageRepeatMode x, BorderImageRepeatMode y)
    {
        var stream = Stream(css);

        Assert.That(BorderImageParsing.TryParseRepeat(ref stream, out var repeat), Is.True);
        Assert.That(repeat, Is.EqualTo(new BorderImageRepeat(x, y)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Width_Takes_Numbers_Lengths_Percentages_And_Auto()
    {
        var stream = Stream("2 10px 50% auto");

        Assert.That(BorderImageParsing.TryParseWidth(ref stream, _css, out var width), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(width.Top, Is.EqualTo(BorderImageValue.FromNumber(2)));
            Assert.That(width.Right.Length, Is.EqualTo(LengthUnit.Px(10)));
            Assert.That(width.Bottom.Length.IsPercent, Is.True);
            Assert.That(width.Left, Is.EqualTo(BorderImageValue.Auto));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Outset_Does_Not_Take_Auto_Or_Percentages()
    {
        var auto = Stream("auto");
        var percent = Stream("10%");

        Assert.That(BorderImageParsing.TryParseOutset(ref auto, _css, out _), Is.False);
        Assert.That(BorderImageParsing.TryParseOutset(ref percent, _css, out _), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Longhands_Come_From_A_Stylesheet()
    {
        var control = Styled(".a { border-image-slice: 10 20 fill; border-image-width: 2px; border-image-outset: 1; border-image-repeat: round space; }");

        Assert.Multiple(() =>
        {
            Assert.That(control.Slice.Fill, Is.True);
            Assert.That(control.Slice.Edges.Top, Is.EqualTo(BorderImageEdge.Number(10)));
            Assert.That(control.Slice.Edges.Right, Is.EqualTo(BorderImageEdge.Number(20)));
            Assert.That(control.Width.Top.Length, Is.EqualTo(LengthUnit.Px(2)));
            Assert.That(control.Outset.Top, Is.EqualTo(BorderImageValue.FromNumber(1)));
            Assert.That(control.Repeat, Is.EqualTo(new BorderImageRepeat(BorderImageRepeatMode.Round, BorderImageRepeatMode.Space)));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Shorthand_Sets_Slice_Width_Outset_And_Repeat()
    {
        var control = Styled(".a { border-image: none 30 fill / 5px / 2 round; }");

        Assert.Multiple(() =>
        {
            Assert.That(control.Source, Is.Null);
            Assert.That(control.Slice.Fill, Is.True);
            Assert.That(control.Slice.Edges.Left, Is.EqualTo(BorderImageEdge.Number(30)));
            Assert.That(control.Width.Left.Length, Is.EqualTo(LengthUnit.Px(5)));
            Assert.That(control.Outset.Left, Is.EqualTo(BorderImageValue.FromNumber(2)));
            Assert.That(control.Repeat.X, Is.EqualTo(BorderImageRepeatMode.Round));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Shorthand_Resets_What_It_Does_Not_Mention()
    {
        var control = Styled(".a { border-image-width: 4px; border-image-repeat: round; } .a { border-image: 10; }");

        Assert.Multiple(() =>
        {
            Assert.That(control.Width, Is.EqualTo(BorderImageWidth.Default));
            Assert.That(control.Repeat, Is.EqualTo(BorderImageRepeat.Default));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Shorthand_Skips_Width_And_Takes_Outset()
    {
        var control = Styled(".a { border-image: 10 / / 3; }");

        Assert.That(control.Outset, Is.Not.EqualTo(BorderImageOutset.Default));
    }
}

public sealed class TestBorderImageControl : CssControl
{
    public Robust.Client.Graphics.Texture? Source => BorderImageSource;
    public BorderImageSlice Slice => BorderImageSlice;
    public BorderImageWidth Width => BorderImageWidth;
    public BorderImageOutset Outset => BorderImageOutset;
    public BorderImageRepeat Repeat => BorderImageRepeat;
}
