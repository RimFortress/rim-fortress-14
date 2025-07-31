using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Stockpile;

public abstract class SharedStockpileSystem : EntitySystem
{
    [Dependency] protected readonly SharedTransformSystem Xform = default!;
    [Dependency] protected readonly SharedMapSystem Map = default!;
    [Dependency] protected readonly TurfSystem Turf = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;

    protected readonly List<Stock> Stockpiles = new();

    private readonly Dictionary<EntProtoId, int> _defaultSettings = new();
    private int _nextStockpileId = 1;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<StockpileCategoryComponent, ComponentShutdown>(OnShutdown);

        SubscribeNetworkEvent<StockpileCreated>(OnCreated);
        SubscribeNetworkEvent<StockpileDeleted>(OnDeleted);
        SubscribeNetworkEvent<StockpileTileAdded>(OnTileAdded);
        SubscribeNetworkEvent<StockpileTileRemoved>(OnTileRemoved);
        SubscribeNetworkEvent<StockpileSettingUpdated>(OnSettingUpdate);
        SubscribeNetworkEvent<StockpileSettingsUpdated>(OnSettingsUpdate);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<EntityPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    #region Events

    protected void OnCreated(StockpileCreated ev, EntitySessionEventArgs args)
    {
        var gridUid = GetEntity(ev.GridUid);

        if (TryGetStock(ev.Id, out _)
            || !TryComp(gridUid, out MapGridComponent? grid)
            || args.SenderSession.AttachedEntity is not { } owner)
            return;

        var tileRefs = new HashSet<TileRef>();

        foreach (var ind in ev.Tiles)
        {
            if (Map.TryGetTileRef(gridUid, grid, ind, out var tileRef))
                tileRefs.Add(tileRef);
        }

        CreateStockpile(tileRefs, owner, ev.Id, false);
    }

    protected void OnDeleted(StockpileDeleted ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock) || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        DeleteStockpile(ev.Id, false);
    }

    protected void OnTileAdded(StockpileTileAdded ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock)
            || !TryComp(stock.GridUid, out MapGridComponent? grid)
            || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        var tileRefs = new HashSet<TileRef>();

        foreach (var ind in ev.Tiles)
        {
            if (Map.TryGetTileRef(stock.GridUid, grid, ind, out var tileRef))
                tileRefs.Add(tileRef);
        }

        AddTiles(tileRefs, stock, false);
    }

    protected void OnTileRemoved(StockpileTileRemoved ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock)
            || !TryComp(stock.GridUid, out MapGridComponent? grid)
            || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        var tileRefs = new HashSet<TileRef>();

        foreach (var ind in ev.Tiles)
        {
            if (Map.TryGetTileRef(stock.GridUid, grid, ind, out var tileRef))
                tileRefs.Add(tileRef);
        }

        RemoveTiles(tileRefs, stock, false);
    }

    protected void OnSettingUpdate(StockpileSettingUpdated ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock) || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        SetSetting(ev.ProtoId, ev.Value, stock, false);
    }

    protected void OnSettingsUpdate(StockpileSettingsUpdated ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock) || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        SetSetting(ev.Settings, stock, false);
    }

    private void OnShutdown(EntityUid uid, StockpileCategoryComponent component, ComponentShutdown args)
    {
        if (TryGetContainingStock(uid, out var stock))
            stock.RemoveEntity(uid);
    }

    #endregion

    private void ReloadPrototypes()
    {
        _defaultSettings.Clear();

        foreach (var proto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.HasComponent<StockpileCategoryComponent>(EntityManager.ComponentFactory))
                continue;

            _defaultSettings.Add(proto, 0);
        }
    }

    public List<Stock> AllStockpiles() => Stockpiles;

    /// <summary>
    /// Creates a stockpile on specified tiles
    /// </summary>
    public Stock? CreateStockpile(HashSet<TileRef> tiles, EntityUid owner, int id = 0, bool dirty = true)
    {
        DebugTools.Assert(id == 0 || !TryGetStock(id, out _));
        DebugTools.Assert(tiles.Count > 0 && tiles.First().GridUid.IsValid());

        var tilesList = new HashSet<Vector2i>();
        var gridUid = tiles.First().GridUid;

        foreach (var tile in tiles)
        {
            if (!ContainsTile(tile) && !Turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                tilesList.Add(tile.GridIndices);
        }

        if (tilesList.Count == 0)
            return null;

        if (id == 0)
        {
            id = _nextStockpileId;
            _nextStockpileId++;
        }

        var stock = new Stock(id, owner, gridUid, tilesList, _defaultSettings);

        if (_net.IsServer)
        {
            stock.OnEntityRemoved += uid =>
                RaiseNetworkEvent(new StockpileEntityDetached(stock.Id, GetNetEntity(uid)));
        }

        Stockpiles.Add(stock);

        if (dirty)
            RaiseNetworkEvent(new StockpileCreated(GetNetEntity(gridUid), _nextStockpileId - 1, tilesList.ToList()));

        return stock;
    }

    public void DeleteStockpile(int id, bool dirty = true)
    {
        if (!TryGetStock(id, out var stock))
            return;

        Stockpiles.Remove(stock);

        if (dirty)
            RaiseNetworkEvent(new StockpileDeleted(stock.Id));
    }

    /// <summary>
    /// Expands the stockpile with the given tiles
    /// </summary>
    public void AddTiles(HashSet<TileRef> tileRefs, Stock stock, bool dirty = true)
    {
        var tiles = new List<Vector2i>();

        foreach (var tile in tileRefs)
        {
            if (Turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
                || ContainsTile(tile.GridUid, tile.GridIndices))
                continue;

            stock.AddTile(tile.GridIndices);
            tiles.Add(tile.GridIndices);
        }

        if (dirty)
            RaiseNetworkEvent(new StockpileTileAdded(stock.Id, tiles));
    }

    /// <summary>
    /// Removes these tiles from stockpiles
    /// </summary>
    public void RemoveTiles(HashSet<TileRef> tiles, bool dirty = true)
    {
        var removed = new Dictionary<int, List<Vector2i>>();

        foreach (var tile in tiles)
        {
            foreach (var stock in Stockpiles)
            {
                stock.RemoveTile(tile.GridIndices);
                removed.GetOrNew(stock.Id).Add(tile.GridIndices);
            }
        }

        foreach (var stock in Stockpiles.ToList())
        {
            if (stock.IsValid())
                continue;

            Stockpiles.Remove(stock);
            removed.Remove(stock.Id);
        }

        if (!dirty)
            return;

        foreach (var (id, indicates) in removed)
        {
            RaiseNetworkEvent(new StockpileTileRemoved(id, indicates));
        }
    }

    public void RemoveTiles(HashSet<TileRef> tiles, Stock stock, bool dirty = true)
    {
        var removed = new List<Vector2i>();

        foreach (var tile in tiles)
        {
            if (tile.GridUid != stock.GridUid || !stock.ContainsTile(tile.GridIndices))
                continue;

            removed.Add(tile.GridIndices);
            stock.RemoveTile(tile.GridIndices);
        }

        if (!stock.IsValid())
        {
            DeleteStockpile(stock.Id);
            return;
        }

        if (dirty)
            RaiseNetworkEvent(new StockpileTileRemoved(stock.Id, removed));
    }

    /// <summary>
    /// Set the maximum number of items of this type in the stockpile
    /// </summary>
    /// <param name="protoId">Item prototype</param>
    /// <param name="value">Maximum number of items that can be in stockpile</param>
    /// <param name="stock"></param>
    /// <param name="dirty"></param>
    public void SetSetting(EntProtoId protoId, int value, Stock stock, bool dirty = true)
    {
        stock.SetSetting(protoId, value);

        if (dirty)
            RaiseNetworkEvent(new StockpileSettingUpdated(stock.Id, protoId, value));
    }

    public void SetSetting(Dictionary<EntProtoId, int> settings, Stock stock, bool dirty = true)
    {
        foreach (var (protoId, value) in settings)
        {
            stock.SetSetting(protoId, value);
        }

        if (dirty)
            RaiseNetworkEvent(new StockpileSettingsUpdated(stock.Id, settings));
    }

    /// <summary>
    /// Returns true if any stockpile contains this tile
    /// </summary>
    public bool ContainsTile(EntityUid gridUid, Vector2i tile)
    {
        foreach (var stock in Stockpiles)
        {
            if (gridUid == stock.GridUid && stock.ContainsTile(tile))
                return true;
        }

        return false;
    }

    public bool ContainsTile(TileRef tile)
    {
        foreach (var stock in Stockpiles)
        {
            if (tile.GridUid == stock.GridUid && stock.ContainsTile(tile.GridIndices))
                return true;
        }

        return false;
    }

    public bool TryGetStock(TileRef tile, [NotNullWhen(true)] out Stock? stockpile)
    {
        stockpile = null;

        foreach (var stock in Stockpiles)
        {
            if (!stock.ContainsTile(tile.GridIndices) || stock.GridUid != tile.GridUid)
                continue;

            stockpile = stock;
            return true;
        }

        return false;
    }

    public bool TryGetStock(EntityUid uid, [NotNullWhen(true)] out Stock? stockpile)
    {
        stockpile = null;

        if (Xform.GetGrid(uid) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !Map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tileRef))
            return false;

        return TryGetStock(tileRef, out stockpile);
    }

    public bool TryGetStock(EntityCoordinates coords, [NotNullWhen(true)] out Stock? stockpile)
    {
        stockpile = null;

        if (!coords.EntityId.IsValid() // weh
            || Xform.GetGrid(coords) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !Map.TryGetTileRef(gridUid, grid, coords, out var tileRef))
            return false;

        return TryGetStock(tileRef, out stockpile);
    }

    public bool TryGetStock(int id, [NotNullWhen(true)] out Stock? stockpile)
    {
        stockpile = null;

        foreach (var stock in Stockpiles)
        {
            if (stock.Id != id)
                continue;

            stockpile = stock;
            return true;
        }

        return false;
    }

    public bool TryGetContainingStock(EntityUid uid, [NotNullWhen(true)] out Stock? stockpile)
    {
        stockpile = null;

        foreach (var stock in Stockpiles)
        {
            if (!stock.ContainsEntity(uid))
                continue;

            stockpile = stock;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Can this entity be placed in stockpile
    /// </summary>
    public bool CanInsert(Stock stock, EntityUid uid)
    {
        return MetaData(uid).EntityPrototype is { } proto
               && stock.CanInsert(proto)
               && !stock.ContainsEntity(uid);
    }

    protected bool AttachEntity(EntityUid uid, Stock stock, bool dirty = true)
    {
        if (Xform.GetGrid(uid) is not { } gridUid
            || stock.GridUid != gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !Map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tileRef)
            || MetaData(uid).EntityPrototype is not { } proto
            || !stock.TryAddEntity(uid, proto, tileRef.GridIndices))
            return false;

        if (dirty)
            RaiseNetworkEvent(new StockpileEntityAttached(stock.Id, GetNetEntity(uid)));

        return true;
    }

    public bool DetachEntity(EntityUid uid, Stock stock, bool dirty = true)
    {
        if (!stock.RemoveEntity(uid))
            return false;

        if (dirty)
            RaiseNetworkEvent(new StockpileEntityDetached(stock.Id, GetNetEntity(uid)));

        return true;
    }
}

[Serializable, NetSerializable]
public sealed class StockpileCreated(NetEntity gridUid, int id, List<Vector2i> tiles) : EntityEventArgs
{
    public NetEntity GridUid = gridUid;
    public int Id = id;
    public List<Vector2i> Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class StockpileDeleted(int id) : EntityEventArgs
{
    public int Id = id;
}

[Serializable, NetSerializable]
public sealed class StockpileTileAdded(int id, List<Vector2i> tiles) : EntityEventArgs
{
    public int Id = id;
    public List<Vector2i> Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class StockpileTileRemoved(int id, List<Vector2i> tiles) : EntityEventArgs
{
    public int Id = id;
    public List<Vector2i> Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class StockpileSettingUpdated(int id, EntProtoId protoId, int value) : EntityEventArgs
{
    public int Id = id;
    public EntProtoId ProtoId = protoId;
    public int Value = value;
}

[Serializable, NetSerializable]
public sealed class StockpileSettingsUpdated(int id, Dictionary<EntProtoId, int> settings) : EntityEventArgs
{
    public int Id = id;
    public Dictionary<EntProtoId, int> Settings = settings;
}

[Serializable, NetSerializable]
public sealed class StockpileEntityAttached(int id, NetEntity uid) : EntityEventArgs
{
    public int Id = id;
    public NetEntity Uid = uid;
}

[Serializable, NetSerializable]
public sealed class StockpileEntityDetached(int id, NetEntity uid) : EntityEventArgs
{
    public int Id = id;
    public NetEntity Uid = uid;
}
