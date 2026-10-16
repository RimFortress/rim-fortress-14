using System.Linq;
using Robust.Client.UserInterface;
using System.Diagnostics.CodeAnalysis;
using Content.Suil;

namespace Content.Client._RF.Suil.HotReload;

public sealed partial class SuilHotReload
{
    /// <summary>
    /// An element of the file after everything that can go wrong was checked: the type is known, the properties have
    /// accessors and values. Applying it can't fail on the file.
    /// </summary>
    private sealed class PlannedElement
    {
        public required Type Type;
        public required SuilElement Element;
        public string? Id;
        public readonly List<PlannedHandler> Handlers = new();
        public readonly List<string> Classes = new();
        public bool HasContentSlot;
        public readonly List<PlannedProperty> Properties = new();
        public readonly List<PlannedElement> Children = new();
    }

    /// <param name="Soft">The <c>?</c> before the value: the styles may override the property. Otherwise they may not.</param>
    private readonly record struct PlannedProperty(string Name, SuilStyleAccessor Accessor, object? Value, bool Soft);

    /// <summary>
    /// Checks the tree of the file against the view and turns it into a plan. The mistakes are put in
    /// <paramref name="errors"/>; the plan is <c>null</c> when the root can't be planned at all.
    /// </summary>
    private PlannedElement? BuildPlan(
        ISuilView view,
        Control control,
        SuilElement root,
        List<string> errors,
        List<string> warnings)
    {
        var rootType = _suil.ResolveTag(root.Tag, out var tagError);

        if (rootType == null)
        {
            errors.Add(At(root.Position, tagError!));
            return null;
        }

        // The class is already compiled: the root can't become a control of another type.
        if (!rootType.IsInstanceOfType(control))
        {
            errors.Add(At(root.Position,
                $"the root is '{root.Tag ?? "_"}' ({rootType.Name}), but {control.GetType().Name} is not one: " +
                "the type of the root is in the code, build the project"));
            return null;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var plan = PlanElement(view, root, rootType, isRoot: true, ids, errors, warnings);

        if (plan != null)
            PlanHandlers(view, plan, errors);

        return plan;
    }

    private PlannedElement? PlanElement(
        ISuilView view,
        SuilElement element,
        Type? knownType,
        bool isRoot,
        HashSet<string> ids,
        List<string> errors,
        List<string> warnings)
    {
        string? tagError = null;
        var type = knownType ?? _suil.ResolveTag(element.Tag, out tagError);

        if (type == null)
        {
            errors.Add(At(element.Position, tagError!));
            return null;
        }

        var plan = new PlannedElement { Type = type, Element = element, Id = element.Id?.ToString() };

        if (plan.Id != null)
        {
            if (!ids.Add(plan.Id))
                errors.Add(At(element.Position, $"the id '{plan.Id}' is used twice"));

            // the root is the view itself: it has no field
            var fieldType = isRoot ? null : view.SuilFieldType(plan.Id);

            if (!isRoot && fieldType == null)
            {
                warnings.Add(At(element.Position,
                    $"the id '{plan.Id}' has no field in the class: the control is built, but the code can't reach it until the project is built"));
            }
            else if (fieldType != null && !fieldType.IsAssignableFrom(type))
            {
                errors.Add(At(element.Position,
                    $"the field of '{plan.Id}' is {fieldType.Name}, and the element is {type.Name}: the type of a field is in the code, build the project"));
            }
        }

        foreach (var cls in element.Classes)
        {
            plan.Classes.Add(cls.ToString());
        }

        foreach (var property in element.Properties)
        {
            // the root is the view itself, so its own properties can be named too
            var propertyOwner = isRoot ? view.GetType() : type;

            // a shorthand sets the longhands it covers
            if (!_suil.TryGetPropertyAccessor(propertyOwner, property.Name, out var accessor)
                && property.Value != null
                && _suil.TryExpandShorthand(propertyOwner, property.Name, property.Value, out var expanded))
            {
                foreach (var longhand in expanded)
                {
                    if (longhand.Value is SuilWideValue)
                    {
                        errors.Add(At(property.Position, $"'{property.Name}': a keyword like inherit can't be assigned in a file"));
                        continue;
                    }

                    plan.Properties.Add(new PlannedProperty(longhand.Name, longhand.Accessor, longhand.Value, property.IsSoft));
                }

                continue;
            }

            if (accessor == null)
            {
                errors.Add(At(property.Position, $"{propertyOwner.Name} has no property '{property.Name}'"));
                continue;
            }

            if (!SuilValueConverter.TryConvert(_suil, accessor.Value, property, out var value, out var error))
            {
                errors.Add(At(property.Position, $"'{property.Name}': {error}"));
                continue;
            }

            plan.Properties.Add(new PlannedProperty(property.Name, accessor.Value, value, property.IsSoft));
        }

        foreach (var node in element.Children)
        {
            if (node is SuilContentSlot)
            {
                plan.HasContentSlot = true;
                continue;
            }

            if (node is not SuilElement child)
            {
                errors.Add(At(node is SuilContentSlot slot ? slot.Position : element.Position, "a content slot is left in the tree"));
                continue;
            }

            var childPlan = PlanElement(view, child, null, isRoot: false, ids, errors, warnings);

            if (childPlan != null)
                plan.Children.Add(childPlan);
        }

        return plan;
    }

    private static string At(SuilPosition position, string message)
        => position.Line > 0 ? $"{message} ({position.File}:{position.Line}:{position.Column})" : message;
}
