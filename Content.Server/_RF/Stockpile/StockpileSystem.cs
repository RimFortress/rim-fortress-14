using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Content.Server.Storage.Components;
using Content.Shared._RF.NPC;
using Content.Shared._RF.Stockpile;
using Content.Shared.Physics;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.Stockpile;

public sealed class StockpileSystem : SharedStockpileSystem
{
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StockpileCategoryComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<StockpileCategoryComponent, ContainerGettingInsertedAttemptEvent>(OnContainerGettingInsertedAttempt);
    }

    private void OnMove(EntityUid uid, StockpileCategoryComponent component, MoveEvent args)
    {
        if (Xform.GetGrid(uid) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !Map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tileRef)
            || Turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
            return;

        TryGetContainingStock(uid, out var oldStock);
        TryGetStock(tileRef, out var newStock);

        if (newStock != null
            && newStock == oldStock
            && oldStock.TryGetContainingTile(uid, out var oldTile)
            && oldTile != tileRef.GridIndices)
        {
            if (!newStock.TryUpdateEntityTile(uid, tileRef.GridIndices))
                DetachEntity(uid, newStock);
        }

        if (oldStock != null && newStock == null)
            DetachEntity(uid, oldStock);

        if (newStock != null && oldStock != newStock)
        {
            if (oldStock != null)
                DetachEntity(uid, oldStock);

            if (TryComp(uid, out EntityStorageComponent? storage))
            {
                if (newStock.ContainerInTile(tileRef.GridIndices))
                    return;

                newStock.SetTileMaxEntities(tileRef.GridIndices, Stock.DefaultMaxTileEntities + storage.Capacity);

                if (!AttachEntity(uid, newStock, tileRef))
                    return;

                newStock.Containers.Add(uid);

                foreach (var ent in storage.Contents.ContainedEntities)
                {
                    foreach (var stock in Stockpiles)
                    {
                        if (DetachEntity(ent, stock))
                            break;
                    }

                    AttachEntity(ent, newStock, tileRef);
                }
            }
            else
                AttachEntity(uid, newStock, tileRef);
        }
    }

    private void OnContainerGettingInsertedAttempt(EntityUid uid,
        StockpileCategoryComponent component,
        ContainerGettingInsertedAttemptEvent args)
    {
        if (TryGetContainingStock(uid, out var stock) && !stock.ContainsEntity(args.Container.Owner))
            DetachEntity(uid, stock);
    }

    private bool AttachEntity(EntityUid uid, Stock stock, TileRef? tileRef = null)
    {
        if (tileRef == null)
        {
            if (Xform.GetGrid(uid) is not { } gridUid
                || stock.GridUid != gridUid
                || !TryComp(gridUid, out MapGridComponent? grid)
                || !Map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tile))
                return false;

            tileRef = tile;
        }

        if (MetaData(uid).EntityPrototype is not { } proto
            || !stock.TryAddEntity(uid, proto, tileRef.Value.GridIndices))
            return false;

        RaiseNetworkEvent(new StockpileEntityAttached(stock.Id, GetNetEntity(uid), proto, tileRef.Value.GridIndices));
        return true;
    }

    public bool DetachEntity(EntityUid uid, Stock stock)
    {
        if (!stock.RemoveEntity(uid))
            return false;

        RaiseNetworkEvent(new StockpileEntityDetached(stock.Id, GetNetEntity(uid)));
        return true;
    }

    /// <summary>
    /// Searches for a path to a tile in the stockpile to which the given item can be stockpiled
    /// </summary>
    /// <remarks>
    /// Helper method for NPC logic
    /// </remarks>
    /// <param name="user">The entity that will perform the stockpiling and from which the path will be constructed</param>
    /// <param name="uid">Entity to be stockpiled</param>
    /// <param name="stock">Stockpile</param>
    /// <param name="range">How close to the center of the tile the path should be built</param>
    /// <param name="cancelToken"></param>
    /// <param name="pathFlags"></param>
    /// <param name="containerOnly">If true, only tiles with containers will be searched for</param>
    /// <returns>Found path and coordinates of the center of the stockpile tile</returns>
    public async Task<(PathResultEvent Path, EntityCoordinates Coords)?> GetStoringTilePath(
        EntityUid user,
        Entity<OwnedComponent?> uid,
        Stock stock,
        float range,
        CancellationToken cancelToken,
        PathFlags pathFlags = PathFlags.None,
        bool containerOnly = false)
    {
        if (!Resolve(uid, ref uid.Comp)
            || !uid.Comp.Owners.Contains(stock.Owner)
            || !CanInsert(stock, uid)
            || Xform.GetGrid(user) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid))
            return null;

        var freePlaces = new List<(EntityCoordinates Coord, float Dist)>();
        var gridEnt = new Entity<MapGridComponent>(gridUid, grid);
        var userCoords = Transform(user).Coordinates;
        var tiles = containerOnly ? stock.FreeContainersTiles() : stock.FreeTiles;

        foreach (var tile in tiles)
        {
            var tileRef = Map.GetTileRef(gridEnt, tile);
            var center = Turf.GetTileCenter(tileRef);

            if (!userCoords.TryDistance(EntityManager, Xform, center, out var distance))
                continue;

            freePlaces.Add((center, distance));
        }

        freePlaces.Sort((x, y) => x.Dist.CompareTo(y.Dist));

        foreach (var (coords, _) in freePlaces)
        {
            var path = await _pathfinding.GetPath(
                user,
                userCoords,
                coords,
                range,
                cancelToken,
                pathFlags);

            if (path.Result != PathResult.Path)
                continue;

            return (path, coords);
        }

        return null;
    }

    /// <summary>
    /// Searches for the last stockpiles in the supply chain to which the given entity can be stocked
    /// </summary>
    public List<Stock> FindLastSupplied(EntityUid uid, Stock startStock)
    {
        var stockpiles = new List<Stock>();
        var queue = new Queue<Stock>();

        if (!CanInsert(startStock, uid))
            return stockpiles;

        queue.Enqueue(startStock);

        while (queue.TryDequeue(out var stock))
        {
            var valid = true;

            foreach (var id in stock.SuppliedStockpiles)
            {
                if (!TryGetStock(id, out var supplied) || !CanInsert(supplied, uid))
                    continue;

                valid = false;
                queue.Enqueue(supplied);
            }

            if (valid)
                stockpiles.Add(stock);
        }

        return stockpiles;
    }
}
