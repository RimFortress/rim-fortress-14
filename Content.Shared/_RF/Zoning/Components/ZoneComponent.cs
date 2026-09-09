using Content.Shared.Physics;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Zoning.Components;

/// <summary>
/// Zone entity component.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)]
public sealed partial class ZoneComponent : Component
{
    /// <inheritdoc cref="ZoneCollisionMode"/>
    [DataField, AutoNetworkedField]
    public ZoneCollisionMode CollisionMode = ZoneCollisionMode.Mono;

    /// <inheritdoc cref="ZoneSplitMode"/>
    [DataField, AutoNetworkedField]
    public ZoneSplitMode SplitMode = ZoneSplitMode.None;

    /// <summary>
    /// A whitelist of entities with which collisions will be tracked.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? CollisionWhitelist;

    /// <summary>
    /// Conditions related to the zone's tiles/entities that
    /// must be met for the zone to be created or to remain valid.
    /// </summary>
    [DataField]
    public Dictionary<ZoneConditionType, List<ZoneCondition>> Conditions = new();

    /// <summary>
    /// Is it allowed to create a zone if not all zone creation conditions are met?
    /// However, such a zone will be considered invalid until the conditions are met.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InvalidCreation;

    /// <summary>
    /// Zone collision layer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public CollisionGroup Layer = CollisionGroup.Impassable;

    /// <summary>
    /// Zone collision mask.
    /// </summary>
    [DataField, AutoNetworkedField]
    public CollisionGroup Mask = CollisionGroup.Impassable;

    /// <summary>
    /// Are all conditions for creating the zone met?
    /// </summary>
    public bool Valid => TilesValid && EntitiesValid;

    [ViewVariables, AutoNetworkedField]
    public bool TilesValid;

    [ViewVariables, AutoNetworkedField]
    public bool EntitiesValid;

    /// <summary>
    /// A list of all tiles assigned to the zone.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public HashSet<Vector2i> Tiles = new();

    /// <summary>
    /// Entities that have entered the zone.
    /// </summary>
    /// <remarks>
    /// This list does not contain all entities within the zone's boundaries,
    /// but only those that have passed the whitelist process and been approved by other systems.
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public HashSet<EntityUid> Entities = new();

    /// <summary>
    /// List all fixtures in the mono zone. Yes, there may be several of them if the tiles are
    /// divided into unconnected regions. Not empty if <see cref="CollisionMode"/>
    /// is <see cref="ZoneCollisionMode.Mono"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public HashSet<string> MonoFixtures = new();

    /// <summary>
    /// A dictionary with the fixtures for each stockpile tile, not empty if
    /// <see cref="CollisionMode"/> is <see cref="ZoneCollisionMode.Tile"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<Vector2i, string> TileFixtures = new();

    public const string MonoFixtureId = "zone_mono_";

    public const string TileFixtureId = "zone_tile_";
}

/// <summary>
/// Mode for tracking entities entering and exiting a zone.
/// </summary>
[Serializable, NetSerializable]
public enum ZoneCollisionMode : byte
{
    /// <summary>
    /// Zone does not track collisions with entities.
    /// </summary>
    None,

    /// <summary>
    /// The zone tracks when entities enter and exit the zone.
    /// </summary>
    Mono,

    /// <summary>
    /// The zone tracks when entities leave the zone and enter specific tiles within the zone.
    /// </summary>
    Tile,
}

/// <summary>
/// Behavior mode of a zone when not all of its tiles are connected.
/// </summary>
[Serializable, NetSerializable]
public enum ZoneSplitMode : byte
{
    /// <summary>
    /// The zone will not be divided into regions; it will always remain a single entity.
    /// </summary>
    None,

    /// <summary>
    /// Separate tile regions will be removed, leaving only the single, largest region.
    /// </summary>
    Delete,

    /// <summary>
    /// Separate tile regions will be split into separate zones.
    /// </summary>
    Split,
}
