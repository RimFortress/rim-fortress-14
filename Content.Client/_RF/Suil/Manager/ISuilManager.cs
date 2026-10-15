using Content.Client._RF.Suil;
using System.Diagnostics.CodeAnalysis;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Suil;

public interface ISuilManager : ISuilPropertyResolver, ISuilValues
{
    void Initialize();

    /// <summary>
    /// Reloads all SUIL files.
    /// </summary>
    void Reload();

    /// <summary>
    /// The path where SUIL files will be searched for.
    /// </summary>
    ResPath StylesPath { get; }

    /// <summary>
    /// A table of all loaded SUIL styles.
    /// </summary>
    SuilStylesheet Stylesheet { get; }

    /// <summary>
    /// Recursively loads all SUIL files in the directory.
    /// </summary>
    /// <param name="path">Directory path.</param>
    void LoadDirectory(ResPath path);

    /// <summary>
    /// Loads data from a SUIL file.
    /// </summary>
    /// <param name="path">File path.</param>
    void LoadFile(ResPath path);

    /// <summary>
    /// Loads rules written in SUIL for the tests.
    /// </summary>
    /// <param name="suil">The text of a .suil file; it may have <c>let</c>, templates and, with a source, <c>use</c>.</param>
    /// <param name="source">Where <c>use</c> takes the files from.</param>
    void LoadSuilString(string suil, ISuilFileSource? source = null);

    /// <summary>
    /// Creates a style sheet from all loaded SUIL files.
    /// </summary>
    SuilStylesheet BuildStylesheet();

    /// <summary>
    /// Creates a style sheet from the loaded test styles.
    /// </summary>
    SuilStylesheet BuildTestStylesheet();

    /// <summary>
    /// Rebuilds the style sheet and updates <see cref="IUserInterfaceManager.Stylesheet"/>.
    /// </summary>
    void UpdateStylesheet();

    /// <summary>
    /// Clears all SUIL rules loaded through <see cref="LoadSuilString"/>.
    /// </summary>
    void ClearTestStylesheet();

    /// <summary>
    /// Queues the restyling of the control, which happens once at the start of the next frame, before the layout, no
    /// matter how many times the control was queued before that. Prefer it to <see cref="UpdateStyle(UiElement)"/>
    /// whenever the result is not needed right away.
    /// </summary>
    void QueueStyleUpdate(UiElement control);

    /// <summary>
    /// Same as <see cref="QueueStyleUpdate(UiElement)"/>, after something that selectors match on changed on the
    /// control: the controls below it whose rules look at it in a combinator are restyled as well, see
    /// <see cref="UpdateDependents"/>.
    /// </summary>
    /// <param name="control">The control that changed.</param>
    /// <param name="key">
    /// What has changed: <c>.class</c>, <c>:pseudo-class</c> or <c>#id</c>; <c>null</c> if anything may have.
    /// </param>
    void QueueStyleUpdate(UiElement control, string? key);

    /// <summary>
    /// Restyles everything that was queued. Done every frame by itself.
    /// </summary>
    void FlushStyleUpdates();

    /// <summary>
    /// Restyles the control right now if it is queued, for when its styles are needed before the next frame.
    /// </summary>
    void FlushStyleUpdate(UiElement control);

    /// <summary>
    /// Updates the control styles that use specific selector.
    /// </summary>
    /// <param name="control">Control to update.</param>
    /// <typeparam name="T">Selector type.</typeparam>
    void UpdateStyle<T>(UiElement control) where T : Selector;

    /// <summary>
    /// Queues the restyling of the control if its rules depend on its siblings (<c>:nth-child</c>, <c>:only-child</c>
    /// and the like), after a sibling was added or removed. Does nothing for the controls that no rule does this for.
    /// </summary>
    void UpdateStyleOnSiblingsChanged(UiElement control);

    /// <summary>
    /// Same as <see cref="UpdateStyleOnSiblingsChanged"/>, after a sibling changed its place among the children
    /// of the parent: only the rules that depend on the place of the control count.
    /// </summary>
    void UpdateStyleOnSiblingsReordered(UiElement control);

    /// <summary>
    /// Updates the control styles that use specific selectors.
    /// </summary>
    /// <param name="control">Control to update.</param>
    /// <param name="selectors">Selector types.</param>
    void UpdateStyle(UiElement control, params Type[] selectors);

    /// <summary>
    /// Styles the control that has just entered the UI tree, without touching its descendants: they enter the tree
    /// after it, and are styled by their own call.
    /// </summary>
    /// <param name="control">Control to update.</param>
    void UpdateStyleOnEnter(UiElement control);

    /// <summary>
    /// Updates all control styles right now.
    /// </summary>
    /// <param name="control">Control to update.</param>
    void UpdateStyle(UiElement control);

    /// <summary>
    /// Restyles the controls below <paramref name="control"/> whose rules look at the control in a combinator
    /// (<c>.a > .b</c>, <c>.a .b</c>), after something that selectors match on changed on the control.
    /// It does nothing when no rule does, and walks only as deep as the rules reach.
    /// </summary>
    /// <param name="control">The control that changed. It is not restyled itself.</param>
    /// <param name="key">
    /// What has changed: <c>.class</c>, <c>:pseudo-class</c> or <c>#id</c>; <c>null</c> if anything may have.
    /// </param>
    void UpdateDependents(UiElement control, string? key);

    /// <summary>
    /// Updates control fields based on SUIL styles loaded during testing.
    /// </summary>
    /// <param name="control">Control to update.</param>
    void UpdateTestStyle(UiElement control);

    /// <summary>
    /// Recursively updates the SUIL styles of the control and all its child elements.
    /// </summary>
    /// <param name="control">The control from which the update will begin.</param>
    void UpdateStyleRecursively(Control control);

    /// <summary>
    /// Sets a SUIL custom property directly on a control (an inline declaration) and restyles
    /// the control and its descendants. See <see cref="UiElement.SetSuilVariable"/>.
    /// </summary>
    /// <param name="control">Control to set the variable on.</param>
    /// <param name="name">Variable name including the leading <c>--</c>.</param>
    /// <param name="suilValue">The value as SUIL text; may end with <c>!important</c>.</param>
    /// <param name="important">Mark the value <c>!important</c>.</param>
    /// <exception cref="ArgumentException">
    /// The name doesn't start with <c>--</c> or the value isn't valid SUIL
    /// (the latter is only logged when <c>EXCEPTION_TOLERANCE</c> is defined).
    /// </exception>
    void SetVariable(UiElement control, string name, string suilValue, bool important = false);

    /// <summary>
    /// Removes an inline custom property previously set with <see cref="SetVariable"/>.
    /// </summary>
    /// <returns><c>true</c> if an inline value was removed.</returns>
    bool RemoveVariable(UiElement control, string name);

    /// <summary>
    /// Reads the computed value of a custom property of a control (inline, stylesheet or inherited)
    /// and parses it into <typeparamref name="T"/>. Reflects the state after the control's last style update.
    /// </summary>
    /// <returns><c>false</c> if the variable is missing, invalid, or can't be parsed as <typeparamref name="T"/>.</returns>
    bool TryGetVariable<T>(UiElement control, string name, out T? value);
}

public interface ISuilPropertyResolver
{
    /// <summary>
    /// Returns the accessor for a SUIL property.
    /// </summary>
    /// <param name="elementType">The control type that contains this property.</param>
    /// <param name="propertyName">SUIL property name.</param>
    /// <param name="accessor">Property accessor.</param>
    /// <returns>True if an accessor is found.</returns>
    bool TryGetPropertyAccessor(
        Type? elementType,
        string propertyName,
        [NotNullWhen(true)] out SuilStyleAccessor? accessor);

    /// <summary>
    /// Returns every property accessor of a control type, base types included (the keys are SUIL names).
    /// </summary>
    IReadOnlyDictionary<string, SuilStyleAccessor> GetPropertyAccessors(Type elementType);
}

/// <summary>
/// A struct that provides access to Control fields via styles.
/// </summary>
/// <param name="ClrType">Type of the target field's value.</param>
/// <param name="Setter">An action triggered when the value of a property changes.</param>
/// <param name="Getter">A function used to retrieve the value of a property; <c>null</c> for a property that can only be set.</param>
/// <param name="CustomParser">The type of custom parser for the values; if null, the default parser is used.</param>
/// <param name="Inherited">Does a control without a value of its own take the one of its ancestor.</param>
public readonly record struct SuilStyleAccessor(
    Type ClrType,
    Action<Control, object> Setter,
    Func<Control, object?>? Getter,
    Type? CustomParser,
    bool Inherited = false);
