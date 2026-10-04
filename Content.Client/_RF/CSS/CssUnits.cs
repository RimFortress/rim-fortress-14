using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

/// <summary>
/// The unit of a CSS <c>&lt;length&gt;</c> or <c>&lt;percentage&gt;</c>.
/// </summary>
public enum LengthUnitType : byte
{
    // Absolute units

    /// <summary>
    /// Aka virtual pixel.
    /// </summary>
    Px,

    /// <summary>
    /// Centimeters (1cm = 96px/2.54).
    /// </summary>
    Cm,

    /// <summary>
    /// Millimeters (1mm = 1/10th of 1cm).
    /// </summary>
    Mm,

    /// <summary>
    /// Quarter-millimeters (1Q = 1/40th of 1cm).
    /// </summary>
    Q,

    /// <summary>
    /// Inches (1in = 2.54cm = 96px).
    /// </summary>
    In,

    /// <summary>
    /// Picas (1pc = 1/6th of 1in).
    /// </summary>
    Pc,

    /// <summary>
    /// Points (1pt = 1/72nd of 1in).
    /// </summary>
    Pt,

    // Font-relative units (see LengthContext)

    /// <summary>
    /// The computed <c>font-size</c> of the element (of its parent when used in <c>font-size</c> itself).
    /// </summary>
    Em,

    /// <summary>
    /// The computed <c>font-size</c> of the root element.
    /// </summary>
    Rem,

    /// <summary>
    /// The x-height of the element's font.
    /// </summary>
    Ex,

    /// <summary>
    /// The advance of the "0" glyph of the element's font.
    /// </summary>
    Ch,

    /// <summary>
    /// The computed <c>line-height</c> of the element.
    /// </summary>
    Lh,

    // Viewport-relative units

    /// <summary>
    /// Equal to 1% of the width of the viewport size.
    /// </summary>
    Vw,

    /// <summary>
    /// Equal to 1% of the height of the viewport size.
    /// </summary>
    Vh,

    /// <summary>
    /// Equal to the smaller of <see cref="Vw"/> or <see cref="Vh"/>.
    /// </summary>
    VMin,

    /// <summary>
    /// Equal to the larger of <see cref="Vw"/> or <see cref="Vh"/>.
    /// </summary>
    VMax,

    /// <summary>
    /// A percentage of a reference value that depends on the property (see <see cref="LengthUnit.Resolve"/>).
    /// </summary>
    Percent,
}

/// <summary>
/// Everything a <see cref="LengthUnit"/> may need to become virtual pixels, other than the percentage reference:
/// the font metrics of the element and the viewport size. All values are in virtual pixels.
/// </summary>
/// <param name="FontSize">The computed <c>font-size</c> of the element (<c>em</c>).</param>
/// <param name="RootFontSize">The computed <c>font-size</c> of the root element (<c>rem</c>).</param>
/// <param name="ExHeight">The x-height of the element's font (<c>ex</c>).</param>
/// <param name="ChWidth">The advance of the "0" glyph of the element's font (<c>ch</c>).</param>
/// <param name="LineHeight">The computed <c>line-height</c> of the element (<c>lh</c>).</param>
/// <param name="Viewport">The viewport size (<c>vw</c>, <c>vh</c>, <c>vmin</c>, <c>vmax</c>).</param>
public readonly record struct LengthContext(
    float FontSize,
    float RootFontSize,
    float ExHeight,
    float ChWidth,
    float LineHeight,
    Vector2 Viewport)
{
    /// <summary>
    /// A context with rough font metrics derived from <paramref name="fontSize"/>, for code that has no font at hand.
    /// </summary>
    public static LengthContext Initial(float fontSize, Vector2 viewport)
        => new(fontSize, fontSize, fontSize * 0.5f, fontSize * 0.5f, fontSize * 1.2f, viewport);
}

/// <summary>
/// A floating-point number calculated in a specific unit.
/// </summary>
/// <remarks>
/// A plain <see cref="float"/> converts to <see cref="LengthUnitType.Px"/> implicitly, so
/// <c>Padding = 4</c> and <c>MarginLeft = 8f</c> keep working. Use <see cref="Resolve"/> to get virtual pixels.
/// </remarks>
public readonly record struct LengthUnit(float Value, LengthUnitType Type)
{
    /// <summary>
    /// Zero pixels.
    /// </summary>
    [PublicAPI]
    public static LengthUnit Zero => new(0, LengthUnitType.Px);

    /// <summary>
    /// Creates a length in pixels.
    /// </summary>
    [PublicAPI, Pure]
    public static LengthUnit Px(float value) => new(value, LengthUnitType.Px);

    /// <summary>
    /// Creates a length in <c>em</c>.
    /// </summary>
    [PublicAPI, Pure]
    public static LengthUnit Em(float value) => new(value, LengthUnitType.Em);

    /// <summary>
    /// Creates a length in <c>rem</c>.
    /// </summary>
    [PublicAPI, Pure]
    public static LengthUnit Rem(float value) => new(value, LengthUnitType.Rem);

    /// <summary>
    /// Creates a length in <c>ex</c>.
    /// </summary>
    [PublicAPI, Pure]
    public static LengthUnit Ex(float value) => new(value, LengthUnitType.Ex);

    /// <summary>
    /// Creates a length in <c>ch</c>.
    /// </summary>
    [PublicAPI, Pure]
    public static LengthUnit Ch(float value) => new(value, LengthUnitType.Ch);

    /// <summary>
    /// Creates a length in <c>lh</c>.
    /// </summary>
    [PublicAPI, Pure]
    public static LengthUnit Lh(float value) => new(value, LengthUnitType.Lh);

    /// <summary>
    /// Creates a length in <c>vw</c>.
    /// </summary>
    [PublicAPI, Pure]
    public static LengthUnit Vw(float value) => new(value, LengthUnitType.Vw);

    /// <summary>
    /// Creates a length in <c>vh</c>.
    /// </summary>
    [PublicAPI, Pure]
    public static LengthUnit Vh(float value) => new(value, LengthUnitType.Vh);

    /// <summary>
    /// Creates a percentage.
    /// </summary>
    [PublicAPI, Pure]
    public static LengthUnit Percent(float value) => new(value, LengthUnitType.Percent);

    /// <summary>
    /// Is this number absolute, i.e., can it be converted to <see cref="LengthUnitType.Px"/>
    /// without needing to know other values (such as the font or the viewport size)?
    /// </summary>
    [PublicAPI]
    public bool IsAbsolute => Type is LengthUnitType.Px
        or LengthUnitType.Cm
        or LengthUnitType.Mm
        or LengthUnitType.Q
        or LengthUnitType.In
        or LengthUnitType.Pc
        or LengthUnitType.Pt;

    /// <summary>
    /// Does this number depend on the font of the element (or of the root element)?
    /// </summary>
    [PublicAPI]
    public bool IsFontRelative => Type is LengthUnitType.Em
        or LengthUnitType.Rem
        or LengthUnitType.Ex
        or LengthUnitType.Ch
        or LengthUnitType.Lh;

    /// <summary>
    /// Does this number depend on the viewport size?
    /// </summary>
    [PublicAPI]
    public bool IsViewportRelative => Type is LengthUnitType.Vw
        or LengthUnitType.Vh
        or LengthUnitType.VMin
        or LengthUnitType.VMax;

    /// <summary>
    /// Is this number a percentage?
    /// </summary>
    [PublicAPI]
    public bool IsPercent => Type == LengthUnitType.Percent;

    /// <summary>
    /// Is the value zero (in any unit)?
    /// </summary>
    [PublicAPI]
    public bool IsZero => Value == 0f;

    /// <summary>
    /// Resolves the number to virtual pixels.
    /// </summary>
    /// <param name="context">The font metrics and the viewport size.</param>
    /// <param name="percentBase">
    /// What 100% is. The caller must supply the right reference for the property the value belongs to
    /// (the parent's font size for <c>font-size</c>, the box size for <c>border-radius</c>, ...).
    /// Properties without a defined reference leave it at zero, so a percentage resolves to zero for them.
    /// </param>
    [PublicAPI, Pure]
    public float Resolve(in LengthContext context, float percentBase = 0f) => Type switch
    {
        LengthUnitType.Em => Value * context.FontSize,
        LengthUnitType.Rem => Value * context.RootFontSize,
        LengthUnitType.Ex => Value * context.ExHeight,
        LengthUnitType.Ch => Value * context.ChWidth,
        LengthUnitType.Lh => Value * context.LineHeight,
        LengthUnitType.Vw => LengthUnitType.FromVw(Value, context.Viewport),
        LengthUnitType.Vh => LengthUnitType.FromVh(Value, context.Viewport),
        LengthUnitType.VMin => LengthUnitType.FromVMin(Value, context.Viewport),
        LengthUnitType.VMax => LengthUnitType.FromVMax(Value, context.Viewport),
        LengthUnitType.Percent => Value / 100f * percentBase,
        _ => Value * AbsoluteRatio(Type),
    };

    /// <summary>
    /// Resolves a percent value against an explicit reference — the caller
    /// must supply the correct axis for the specific CSS property this value
    /// belongs to (parent width for margin/padding/width, own box width/height
    /// for border-radius, etc.). Not meaningful for non-Percent units.
    /// </summary>
    [PublicAPI, Pure]
    public LengthUnit PercentToPx(float referenceValue)
    {
        DebugTools.AssertEqual(Type, LengthUnitType.Percent);
        return new LengthUnit(Value / 100f * referenceValue, LengthUnitType.Px);
    }

    /// <summary>
    /// Converts viewport-relative values to <see cref="LengthUnitType.Px"/>.
    /// </summary>
    /// <param name="viewportSize">The viewport size relative to which the value should be calculated.</param>
    [PublicAPI, Pure]
    public LengthUnit ViewportToPx(Vector2 viewportSize) => Type switch
    {
        LengthUnitType.Vw => new LengthUnit(LengthUnitType.FromVw(Value, viewportSize), LengthUnitType.Px),
        LengthUnitType.Vh => new LengthUnit(LengthUnitType.FromVh(Value, viewportSize), LengthUnitType.Px),
        LengthUnitType.VMin => new LengthUnit(LengthUnitType.FromVMin(Value, viewportSize), LengthUnitType.Px),
        LengthUnitType.VMax => new LengthUnit(LengthUnitType.FromVMax(Value, viewportSize), LengthUnitType.Px),
        _ => throw new ArgumentOutOfRangeException(nameof(Type), Type, null),
    };

    /// <summary>
    /// Converts the absolute unit value to <see cref="LengthUnitType.Px"/>.
    /// </summary>
    [PublicAPI, Pure]
    public LengthUnit AbsoluteToPx()
    {
        if (Type == LengthUnitType.Px)
            return this;

        DebugTools.Assert(IsAbsolute);
        return new LengthUnit(Value * AbsoluteRatio(Type), LengthUnitType.Px);
    }

    /// <summary>
    /// Converts a pixel value to a length in pixels.
    /// </summary>
    public static implicit operator LengthUnit(float pixels) => new(pixels, LengthUnitType.Px);

    /// <summary>
    /// Scales the value, keeping the unit.
    /// </summary>
    public static LengthUnit operator *(LengthUnit length, float factor) => length with { Value = length.Value * factor };

    /// <summary>
    /// Negates the value, keeping the unit.
    /// </summary>
    public static LengthUnit operator -(LengthUnit length) => length with { Value = -length.Value };

    /// <inheritdoc/>
    public override string ToString()
        => Type == LengthUnitType.Percent ? $"{Value:F2}%" : $"{Value:F2}{Type.ToString().ToLowerInvariant()}";

    // How many virtual pixels are in one of the given absolute units.
    private static float AbsoluteRatio(LengthUnitType type) => type switch
    {
        LengthUnitType.In => 96f,
        LengthUnitType.Cm => 96f / 2.54f,
        LengthUnitType.Mm => 96f / 25.4f,
        LengthUnitType.Q => 96f / 101.6f, // 1q = 1/40 cm
        LengthUnitType.Pc => 16f,         // 1pc = 12pt = 16px
        LengthUnitType.Pt => 96f / 72f,
        _ => 1f,
    };
}

/// <summary>
/// A two-dimensional vector whose components are expressed in specific unit systems.
/// </summary>
public record struct UnitVector2(LengthUnit X, LengthUnit Y)
{
    /// <summary>
    /// Are all the components of the vector absolute?
    /// </summary>
    /// <seealso cref="LengthUnit.IsAbsolute"/>
    [PublicAPI]
    public bool IsAbsolute => X.IsAbsolute && Y.IsAbsolute;

    /// <summary>
    /// Are both components zero?
    /// </summary>
    [PublicAPI]
    public bool IsZero => X.IsZero && Y.IsZero;

    /// <summary>
    /// Resolves both components to virtual pixels.
    /// </summary>
    /// <param name="context">The font metrics and the viewport size.</param>
    /// <param name="percentBase">What 100% is on each axis.</param>
    [PublicAPI, Pure]
    public Vector2 Resolve(in LengthContext context, Vector2 percentBase = default)
        => new(X.Resolve(context, percentBase.X), Y.Resolve(context, percentBase.Y));

    /// <inheritdoc/>
    public override string ToString() => $"{X.ToString()},{Y.ToString()}";
}

public static class LengthUnitsExtensions
{
    private static readonly Dictionary<LengthUnitType, float> AbsoluteRatios = new()
    {
        [LengthUnitType.Px] = 1f,
        [LengthUnitType.In] = 96f,
        [LengthUnitType.Cm] = 96f / 2.54f,
        [LengthUnitType.Mm] = 96f / 25.4f,
        [LengthUnitType.Q] = 96f / 101.6f,
        [LengthUnitType.Pc] = 16f,
        [LengthUnitType.Pt] = 96f / 72f,
    };

    private static readonly HashSet<LengthUnitType> Absolute = new(AbsoluteRatios.Keys);

    private static readonly HashSet<LengthUnitType> FontRelative =
        [LengthUnitType.Em, LengthUnitType.Rem, LengthUnitType.Ex, LengthUnitType.Ch, LengthUnitType.Lh];

    private static readonly HashSet<LengthUnitType> Viewport =
        [LengthUnitType.Vw, LengthUnitType.Vh, LengthUnitType.VMin, LengthUnitType.VMax];

    extension(LengthUnitType)
    {
        /// <summary>
        /// Predefined conversion ratios between absolute CSS units and RT UI virtual pixels.
        /// </summary>
        public static IReadOnlyDictionary<LengthUnitType, float> AbsoluteToPxRatio => AbsoluteRatios;

        /// <summary>
        /// All absolute CSS units (whose ratio to virtual pixels remains constant).
        /// </summary>
        public static IReadOnlySet<LengthUnitType> AbsoluteUnits => Absolute;

        /// <summary>
        /// All font-relative CSS units.
        /// </summary>
        public static IReadOnlySet<LengthUnitType> FontRelativeUnits => FontRelative;

        /// <summary>
        /// All viewport-relative CSS units.
        /// </summary>
        public static IReadOnlySet<LengthUnitType> ViewportUnits => Viewport;

        /// <summary>
        /// Converts <see cref="LengthUnitType.Vw"/> to <see cref="LengthUnitType.Px"/>.
        /// </summary>
        [PublicAPI, Pure]
        public static float FromVw(float value, Vector2 viewportSize) => value / 100f * viewportSize.X;

        /// <summary>
        /// Converts <see cref="LengthUnitType.Vh"/> to <see cref="LengthUnitType.Px"/>.
        /// </summary>
        [PublicAPI, Pure]
        public static float FromVh(float value, Vector2 viewportSize) => value / 100f * viewportSize.Y;

        /// <summary>
        /// Converts <see cref="LengthUnitType.VMin"/> to <see cref="LengthUnitType.Px"/>.
        /// </summary>
        [PublicAPI, Pure]
        public static float FromVMin(float value, Vector2 viewportSize)
            => value / 100f * MathF.Min(viewportSize.X, viewportSize.Y);

        /// <summary>
        /// Converts <see cref="LengthUnitType.VMax"/> to <see cref="LengthUnitType.Px"/>.
        /// </summary>
        [PublicAPI, Pure]
        public static float FromVMax(float value, Vector2 viewportSize)
            => value / 100f * MathF.Max(viewportSize.X, viewportSize.Y);

        /// <summary>
        /// Parses the unit of a CSS dimension (<c>px</c>, <c>em</c>, <c>vw</c>, ...), case-insensitively.
        /// <see cref="LengthUnitType.Percent"/> is not a dimension unit: a percentage has its own token.
        /// </summary>
        public static bool TryParse(string unitText, [NotNullWhen(true)] out LengthUnitType? parsed)
        {
            parsed = unitText.ToLowerInvariant() switch
            {
                "px" => LengthUnitType.Px,
                "cm" => LengthUnitType.Cm,
                "mm" => LengthUnitType.Mm,
                "q" => LengthUnitType.Q,
                "in" => LengthUnitType.In,
                "pc" => LengthUnitType.Pc,
                "pt" => LengthUnitType.Pt,
                "em" => LengthUnitType.Em,
                "rem" => LengthUnitType.Rem,
                "ex" => LengthUnitType.Ex,
                "ch" => LengthUnitType.Ch,
                "lh" => LengthUnitType.Lh,
                "vw" => LengthUnitType.Vw,
                "vh" => LengthUnitType.Vh,
                "vmin" => LengthUnitType.VMin,
                "vmax" => LengthUnitType.VMax,
                _ => null,
            };

            return parsed != null;
        }
    }
}
