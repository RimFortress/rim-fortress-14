using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Suil;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client._RF.Suil;

/// <summary>
/// Turns the values written in a <c>.suil</c> file for a property of an element into the values of the member. The
/// hot reload and the code that the generator writes both go through it, so a file means the same in both.
/// </summary>
public static class SuilValueConverter
{

    /// <summary>
    /// Turns the value of an element property into the value of the member. The types that the generator writes into
    /// <c>InitializeComponent()</c> are converted the same way (plain numbers and <c>px</c>, an ident or a localization key
    /// for a string, the members of enums); anything else goes to the parsers of the styles.
    /// </summary>
    public static bool TryConvert(
        ISuilManager suil,
        SuilStyleAccessor accessor,
        SuilProperty property,
        out object? value,
        [NotNullWhen(false)] out string? error)
    {
        value = null;
        error = null;

        var values = property.Value;
        var target = Nullable.GetUnderlyingType(accessor.ClrType) ?? accessor.ClrType;

        // a flag without a value
        if (values == null)
        {
            if (target == typeof(bool))
            {
                value = true;
                return true;
            }

            error = "it needs a value";
            return false;
        }

        if (accessor.CustomParser is { } parserType)
        {
            var custom = new SuilValueStream(values);

            if (suil.TryParseWith(parserType, ref custom, out value) && custom.AtEnd)
                return true;

            error = $"{parserType.Name} can't read the value";
            return false;
        }

        if (target == typeof(string))
            return TryConvertString(values, out value, out error);

        if (target == typeof(bool))
        {
            if (values is [SuilIdent { Value: "true" or "false" } boolean])
            {
                value = boolean.Value == "true";
                return true;
            }

            error = "expected true or false";
            return false;
        }

        if (IsNumber(target))
        {
            if (values is [var single] && TryNumber(single, out var number, out var integer))
                return TryConvertNumber(target, number, integer, out value, out error);

            error = "expected a number";
            return false;
        }

        if (target == typeof(System.Numerics.Vector2))
        {
            var numbers = Numbers(values);

            if (numbers is { Count: 1 })
            {
                value = new System.Numerics.Vector2((float) numbers[0]);
                return true;
            }

            if (numbers is { Count: 2 })
            {
                value = new System.Numerics.Vector2((float) numbers[0], (float) numbers[1]);
                return true;
            }

            error = "expected one or two numbers";
            return false;
        }

        if (target == typeof(Thickness))
        {
            // as in SUIL: one value is all sides, two are vertical and horizontal, four are top, right, bottom, left
            var n = Numbers(values);

            if (n is { Count: 1 })
            {
                value = new Thickness((float) n[0]);
                return true;
            }

            if (n is { Count: 2 })
            {
                value = new Thickness((float) n[1], (float) n[0], (float) n[1], (float) n[0]);
                return true;
            }

            if (n is { Count: 4 })
            {
                value = new Thickness((float) n[3], (float) n[0], (float) n[1], (float) n[2]);
                return true;
            }

            error = "expected one, two or four numbers (top right bottom left)";
            return false;
        }

        var stream = new SuilValueStream(values);

        if (suil.TryParse(ref stream, accessor.ClrType, out value) && stream.AtEnd)
            return true;

        error = $"can't read the value as {accessor.ClrType.Name}";
        return false;
    }

    private static bool TryConvertString(List<ISuilValue> values, out object? value, [NotNullWhen(false)] out string? error)
    {
        value = null;
        error = null;

        if (values.Count == 1)
        {
            switch (values[0])
            {
                case SuilString str:
                    value = string.Concat(str.Parts.Select(p => p.Text));
                    return true;
                case SuilIdent ident:
                    value = ident.Value;
                    return true;
                case SuilLocalization loc:
                    return TryLocalize(loc, out value, out error);
            }
        }

        error = "expected a string, an ident or a $localization key";
        return false;
    }

    private static bool TryLocalize(SuilLocalization loc, out object? value, [NotNullWhen(false)] out string? error)
    {
        value = null;
        error = null;

        var key = string.Concat(loc.Key.Select(p => p.Text));
        var arguments = new List<(string, object)>(loc.Arguments.Count);

        foreach (var argument in loc.Arguments)
        {
            if (argument.Value.Count != 1)
            {
                error = $"the localization argument '{argument.Name}' must be one value";
                return false;
            }

            switch (argument.Value[0])
            {
                case SuilString str:
                    arguments.Add((argument.Name, string.Concat(str.Parts.Select(p => p.Text))));
                    break;
                case SuilIdent ident:
                    arguments.Add((argument.Name, ident.Value));
                    break;
                case var other when TryNumber(other, out var number, out var integer):
                    arguments.Add((argument.Name, integer ? (object) (long) number : number));
                    break;
                default:
                    error = $"the localization argument '{argument.Name}' must be a string, an ident or a number";
                    return false;
            }
        }

        value = Loc.GetString(key, arguments.ToArray());
        return true;
    }

    private static bool IsNumber(Type type)
        => type == typeof(float) || type == typeof(double) || type == typeof(int) || type == typeof(uint)
           || type == typeof(long) || type == typeof(ulong) || type == typeof(short) || type == typeof(ushort)
           || type == typeof(byte) || type == typeof(sbyte);

    private static bool TryConvertNumber(
        Type target,
        double number,
        bool integer,
        out object? value,
        [NotNullWhen(false)] out string? error)
    {
        error = null;

        if (target == typeof(float))
            value = (float) number;
        else if (target == typeof(double))
            value = number;
        else if (!integer)
        {
            value = null;
            error = "expected a whole number";
            return false;
        }
        else if (target == typeof(int))
            value = (int) number;
        else if (target == typeof(uint))
            value = (uint) number;
        else if (target == typeof(long))
            value = (long) number;
        else if (target == typeof(ulong))
            value = (ulong) number;
        else if (target == typeof(short))
            value = (short) number;
        else if (target == typeof(ushort))
            value = (ushort) number;
        else if (target == typeof(byte))
            value = (byte) number;
        else
            value = (sbyte) number;

        return true;
    }

    private static bool TryNumber(ISuilValue value, out double number, out bool integer)
    {
        switch (value)
        {
            case SuilNumber n:
                number = n.Value;
                integer = n.IsInteger;
                return true;
            case SuilDimension { Unit: "px" } d:
                number = d.Value;
                integer = d.IsInteger;
                return true;
            default:
                number = 0;
                integer = false;
                return false;
        }
    }

    private static List<double>? Numbers(List<ISuilValue> values)
    {
        var result = new List<double>(values.Count);

        foreach (var value in values)
        {
            if (!TryNumber(value, out var number, out _))
                return null;

            result.Add(number);
        }

        return result;
    }
}
