using System.Linq;
using Content.Suil;
using Robust.Client.UserInterface;

namespace Content.Client._RF.Suil;

/// <summary>
/// What the generated <c>InitializeComponent()</c> calls for a property whose value it can't write as a C# literal
/// (a color, a texture, a border image): the value is read by the same parsers as in the styles.
/// </summary>
public static class SuilElementValues
{
    /// <summary>
    /// Sets a property the generated code can't reach as a member (one that is not public but marked
    /// <c>[SuilProperty]</c>, like <c>background-image</c>), the way the styles do.
    /// </summary>
    public static void Apply(Control target, string name, ISuilValue[] values)
    {
        var suil = IoCManager.Resolve<ISuilManager>();

        if (!suil.TryGetPropertyAccessor(target.GetType(), name, out var accessor))
            throw new InvalidOperationException($"{target.GetType().Name} has no SUIL property '{name}'");

        accessor.Value.Setter(target, Read(target.GetType(), name, values)!);
    }

    /// <summary>
    /// Sets a shorthand (<c>padding</c>, <c>border-bottom</c>): every longhand it covers, the way the styles expand it.
    /// What the file sets is out of reach of the styles unless <paramref name="soft"/>.
    /// </summary>
    public static void ApplyShorthand(Control target, string name, ISuilValue[] values, bool soft = false)
    {
        var suil = IoCManager.Resolve<ISuilManager>();

        if (!suil.TryExpandShorthand(target.GetType(), name, values, out var expanded))
            throw new InvalidOperationException($"{target.GetType().Name}: can't read the value of the shorthand '{name}'");

        foreach (var property in expanded)
        {
            if (property.Value is SuilWideValue)
                throw new InvalidOperationException($"{target.GetType().Name}.{name}: a keyword like inherit can't be assigned in a file");

            property.Accessor.Setter(target, property.Value);

            if (!soft && target is UiElement styled)
                styled.SuilSetHard(property.Name);
        }
    }

    /// <param name="owner">The type of the control the property belongs to.</param>
    /// <param name="name">The name of the property in SUIL.</param>
    /// <param name="values">The values as the file has them, after the variables are put in.</param>
    public static object? Read(Type owner, string name, ISuilValue[] values)
    {
        var suil = IoCManager.Resolve<ISuilManager>();

        if (!suil.TryGetPropertyAccessor(owner, name, out var accessor))
            throw new InvalidOperationException($"{owner.Name} has no SUIL property '{name}'");

        var property = new SuilProperty(name, values.ToList(), false, default);

        if (!SuilValueConverter.TryConvert(suil, accessor.Value, property, out var value, out var error))
            throw new InvalidOperationException($"{owner.Name}.{name}: {error}");

        return value;
    }
}
