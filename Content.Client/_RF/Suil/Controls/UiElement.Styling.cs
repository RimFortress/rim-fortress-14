using Content.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RF.Suil;

public partial class UiElement
{
    /// <summary>
    /// What the control had in the properties the styles have changed, before the first change: this is what a
    /// property goes back to when no rule sets it any more, and what <c>initial</c> is. Filled by the style manager.
    /// </summary>
    internal Dictionary<string, object?>? SuilOriginalValues;

    /// <summary>
    /// Remembers what the control has in a property before the styles (a rule or the inheritance) change it, once.
    /// </summary>
    internal void RememberOriginal(string name, SuilStyleAccessor accessor)
    {
        if (accessor.Getter is not { } getter)
            return;

        SuilOriginalValues ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        SuilOriginalValues.TryAdd(name, getter(this));
    }

    /// <summary>
    /// Applied-property bookkeeping for full UpdateStyle(control) calls.
    /// </summary>
    internal HashSet<string>? SuilLastAppliedProperties;

    /// <summary>
    /// A cleared set kept from the last style update to be filled by the next one, instead of making a new one.
    /// </summary>
    internal HashSet<string>? SuilSpareAppliedProperties;

    /// <summary>
    /// The control has changed since it was last restyled, see <see cref="ISuilManager.QueueStyleUpdate(UiElement)"/>.
    /// </summary>
    internal bool SuilStyleDirty;

    /// <summary>
    /// The control is in the queue of style updates (it can be there although nothing is dirty any more).
    /// </summary>
    internal bool SuilQueued;

    /// <summary>
    /// How far below the control the changes of its classes have yet to restyle other controls.
    /// </summary>
    internal SuilAncestorReach SuilPendingReach;

    /// <summary>
    /// The stylesheet the control was last styled with.
    /// </summary>
    internal SuilStylesheet? SuilComputedStylesheet;

    /// <summary>
    /// The SUIL style identifier for the control.
    /// </summary>
    /// <remarks>
    /// It is this field that supports SUIL, not <see cref="Control.StyleIdentifier"/>.
    /// </remarks>
    [ViewVariables]
    public string? SuilStyleIdentifier
    {
        get;
        set
        {
            var old = field;
            field = value;
            Suil.QueueStyleUpdate(this, "#" + old);
            Suil.QueueStyleUpdate(this, "#" + value);
        }
    }

    /// <summary>
    /// Queues the restyling of the control after its class changed, and of the controls whose rules look at this
    /// class on their ancestor.
    /// </summary>
    /// <param name="key">The class with its prefix, see <see cref="SuilStyleClassCollection"/>; <c>null</c> for any.</param>
    internal void RestyleSuil(string? key) => Suil.QueueStyleUpdate(this, key);

    /// <summary>
    /// Current control style classes. Can be set from XAML with the classes separated by spaces.
    /// </summary>
    [ViewVariables]
    public SuilStyleClassCollection SuilStyleClasses
    {
        get => field ??= new(this);
        set
        {
            field = new(value) { Owner = this };
            RestyleSuil(null);
        }
    }

    /// <summary>
    /// Current control pseudo style classes. Can be set from XAML with the classes separated by spaces.
    /// </summary>
    [ViewVariables]
    public SuilStyleClassCollection SuilPseudoStyleClasses
    {
        get => field ??= new(this, ":");
        set
        {
            field = new(value) { Owner = this, Prefix = ":" };
            RestyleSuil(null);
        }
    }

    [PublicAPI, Pure]
    public bool HasSuilPseudoStyleClass(string className) => SuilPseudoStyleClasses.Contains(className);

    [PublicAPI]
    protected void AddSuilPseudoStyleClass(string className)
    {
        if (SuilPseudoStyleClasses.Contains(className))
            return;

        SuilPseudoStyleClasses.Add(className);
    }

    [PublicAPI]
    protected void RemoveSuilPseudoStyleClass(string className)
    {
        SuilPseudoStyleClasses.Remove(className);
    }

    [PublicAPI, Pure]
    public bool HasSuilStyleClass(string className) => SuilStyleClasses.Contains(className);

    [PublicAPI]
    public void AddSuilStyleClass(string className)
    {
        if (SuilStyleClasses.Contains(className))
            return;

        SuilStyleClasses.Add(className);
    }

    [PublicAPI]
    public void RemoveSuilStyleClass(string className)
    {
        SuilStyleClasses.Remove(className);
    }

    /// <summary>
    /// Adds or removes a pseudo-class depending on <paramref name="value"/>.
    /// </summary>
    [PublicAPI]
    public void SetSuilPseudoStyleClass(string className, bool value)
    {
        if (value)
            AddSuilPseudoStyleClass(className);
        else
            RemoveSuilPseudoStyleClass(className);
    }
}
