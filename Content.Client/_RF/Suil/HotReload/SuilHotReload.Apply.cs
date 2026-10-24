using System.Linq;
using Robust.Client.UserInterface;
using Content.Suil;

namespace Content.Client._RF.Suil.HotReload;

public sealed partial class SuilHotReload
{
    private const string RootKey = "";

    /// <summary>
    /// Puts the planned tree on the live controls of the view.
    /// </summary>
    /// <remarks>
    /// A control that the view has a field for is kept when the file still has an element of that id and type: the code
    /// may have subscribed to it, and a new control would lose that. Its children are rebuilt like the others, if the file
    /// has any. Whatever the
    /// previous plan had on a kept control and the new one hasn't (a class, a property) is taken off.
    /// </remarks>
    private void Apply(
        ISuilView view,
        Control root,
        PlannedElement plan,
        ViewState state,
        List<string> errors,
        List<string> warnings)
    {
        var reusable = new Dictionary<string, Control>(StringComparer.Ordinal);

        foreach (var id in view.SuilFieldIds)
        {
            if (view.SuilGetField(id) is Control control && !ReferenceEquals(control, root))
                reusable[id] = control;
        }

        var previous = new Dictionary<string, PlannedElement>(StringComparer.Ordinal);

        if (state.Plan != null)
            Collect(state.Plan, RootKey, previous);

        ApplyElement(view, root, plan, RootKey, previous, state, reusable, errors, warnings);

        foreach (var (id, _) in reusable)
        {
            warnings.Add($"the id '{id}' is not in the file any more: its field keeps the old control, which is out of the tree");
        }
    }

    private static void Collect(PlannedElement element, string key, Dictionary<string, PlannedElement> into)
    {
        into[key] = element;

        foreach (var child in element.Children)
        {
            if (child.Id != null)
                Collect(child, child.Id, into);
        }
    }

    private void ApplyElement(
        ISuilView view,
        Control control,
        PlannedElement plan,
        string? key,
        Dictionary<string, PlannedElement> previous,
        ViewState state,
        Dictionary<string, Control> reusable,
        List<string> errors,
        List<string> warnings)
    {
        // an element without an id is always new, so there is nothing to compare it with
        PlannedElement? before = null;

        if (key != null)
            previous.TryGetValue(key, out before);

        ApplyIdentity(control, plan, before);
        ApplyProperties(control, plan, before, key, state, errors);

        // Everything below is built again. The controls that are kept are taken out first, so nothing is added twice.
        // The children go where XAML would put them: the root's place is the one the generated code used, the others
        // are new or already built, and have their XamlChildren as they were left by the constructor.
        var target = ReferenceEquals(control, view) && view.SuilRootChildren is ICollection<Control> root
            ? root
            : control.XamlChildren;

        // An element written without a body has no children in the file: whatever it holds is its own (the parts of a
        // ChatBox, the tree of another view) or was put there by the code (the setup windows of the lobby), and is not
        // touched. The children are rebuilt only where the file has them, now or before.
        if (plan.Children.Count > 0 || before?.Children.Count > 0 || ReferenceEquals(control, view))
        {
            foreach (var child in new List<Control>(target))
            {
                target.Remove(child);
            }
        }

        if (plan.HasContentSlot)
            state.Content = target;

        foreach (var childPlan in plan.Children)
        {
            Control child;
            if (childPlan.Id != null && reusable.Remove(childPlan.Id, out var existing))
            {
                if (existing.GetType() == childPlan.Type)
                {
                    existing.Orphan();
                    child = existing;
                }
                else
                {
                    // the type of the element changed: the field accepts the new type (it was checked), the control is new
                    warnings.Add($"'{childPlan.Id}' is {childPlan.Type.Name} now (it was {existing.GetType().Name}): the control is new, " +
                                 "what the code subscribed to the old one is lost");
                    child = Create(childPlan.Type);
                    state.Originals.Remove(childPlan.Id);
                }
            }
            else
            {
                child = Create(childPlan.Type);

                if (childPlan.Id != null)
                    state.Originals.Remove(childPlan.Id);
            }

            ApplyElement(view, child, childPlan, childPlan.Id, previous, state, reusable, errors, warnings);

            target.Add(child);

            if (childPlan.Id != null && !view.SuilSetField(childPlan.Id, child) && view.SuilFieldType(childPlan.Id) != null)
                errors.Add($"the field of '{childPlan.Id}' did not accept {child.GetType().Name}");
        }
    }

    private Control Create(Type type) => (Control) _factory.CreateInstance(type);

    private static void ApplyIdentity(Control control, PlannedElement plan, PlannedElement? before)
    {
        if (control is UiElement element)
        {
            if (plan.Id != null || before?.Id != null)
                element.SuilStyleIdentifier = plan.Id;

            if (before != null)
            {
                foreach (var cls in before.Classes)
                {
                    if (!plan.Classes.Contains(cls))
                        element.RemoveSuilStyleClass(cls);
                }
            }

            foreach (var cls in plan.Classes)
            {
                element.AddSuilStyleClass(cls);
            }

            return;
        }

        if (plan.Id != null || before?.Id != null)
            control.Name = plan.Id;

        if (before != null)
        {
            foreach (var cls in before.Classes)
            {
                if (!plan.Classes.Contains(cls))
                    control.RemoveStyleClass(cls);
            }
        }

        foreach (var cls in plan.Classes)
        {
            control.AddStyleClass(cls);
        }
    }

    private void ApplyProperties(
        Control control,
        PlannedElement plan,
        PlannedElement? before,
        string? key,
        ViewState state,
        List<string> errors)
    {
        Dictionary<string, object?>? originals = null;

        // what the file had set and doesn't any more goes back to what the control had before the file set it, and
        // is the styles' again
        if (before != null)
        {
            if (key != null)
                state.Originals.TryGetValue(key, out originals);

            foreach (var old in before.Properties)
            {
                if (plan.Properties.Any(p => p.Name == old.Name))
                    continue;

                if (control is UiElement element)
                    element.SuilSetHard(old.Name, false);

                if (originals == null || !originals.TryGetValue(old.Name, out var original))
                    continue;

                Set(control, old.Accessor, original, old.Name, errors);
                originals.Remove(old.Name);
            }
        }

        foreach (var property in plan.Properties)
        {
            if (key != null && property.Accessor.Getter is { } getter)
            {
                if (!state.Originals.TryGetValue(key, out originals))
                    state.Originals[key] = originals = new Dictionary<string, object?>(StringComparer.Ordinal);

                if (!originals.ContainsKey(property.Name))
                    originals[property.Name] = getter(control);
            }

            Set(control, property.Accessor, property.Value, property.Name, errors);

            // as in the generated code: what the file assigns is out of reach of the styles, unless it is soft
            if (control is UiElement styled)
                styled.SuilSetHard(property.Name, !property.Soft);
        }
    }

    private static void Set(Control control, SuilStyleAccessor accessor, object? value, string name, List<string> errors)
    {
        try
        {
            accessor.Setter(control, value!);
        }
        catch (Exception e)
        {
            errors.Add($"'{name}' could not be set: {e.Message}");
        }
    }
}
