using Content.Shared._RF.Zoning.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Zoning.Components;

/// <summary>
/// Zone entity component.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class ZoneComponent : Component
{
    /// <summary>
    /// A prototype with all zone settings.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ZonePrototype> Type;

    /// <summary>
    /// Are all conditions for creating the zone met?
    /// </summary>
    public bool Valid => TilesValid && EntitiesValid;

    [AutoNetworkedField]
    public bool TilesValid;

    [AutoNetworkedField]
    public bool EntitiesValid;

    /// <summary>
    /// A list of all tiles assigned to the zone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<Vector2i> Tiles = new();

    /// <summary>
    /// Entities that have entered the zone.
    /// </summary>
    /// <remarks>
    /// This list does not contain all entities within the zone's boundaries,
    /// but only those that have passed the whitelist process and been approved by other systems.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Entities = new();

    /// <summary>
    /// List all fixtures in the mono zone. Yes, there may be several of them if the tiles are
    /// divided into unconnected regions. Not empty if <see cref="ZonePrototype.CollisionMode"/>
    /// is <see cref="ZoneCollisionMode.Mono"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public HashSet<string> MonoFixtures = new();

    /// <summary>
    /// A dictionary with the fixtures for each stockpile tile, not empty if
    /// <see cref="ZonePrototype.CollisionMode"/> is <see cref="ZoneCollisionMode.Tile"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<Vector2i, string> TileFixtures = new();

    public const string MonoFixtureId = "zone_mono_";

    public const string TileFixtureId = "zone_tile_";
}
