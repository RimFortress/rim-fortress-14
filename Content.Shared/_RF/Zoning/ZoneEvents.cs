using Content.Shared._RF.Zoning.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Zoning;

[ByRefEvent]
public record struct ZoneAddTileCheck<T>(T Condition, TileRef Tile, bool Result)
    where T : BaseZoneCondition<T>;

[ByRefEvent]
public record struct ZoneTileCheck<T>(T Condition, IReadOnlySet<TileRef> Tiles, bool Result)
    where T : BaseZoneCondition<T>;

[ByRefEvent]
public record struct ZoneEntityCheck<T>(T Condition, IReadOnlySet<EntityUid> Entities, bool Result)
    where T : BaseZoneCondition<T>;

[ByRefEvent]
public record struct GetZoneTileAddConditionDescription<T>(T Condition, TileRef Tile, ZoneConditionDesc Result)
    where T : BaseZoneCondition<T>;

[ByRefEvent]
public record struct GetZoneTileConditionDescription<T>(T Condition, IReadOnlySet<TileRef> Tiles, ZoneConditionDesc Result)
    where T : BaseZoneCondition<T>;

[ByRefEvent]
public record struct GetZoneEntityConditionDescription<T>(T Condition, IReadOnlySet<EntityUid> Entities, ZoneConditionDesc Result)
    where T : BaseZoneCondition<T>;

/// <summary>
/// Raised for each new tile when the zone expands.
/// </summary>
/// <param name="Tile">Added tile.</param>
[PublicAPI]
public readonly record struct ZoneTileAdded(TileRef Tile);

/// <summary>
/// Raised every time a tile is removed from the zone.
/// </summary>
/// <remarks>
/// This is not raised for tiles removed during the zone deletion.
/// </remarks>
/// <param name="Tile">Removed tile.</param>
[PublicAPI]
public readonly record struct ZoneTileRemoved(TileRef Tile);

/// <summary>
/// An event raised before an entity enters a zone so that other systems can cancel it.
/// </summary>
/// <param name="Uid">An entity that enters the zone.</param>
/// <param name="Tile">
/// The tile of the zone in which the entity is located.
/// Not null if <see cref="ZoneComponent.CollisionMode"/> is <see cref="ZoneCollisionMode.Tile"/>.
/// </param>
/// <param name="Canceled">Was the entity's entrance canceled?</param>
[ByRefEvent]
public record struct BeforeZoneEnter(EntityUid Uid, TileRef? Tile, bool Canceled = false)
{
    public void Cancel()
    {
        Canceled = true;
    }
}

/// <summary>
/// An event raised when an entity enters a zone. It is raised both
/// for the zone entity and for the entity that entered the zone.
/// </summary>
/// <param name="ZoneUid">Zone entity.</param>
/// <param name="Tile">
/// The tile of the zone in which the entity is located.
/// Not null if <see cref="ZoneComponent.CollisionMode"/> is <see cref="ZoneCollisionMode.Tile"/>.
/// </param>
/// <param name="Uid">Entered entity.</param>
[PublicAPI]
public record struct EntityEnteredZone(EntityUid ZoneUid, TileRef? Tile, EntityUid Uid);

/// <summary>
/// An event raised when an entity leaves a zone. It is raised both
/// for the zone entity and for the entity that leaved the zone.
/// </summary>
/// <param name="ZoneUid">Zone entity.</param>
/// <param name="Tile">
/// The tile of the zone in which the entity is located.
/// Not null if <see cref="ZoneComponent.CollisionMode"/> is <see cref="ZoneCollisionMode.Tile"/>.
/// </param>
/// <param name="Uid">Leaved entity.</param>
[PublicAPI]
public record struct EntityLeavedZone(EntityUid ZoneUid, TileRef? Tile, EntityUid Uid);

/// <summary>
/// An event raised when a zone becomes invalid (the zone's conditions are not met).
/// </summary>
[PublicAPI]
public record struct ZoneInvalid;

/// <summary>
/// An event raised when a zone becomes valid (the zone's conditions are met).
/// </summary>
[PublicAPI]
public record struct ZoneValid;

// Client events

/// <summary>
/// An event raised on the client when a zone created by selecting
/// an area has been created on the server and sent to the client.
/// </summary>
/// <param name="Handled">Has this event been handled by other systems?</param>
[ByRefEvent]
public record struct ZoneCreated(bool Handled = false)
{
    public void Handle()
    {
        Handled = true;
    }
}

/// <summary>
/// An event raised on the client upon clicking a zone in edit mode.
/// It allows other systems to open different user interfaces for different types of zones.
/// </summary>
/// <param name="Handled">Has this event been handled by other systems?</param>
[ByRefEvent]
public record struct ZonePicked(bool Handled = false)
{
    public void Handle()
    {
        Handled = true;
    }
}

/// <summary>
/// An event raised on the client when zone picking mode is canceled.
/// </summary>
[PublicAPI]
public record struct ZonePickingCancel;

#region NetMessages

/// <summary>
/// Client request to create a zone.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZoneCreateRequest : EntityEventArgs
{
    /// <summary>
    /// Zone type.
    /// </summary>
    public EntProtoId<ZoneComponent> Type;

    /// <summary>
    /// The grid on which the zone will be created.
    /// </summary>
    public NetEntity GridUid;

    /// <summary>
    /// The tiles on which the zone will be created.
    /// </summary>
    public HashSet<Vector2i> Tiles = new();
}

/// <summary>
/// Client request to delete a zone.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZoneDeleteRequest : EntityEventArgs
{
    /// <summary>
    /// Zone entity.
    /// </summary>
    public NetEntity Uid;
}

/// <summary>
/// Client request to add tiles to the zone.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZoneTileAddRequest : EntityEventArgs
{
    /// <summary>
    /// Zone entity.
    /// </summary>
    public NetEntity Uid;

    /// <summary>
    /// Tiles to add.
    /// </summary>
    public HashSet<Vector2i> Tiles = new();
}

/// <summary>
/// Client request to remove tiles to the zone.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZoneTileRemoveRequest : EntityEventArgs
{
    /// <summary>
    /// Zone entity.
    /// </summary>
    public NetEntity Uid;

    /// <summary>
    /// Tiles to remove.
    /// </summary>
    public HashSet<Vector2i> Tiles = new();
}

/// <summary>
/// Client request to change the zone name.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZoneNameChangeRequest : EntityEventArgs
{
    /// <summary>
    /// Zone entity.
    /// </summary>
    public NetEntity Uid;

    /// <summary>
    /// New zone Name.
    /// </summary>
    public string Name = string.Empty;
}

/// <summary>
/// Client request to change the appearance of the zone.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZoneVisualsChangeRequest : EntityEventArgs
{
    /// <summary>
    /// Zone entity.
    /// </summary>
    public NetEntity Uid;

    /// <summary>
    /// New zone visuals.
    /// </summary>
    public Dictionary<ZoneVisualsState, ZoneVisualsStyle> States = new();
}

#endregion
