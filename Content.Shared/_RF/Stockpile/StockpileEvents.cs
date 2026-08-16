using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Stockpile;

/// <summary>
/// An event raised when an entity is stored in the stockpile.
/// It is raised for both the stock and the entity that was stored.
/// </summary>
/// <param name="StockUid">Stockpile entity.</param>
/// <param name="Inserted">Inserted entity.</param>
[PublicAPI]
public readonly record struct StockEntityInserted(
    EntityUid StockUid,
    EntityUid Inserted);

/// <summary>
/// An event raised when an entity is removed from the stock.
/// It is raised for both the stock and the entity removed from the stock.
/// </summary>
/// <param name="StockUid">Stockpile entity.</param>
/// <param name="Removed">Removed entity.</param>
[PublicAPI]
public readonly record struct StockEntityRemoved(
    EntityUid StockUid,
    EntityUid Removed);

/// <summary>
/// An event triggered when the setting for the maximum number
/// of entities of a specific type in the stockpile is changed.
/// </summary>
/// <param name="StockUid">Stockpile entity.</param>
/// <param name="ProtoId">Entity type.</param>
/// <param name="OldSetting">Old settings.</param>
/// <param name="NewSetting">New settings.</param>
[PublicAPI]
public readonly record struct StockSettingsChanged(
    EntityUid StockUid,
    EntProtoId ProtoId,
    int OldSetting,
    int NewSetting);

/// <summary>
/// An event raised when a supply connection between stockpiles is added.
/// </summary>
/// <param name="Supplier">Supplier stockpile entity.</param>
/// <param name="Supplied">Supplied stockpile entity.</param>
[PublicAPI]
public readonly record struct StockpileSupplyingAdded(
    EntityUid Supplier,
    EntityUid Supplied);

/// <summary>
/// An event raised when a supply connection between stockpiles is removed.
/// </summary>
/// <param name="Supplier">Supplier stockpile entity.</param>
/// <param name="Supplied">Supplied stockpile entity.</param>
[PublicAPI]
public readonly record struct StockpileSupplyingRemoved(
    EntityUid Supplier,
    EntityUid Supplied);

#region Net messages

[Serializable, NetSerializable]
public sealed class StockpileCreateRequest(NetEntity gridUid, HashSet<Vector2i> tiles) : EntityEventArgs
{
    public NetEntity GridUid = gridUid;
    public HashSet<Vector2i> Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class StockpileCreated(NetEntity uid) : EntityEventArgs
{
    public NetEntity Uid = uid;
}

[Serializable, NetSerializable]
public sealed class StockpileDeleted(NetEntity uid) : EntityEventArgs
{
    public NetEntity Uid = uid;
}

[Serializable, NetSerializable]
public sealed class StockpileTileAdded(NetEntity uid, HashSet<Vector2i> tiles) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public HashSet<Vector2i> Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class StockpileTileRemoved(NetEntity uid, HashSet<Vector2i> tiles) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public HashSet<Vector2i> Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class StockpileSettingUpdated(NetEntity uid, EntProtoId protoId, int value) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public EntProtoId ProtoId = protoId;
    public int Value = value;
}

[Serializable, NetSerializable]
public sealed class StockpileSettingsUpdated(NetEntity uid, Dictionary<EntProtoId, int> settings) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public Dictionary<EntProtoId, int> Settings = settings;
}

[Serializable, NetSerializable]
public sealed class StockpileSuppliedAdded(NetEntity supplier, NetEntity supplied) : EntityEventArgs
{
    public NetEntity Supplier = supplier;
    public NetEntity Supplied = supplied;
}

[Serializable, NetSerializable]
public sealed class StockpileSuppliedRemoved(NetEntity supplier, NetEntity supplied) : EntityEventArgs
{
    public NetEntity Supplier = supplier;
    public NetEntity Supplied = supplied;
}

[Serializable, NetSerializable]
public sealed class StockpileColorSet(NetEntity uid, Color color) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public Color Color = color;
}

[Serializable, NetSerializable]
public sealed class StockpileContentUpdated(NetEntity uid) : EntityEventArgs
{
    public NetEntity Uid = uid;
}

#endregion
