using System;

namespace Content.Suil;

/// <summary>
/// The conversions the generated code and the hot reload need to put the result of a number expression (always a
/// <see cref="double"/>) into a field of another numeric type.
/// </summary>
public static class SuilMath
{
    /// <summary>
    /// A whole number in the range of the type: rounded, and held to the range. Not a number is zero.
    /// </summary>
    public static double ToInteger(double value, double min, double max)
    {
        if (double.IsNaN(value))
            return 0;

        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded < min ? min : rounded > max ? max : rounded;
    }

    /// <summary>
    /// Converts a number to the numeric type of a field.
    /// </summary>
    /// <param name="type">The type of the field: float, double or one of the integer types.</param>
    /// <returns>The value in the type, boxed; <c>null</c> if the type is not numeric.</returns>
    public static object? FromDouble(Type type, double value)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(double))
            return value;

        if (type == typeof(float))
            return (float) value;

        if (type == typeof(int))
            return (int) ToInteger(value, int.MinValue, int.MaxValue);

        if (type == typeof(uint))
            return (uint) ToInteger(value, uint.MinValue, uint.MaxValue);

        if (type == typeof(long))
            return (long) ToInteger(value, long.MinValue, long.MaxValue);

        if (type == typeof(ulong))
            return (ulong) ToInteger(value, ulong.MinValue, ulong.MaxValue);

        if (type == typeof(short))
            return (short) ToInteger(value, short.MinValue, short.MaxValue);

        if (type == typeof(ushort))
            return (ushort) ToInteger(value, ushort.MinValue, ushort.MaxValue);

        if (type == typeof(byte))
            return (byte) ToInteger(value, byte.MinValue, byte.MaxValue);

        if (type == typeof(sbyte))
            return (sbyte) ToInteger(value, sbyte.MinValue, sbyte.MaxValue);

        return null;
    }

    /// <summary>
    /// Whether the type is one of the numeric types of the fields.
    /// </summary>
    public static bool IsNumeric(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type == typeof(double) || type == typeof(float) || type == typeof(int) || type == typeof(uint)
               || type == typeof(long) || type == typeof(ulong) || type == typeof(short) || type == typeof(ushort)
               || type == typeof(byte) || type == typeof(sbyte);
    }

    /// <summary>
    /// A boxed number read from a field, as a <see cref="double"/>. The sandbox doesn't allow <c>Convert</c>, so the
    /// types are listed.
    /// </summary>
    public static double ToDouble(object? value) => value switch
    {
        double d => d,
        float f => f,
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul,
        short s => s,
        ushort us => us,
        byte b => b,
        sbyte sb => sb,
        _ => 0,
    };
}
