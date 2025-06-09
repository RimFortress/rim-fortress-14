using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Stockpile;

public abstract class SharedStockpileSystem : EntitySystem
{
    [Dependency] protected readonly SharedTransformSystem Xform = default!;
    [Dependency] protected readonly SharedMapSystem Map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;

    protected readonly List<Stockpile> Stockpiles = new();

    private readonly Dictionary<ProtoId<StockpileCategoryPrototype>, List<EntProtoId>> _categoryEntities = new();
    private readonly Dictionary<EntProtoId, int> _defaultSettings = new();

    private int _nextStockpileId = 1;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<StockpileCategoryComponent, MoveEvent>(OnMove);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<EntityPrototype>())
                ReloadPrototypes();
        };
    }

    private void OnMove(EntityUid uid, StockpileCategoryComponent component, MoveEvent args)
    {
        foreach (var stock in Stockpiles)
        {
            stock.RemoveEntity(uid);
        }

        TryInsert(uid);
    }

    private void ReloadPrototypes()
    {
        _defaultSettings.Clear();
        _categoryEntities.Clear();

        foreach (var proto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryGetComponent(out StockpileCategoryComponent? comp, EntityManager.ComponentFactory))
                continue;

            _categoryEntities.TryAdd(comp.Category, new());
            _categoryEntities[comp.Category].Add(proto);
            _defaultSettings.Add(proto, -1);
        }
    }

    protected void CreateStockpile(List<Vector2i> tiles, EntityUid gridUid)
    {
        var tilesList = new List<Vector2i>();

        foreach (var tile in tiles)
        {
            if (ContainsTile(tile))
                tilesList.Add(tile);
        }

        if (tilesList.Count == 0)
            return;

        Stockpiles.Add(new Stockpile(_nextStockpileId, gridUid, tilesList, _defaultSettings));
        _nextStockpileId++;

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileCreated(GetNetEntity(gridUid), tiles));
    }

    protected void AddTiles(List<TileRef> tileRefs)
    {
        if (tileRefs.Count == 0)
            return;

        var tiles = tileRefs.Select(x => x.GridIndices).ToList();

        foreach (var stock in Stockpiles)
        {
            if (!stock.ConnectedTo(tiles))
                continue;

            foreach (var tile in tiles)
            {
                if (!ContainsTile(tile))
                    stock.AddTile(tile);
            }

            return;
        }

        CreateStockpile(tiles, tileRefs.First().GridUid);
    }

    protected void RemoveTiles(List<Vector2i> tiles)
    {
        foreach (var tile in tiles)
        {
            foreach (var stock in Stockpiles)
            {
                stock.RemoveTile(tile);
            }
        }

        foreach (var stock in Stockpiles.ToList())
        {
            if (!stock.IsValid())
                Stockpiles.Remove(stock);
        }
    }

    public bool ContainsTile(Vector2i tile)
    {
        foreach (var stock in Stockpiles)
        {
            if (stock.ContainsTile(tile))
                return true;
        }

        return false;
    }

    public bool TryGetStock(TileRef tile, [NotNullWhen(true)] out Stockpile? stockpile)
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

    public bool TryGetStock(EntityUid uid, [NotNullWhen(true)] out Stockpile? stockpile)
    {
        stockpile = null;

        if (Xform.GetGrid(uid) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !Map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tileRef))
            return false;

        return TryGetStock(tileRef, out stockpile);
    }

    public bool TryGetStock(EntityCoordinates coords, [NotNullWhen(true)] out Stockpile? stockpile)
    {
        stockpile = null;

        if (Xform.GetGrid(coords) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !Map.TryGetTileRef(gridUid, grid, coords, out var tileRef))
            return false;

        return TryGetStock(tileRef, out stockpile);
    }

    private bool TryInsert(EntityUid uid)
    {
        return TryGetStock(uid, out var stock) && TryInsert(uid, stock);
    }

    private bool TryInsert(EntityUid uid, Stockpile stockpile)
    {
        if (Xform.GetGrid(uid) is not { } gridUid
            || !TryComp(uid, out MapGridComponent? grid)
            || !Map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tileRef)
            || MetaData(uid).EntityPrototype is not { } proto)
            return false;

        var bounds = _lookup.GetLocalBounds(tileRef, grid.TileSize).Enlarged(0.5f);
        var entities = _lookup.GetEntitiesIntersecting(gridUid, bounds);

        foreach (var entity in entities)
        {
            // TODO: container check
            if (stockpile.ContainsEntity(entity))
                return false;
        }

        return stockpile.TryAddEntity(uid, proto, tileRef.GridIndices);
    }
}

[Serializable, NetSerializable]
public sealed class StockpileCreated(NetEntity gridUid, List<Vector2i> tiles) : EntityEventArgs
{
    public NetEntity GridUid = gridUid;
    public List<Vector2i> Tiles = tiles;
}

public sealed class Stockpile
{
    public int Id { get; }
    public EntityUid GridUid { get; }
    public List<Vector2i> FreeTiles { get; } = new();

    private readonly List<Vector2i> _tiles;
    private readonly Dictionary<EntityUid, (EntProtoId Proto, Vector2i Tile)> _entities = new();
    private readonly Dictionary<EntProtoId, int> _settings;
    private readonly Dictionary<EntProtoId, int> _prototypes = new();

    public Stockpile(int id, EntityUid gridUid, List<Vector2i> tiles, Dictionary<EntProtoId, int> settings)
    {
        Id = id;
        GridUid = gridUid;
        _tiles = tiles;
        _settings = settings;
    }

    public void SetSetting(EntProtoId protoId, int value)
    {
        _settings[protoId] = value;

        if (!_prototypes.TryGetValue(protoId, out var count) || count <= value || count == -1)
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

    public bool TryAddEntity(EntityUid uid, EntProtoId protoId, Vector2i tile)
    {
        if (!_settings.TryGetValue(protoId, out var settings)
            || !_prototypes.TryGetValue(protoId, out var protoCount)
            || settings != -1 && protoCount >= settings)
            return false;

        _entities[uid] = (protoId, tile);
        FreeTiles.Remove(tile);
        return true;
    }

    public bool RemoveEntity(EntityUid uid)
    {
        if (!_entities.ContainsKey(uid)
            || !_entities.TryGetValue(uid, out var data)
            || !_prototypes.ContainsKey(data.Proto))
            return false;

        _prototypes[data.Proto]--;
        _prototypes.Remove(data.Proto);
        FreeTiles.Add(data.Tile);
        return true;
    }

    public bool ContainsEntity(EntityUid uid)
    {
        return _entities.ContainsKey(uid);
    }

    public void AddTiles(List<Vector2i> tiles)
    {
        foreach (var tile in tiles)
        {
            if (_tiles.Contains(tile))
                continue;

            _tiles.Add(tile);
            FreeTiles.Add(tile);
        }
    }

    public void AddTile(Vector2i tile)
    {
        if (_tiles.Contains(tile))
            return;

        _tiles.Add(tile);
        FreeTiles.Add(tile);
    }

    public void RemoveTiles(List<Vector2i> tiles)
    {
        foreach (var tile in tiles)
        {
            _tiles.Remove(tile);
            FreeTiles.Remove(tile);
        }

        foreach (var (uid, (_, tile)) in _entities.ToList())
        {
            if (tiles.Contains(tile))
                RemoveEntity(uid);
        }
    }

    public void RemoveTile(Vector2i tile)
    {
        _tiles.Remove(tile);

        foreach (var (uid, (_, ind)) in _entities.ToList())
        {
            if (tile == ind)
                RemoveEntity(uid);
        }
    }

    public bool ContainsTile(Vector2i tileInd)
    {
        return _tiles.Contains(tileInd);
    }

    public bool IsValid()
    {
        return _tiles.Count > 0;
    }

    public bool ConnectedTo(List<Vector2i> tiles)
    {
        var directions = new[] { Vector2i.Left, Vector2i.Right, Vector2i.Up, Vector2i.Down };
        var queue = new Queue<Vector2i>();

        queue.Enqueue(_tiles.First());

        while (queue.TryDequeue(out var tile))
        {
            foreach (var dir in directions)
            {
                var newDir = tile + dir;

                if (tiles.Contains(newDir))
                    return true;

                if (tiles.Contains(newDir))
                    queue.Enqueue(newDir);
            }
        }

        return false;
    }

    public bool CanInsert(EntProtoId protoId, int value = 1)
    {
        if (!_settings.TryGetValue(protoId, out var settings))
            return false;

        if (!_prototypes.TryGetValue(protoId, out var protoCount) && (value <= settings || settings == -1))
            return true;

        return protoCount + value <= settings || settings == -1;
    }
}
