using System.Numerics;
using System.Text;
using Robust.Client.Graphics;

namespace Content.Client._RF.Suil.Text;

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
    public SuilFontFamily FontFamily { get; init; } = SuilFontFamily.SansSerif;

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
    /// SUIL <c>color</c>. Markup tags may still replace it for a part of the text.
    /// </summary>
    public Color Color { get; init; } = new(200, 200, 200);

    /// <summary>
    /// SUIL <c>letter-spacing</c>.
    /// </summary>
    public float LetterSpacing { get; init; }

    /// <summary>
    /// SUIL <c>word-spacing</c>.
    /// </summary>
    public float WordSpacing { get; init; }

    /// <summary>
    /// SUIL <c>line-height</c>.
    /// </summary>
    public SuilLineHeight LineHeight { get; init; } = SuilLineHeight.Normal;

    /// <summary>
    /// SUIL <c>text-align</c>.
    /// </summary>
    public SuilTextAlign TextAlign { get; init; } = SuilTextAlign.Start;

    /// <summary>
    /// SUIL <c>text-indent</c> (first line only, negative values are clamped to zero).
    /// </summary>
    public float TextIndent { get; init; }

    /// <summary>
    /// SUIL <c>text-transform</c>.
    /// </summary>
    public SuilTextTransform TextTransform { get; init; } = SuilTextTransform.None;

    /// <summary>
    /// SUIL <c>text-overflow</c>.
    /// </summary>
    public SuilTextOverflow TextOverflow { get; init; } = SuilTextOverflow.Clip;

    /// <summary>
    /// SUIL <c>white-space</c>.
    /// </summary>
    public SuilWhiteSpace WhiteSpace { get; init; } = SuilWhiteSpace.Normal;

    /// <summary>
    /// SUIL <c>word-break</c>.
    /// </summary>
    public SuilWordBreak WordBreak { get; init; } = SuilWordBreak.Normal;

    /// <summary>
    /// SUIL <c>overflow-wrap</c>.
    /// </summary>
    public SuilOverflowWrap OverflowWrap { get; init; } = SuilOverflowWrap.Normal;

    /// <summary>
    /// SUIL <c>line-clamp</c>: the maximum number of lines, or <c>null</c> for no limit.
    /// If the text gets truncated an ellipsis is drawn at the end of the last line.
    /// </summary>
    public int? LineClamp { get; init; }

    /// <summary>
    /// SUIL <c>text-decoration-line</c>.
    /// </summary>
    public SuilTextDecorationLine DecorationLine { get; init; } = SuilTextDecorationLine.None;

    /// <summary>
    /// SUIL <c>text-decoration-style</c>.
    /// </summary>
    public SuilTextDecorationStyle DecorationStyle { get; init; } = SuilTextDecorationStyle.Solid;

    /// <summary>
    /// SUIL <c>text-decoration-color</c>; <c>null</c> = <c>currentcolor</c>.
    /// </summary>
    public Color? DecorationColor { get; init; }

    /// <summary>
    /// SUIL <c>text-decoration-thickness</c>; <c>null</c> = <c>auto</c>.
    /// </summary>
    public float? DecorationThickness { get; init; }

    /// <summary>
    /// SUIL <c>text-underline-offset</c>; <c>null</c> = <c>auto</c>.
    /// </summary>
    public float? UnderlineOffset { get; init; }

    /// <summary>
    /// SUIL <c>text-shadow</c> layers. The first layer is painted on top, as in SUIL.
    /// </summary>
    public IReadOnlyList<RfTextShadow> Shadows { get; init; } = [];

    /// <summary>
    /// Text outline (RF extension, e.g. <c>font-outline</c> or <c>-webkit-text-stroke</c>).
    /// </summary>
    public TextOutline? Outline { get; init; }

    /// <summary>
    /// Does this style allow soft wrapping?
    /// </summary>
    public bool Wraps => WhiteSpace is SuilWhiteSpace.Normal
        or SuilWhiteSpace.PreWrap
        or SuilWhiteSpace.PreLine
        or SuilWhiteSpace.BreakSpaces;
}

/// <summary>
/// A resolved <c>text-shadow</c> layer, in virtual pixels.
/// </summary>
/// <param name="Offset">Offset.</param>
/// <param name="Blur">Blur radius. Stored, but not rendered yet (shadows are sharp).</param>
/// <param name="Color">Shadow color; <c>null</c> = <c>currentcolor</c>.</param>
public readonly record struct RfTextShadow(Vector2 Offset, float Blur, Color? Color);

/// <summary>
/// Stateful per-pass filter that applies <c>white-space</c> collapsing and <c>text-transform</c> to the text
/// returned by markup nodes. It must be created anew for every layout/draw pass, and fed with exactly the same
/// text in the same order, so that rune indices match between <see cref="RfRichTextEntry.Update"/> and drawing.
/// </summary>
public struct RfTextFilter
{
    private readonly SuilWhiteSpace _whiteSpace;
    private readonly SuilTextTransform _transform;
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

        var keepsWhitespace = _whiteSpace is SuilWhiteSpace.Pre or SuilWhiteSpace.PreWrap or SuilWhiteSpace.BreakSpaces;

        // Fast path: nothing to collapse, nothing to transform.
        if (keepsWhitespace
            && _transform == SuilTextTransform.None
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
            else if (_whiteSpace == SuilWhiteSpace.PreLine && isNewline)
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
            case SuilTextTransform.Uppercase:
                return Rune.ToUpperInvariant(r);
            case SuilTextTransform.Lowercase:
                return Rune.ToLowerInvariant(r);
            case SuilTextTransform.Capitalize:
                var result = _atWordStart && Rune.IsLetter(r) ? Rune.ToUpperInvariant(r) : r;
                _atWordStart = Rune.IsWhiteSpace(r) || r.Value == '-';
                return result;
            default:
                return r;
        }
    }
}
