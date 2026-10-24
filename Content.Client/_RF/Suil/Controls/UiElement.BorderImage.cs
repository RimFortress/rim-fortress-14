using Content.Client._RF.Suil.Parsers;
using Content.Suil;
using Content.Client._RF.Graphics;
using Robust.Client.Graphics;

namespace Content.Client._RF.Suil;

public partial class UiElement
{
    private const string BorderImageShorthandName = "border-image";

    /// <summary>
    /// Sets the image to draw instead of the border-style of the border. The widths of the border are still
    /// set by <c>border-width</c> (and the border-style must not be <c>none</c>).
    /// </summary>
    [SuilProperty]
    [SuilShorthand(BorderImageShorthandName, nameof(ParseBorderImageShorthand))]
    protected Texture? BorderImageSource
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    /// <summary>
    /// Cuts the border image into nine regions: four corners, four edges and the middle.
    /// A number is in image pixels, a percentage is of the size of the image.
    /// </summary>
    [SuilProperty]
    [SuilShorthand(BorderImageShorthandName)]
    protected BorderImageSlice BorderImageSlice
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = BorderImageSlice.Default;

    /// <summary>
    /// Sets the width of the area the border image is drawn in. A number is a multiple of the border width.
    /// </summary>
    [SuilProperty]
    [SuilShorthand(BorderImageShorthandName)]
    protected BorderImageWidth BorderImageWidth
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = BorderImageWidth.Default;

    /// <summary>
    /// Sets how far the border image extends beyond the border box. A number is a multiple of the border width.
    /// </summary>
    [SuilProperty]
    [SuilShorthand(BorderImageShorthandName)]
    protected BorderImageOutset BorderImageOutset
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = BorderImageOutset.Default;

    /// <summary>
    /// Sets how the edges of the border image are stretched or tiled between the corners.
    /// </summary>
    [SuilProperty]
    [SuilShorthand(BorderImageShorthandName)]
    protected BorderImageRepeat BorderImageRepeat
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = BorderImageRepeat.Default;

    private StyleBoxBorderImage? BuildBorderImage(BorderSide top, BorderSide right, BorderSide bottom, BorderSide left)
    {
        if (BorderImageSource is not { } texture)
            return null;

        var slice = BorderImageSlice;
        var width = BorderImageWidth;
        var outset = BorderImageOutset;
        var repeat = BorderImageRepeat;

        return new StyleBoxBorderImage(ResolveBorderRadius(BorderRadius), top, right, bottom, left)
        {
            Texture = texture,
            Slice = slice.Edges,
            Fill = slice.Fill,
            Width = new BorderImageEdges(
                ResolveBorderImageValue(width.Top),
                ResolveBorderImageValue(width.Right),
                ResolveBorderImageValue(width.Bottom),
                ResolveBorderImageValue(width.Left)),
            Outset = new BorderImageEdges(
                ResolveBorderImageValue(outset.Top),
                ResolveBorderImageValue(outset.Right),
                ResolveBorderImageValue(outset.Bottom),
                ResolveBorderImageValue(outset.Left)),
            RepeatX = repeat.X,
            RepeatY = repeat.Y,
        };
    }

    // Font-relative and viewport units are resolved here; a percentage needs the size of the border image area.
    private BorderImageEdge ResolveBorderImageValue(BorderImageValue value) => value.Kind switch
    {
        BorderImageValueKind.Number => BorderImageEdge.Number(value.Number),
        BorderImageValueKind.Auto => BorderImageEdge.Auto,
        _ when value.Length.IsPercent => BorderImageEdge.Percent(value.Length.Value),
        _ => new BorderImageEdge(BorderImageEdgeKind.Length, MathF.Max(0f, ToPx(value.Length))),
    };

    #region Parsing

    /// <summary>
    /// "border-image" shorthand:
    /// <c>&lt;source&gt; || &lt;slice&gt; [ / &lt;width&gt;? [ / &lt;outset&gt; ]? ]? || &lt;repeat&gt;</c>.
    /// Every longhand is written with its initial value up front.
    /// </summary>
    private static bool ParseBorderImageShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
    {
        Texture? source = null;
        var slice = BorderImageSlice.Default;
        var width = BorderImageWidth.Default;
        var outset = BorderImageOutset.Default;
        var repeat = BorderImageRepeat.Default;

        var sawSource = false;
        var sawSlice = false;
        var sawRepeat = false;

        while (!stream.AtEnd)
        {
            switch (stream.Peek())
            {
                case SuilIdent { Value: "none" } when !sawSource:
                    stream.ConsumeNext();
                    sawSource = true;
                    continue;
                case SuilFunction { Name: "url" or "texture" } when !sawSource:
                    if (!parsers.TryParse<Texture>(ref stream, out source))
                        return false;

                    sawSource = true;
                    continue;
                case SuilIdent { Value: "stretch" or "repeat" or "round" or "space" } when !sawRepeat:
                    if (!BorderImageParsing.TryParseRepeat(ref stream, out repeat))
                        return false;

                    sawRepeat = true;
                    continue;
                case SuilNumber or SuilPercentage or SuilIdent { Value: "fill" } when !sawSlice:
                    if (!BorderImageParsing.TryParseSlice(ref stream, out slice))
                        return false;

                    sawSlice = true;

                    if (stream.Peek() is not SuilDelim { CodePoint: '/' })
                        continue;

                    stream.ConsumeNext();

                    var hasWidth = BorderImageParsing.TryParseWidth(ref stream, parsers, out width);

                    if (stream.Peek() is SuilDelim { CodePoint: '/' })
                    {
                        stream.ConsumeNext();

                        if (!BorderImageParsing.TryParseOutset(ref stream, parsers, out outset))
                            return false;
                    }
                    else if (!hasWidth)
                    {
                        return false; // a lone "/"
                    }

                    continue;
            }

            return false;
        }

        values["border-image-source"] = source;
        values["border-image-slice"] = slice;
        values["border-image-width"] = width;
        values["border-image-outset"] = outset;
        values["border-image-repeat"] = repeat;
        return true;
    }

    #endregion
}
