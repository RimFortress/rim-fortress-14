#nullable enable
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.Parsers;
using Content.IntegrationTests.Tests._RF.Suil;
using Content.Suil;
using Content.Client._RF.Graphics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// <c>border-image-*</c> and the <c>border-image</c> shorthand.
/// </summary>
[TestFixture]
public sealed class SuilBorderImageTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;

    private TestBorderImageControl Styled(string rules)
    {
        _suil.ClearTestStylesheet();
        _suil.LoadSuilString(rules);
        _suil.BuildTestStylesheet();

        var control = new TestBorderImageControl();
        control.AddSuilStyleClass("a");
        _suil.UpdateTestStyle(control);
        return control;
    }

    private static SuilValueStream Stream(string suil)
        => SuilTestValues.Stream(suil);

    [TestCase("-1")]
    [TestCase("-1px")]
    [TestCase("10em")]
    [TestCase("fill")]
    [TestCase("1 2 3 4 5")]
    [RunOnSide(Side.Client)]
    public void Invalid_Slice_Is_Rejected(string suil)
    {
        var stream = Stream(suil);

        Assert.That(BorderImageParsing.TryParseSlice(ref stream, out _) && stream.AtEnd, Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Width_Takes_Numbers_Lengths_Percentages_And_Auto()
    {
        var stream = Stream("2 10px 50% auto");

        Assert.That(BorderImageParsing.TryParseWidth(ref stream, _suil, out var width), Is.True);
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

        Assert.That(BorderImageParsing.TryParseOutset(ref auto, _suil, out _), Is.False);
        Assert.That(BorderImageParsing.TryParseOutset(ref percent, _suil, out _), Is.False);
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

public sealed class TestBorderImageControl : UiElement
{
    public Robust.Client.Graphics.Texture? Source => BorderImageSource;
    public BorderImageSlice Slice => BorderImageSlice;
    public BorderImageWidth Width => BorderImageWidth;
    public BorderImageOutset Outset => BorderImageOutset;
    public BorderImageRepeat Repeat => BorderImageRepeat;
}
