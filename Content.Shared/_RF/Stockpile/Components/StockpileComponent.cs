using Content.Shared._RF.Stockpile.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Stockpile.Components;

/// <summary>
/// A component of the stockpile entity.
/// </summary>
[Access(typeof(StockpileSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class StockpileComponent : Component
{
    /// <summary>
    /// The color with which the stockpile is rendered in the UI.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.DarkOrange;

    /// <summary>
    /// The maximum number of entities that can be in a single
    /// stockpile tile (not including entities in a container).
    /// </summary>
    [DataField]
    public int MaxTileEntities = 1;

    /// <summary>
    /// List of stockpiles supplied from this one.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public readonly HashSet<EntityUid> Supplied = new();

    /// <summary>
    /// List of stockpiles supplying this one.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public readonly HashSet<EntityUid> Suppliers = new();

    /// <summary>
    /// Settings for the maximum number of specific items in the stockpile.
    /// -1 means there is no limit.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    [Access(typeof(StockpileSystem))]
    public Dictionary<EntProtoId, int> Settings = new();

    /// <summary>
    /// A dictionary with the fixtures for each stockpile tile.
    /// </summary>
    [DataField]
    public Dictionary<Vector2i, string> TileFixtures = new();

    /// <summary>
    /// A list of all tiles assigned to the stockpile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<Vector2i> Tiles = new();

    /// <summary>
    /// A list of all entities in the stockpile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Stored = new();
}
