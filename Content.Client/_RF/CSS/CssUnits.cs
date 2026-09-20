using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS;

public enum LengthUnit : byte
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

// TODO: Relative units PLEASE!!!

public static class LengthUnitsExtensions
{
    extension(LengthUnit unit)
    {
        /// <summary>
        /// A dictionary that stores predefined conversion ratios between absolute CSS units and RT UI virtual pixels.
        /// </summary>
        public static Dictionary<LengthUnit, float> AbsoluteToPxRatio => new()
        {
            [LengthUnit.Px] = 1f,
            [LengthUnit.In] = 96f,
            [LengthUnit.Cm] = 96f / 2.54f,
            [LengthUnit.Mm] = 96f / 25.4f,
            [LengthUnit.Q]  = 96f / 101.6f, // 1q = 1/40 cm
            [LengthUnit.Pc] = 16f,          // 1pc = 12pt = 16px
            [LengthUnit.Pt] = 96f / 72f,
        };

        /// <summary>
        /// A list of all absolute CSS units (whose ratio to virtual pixels remains constant).
        /// </summary>
        public static HashSet<LengthUnit> AbsoluteUnits => new()
        {
            LengthUnit.Px,
            LengthUnit.In,
            LengthUnit.Cm,
            LengthUnit.Mm,
            LengthUnit.Q,
            LengthUnit.Pc,
            LengthUnit.Pt,
        };

        /// <summary>
        /// Converts <see cref="LengthUnit.Vw"/> to <see cref="LengthUnit.Px"/>.
        /// </summary>
        [PublicAPI, Pure]
        public static float FromVw(float value, Vector2 viewportSize) => value / 100f * viewportSize.X;

        /// <summary>
        /// Converts <see cref="LengthUnit.Vh"/> to <see cref="LengthUnit.Px"/>.
        /// </summary>
        [PublicAPI, Pure]
        public static float FromVh(float value, Vector2 viewportSize) => value / 100f * viewportSize.Y;

        /// <summary>
        /// Converts <see cref="LengthUnit.VMin"/> to <see cref="LengthUnit.Px"/>.
        /// </summary>
        [PublicAPI, Pure]
        public static float FromVMin(float value, Vector2 viewportSize)
            => value / 100f * MathF.Min(viewportSize.X, viewportSize.Y);

        /// <summary>
        /// Converts <see cref="LengthUnit.VMax"/> to <see cref="LengthUnit.Px"/>.
        /// </summary>
        [PublicAPI, Pure]
        public static float FromVMax(float value, Vector2 viewportSize)
            => value / 100f * MathF.Max(viewportSize.X, viewportSize.Y);

        public static bool TryParse(string unitText, [NotNullWhen(true)] out LengthUnit? parsed)
        {
            parsed = null;

            if (!Enum.TryParse<LengthUnit>(unitText, true, out var value))
                return false;

            parsed = value;
            return true;
        }
    }
}
