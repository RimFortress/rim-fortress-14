using Content.Suil;
using Robust.Client.Graphics;

namespace Content.Client._RF.Suil.Text;

// NOTE: enum member names are chosen so that the generic kebab-case -> PascalCase enum parser
// in SuilManager.TryParseEnum works out of the box ("pre-wrap" -> PreWrap, "break-all" -> BreakAll, ...).

/// <summary>
/// SUIL <c>text-align</c>. <c>Start</c>/<c>End</c> are treated as left/right (no RTL support yet).
/// </summary>
public enum SuilTextAlign : byte
{
    Start,
    Left,
    Right,
    End,
    Center,
    Justify,
}

/// <summary>
/// SUIL <c>white-space</c>.
/// </summary>
public enum SuilWhiteSpace : byte
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
/// SUIL <c>word-break</c>.
/// </summary>
public enum SuilWordBreak : byte
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
/// SUIL <c>overflow-wrap</c>.
/// </summary>
public enum SuilOverflowWrap : byte
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
/// SUIL <c>text-transform</c>.
/// </summary>
public enum SuilTextTransform : byte
{
    None,
    Uppercase,
    Lowercase,
    Capitalize,
}

/// <summary>
/// SUIL <c>text-overflow</c>. Only has a visible effect if the owner clips its content.
/// </summary>
public enum SuilTextOverflow : byte
{
    Clip,
    Ellipsis,
}

/// <summary>
/// SUIL <c>text-decoration-line</c>.
/// </summary>
[Flags]
public enum SuilTextDecorationLine : byte
{
    None = 0,
    Underline = 1,
    Overline = 2,
    LineThrough = 4,
}

/// <summary>
/// SUIL <c>text-decoration-style</c>. Only <see cref="Solid"/> and <see cref="Double"/> are rendered
/// at the moment, the rest fall back to <see cref="Solid"/>.
/// </summary>
public enum SuilTextDecorationStyle : byte
{
    Solid,
    Double,
    Dotted,
    Dashed,
    Wavy,
}

/// <summary>
/// How a <see cref="SuilLineHeight"/> value must be interpreted.
/// </summary>
public enum SuilLineHeightKind : byte
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
/// SUIL <c>line-height</c> value: <c>normal</c>, a unitless number, or a length.
/// </summary>
/// <remarks>
/// Unlike real SUIL, a multiplier is applied to the font's natural line height
/// (<see cref="Font.GetLineHeight"/>), not to the font size, because <see cref="Font"/> doesn't expose its size.
/// <c>1</c> is therefore the same as <c>normal</c>. A percentage is stored as a multiplier.
/// A length may be written in any unit except a percentage; <see cref="Resolve"/> turns it into pixels,
/// and only then <see cref="ResolvePx"/> may be used.
/// </remarks>
public readonly record struct SuilLineHeight(SuilLineHeightKind Kind, float Value, LengthUnitType Unit = LengthUnitType.Px)
{
    /// <summary>
    /// <c>line-height: normal</c>.
    /// </summary>
    public static readonly SuilLineHeight Normal = new(SuilLineHeightKind.Normal, 1f);

    /// <summary>
    /// Creates a unitless multiplier of the natural font line height.
    /// </summary>
    public static SuilLineHeight FromMultiplier(float multiplier) => new(SuilLineHeightKind.Multiplier, multiplier);

    /// <summary>
    /// Creates an absolute line height in virtual pixels.
    /// </summary>
    public static SuilLineHeight FromLength(float virtualPixels) => new(SuilLineHeightKind.Length, virtualPixels);

    /// <summary>
    /// Creates a line height from a length in any unit.
    /// </summary>
    public static SuilLineHeight FromLength(LengthUnit length) => new(SuilLineHeightKind.Length, length.Value, length.Type);

    /// <summary>
    /// Converts the length of a <see cref="SuilLineHeightKind.Length"/> value to virtual pixels.
    /// Other kinds are returned as is.
    /// </summary>
    /// <param name="context">The font metrics and the viewport size.</param>
    public SuilLineHeight Resolve(in LengthContext context)
        => Kind == SuilLineHeightKind.Length && Unit != LengthUnitType.Px
            ? FromLength(new LengthUnit(Value, Unit).Resolve(context))
            : this;

    /// <summary>
    /// Resolves the value to a line box height in real pixels. The length, if any, must already be in pixels.
    /// </summary>
    /// <param name="font">The font the line is measured with.</param>
    /// <param name="uiScale">Current UI scale.</param>
    public int ResolvePx(Font font, float uiScale)
    {
        var natural = font.GetLineHeight(uiScale);

        return Kind switch
        {
            SuilLineHeightKind.Multiplier => Math.Max(0, (int) MathF.Round(natural * Value)),
            SuilLineHeightKind.Length => Math.Max(0, (int) MathF.Round(Value * uiScale)),
            _ => natural,
        };
    }
}

/// <summary>
/// A single SUIL <c>text-shadow</c> layer as it was written (lengths in any unit).
/// </summary>
/// <param name="Offset">Offset.</param>
/// <param name="Blur">Blur radius. Stored, but not rendered yet (shadows are sharp).</param>
/// <param name="Color">Shadow color; <c>null</c> = <c>currentcolor</c>.</param>
public readonly record struct SuilTextShadow(UnitVector2 Offset, LengthUnit Blur, Color? Color);
