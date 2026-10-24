using System.Linq;
using Content.Suil;
using Robust.Client.UserInterface;

namespace Content.Client._RF.Suil;

/// <summary>
/// What the generated <c>InitializeComponent()</c> calls for a property whose value it can't write as a C# literal
/// (a color, a texture, a border image): the value is read by the same parsers as in the styles.
/// </summary>
/// <remarks>
/// A mistake is an <see cref="InvalidOperationException"/> that says where in which file it is (<c>at</c>). In a build
/// with <c>EXCEPTION_TOLERANCE</c> it is written to the log instead, and the property keeps the value it had.
/// </remarks>
public static class SuilElementValues
{
    /// <summary>
    /// Sets a property the generated code can't reach as a member (one that is not public but marked
    /// <c>[SuilProperty]</c>, like <c>background-image</c>), the way the styles do.
    /// </summary>
    /// <param name="at">Where the property is written: <c>file:line:column</c>.</param>
    public static void Apply(Control target, string name, ISuilValue[] values, string? at = null)
    {
#if EXCEPTION_TOLERANCE
        try
        {
            ApplyCore(target, name, values, at);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or InvalidCastException)
        {
            Report(e);
        }
#else
        ApplyCore(target, name, values, at);
#endif
    }

    private static void ApplyCore(Control target, string name, ISuilValue[] values, string? at)
    {
        var suil = IoCManager.Resolve<ISuilManager>();

        if (!suil.TryGetPropertyAccessor(target.GetType(), name, out var accessor))
            throw Error(at, $"{target.GetType().Name} has no SUIL property '{name}'");

        accessor.Value.Setter(target, ReadCore(target.GetType(), name, values, at)!);
    }

    /// <summary>
    /// Sets a shorthand (<c>padding</c>, <c>border-bottom</c>): every longhand it covers, the way the styles expand it.
    /// What the file sets is out of reach of the styles unless <paramref name="soft"/>.
    /// </summary>
    public static void ApplyShorthand(Control target, string name, ISuilValue[] values, bool soft = false, string? at = null)
    {
#if EXCEPTION_TOLERANCE
        try
        {
            ApplyShorthandCore(target, name, values, soft, at);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or InvalidCastException)
        {
            Report(e);
        }
#else
        ApplyShorthandCore(target, name, values, soft, at);
#endif
    }

    private static void ApplyShorthandCore(Control target, string name, ISuilValue[] values, bool soft, string? at)
    {
        var suil = IoCManager.Resolve<ISuilManager>();

        if (!suil.TryExpandShorthand(target.GetType(), name, values, out var expanded))
            throw Error(at, $"{target.GetType().Name}: can't read the value of the shorthand '{name}'");

        foreach (var property in expanded)
        {
            if (property.Value is SuilWideValue)
                throw Error(at, $"{target.GetType().Name}.{name}: a keyword like inherit can't be assigned in a file");

            property.Accessor.Setter(target, property.Value);

            if (!soft && target is UiElement styled)
                styled.SuilSetHard(property.Name);
        }
    }

    /// <param name="owner">The type of the control the property belongs to.</param>
    /// <param name="name">The name of the property in SUIL.</param>
    /// <param name="values">The values as the file has them, after the variables are put in.</param>
    /// <param name="at">Where the property is written: <c>file:line:column</c>.</param>
    public static object? Read(Type owner, string name, ISuilValue[] values, string? at = null)
        => ReadCore(owner, name, values, at);

    /// <summary>
    /// <see cref="Read"/> as the type of the property. A value that can't be read is <c>default</c> in a build with
    /// <c>EXCEPTION_TOLERANCE</c>, and an exception otherwise.
    /// </summary>
    public static T Read<T>(Type owner, string name, ISuilValue[] values, string? at = null)
    {
#if EXCEPTION_TOLERANCE
        try
        {
            return (T) ReadCore(owner, name, values, at)!;
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or InvalidCastException)
        {
            Report(e);
            return default!;
        }
#else
        return (T) ReadCore(owner, name, values, at)!;
#endif
    }

    private static object? ReadCore(Type owner, string name, ISuilValue[] values, string? at)
    {
        var suil = IoCManager.Resolve<ISuilManager>();

        if (!suil.TryGetPropertyAccessor(owner, name, out var accessor))
            throw Error(at, $"{owner.Name} has no SUIL property '{name}'");

        var property = new SuilProperty(name, values.ToList(), false, default);

        if (!SuilValueConverter.TryConvert(suil, accessor.Value, property, out var value, out var error))
            throw Error(at, $"{owner.Name}.{name}: {error}");

        return value;
    }

    private static InvalidOperationException Error(string? at, string message)
        => new(at == null ? message : $"{message} ({at})");

#if EXCEPTION_TOLERANCE
    private static void Report(Exception e) => Logger.GetSawmill("suil").Error(e.Message);
#endif
}
