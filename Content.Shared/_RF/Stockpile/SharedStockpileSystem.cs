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
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;

    protected readonly List<Stock> Stockpiles = new();

    private readonly Dictionary<EntProtoId, int> _defaultSettings = new();
    private int _nextStockpileId = 1;

    /// <inheritdoc/>
    public override void Initialize()
    {
        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<EntityPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

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
            if (_map.TryGetTileRef(gridUid, grid, ind, out var tileRef))
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
            if (_map.TryGetTileRef(stock.GridUid, grid, ind, out var tileRef))
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
            if (_map.TryGetTileRef(stock.GridUid, grid, ind, out var tileRef))
                tileRefs.Add(tileRef);
        }

        RemoveTiles(tileRefs, stock, false);
    }

    protected void OnSettingUpdate(StockpileSettingUpdate ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock) || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        SetSetting(ev.ProtoId, ev.Value, stock, false);
    }

    protected void OnSettingsUpdate(StockpileSettingsUpdate ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock) || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        SetSetting(ev.Settings, stock, false);
    }

    protected void OnAttachedEntity(StockpileEntityAttached ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock) || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        TryInsert(GetEntity(ev.Uid), stock, false);
    }

    protected void OnDetachedEntity(StockpileEntityDetached ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock) || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        DetachEntity(GetEntity(ev.Uid), stock, false);
    }

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
            if (!ContainsTile(tile) && !_turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
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
            if (_turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
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
            RaiseNetworkEvent(new StockpileSettingUpdate(stock.Id, protoId, value));
    }

    public void SetSetting(Dictionary<EntProtoId, int> settings, Stock stock, bool dirty = true)
    {
        foreach (var (protoId, value) in settings)
        {
            stock.SetSetting(protoId, value);
        }

        if (dirty)
            RaiseNetworkEvent(new StockpileSettingsUpdate(stock.Id, settings));
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

        if (_xform.GetGrid(uid) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !_map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tileRef))
            return false;

        return TryGetStock(tileRef, out stockpile);
    }

    public bool TryGetStock(EntityCoordinates coords, [NotNullWhen(true)] out Stock? stockpile)
    {
        stockpile = null;

        if (!coords.EntityId.IsValid() // weh
            || _xform.GetGrid(coords) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !_map.TryGetTileRef(gridUid, grid, coords, out var tileRef))
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

    /// <summary>
    /// Can this entity be placed in stockpile
    /// </summary>
    public bool CanInsert(Stock stock, EntityUid uid)
    {
        return MetaData(uid).EntityPrototype is { } proto
               && stock.CanInsert(proto)
               && !stock.ContainsEntity(uid);
    }

    protected bool TryInsert(EntityUid uid, Stock stockpile, bool dirty = true)
    {
        if (_xform.GetGrid(uid) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !_map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tileRef)
            || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
            || MetaData(uid).EntityPrototype is not { } proto)
            return false;

        foreach (var stock in Stockpiles)
        {
            DetachEntity(uid, stock, dirty);
        }

        if (!stockpile.TryAddEntity(uid, proto, tileRef.GridIndices))
            return false;

        if (dirty)
            RaiseNetworkEvent(new StockpileEntityAttached(stockpile.Id, GetNetEntity(uid)));

        return true;
    }

    protected void DetachEntity(EntityUid uid, Stock stock, bool dirty = true)
    {
        if (!stock.ContainsEntity(uid))
            return;

        stock.RemoveEntity(uid);

        if (dirty)
            RaiseNetworkEvent(new StockpileEntityDetached(stock.Id, GetNetEntity(uid)));
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
public sealed class StockpileSettingUpdate(int id, EntProtoId protoId, int value) : EntityEventArgs
{
    public int Id = id;
    public EntProtoId ProtoId = protoId;
    public int Value = value;
}

[Serializable, NetSerializable]
public sealed class StockpileSettingsUpdate(int id, Dictionary<EntProtoId, int> settings) : EntityEventArgs
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

/// <summary>
/// Object of stockpile of items
/// </summary>
[Access(typeof(SharedStockpileSystem))]
public sealed class Stock
{
    /// <summary>
    /// Unique stockpile id
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// The owner of the stockpile, who can change it
    /// </summary>
    public readonly EntityUid Owner;

    /// <summary>
    /// The grid on which the stockpile is located
    /// </summary>
    public EntityUid GridUid { get; }

    /// <summary>
    /// Tiles not occupied by objects
    /// </summary>
    public HashSet<Vector2i> FreeTiles { get; }

    /// <summary>
    /// All the tiles owned by the stockpile
    /// </summary>
    [Access(Other = AccessPermissions.ReadExecute)]
    public HashSet<Vector2i> Tiles => _tiles;

    public event Action<EntityUid>? OnEntityRemoved;

    private readonly Dictionary<EntityUid, (EntProtoId Proto, Vector2i Tile)> _entities = new();
    private readonly Dictionary<EntProtoId, int> _prototypes = new();
    private readonly Dictionary<EntProtoId, int> _settings = new();
    private readonly Dictionary<EntProtoId, int> _defaultSettings;
    private readonly HashSet<Vector2i> _tiles;

    public Stock(int id,
        EntityUid owner,
        EntityUid gridUid,
        HashSet<Vector2i> tiles,
        Dictionary<EntProtoId, int> settings)
    {
        Id = id;
        Owner = owner;
        GridUid = gridUid;
        _tiles = tiles;
        FreeTiles = tiles.ToHashSet();
        _defaultSettings = settings;

        foreach (var (protoId, _) in settings)
        {
            _prototypes[protoId] = 0;
        }
    }

    /// <summary>
    /// Set the maximum number of items of this type in the stockpile
    /// </summary>
    /// <param name="protoId">Item prototype</param>
    /// <param name="value">Maximum number of items that can be in stockpile</param>
    public void SetSetting(EntProtoId protoId, int value)
    {
        if (!_defaultSettings.ContainsKey(protoId))
            return;

        _settings[protoId] = value;
        var count = GetCount(protoId);

        if (count <= value || value == -1)
            return;

        var i = count - value;

        foreach (var (uid, (proto, _)) in _entities.ToList())
        {
            if (proto != protoId)
                continue;

            RemoveEntity(uid);
            i--;

            if (i == 0)
                break;
        }
    }

    /// <summary>
    /// Tries to add an entity to a stockpile
    /// </summary>
    public bool TryAddEntity(EntityUid uid, EntProtoId protoId, Vector2i tile)
    {
        var setting = GetSetting(protoId);

        if (setting != -1 && GetCount(protoId) >= setting
            || !FreeTiles.Remove(tile))
            return false;

        _prototypes[protoId]++;
        _entities[uid] = (protoId, tile);
        return true;
    }

    /// <summary>
    /// Deletes an entity from the stockpile
    /// </summary>
    public bool RemoveEntity(EntityUid uid)
    {
        if (!_entities.TryGetValue(uid, out var data)
            || !_prototypes.ContainsKey(data.Proto))
            return false;

        _prototypes[data.Proto]--;
        _entities.Remove(uid);
        FreeTiles.Add(data.Tile);
        OnEntityRemoved?.Invoke(uid);
        return true;
    }

    /// <summary>
    /// Returns true if the stockpile contains the given entity
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public bool ContainsEntity(EntityUid uid)
    {
        return _entities.ContainsKey(uid);
    }

    /// <summary>
    /// Expands the stockpile with the given tiles
    /// </summary>
    public void AddTiles(List<Vector2i> tiles1)
    {
        foreach (var tile in tiles1)
        {
            if (!_tiles.Add(tile))
                continue;

            FreeTiles.Add(tile);
        }
    }

    public void AddTile(Vector2i tile)
    {
        if (!_tiles.Add(tile))
            return;

        FreeTiles.Add(tile);
    }

    /// <summary>
    /// Removes the given tiles from the stockpile
    /// </summary>
    public void RemoveTiles(List<Vector2i> removeTiles)
    {
        foreach (var tile in removeTiles)
        {
            _tiles.Remove(tile);
            FreeTiles.Remove(tile);
        }

        foreach (var (uid, (_, tile)) in _entities.ToList())
        {
            if (removeTiles.Contains(tile))
                RemoveEntity(uid);
        }
    }

    /// <summary>
    /// Removes the given tile from the stockpile
    /// </summary>
    public void RemoveTile(Vector2i tile)
    {
        _tiles.Remove(tile);
        FreeTiles.Remove(tile);

        foreach (var (uid, (_, ind)) in _entities.ToList())
        {
            if (tile == ind)
                RemoveEntity(uid);
        }
    }

    /// <summary>
    /// Returns true if the given tile is a part of a stockpile
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public bool ContainsTile(Vector2i tileInd)
    {
        return _tiles.Contains(tileInd);
    }

    [Access(Other = AccessPermissions.Execute)]
    public bool IsValid()
    {
        return _tiles.Count > 0;
    }

    /// <summary>
    /// Returns true if at least one tile from the list is connected to the stockpile
    /// </summary>
    public bool ConnectedTo(List<Vector2i> tiles1)
    {
        var directions = new[] { Vector2i.Left, Vector2i.Right, Vector2i.Up, Vector2i.Down };
        var queue = new Queue<Vector2i>();

        queue.Enqueue(_tiles.First());

        while (queue.TryDequeue(out var tile))
        {
            foreach (var dir in directions)
            {
                var newDir = tile + dir;

                if (tiles1.Contains(newDir))
                    return true;

                if (tiles1.Contains(newDir))
                    queue.Enqueue(newDir);
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if there is space in the stockpile for the given number of items
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public bool CanInsert(EntProtoId protoId, int value = 1)
    {
        var setting = GetSetting(protoId);

        return (GetCount(protoId) + value <= setting || setting == -1) && FreeTiles.Count >= value;
    }

    /// <summary>
    /// Returns the maximum number of items of this type that can be in stockpile
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public int GetSetting(EntProtoId protoId)
    {
        return _settings.TryGetValue(protoId, out var value)
            ? value
            :_defaultSettings.GetValueOrDefault(protoId, 0);
    }

    /// <summary>
    /// Returns the number of items of this type in the stockpile
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public int GetCount(EntProtoId protoId)
    {
        return _prototypes.GetValueOrDefault(protoId, 0);
    }
}
