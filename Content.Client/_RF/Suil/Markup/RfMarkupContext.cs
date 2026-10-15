using Content.Client._RF.Suil.Text;
using Robust.Client.Graphics;

namespace Content.Client._RF.Suil.Markup;

/// <summary>
/// The text properties that markup tags are allowed to change for a run of text.
/// Immutable: tags derive a new value with <c>with</c> and push it onto <see cref="RfMarkupContext"/>.
/// </summary>
/// <param name="Family">The <c>font-family</c> list.</param>
/// <param name="Size">The font size in virtual pixels.</param>
/// <param name="Weight">The font weight.</param>
/// <param name="Slant">The font slant.</param>
/// <param name="Color">The text color.</param>
/// <param name="Decoration">Decoration lines added by tags on top of the control's own <c>text-decoration</c>.</param>
/// <param name="DecorationColor">Color of the decoration lines added by tags; <c>null</c> = the style's color.</param>
public sealed record RfInlineStyle(
    SuilFontFamily Family,
    float Size,
    FontWeight Weight,
    FontSlant Slant,
    Color Color,
    SuilTextDecorationLine Decoration = SuilTextDecorationLine.None,
    Color? DecorationColor = null)
{
    /// <summary>
    /// Creates the root inline style of a text from the control's computed style.
    /// </summary>
    public static RfInlineStyle FromTextStyle(RfTextStyle style)
        => new(style.FontFamily, style.FontSize, style.FontWeight, style.FontSlant, style.Color);
}

/// <summary>
/// A fork of the engine's <c>MarkupDrawingContext</c>. Instead of separate color and font stacks it keeps
/// one stack of <see cref="RfInlineStyle"/> and resolves the <see cref="Font"/> from it through
/// <see cref="ISuilFontManager"/>, so <c>[b]</c>, <c>[i]</c> and <c>[font]</c> use the faces registered for the
/// control's <c>font-family</c> instead of engine font prototypes.
/// </summary>
/// <remarks>
/// <para>
/// Tag handlers only push styles. Which styles belong to which tag is tracked here, so closing a tag
/// restores the context automatically, an unbalanced closing tag is ignored instead of throwing,
/// and a handler can't corrupt the stack by popping too much or too little.
/// </para>
/// <para>
/// The context is reused: <see cref="Reset"/> must be called before every layout or draw pass.
/// </para>
/// </remarks>
public sealed class RfMarkupContext(ISuilFontManager fonts)
{
    private readonly List<RfInlineStyle> _styles = new();
    private readonly List<(string Name, int Depth)> _openTags = new();
    private RfInlineStyle? _baseStyle;
    private Font? _baseFont;
    private Font? _font;

    /// <summary>
    /// The style that applies to the text at the current position.
    /// </summary>
    public RfInlineStyle Current => _styles[^1];

    /// <summary>
    /// The current text color.
    /// </summary>
    public Color Color => Current.Color;

    /// <summary>
    /// The font for the current style. Cached until the style changes.
    /// </summary>
    public Font Font => _font ??= ResolveFont();

    /// <summary>
    /// The number of styles on the stack (the root style counts).
    /// </summary>
    public int Depth => _styles.Count;

    /// <summary>
    /// Clears the context and sets the root style.
    /// </summary>
    /// <param name="baseStyle">The root style.</param>
    /// <param name="baseFont">
    /// The font of the root style. It is used as is while the current style equals the root one,
    /// so text without tags never goes through the font lookup.
    /// </param>
    public void Reset(RfInlineStyle baseStyle, Font baseFont)
    {
        _styles.Clear();
        _openTags.Clear();
        _styles.Add(baseStyle);
        _baseStyle = baseStyle;
        _baseFont = baseFont;
        _font = null;
    }

    /// <summary>
    /// Makes <paramref name="style"/> the current style until the tag that pushed it is closed.
    /// </summary>
    public void Push(RfInlineStyle style)
    {
        _styles.Add(style);
        _font = null;
    }

    /// <summary>
    /// Marks the start of a tag. Called by <see cref="RfMarkupTagManager"/> before the handler runs.
    /// </summary>
    /// <param name="name">The tag name.</param>
    public void BeginTag(string name) => _openTags.Add((name, _styles.Count));

    /// <summary>
    /// Marks the end of a tag and drops everything that tag (and any unclosed tags inside it) pushed.
    /// </summary>
    /// <param name="name">The tag name.</param>
    /// <returns><c>false</c> if no such tag is open, in which case nothing changes.</returns>
    public bool EndTag(string name)
    {
        for (var i = _openTags.Count - 1; i >= 0; i--)
        {
            if (!_openTags[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;

            var depth = Math.Max(1, _openTags[i].Depth);
            _openTags.RemoveRange(i, _openTags.Count - i);

            if (depth >= _styles.Count)
                return true;

            _styles.RemoveRange(depth, _styles.Count - depth);
            _font = null;
            return true;
        }

        return false;
    }

    private Font ResolveFont()
    {
        var style = Current;

        if (_baseFont != null && (ReferenceEquals(style, _baseStyle) || style == _baseStyle))
            return _baseFont;

        return fonts.Resolve(style.Family, style.Weight, style.Slant, style.Size);
    }
}
