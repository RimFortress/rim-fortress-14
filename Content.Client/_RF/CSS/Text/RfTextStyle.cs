using System.Text;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS.Text;

/// <summary>
/// The computed text style of a control: everything <see cref="RfRichTextEntry"/> needs to lay out and draw text.
/// </summary>
/// <remarks>
/// All lengths are in virtual pixels (they are multiplied by the UI scale inside the entry).
/// The entry takes the style on every <see cref="RfRichTextEntry.Update"/>, so changing a style property
/// never requires recreating the entry (and with it the controls created by markup tags).
/// </remarks>
public sealed class RfTextStyle
{
    /// <summary>
    /// Resolved font for <c>font-family</c>, <c>font-size</c>, <c>font-weight</c> and <c>font-style</c>.
    /// Markup tags may still replace it for a part of the text.
    /// </summary>
    public required Font Font { get; init; }

    /// <summary>
    /// The <c>font-family</c> list <see cref="Font"/> was resolved from. Markup tags derive their fonts from it.
    /// </summary>
    public CssFontFamily FontFamily { get; init; } = CssFontFamily.SansSerif;

    /// <summary>
    /// The <c>font-size</c> <see cref="Font"/> was resolved with, in virtual pixels.
    /// </summary>
    public float FontSize { get; init; } = 12f;

    /// <summary>
    /// The <c>font-weight</c> <see cref="Font"/> was resolved with.
    /// </summary>
    public FontWeight FontWeight { get; init; } = FontWeight.Normal;

    /// <summary>
    /// The <c>font-style</c> <see cref="Font"/> was resolved with.
    /// </summary>
    public FontSlant FontSlant { get; init; } = FontSlant.Normal;

    /// <summary>
    /// CSS <c>color</c>. Markup tags may still replace it for a part of the text.
    /// </summary>
    public Color Color { get; init; } = new(200, 200, 200);

    /// <summary>
    /// CSS <c>letter-spacing</c>.
    /// </summary>
    public float LetterSpacing { get; init; }

    /// <summary>
    /// CSS <c>word-spacing</c>.
    /// </summary>
    public float WordSpacing { get; init; }

    /// <summary>
    /// CSS <c>line-height</c>.
    /// </summary>
    public CssLineHeight LineHeight { get; init; } = CssLineHeight.Normal;

    /// <summary>
    /// CSS <c>text-align</c>.
    /// </summary>
    public CssTextAlign TextAlign { get; init; } = CssTextAlign.Start;

    /// <summary>
    /// CSS <c>text-indent</c> (first line only, negative values are clamped to zero).
    /// </summary>
    public float TextIndent { get; init; }

    /// <summary>
    /// CSS <c>text-transform</c>.
    /// </summary>
    public CssTextTransform TextTransform { get; init; } = CssTextTransform.None;

    /// <summary>
    /// CSS <c>text-overflow</c>.
    /// </summary>
    public CssTextOverflow TextOverflow { get; init; } = CssTextOverflow.Clip;

    /// <summary>
    /// CSS <c>white-space</c>.
    /// </summary>
    public CssWhiteSpace WhiteSpace { get; init; } = CssWhiteSpace.Normal;

    /// <summary>
    /// CSS <c>word-break</c>.
    /// </summary>
    public CssWordBreak WordBreak { get; init; } = CssWordBreak.Normal;

    /// <summary>
    /// CSS <c>overflow-wrap</c>.
    /// </summary>
    public CssOverflowWrap OverflowWrap { get; init; } = CssOverflowWrap.Normal;

    /// <summary>
    /// CSS <c>line-clamp</c>: the maximum number of lines, or <c>null</c> for no limit.
    /// If the text gets truncated an ellipsis is drawn at the end of the last line.
    /// </summary>
    public int? LineClamp { get; init; }

    /// <summary>
    /// CSS <c>text-decoration-line</c>.
    /// </summary>
    public CssTextDecorationLine DecorationLine { get; init; } = CssTextDecorationLine.None;

    /// <summary>
    /// CSS <c>text-decoration-style</c>.
    /// </summary>
    public CssTextDecorationStyle DecorationStyle { get; init; } = CssTextDecorationStyle.Solid;

    /// <summary>
    /// CSS <c>text-decoration-color</c>; <c>null</c> = <c>currentcolor</c>.
    /// </summary>
    public Color? DecorationColor { get; init; }

    /// <summary>
    /// CSS <c>text-decoration-thickness</c>; <c>null</c> = <c>auto</c>.
    /// </summary>
    public float? DecorationThickness { get; init; }

    /// <summary>
    /// CSS <c>text-underline-offset</c>; <c>null</c> = <c>auto</c>.
    /// </summary>
    public float? UnderlineOffset { get; init; }

    /// <summary>
    /// CSS <c>text-shadow</c> layers. The first layer is painted on top, as in CSS.
    /// </summary>
    public IReadOnlyList<CssTextShadow> Shadows { get; init; } = [];

    /// <summary>
    /// Text outline (RF extension, e.g. <c>-rf-text-outline</c> or <c>-webkit-text-stroke</c>).
    /// </summary>
    public TextOutline? Outline { get; init; }

    /// <summary>
    /// Does this style allow soft wrapping?
    /// </summary>
    public bool Wraps => WhiteSpace is CssWhiteSpace.Normal
        or CssWhiteSpace.PreWrap
        or CssWhiteSpace.PreLine
        or CssWhiteSpace.BreakSpaces;
}

/// <summary>
/// Stateful per-pass filter that applies <c>white-space</c> collapsing and <c>text-transform</c> to the text
/// returned by markup nodes. It must be created anew for every layout/draw pass, and fed with exactly the same
/// text in the same order, so that rune indices match between <see cref="RfRichTextEntry.Update"/> and drawing.
/// </summary>
public struct RfTextFilter
{
    private readonly CssWhiteSpace _whiteSpace;
    private readonly CssTextTransform _transform;
    private bool _previousWasSpace;
    private bool _atWordStart;

    /// <summary>
    /// Creates a filter for the given style.
    /// </summary>
    public RfTextFilter(RfTextStyle style)
    {
        _whiteSpace = style.WhiteSpace;
        _transform = style.TextTransform;
        _previousWasSpace = false;
        _atWordStart = true;
    }

    /// <summary>
    /// Applies the filter to the next chunk of text.
    /// </summary>
    public string Apply(string text)
    {
        if (text.Length == 0)
            return text;

        var keepsWhitespace = _whiteSpace is CssWhiteSpace.Pre or CssWhiteSpace.PreWrap or CssWhiteSpace.BreakSpaces;

        // Fast path: nothing to collapse, nothing to transform.
        if (keepsWhitespace
            && _transform == CssTextTransform.None
            && text.AsSpan().IndexOfAny('\t', '\r') < 0)
            return text;

        var sb = new StringBuilder(text.Length);

        foreach (var rune in text.EnumerateRunes())
        {
            var r = rune;
            var isSpace = r.Value is ' ' or '\t' or '\r';
            var isNewline = r.Value == '\n';

            if (keepsWhitespace)
            {
                if (r.Value == '\r')
                    continue;

                if (r.Value == '\t')
                    r = new Rune(' ');
            }
            else if (_whiteSpace == CssWhiteSpace.PreLine && isNewline)
            {
                _previousWasSpace = false;
            }
            else if (isSpace || isNewline)
            {
                if (_previousWasSpace)
                    continue;

                _previousWasSpace = true;
                r = new Rune(' ');
            }
            else
            {
                _previousWasSpace = false;
            }

            r = ApplyTransform(r);
            sb.Append(r.ToString());
        }

        return sb.ToString();
    }

    private Rune ApplyTransform(Rune r)
    {
        switch (_transform)
        {
            case CssTextTransform.Uppercase:
                return Rune.ToUpperInvariant(r);
            case CssTextTransform.Lowercase:
                return Rune.ToLowerInvariant(r);
            case CssTextTransform.Capitalize:
                var result = _atWordStart && Rune.IsLetter(r) ? Rune.ToUpperInvariant(r) : r;
                _atWordStart = Rune.IsWhiteSpace(r) || r.Value == '-';
                return result;
            default:
                return r;
        }
    }
}
