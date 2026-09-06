using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Selection.Components;

/// <summary>
/// This is used to store the settings for the player's current selection.
/// </summary>
[RegisterComponent]
public sealed partial class SelectionComponent : Component
{
    /// <summary>
    /// Selection frame start point
    /// </summary>
    [ViewVariables]
    public MapCoordinates? StartPoint;

    /// <summary>
    /// Selection frame endpoint
    /// </summary>
    [ViewVariables]
    public MapCoordinates? EndPoint;

    [ViewVariables]
    public ISelection? CurrentSelection;

    [ViewVariables]
    public ISelection? DefaultSelection;
}

/// <summary>
/// A class that stores all user selection settings.
/// </summary>
/// <param name="Selected"><typeparamref name="T"/> within the boundaries of the selection frame.</param>
/// <param name="Color">Selection drawing color.</param>
/// <param name="Filter">A function that filters <typeparamref name="T"/> in selection</param>
/// <param name="OnSelected">Action taken when the selection is completed.</param>
/// <param name="Act">The action that will be invoked when the action button is clicked.</param>
/// <param name="Icon">An icon that will be drawn next to the mouse cursor.</param>
/// <param name="IconColor">Color of the icon that will be drawn next to the mouse cursor.</param>
/// <param name="AllowedModes">Allowed selection modes.</param>
/// <param name="CurrentMode">Current selection modes.</param>
/// <typeparam name="T">The type of object that would be included in the selection.</typeparam>
public record struct Selection<T>(
    HashSet<T> Selected,
    HashSet<T> DragBase,
    Color Color,
    Func<T, bool>? Filter,
    Action<HashSet<T>>? OnSelected,
    SelectionActionHandler<T>? Act,
    SpriteSpecifier? Icon,
    Color IconColor,
    SelectionMode[] AllowedModes,
    SelectionMode CurrentMode)
    : ISelection where T : struct
{
    public static readonly Selection<T> Defaults = new(
        Selected: new(),
        DragBase: new(),
        Color: Color.LightGray,
        Filter: null,
        OnSelected: null,
        Act: null,
        Icon: null,
        IconColor: Color.LightGray,
        AllowedModes: [SelectionMode.Default],
        CurrentMode: SelectionMode.Default);

    public static Selection<T> FromDefault(
        Color? color = null,
        Func<T, bool>? filter = null,
        Action<HashSet<T>>? onSelected = null,
        SelectionActionHandler<T>? act = null,
        SpriteSpecifier? icon = null,
        Color? iconColor = null,
        SelectionMode[]? allowedModes = null)
        => new(Selected: new(),
            DragBase: new(),
            Color: color ?? Defaults.Color,
            Filter: filter ?? Defaults.Filter,
            OnSelected: onSelected ?? Defaults.OnSelected,
            Act: act ?? Defaults.Act,
            Icon: icon ?? Defaults.Icon,
            IconColor: iconColor ?? Defaults.IconColor,
            AllowedModes: allowedModes ?? Defaults.AllowedModes,
            CurrentMode: Defaults.CurrentMode);

    /// <summary>
    /// Returns a copy with independent Selected/DragBase sets, so assigning this to
    /// CurrentSelection can never alias (and later corrupt) a stored template such
    /// as DefaultSelection.
    /// </summary>
    public Selection<T> Cloned() => this with
    {
        Selected = new HashSet<T>(Selected),
        DragBase = new HashSet<T>(DragBase),
    };

    /// <inheritdoc/>
    public void CaptureDragBase()
    {
        DragBase.Clear();
        DragBase.UnionWith(Selected);
    }
}

public interface ISelection
{
    Color Color { get; set; }

    SpriteSpecifier? Icon { get; set; }

    Color IconColor { get; set; }

    SelectionMode[] AllowedModes { get; set; }

    SelectionMode CurrentMode { get; set; }

    /// <summary>
    /// Snapshots the current <c>Selected</c> set into <c>DragBase</c>. Call this once,
    /// when a new selection drag begins, so Append/Remove mode logic has a stable
    /// starting point for the duration of the drag, independent of the generic type.
    /// </summary>
    void CaptureDragBase();
}

/// <summary>
/// The action that will be invoked when the action button is clicked.
/// </summary>
/// <param name="selected"><typeparamref name="T"/> within the boundaries of the selection frame.</param>
/// <param name="target"><typeparamref name="T"/> under the cursor when the action is invoked.</param>
/// <param name="actCoords">Cursor coordinates when the action is invoked.</param>
/// <typeparam name="T">The type of object that would be included in the selection.</typeparam>
public delegate void SelectionActionHandler<T>(
    HashSet<T> selected,
    T? target,
    EntityCoordinates actCoords)
    where T : struct;

public enum SelectionMode : byte
{
    /// <summary>
    /// The default selection mode, in which previously selected
    /// entities/tiles are cleared when a new selection begins.
    /// </summary>
    Default = 0,

    /// <summary>
    /// A selection mode in which selected entities are added to the previous ones.
    /// </summary>
    Append = 1,

    /// <summary>
    /// A selection mode in which selected entities are removed from the previous selection.
    /// </summary>
    Remove = 2,
}
