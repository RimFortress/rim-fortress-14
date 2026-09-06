using Content.Shared._RF.Selection.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client._RF.Selection;

public partial class SelectionSystem
{
    /// <summary>
    /// Sets the settings for player entity selection.
    /// </summary>
    /// <param name="act"><see cref="Selection{T}.Act"/></param>
    /// <param name="color"><see cref="Selection{T}.Color"/></param>
    /// <param name="filter"><see cref="Filter"/></param>
    /// <param name="onSelected"><see cref="Selection{T}.OnSelected"/></param>
    /// <param name="icon"><see cref="Selection{T}.Icon"/></param>
    /// <param name="iconColor"><see cref="Selection{T}.IconColor"/></param>
    /// <param name="allowedModes"><see cref="Selection{T}.AllowedModes"/></param>
    /// <param name="default"></param>
    [PublicAPI]
    public void SetSelection(
        SelectionActionHandler<EntityUid>? act = null,
        Color? color = null,
        Func<EntityUid, bool>? filter = null,
        Action<HashSet<EntityUid>>? onSelected = null,
        SpriteSpecifier? icon = null,
        Color? iconColor = null,
        SelectionMode[]? allowedModes = null,
        bool @default = false)
        => SetSelection(Selection<EntityUid>.FromDefault(
                act: act,
                color: color,
                filter: filter,
                onSelected: onSelected,
                icon: icon,
                iconColor: iconColor,
                allowedModes: allowedModes),
            @default);

    /// <summary>
    /// Sets the settings for player tile selection.
    /// </summary>
    /// <param name="act"><see cref="Selection{T}.Act"/></param>
    /// <param name="color"><see cref="Selection{T}.Color"/></param>
    /// <param name="filter"><see cref="Selection{T}.Filter"/></param>
    /// <param name="onSelected"><see cref="Selection{T}.OnSelected"/></param>
    /// <param name="icon"><see cref="Selection{T}.Icon"/></param>
    /// <param name="iconColor"><see cref="Selection{T}.IconColor"/></param>
    /// <param name="allowedModes"><see cref="Selection{T}.AllowedModes"/></param>
    /// <param name="default"></param>
    [PublicAPI]
    public void SetSelection(
        SelectionActionHandler<TileRef>? act = null,
        Color? color = null,
        Func<TileRef, bool>? filter = null,
        Action<HashSet<TileRef>>? onSelected = null,
        SpriteSpecifier? icon = null,
        Color? iconColor = null,
        SelectionMode[]? allowedModes = null,
        bool @default = false)
        => SetSelection(Selection<TileRef>.FromDefault(
                act: act,
                color: color,
                filter: filter,
                onSelected: onSelected,
                icon: icon,
                iconColor: iconColor,
                allowedModes: allowedModes),
            @default);

    /// <summary>
    /// Sets the selection to the default.
    /// </summary>
    /// <typeparam name="T">Selection type.</typeparam>
    [PublicAPI]
    public void SetDefault<T>() where T : struct
    {
        if (TryComp(_player.LocalEntity, out SelectionComponent? comp))
            SetDefault<T>(new(_player.LocalEntity.Value, comp));
    }

    /// <summary>
    /// Adds an entity to the player's current selection.
    /// </summary>
    [PublicAPI]
    public bool Select(EntityUid uid)
        => TryComp(_player.LocalEntity, out SelectionComponent? comp)
           && Select(new(_player.LocalEntity.Value, comp), uid);

    /// <summary>
    /// Removes the entity from the player's current selection.
    /// </summary>
    [PublicAPI]
    public bool DeSelect(EntityUid uid)
        => TryComp(_player.LocalEntity, out SelectionComponent? comp)
           && DeSelect(new(_player.LocalEntity.Value, comp), uid);

    /// <summary>
    /// Clears all entities selected by the user.
    /// </summary>
    [PublicAPI]
    public void ClearSelection()
    {
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp))
            return;

        ClearSelection(new(_player.LocalEntity.Value, comp));
    }

    /// <summary>
    /// Returns a list of objects in the player's selection.
    /// </summary>
    [PublicAPI]
    public IReadOnlySet<T> Selected<T>() where T : struct
        => GetSelection<T>() is { } selection
            ? selection.Selected
            : new HashSet<T>();

    [PublicAPI]
    public ISelection? GetSelection()
        => TryComp(_player.LocalEntity, out SelectionComponent? comp)
           ? comp.CurrentSelection
           : null;
}
