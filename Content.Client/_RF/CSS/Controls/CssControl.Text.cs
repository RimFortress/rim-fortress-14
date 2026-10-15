using System.Linq;
using Content.Client._RF.CSS.Parsers;
using Content.Client._RF.CSS.Streams;
using Content.Client._RF.CSS.Text;
using Content.Shared._RF.Utilities;
using Robust.Client.Graphics;
using Content.Client._RF.CSS.Markup;
using Content.Suil;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
    private const string TextDecorationShorthandName = "text-decoration";
    private const string FontShorthandName = "font";

    /// <summary>
    /// The default <c>font-size</c> in virtual pixels.
    /// </summary>
    public const float DefaultFontSize = 12f;

    [Dependency] private IRfMarkupTagManager _tagManager = null!;
    [Dependency] private ICssFontManager _fonts = null!;

    private RfRichTextEntry? _textEntry;
    private RfTextStyle? _textStyle;

    #region Content

    /// <summary>
    /// The markup text displayed by this control, or <c>null</c> if it displays no text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Setting this converts the string with <see cref="FormattedMessage.FromMarkupPermissive(string)"/>;
    /// the original string is not kept, so a get may return a different, equivalent markup.
    /// All markup tags are allowed, so never pass untrusted input here.
    /// </para>
    /// <para>
    /// A control that displays text must not have other children: its children are the controls created
    /// by markup tags, and they are laid out by the text.
    /// </para>
    /// </remarks>
    public string? Text
    {
        get => _textEntry?.Message.ToMarkup();
        set
        {
            if (value == null)
                ClearText();
            else
                SetMessage(FormattedMessage.FromMarkupPermissive(value), null);
        }
    }

    /// <summary>
    /// Displays a formatted message, allowing only a small set of safe formatting tags.
    /// </summary>
    public void SetMessage(FormattedMessage message) => SetMessage(message, RfRichTextEntry.DefaultTags);

    /// <summary>
    /// Displays a formatted message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="tagsAllowed">The allowed markup tags; <c>null</c> allows all of them.</param>
    public void SetMessage(FormattedMessage message, Type[]? tagsAllowed)
    {
        _textEntry?.RemoveControls();
        _textEntry = new RfRichTextEntry(message, this, _tagManager, tagsAllowed);
        InvalidateMeasure();
    }

    /// <summary>
    /// Displays plain text: markup in <paramref name="text"/> is not interpreted.
    /// </summary>
    public void SetPlainText(string text)
    {
        var message = new FormattedMessage();
        message.AddText(text);
        SetMessage(message, RfRichTextEntry.DefaultTags);
    }

    /// <summary>
    /// Removes the displayed text and the controls created by its markup tags.
    /// </summary>
    public void ClearText()
    {
        _textEntry?.RemoveControls();
        _textEntry = null;
        InvalidateMeasure();
    }

    /// <summary>
    /// Does this control display text?
    /// </summary>
    public bool HasText => _textEntry != null;

    #endregion

    #region Font

    /// <summary>
    /// CSS <c>font-family</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    [SuilShorthand(FontShorthandName, nameof(ParseFontShorthand))]
    protected CssFontFamily FontFamily
    {
        get;
        set
        {
            field = value;
            OnFontChanged();
        }
    } = CssFontFamily.SansSerif;

    /// <summary>
    /// CSS <c>font-size</c>. Relative units refer to the parent's font size; the default <c>1em</c> inherits it.
    /// </summary>
    [SuilProperty(parser: typeof(CssFontSizeParser), Inherited = true)]
    [SuilShorthand(FontShorthandName)]
    protected LengthUnit FontSize
    {
        get;
        set
        {
            field = value;
            OnFontChanged();
        }
    } = LengthUnit.Em(1f);

    /// <summary>
    /// CSS <c>font-weight</c> (<c>normal</c>, <c>bold</c> or a number).
    /// </summary>
    [SuilProperty(parser: typeof(CssFontWeightParser), Inherited = true)]
    [SuilShorthand(FontShorthandName)]
    protected FontWeight FontWeight
    {
        get;
        set
        {
            field = value;
            OnFontChanged();
        }
    } = FontWeight.Normal;

    /// <summary>
    /// CSS <c>font-style</c> (<c>normal</c>, <c>italic</c>, <c>oblique</c>).
    /// </summary>
    [SuilProperty(Inherited = true)]
    [SuilShorthand(FontShorthandName)]
    protected FontSlant FontStyle
    {
        get;
        set
        {
            field = value;
            OnFontChanged();
        }
    } = FontSlant.Normal;

    #endregion

    #region Text

    /// <summary>
    /// CSS <c>color</c>.
    /// </summary>
    [SuilProperty("color", Inherited = true)]
    protected Color TextColor
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = new(200, 200, 200);

    /// <summary>
    /// CSS <c>letter-spacing</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    protected LengthUnit LetterSpacing
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    }

    /// <summary>
    /// CSS <c>word-spacing</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    protected LengthUnit WordSpacing
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    }

    /// <summary>
    /// CSS <c>line-height</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    [SuilShorthand(FontShorthandName)]
    protected CssLineHeight LineHeight
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = CssLineHeight.Normal;

    /// <summary>
    /// CSS <c>text-align</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    protected CssTextAlign TextAlign
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = CssTextAlign.Start;

    /// <summary>
    /// CSS <c>text-indent</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    protected LengthUnit TextIndent
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    }

    /// <summary>
    /// CSS <c>text-transform</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    protected CssTextTransform TextTransform
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = CssTextTransform.None;

    /// <summary>
    /// CSS <c>text-overflow</c>. The ellipsis is only visible if the control clips its content.
    /// </summary>
    [SuilProperty]
    protected CssTextOverflow TextOverflow
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = CssTextOverflow.Clip;

    /// <summary>
    /// CSS <c>white-space</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    protected CssWhiteSpace WhiteSpace
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = CssWhiteSpace.Normal;

    /// <summary>
    /// CSS <c>word-break</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    protected CssWordBreak WordBreak
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = CssWordBreak.Normal;

    /// <summary>
    /// CSS <c>overflow-wrap</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    protected CssOverflowWrap OverflowWrap
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = CssOverflowWrap.Normal;

    /// <summary>
    /// CSS <c>line-clamp</c>: the maximum number of lines, <c>0</c> (<c>none</c>) for no limit.
    /// </summary>
    [SuilProperty(parser: typeof(CssLineClampParser))]
    protected int LineClamp
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    }

    #endregion

    #region Decoration, shadow, outline

    /// <summary>
    /// CSS <c>text-decoration-line</c>.
    /// </summary>
    [SuilProperty(parser: typeof(CssTextDecorationLineParser))]
    [SuilShorthand(TextDecorationShorthandName, nameof(ParseTextDecorationShorthand))]
    protected CssTextDecorationLine TextDecorationLine
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = CssTextDecorationLine.None;

    /// <summary>
    /// CSS <c>text-decoration-style</c>.
    /// </summary>
    [SuilProperty]
    [SuilShorthand(TextDecorationShorthandName)]
    protected CssTextDecorationStyle TextDecorationStyle
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = CssTextDecorationStyle.Solid;

    /// <summary>
    /// CSS <c>text-decoration-color</c>; <c>null</c> means <c>currentcolor</c>.
    /// </summary>
    [SuilProperty]
    [SuilShorthand(TextDecorationShorthandName)]
    protected Color? TextDecorationColor
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    }

    /// <summary>
    /// CSS <c>text-decoration-thickness</c>; <c>null</c> means <c>auto</c>.
    /// </summary>
    [SuilProperty]
    [SuilShorthand(TextDecorationShorthandName)]
    protected LengthUnit? TextDecorationThickness
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    }

    /// <summary>
    /// CSS <c>text-underline-offset</c>; <c>null</c> means <c>auto</c>.
    /// </summary>
    [SuilProperty]
    protected LengthUnit? TextUnderlineOffset
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    }

    /// <summary>
    /// CSS <c>text-shadow</c>.
    /// </summary>
    [SuilProperty(Inherited = true)]
    protected CssTextShadow[] TextShadow
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    } = [];

    /// <summary>
    /// Text outline width (RF extension, <c>-rf-text-outline-width</c>); <c>0</c> disables the outline.
    /// </summary>
    [SuilProperty("-rf-text-outline-width", Inherited = true)]
    protected LengthUnit TextOutlineWidth
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    }

    /// <summary>
    /// Text outline color (RF extension, <c>-rf-text-outline-color</c>); <c>null</c> means black.
    /// </summary>
    [SuilProperty("-rf-text-outline-color", Inherited = true)]
    protected Color? TextOutlineColor
    {
        get;
        set
        {
            field = value;
            InvalidateTextStyle();
        }
    }

    #endregion

    #region Layout and drawing

    internal void RefreshFonts() => InvalidateTextStyle();

    /// <summary>
    /// Drops the cached <see cref="RfTextStyle"/> and schedules a new layout.
    /// </summary>
    private void InvalidateTextStyle()
    {
        _textStyle = null;
        OnTextStyleChanged();

        // The entry keeps the style of its last layout, so even a pure color change needs a new Update.
        if (_textEntry != null)
            InvalidateMeasure();
    }

    private RfTextStyle BuildTextStyle()
    {
        if (_textStyle is { } cached)
            return cached;

        var context = LengthContext;
        var outlineWidth = ToPx(TextOutlineWidth);

        return _textStyle = new RfTextStyle
        {
            Font = _fonts.Resolve(FontFamily, FontWeight, FontStyle, context.FontSize),
            FontFamily = FontFamily,
            FontSize = context.FontSize,
            FontWeight = FontWeight,
            FontSlant = FontStyle,
            Color = TextColor,
            LetterSpacing = ToPx(LetterSpacing),
            WordSpacing = ToPx(WordSpacing),
            LineHeight = LineHeight.Resolve(context),
            TextAlign = TextAlign,
            TextIndent = ToPx(TextIndent),
            TextTransform = TextTransform,
            TextOverflow = TextOverflow,
            WhiteSpace = WhiteSpace,
            WordBreak = WordBreak,
            OverflowWrap = OverflowWrap,
            LineClamp = LineClamp > 0 ? LineClamp : null,
            DecorationLine = TextDecorationLine,
            DecorationStyle = TextDecorationStyle,
            DecorationColor = TextDecorationColor,
            DecorationThickness = TextDecorationThickness is { } thickness ? ToPx(thickness) : null,
            UnderlineOffset = TextUnderlineOffset is { } offset ? ToPx(offset) : null,
            Shadows = TextShadow.Select(ResolveShadow).ToArray(),
            Outline = outlineWidth > 0f ? new TextOutline(outlineWidth, TextOutlineColor ?? Color.Black) : null,
        };
    }

    private RfTextShadow ResolveShadow(CssTextShadow shadow)
        => new(ToPx(shadow.Offset), ToPx(shadow.Blur), shadow.Color);

    /// <summary>
    /// The space between the control's outer edge and its content (text or children), in virtual pixels (padding plus borders).
    /// </summary>
    protected Thickness ContentInset
    {
        get
        {
            var border = Background?.Border;

            return new Thickness(
                MathF.Max(0f, ToPx(PaddingLeft)) + (border?.Left.Width ?? 0f),
                MathF.Max(0f, ToPx(PaddingTop)) + (border?.Top.Width ?? 0f),
                MathF.Max(0f, ToPx(PaddingRight)) + (border?.Right.Width ?? 0f),
                MathF.Max(0f, ToPx(PaddingBottom)) + (border?.Bottom.Width ?? 0f));
        }
    }

    /// <summary>
    /// Whether the text is centered vertically in the content box when the control is taller than the text.
    /// </summary>
    protected virtual bool CenterTextVertically => false;

    /// <summary>
    /// Draws the control's text inside its padding and borders. Called after <see cref="DrawBackground"/>.
    /// </summary>
    protected virtual void DrawText(DrawingHandleScreen handle)
    {
        if (_textEntry == null)
            return;

        var inset = ContentInset;
        var area = PixelSizeBox;
        var left = area.Left + inset.Left * UIScale;
        var top = area.Top + inset.Top * UIScale;

        // The control can be taller than its text (stretched by a flex container): the text of a button stays
        // in the middle instead of sticking to the top edge.
        if (CenterTextVertically)
        {
            var free = area.Height - (inset.Top + inset.Bottom) * UIScale - _textEntry.Height;

            if (free > 0f)
                top += free / 2f;
        }

        // The control can be smaller than its padding and borders (e.g. squeezed by a container).
        var box = new UIBox2(
            left,
            top,
            MathF.Max(left, area.Right - inset.Right * UIScale),
            MathF.Max(top, area.Bottom - inset.Bottom * UIScale));

        _textEntry.Draw(_tagManager, handle, box, UIScale);
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Custom parser for the <c>font</c> shorthand:
    /// <c>[ &lt;style&gt; || &lt;weight&gt; ]* &lt;size&gt; [ / &lt;line-height&gt; ]? &lt;family&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Size and family are required, as in CSS. Every longhand the shorthand covers is written explicitly
    /// (style, weight and line-height get their initial values when omitted), because the generic fallback
    /// would use <c>default(ClrType)</c>, which is not a valid weight. <c>small-caps</c> is accepted and ignored.
    /// Not supported: system fonts (<c>font: menu</c>), keyword and percentage sizes, <c>font-stretch</c>.
    /// </remarks>
    private static bool ParseFontShorthand(
        ref CssValueStream stream,
        ICssManager css,
        Dictionary<string, object?> values)
    {
        var style = FontSlant.Normal;
        var weight = FontWeight.Normal;
        var lineHeight = CssLineHeight.Normal;

        // Optional style / variant / weight, in any order.
        while (!stream.AtEnd)
        {
            if (stream.Peek() is IdentToken { Value: var ident })
            {
                if (ident.Equals("normal", StringComparison.OrdinalIgnoreCase)
                    || ident.Equals("small-caps", StringComparison.OrdinalIgnoreCase))
                {
                    stream.ConsumeNext();
                    continue;
                }

                if (ident.Equals("italic", StringComparison.OrdinalIgnoreCase))
                {
                    stream.ConsumeNext();
                    style = FontSlant.Italic;
                    continue;
                }

                if (ident.Equals("oblique", StringComparison.OrdinalIgnoreCase))
                {
                    stream.ConsumeNext();
                    style = FontSlant.Oblique;
                    continue;
                }

                if (ident.Equals("bold", StringComparison.OrdinalIgnoreCase))
                {
                    stream.ConsumeNext();
                    weight = FontWeight.Bold;
                    continue;
                }

                break;
            }

            // A unitless number before the size is a weight; "0" and lengths are sizes.
            if (stream.Peek() is NumberToken { Value: >= 1 and <= 1000 } number)
            {
                stream.ConsumeNext();
                weight = (FontWeight) (ushort) number.Value;
                continue;
            }

            break;
        }

        if (!CssFontSizeParser.TryParseSize(ref stream, css, out var size))
            return false;

        if (stream.Peek() is DelimToken { CodePoint: '/' })
        {
            stream.ConsumeNext();

            if (!css.TryParseValue(ref stream, out lineHeight))
                return false;
        }

        if (!css.TryParseValue<CssFontFamily>(ref stream, out var family) || family == null || !stream.AtEnd)
            return false;

        values["font-style"] = style;
        values["font-weight"] = weight;
        values["font-size"] = size;
        values["line-height"] = lineHeight;
        values["font-family"] = family;
        return true;
    }

    /// <summary>
    /// Custom parser for the <c>text-decoration</c> shorthand:
    /// <c>&lt;line&gt; || &lt;style&gt; || &lt;color&gt; || &lt;thickness&gt;</c> in any order.
    /// Omitted components are reset to their defaults by the caller.
    /// </summary>
    private static bool ParseTextDecorationShorthand(
        ref CssValueStream stream,
        ICssManager css,
        Dictionary<string, object?> values)
    {
        var line = CssTextDecorationLine.None;
        var style = CssTextDecorationStyle.Solid;
        Color? color = null;
        LengthUnit? thickness = null;

        while (!stream.AtEnd)
        {
            if (stream.Peek() is IdentToken { Value: var ident })
            {
                if (CssTextDecorationLineParser.TryParseKeyword(ident, out _))
                {
                    if (!CssTextDecorationLineParser.TryParseLine(ref stream, out var parsedLine))
                        return false;

                    line |= parsedLine;
                    continue;
                }

                if (Enum.TryParse<CssTextDecorationStyle>(RfCaseConversion.KebabToPascal(ident), true, out var parsedStyle))
                {
                    stream.ConsumeNext();
                    style = parsedStyle;
                    continue;
                }
            }

            var mark = stream.Mark();

            if (css.TryParseValue<LengthUnit>(ref stream, out var width))
            {
                thickness = width;
                continue;
            }

            stream.Reset(mark);

            if (css.TryParseValue<Color>(ref stream, out var parsedColor))
            {
                color = parsedColor;
                continue;
            }

            return false;
        }

        values["text-decoration-line"] = line;
        values["text-decoration-style"] = style;
        values["text-decoration-color"] = color;
        values["text-decoration-thickness"] = thickness;
        return true;
    }

    #endregion
}
