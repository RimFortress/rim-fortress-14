using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

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

    //

    // Viewport-relative Units

    /// <summary>
    /// Equal to 1% of the height of the viewport size.
    /// </summary>
    Vw,

    /// <summary>
    /// Equal to 1% of the width of the viewport size.
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

    //

    Percent,
}

/// <summary>
/// A floating-point number calculated in a specific unit.
/// </summary>
public readonly record struct LengthUnit(float Value, LengthUnitType Type)
{
    /// <summary>
    /// Is this number absolute, i.e., can it be converted to <see cref="LengthUnitType.Px"/>
    /// without needing to know other values (such as the viewport or parent size)?
    /// </summary>
    public bool IsAbsolute => LengthUnitType.AbsoluteUnits.Contains(Type);

    public static LengthUnit Zero => new(0, LengthUnitType.Px);

    public bool IsZero => Value == 0f;

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

        DebugTools.Assert(LengthUnitType.AbsoluteUnits.Contains(Type));
        return new LengthUnit(Value / LengthUnitType.AbsoluteToPxRatio[Type], LengthUnitType.Px);
    }

    public override string ToString() => $"{Value:F2}{Type.ToString().ToLowerInvariant()}";
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
    public bool IsAbsolute => X.IsAbsolute &&  Y.IsAbsolute;

    public bool IsZero => X.IsZero && Y.IsZero;

    public override string ToString() => $"{X.ToString()},{Y.ToString()}";
}

public static class LengthUnitsExtensions
{
    extension(LengthUnitType unitType)
    {
        /// <summary>
        /// A dictionary that stores predefined conversion ratios between absolute CSS units and RT UI virtual pixels.
        /// </summary>
        public static Dictionary<LengthUnitType, float> AbsoluteToPxRatio => new()
        {
            [LengthUnitType.Px] = 1f,
            [LengthUnitType.In] = 96f,
            [LengthUnitType.Cm] = 96f / 2.54f,
            [LengthUnitType.Mm] = 96f / 25.4f,
            [LengthUnitType.Q]  = 96f / 101.6f, // 1q = 1/40 cm
            [LengthUnitType.Pc] = 16f,          // 1pc = 12pt = 16px
            [LengthUnitType.Pt] = 96f / 72f,
        };

        /// <summary>
        /// A list of all absolute CSS units (whose ratio to virtual pixels remains constant).
        /// </summary>
        public static HashSet<LengthUnitType> AbsoluteUnits => new()
        {
            LengthUnitType.Px,
            LengthUnitType.In,
            LengthUnitType.Cm,
            LengthUnitType.Mm,
            LengthUnitType.Q,
            LengthUnitType.Pc,
            LengthUnitType.Pt,
        };

        /// <summary>
        /// A list of all viewport-relative CSS units.
        /// </summary>
        public static HashSet<LengthUnitType> ViewportUnits => new()
        {
            LengthUnitType.Vw,
            LengthUnitType.Vh,
            LengthUnitType.VMin,
            LengthUnitType.VMax,
        };

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

        public static bool TryParse(string unitText, [NotNullWhen(true)] out LengthUnitType? parsed)
        {
            parsed = null;

            if (!Enum.TryParse<LengthUnitType>(unitText, true, out var value))
                return false;

            parsed = value;
            return true;
        }
    }
}
