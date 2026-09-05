using Content.Shared.Physics;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Prototypes;

/// <summary>
/// A prototype of a zone that a player can create.
/// </summary>
[Prototype]
public sealed partial class ZonePrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ZonePrototype>))]
    public string[]? Parents { get; set; }

    /// <inheritdoc/>
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; set; }

    public LocId Name => $"zone-{CaseConversion.PascalToKebab(ID)}-name";

    /// <summary>
    /// A prototype of the entity that will be created as a zone entity.
    /// </summary>
    [DataField]
    public EntProtoId Entity = "BaseZone";

    /// <inheritdoc cref="ZoneCollisionMode"/>
    [DataField]
    public ZoneCollisionMode CollisionMode = ZoneCollisionMode.Mono;

    /// <inheritdoc cref="ZoneSplitMode"/>
    [DataField]
    public ZoneSplitMode SplitMode = ZoneSplitMode.None;

    /// <summary>
    /// A whitelist of entities with which collisions will be tracked.
    /// </summary>
    [DataField]
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
    [DataField]
    public bool InvalidCreation;

    /// <summary>
    /// Zone collision layer.
    /// </summary>
    [DataField]
    public CollisionGroup Layer = CollisionGroup.Impassable;

    /// <summary>
    /// Zone collision mask.
    /// </summary>
    [DataField]
    public CollisionGroup Mask = CollisionGroup.Impassable;

    #region Visuals

    /// <summary>
    /// A sprite that will be rendered on every tile in the zone.
    /// </summary>
    [DataField]
    public SpriteSpecifier? TileSprite;

    /// <summary>
    /// A sprite that will be rendered on every tile in the zone, if the zone is selected by the player.
    /// </summary>
    [DataField]
    public SpriteSpecifier? SelectedTileSprite;

    /// <summary>
    /// The color that will be applied to all content within the zone.
    /// </summary>
    [DataField]
    public Color? ZoneColor;

    /// <summary>
    /// The color that will be applied to all content within the zone, if the zone is selected by the player.
    /// </summary>
    [DataField]
    public Color? SelectedZoneColor;

    /// <summary>
    /// The color used to draw the zone's border.
    /// </summary>
    [DataField]
    public Color? BorderColor;

    /// <summary>
    /// The color used to draw the zone's border, if the zone is selected by the player.
    /// </summary>
    [DataField]
    public Color? SelectedBorderColor;

    /// <summary>
    /// The thickness of the zone border, in world coordinates.
    /// </summary>
    [DataField]
    public float BorderThickness;

    /// <summary>
    /// The thickness of the zone's border, if the zone is selected by the player.
    /// </summary>
    [DataField]
    public float SelectedBorderThickness;

    #endregion
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

