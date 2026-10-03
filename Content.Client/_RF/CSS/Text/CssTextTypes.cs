using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS.Text;

// NOTE: enum member names are chosen so that the generic kebab-case -> PascalCase enum parser
// in CssManager.TryParseEnum works out of the box ("pre-wrap" -> PreWrap, "break-all" -> BreakAll, ...).

/// <summary>
/// CSS <c>text-align</c>. <c>Start</c>/<c>End</c> are treated as left/right (no RTL support yet).
/// </summary>
public enum CssTextAlign : byte
{
    Start,
    Left,
    Right,
    End,
    Center,
    Justify,
}

/// <summary>
/// CSS <c>white-space</c>.
/// </summary>
public enum CssWhiteSpace : byte
{
    /// <summary>
    /// Collapse whitespace and newlines, wrap lines.
    /// </summary>
    Normal,

    /// <summary>
    /// Collapse whitespace and newlines, never wrap.
    /// </summary>
    Nowrap,

    /// <summary>
    /// Keep everything as is, never wrap.
    /// </summary>
    Pre,

    /// <summary>
    /// Keep everything as is, wrap lines.
    /// </summary>
    PreWrap,

    /// <summary>
    /// Collapse spaces, keep newlines, wrap lines.
    /// </summary>
    PreLine,

    /// <summary>
    /// Same as <see cref="PreWrap"/> (trailing-space hanging is not implemented).
    /// </summary>
    BreakSpaces,
}

/// <summary>
/// CSS <c>word-break</c>.
/// </summary>
public enum CssWordBreak : byte
{
    Normal,

    /// <summary>
    /// Break between any two characters.
    /// </summary>
    BreakAll,

    /// <summary>
    /// Same as <see cref="Normal"/> (there is no CJK-specific logic).
    /// </summary>
    KeepAll,

    /// <summary>
    /// Legacy alias: behaves like <c>overflow-wrap: anywhere</c>.
    /// </summary>
    BreakWord,
}

/// <summary>
/// CSS <c>overflow-wrap</c>.
/// </summary>
public enum CssOverflowWrap : byte
{
    /// <summary>
    /// A word longer than the line overflows the box.
    /// </summary>
    Normal,

    /// <summary>
    /// A word longer than the line is broken between characters.
    /// </summary>
    Anywhere,

    /// <summary>
    /// Same as <see cref="Anywhere"/> for our purposes (min-content sizing is not modelled).
    /// </summary>
    BreakWord,
}

/// <summary>
/// CSS <c>text-transform</c>.
/// </summary>
public enum CssTextTransform : byte
{
    None,
    Uppercase,
    Lowercase,
    Capitalize,
}

/// <summary>
/// CSS <c>text-overflow</c>. Only has a visible effect if the owner clips its content.
/// </summary>
public enum CssTextOverflow : byte
{
    Clip,
    Ellipsis,
}

/// <summary>
/// CSS <c>text-decoration-line</c>.
/// </summary>
[Flags]
public enum CssTextDecorationLine : byte
{
    None = 0,
    Underline = 1,
    Overline = 2,
    LineThrough = 4,
}

/// <summary>
/// CSS <c>text-decoration-style</c>. Only <see cref="Solid"/> and <see cref="Double"/> are rendered
/// at the moment, the rest fall back to <see cref="Solid"/>.
/// </summary>
public enum CssTextDecorationStyle : byte
{
    Solid,
    Double,
    Dotted,
    Dashed,
    Wavy,
}

/// <summary>
/// How a <see cref="CssLineHeight"/> value must be interpreted.
/// </summary>
public enum CssLineHeightKind : byte
{
    /// <summary>
    /// The natural line height of the font.
    /// </summary>
    Normal,

    /// <summary>
    /// A multiplier of the natural line height of the font.
    /// </summary>
    Multiplier,

    /// <summary>
    /// An absolute length in virtual pixels.
    /// </summary>
    Length,
}

/// <summary>
/// CSS <c>line-height</c> value: <c>normal</c>, a unitless number, or an absolute length.
/// </summary>
/// <remarks>
/// Unlike real CSS, a multiplier is applied to the font's natural line height
/// (<see cref="Font.GetLineHeight"/>), not to the font size, because <see cref="Font"/> doesn't expose its size.
/// <c>1</c> is therefore the same as <c>normal</c>. Relative lengths (<c>em</c>, <c>%</c>) must be converted
/// by the parser/style resolver before they get here.
/// </remarks>
public readonly record struct CssLineHeight(CssLineHeightKind Kind, float Value)
{
    /// <summary>
    /// <c>line-height: normal</c>.
    /// </summary>
    public static readonly CssLineHeight Normal = new(CssLineHeightKind.Normal, 1f);

    /// <summary>
    /// Creates a unitless multiplier of the natural font line height.
    /// </summary>
    public static CssLineHeight FromMultiplier(float multiplier) => new(CssLineHeightKind.Multiplier, multiplier);

    /// <summary>
    /// Creates an absolute line height in virtual pixels.
    /// </summary>
    public static CssLineHeight FromLength(float virtualPixels) => new(CssLineHeightKind.Length, virtualPixels);

    /// <summary>
    /// Resolves the value to a line box height in real pixels.
    /// </summary>
    /// <param name="font">The font the line is measured with.</param>
    /// <param name="uiScale">Current UI scale.</param>
    public int ResolvePx(Font font, float uiScale)
    {
        var natural = font.GetLineHeight(uiScale);

        return Kind switch
        {
            CssLineHeightKind.Multiplier => Math.Max(0, (int) MathF.Round(natural * Value)),
            CssLineHeightKind.Length => Math.Max(0, (int) MathF.Round(Value * uiScale)),
            _ => natural,
        };
    }
}

/// <summary>
/// A single CSS <c>text-shadow</c> layer.
/// </summary>
/// <param name="Offset">Offset in virtual pixels.</param>
/// <param name="Blur">Blur radius in virtual pixels. Stored, but not rendered yet (shadows are sharp).</param>
/// <param name="Color">Shadow color; <c>null</c> = <c>currentcolor</c>.</param>
public readonly record struct CssTextShadow(Vector2 Offset, float Blur, Color? Color);
