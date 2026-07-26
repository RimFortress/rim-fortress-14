using Content.Shared._RF.Selection.Systems;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Selection.Components;

/// <summary>
/// This is used to store the settings for the player's current selection.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedSelectionSystem))]
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

    /// <summary>
    /// Entities within the boundaries of the selection frame.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> Selected = new();

    /// <summary>
    /// Tiles within the boundaries of the selection frame.
    /// </summary>
    [ViewVariables]
    public HashSet<TileRef> SelectedTiles = new();

    /// <summary>
    /// Will information about the current status of the selection be sent to the server?
    /// </summary>
    [ViewVariables]
    public bool NetSync;

    /// <summary>
    /// Selection drawing color
    /// </summary>
    [ViewVariables]
    public Color SelectionColor = Color.White;

    /// <summary>
    /// A function that filters entities for selection
    /// </summary>
    [ViewVariables]
    public Func<EntityUid, bool>? SelectionFilter;

    /// <summary>
    /// A function that filters tiles for selection
    /// </summary>
    [ViewVariables]
    public Func<TileRef, bool>? TileSelectionFilter;

    /// <summary>
    /// Action taken when the selection is completed, if selection mode in Entity
    /// </summary>
    [ViewVariables]
    public Action<HashSet<EntityUid>>? OnSelected;

    /// <summary>
    /// Action taken when the selection is completed, if selection mode in Tile
    /// </summary>
    [ViewVariables]
    public Action<HashSet<TileRef>>? OnTileSelected;

    /// <summary>
    /// The action performed on selected entities when the right mouse button is pressed
    /// </summary>
    [ViewVariables]
    public Action<(HashSet<EntityUid> Selected, EntityUid? ActUid, EntityCoordinates ActCoords)>? Act;

    /// <summary>
    /// The action performed on selected tiles when the right mouse button is pressed
    /// </summary>
    [ViewVariables]
    public Action<(HashSet<TileRef> Selected, EntityCoordinates ActCoords)>? TileAct;

    /// <summary>
    /// An icon that will be drawn next to the mouse cursor
    /// </summary>
    [ViewVariables]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Color of the icon that will be drawn next to the mouse cursor
    /// </summary>
    [ViewVariables]
    public Color IconColor = Color.White;

    /// <summary>
    /// Current selection mode
    /// </summary>
    [ViewVariables]
    public SelectionMode Mode = SelectionMode.Entity;
}

public enum SelectionMode : byte
{
    /// <summary>
    ///  Only entities can be selected.
    /// </summary>
    Entity = 0,

    /// <summary>
    ///  Only tiles can be selected.
    /// </summary>
    Tile = 1,
}

[Serializable, NetSerializable]
public sealed class SelectionEntityDeltaMessage(
    HashSet<NetEntity>? added,
    HashSet<NetEntity>? removed) : EntityEventArgs
{
    public HashSet<NetEntity>? Added = added;
    public HashSet<NetEntity>? Removed = removed;
}

[Serializable, NetSerializable]
public sealed class SelectionClearedMessage : EntityEventArgs;
