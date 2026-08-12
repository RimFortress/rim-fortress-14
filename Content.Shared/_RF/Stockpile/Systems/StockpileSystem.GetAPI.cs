using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.NPC;
using Content.Shared._RF.Stockpile.Components;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Stockpile.Systems;

public partial class StockpileSystem
{
    /// <summary>
    /// Checks whether the target entity can be stored in any tile or container in the target stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="toInsert">Entity to insert.</param>
    [PublicAPI, Pure]
    public bool CanInsert(Entity<StockpileComponent> ent, EntityUid toInsert)
    {
        if (!CanInsert(ent, Prototype(toInsert)?.ID))
            return false;

        if (ent.Comp.FreeTiles.Count > 0)
            return true;

        foreach (var uid in ent.Comp.Stored)
        {
            if (_storageQuery.TryComp(uid, out var comp)
                && _storage.CanInsert(toInsert, uid, comp))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether given entity type can be stored in the stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="proto">Entity prototype to insert.</param>
    [PublicAPI, Pure]
    public bool CanInsert(Entity<StockpileComponent> ent, EntProtoId? proto)
    {
        if (proto == null)
            return false;

        var max = GetProtoMax(ent, proto.Value);
        var current = GetTypeCount(ent, proto.Value);
        return max == -1 || current < max;
    }

    /// <summary>
    /// Checks whether given entity can be stored in the target tile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="toInsert">Entity to insert.</param>
    /// <param name="position">The target location where the entity should be stored.</param>
    [PublicAPI, Pure]
    public bool CanInsert(Entity<StockpileComponent> ent, EntityUid toInsert, Vector2i position)
    {
        if (!CanInsert(ent, Prototype(toInsert)?.ID))
            return false;

        if (ent.Comp.FreeTiles.Contains(position))
            return true;

        if (_xform.GetGrid(ent.Owner) is not { } grid)
            return false;

        var intersecting = new HashSet<Entity<ContainerManagerComponent>>();
        _lookup.GetLocalEntitiesIntersecting(grid,
            position,
            intersecting,
            flags: LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Uncontained);

        var stored = intersecting.Count(x => ent.Comp.Stored.Contains(x));

        if (stored < ent.Comp.MaxTileEntities)
            return true;

        foreach (var con in intersecting)
        {
            if (CanInsertInContainer(con.AsNullable()))
                return true;
        }

        return false;

        bool CanInsertInContainer(Entity<ContainerManagerComponent?> con)
        {
            if (!Resolve(con, ref con.Comp, false))
                return false;

            foreach (var container in _container.GetAllContainers(con, con.Comp))
            {
                if (_container.CanInsert(toInsert, container))
                    return true;

                foreach (var contained in container.ContainedEntities)
                {
                    if (CanInsertInContainer(contained))
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Checks whether a tile belongs to any stockpile.
    /// </summary>
    [PublicAPI, Pure]
    public bool TileInStock(TileRef tile) => TryGetStock(tile, out _);

    /// <summary>
    /// Searches for a stockpile located in given tile.
    /// </summary>
    /// <param name="tile">Tile.</param>
    /// <param name="stock">Found stockpile entity.</param>
    /// <returns>True, if found a stockpile to which this tile is assigned.</returns>
    [PublicAPI, Pure]
    public bool TryGetStock(TileRef tile, [NotNullWhen(true)] out Entity<StockpileComponent>? stock)
    {
        stock = null;

        var intersecting = new HashSet<Entity<StockpileComponent>>();
        _lookup.GetLocalEntitiesIntersecting(tile.GridUid, tile.GridIndices, intersecting);
        DebugTools.Assert(intersecting.Count <= 1); // The rest of the logic must ensure that a tile cannot be in multiple stockpiles

        if (intersecting.Count == 0)
            return false;

        stock = intersecting.First();
        return true;
    }

    /// <summary>
    /// Searching for a stockpile at the specified coordinates.
    /// </summary>
    /// <param name="coords">Target coordinates.</param>
    /// <param name="stock">Found stockpile entity.</param>
    /// <returns>True, if found a stockpile in the target coordinates.</returns>
    [PublicAPI, Pure]
    public bool TryGetStock(
        EntityCoordinates coords,
        [NotNullWhen(true)] out Entity<StockpileComponent>? stock)
    {
        stock = null;
        return _turf.TryGetTileRef(coords, out var tile) && TryGetStock(tile.Value, out stock);
    }

    /// <summary>
    /// Returns the stockpile entity by its Net ID.
    /// </summary>
    /// <param name="netEnt">Stockpile Net ID.</param>
    /// <param name="stock">Stockpile entity.</param>
    [PublicAPI, Pure]
    public bool TryGetStock(
        [NotNullWhen(true)] NetEntity? netEnt,
        [NotNullWhen(true)] out Entity<StockpileComponent>? stock)
    {
        stock = null;
        return TryGetEntity(netEnt, out var uid) && TryGetStock(uid.Value, out stock);
    }

    [PublicAPI, Pure]
    public bool TryGetStock(
        [NotNullWhen(true)] EntityUid? uid,
        [NotNullWhen(true)] out Entity<StockpileComponent>? stock)
    {
        stock = null;

        if (!_stockQuery.TryComp(uid, out var comp))
            return false;

        stock = new(uid.Value, comp);
        return true;
    }

    /// <summary>
    /// Returns the maximum possible number of items of this type currently in stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="protoId">Entity prototype.</param>
    [PublicAPI, Pure]
    public int GetProtoMax(Entity<StockpileComponent> ent, EntProtoId protoId)
        => !ent.Comp.Settings.TryGetValue(protoId, out var current)
            ? _defaultSettings.GetValueOrDefault(protoId, 0)
            : current;

    [PublicAPI, Pure]
    public OwnershipSystem.SameOwnerEntitiesEnumerator<StockpileComponent> GetStockpilesEnumerator(EntityUid owner)
        => _ownership.GetEntitiesEnumerator<StockpileComponent>(owner);

    /// <summary>
    /// Returns the coordinates of the stockpile center.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    [PublicAPI, Pure]
    public EntityCoordinates StockCenter(Entity<StockpileComponent> ent)
    {
        var pos = Vector2.Zero;

        foreach (var ind in ent.Comp.Tiles)
        {
            pos += ind + new Vector2(0.5f);
        }

        pos /= ent.Comp.Tiles.Count;
        return new EntityCoordinates(Transform(ent).Coordinates.EntityId, pos);
    }

    /// <summary>
    /// Checks whether this tile has been assigned to a stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="tile">Target tile.</param>
    [PublicAPI, Pure]
    public bool TileInStock(Entity<StockpileComponent> ent, TileRef tile)
        => _xform.GetGrid(ent.Owner) == tile.GridUid
           && TileInStock(ent, tile.GridIndices);

    /// <summary>
    /// Checks whether this tile has been assigned to a stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="tile">Target tile.</param>
    [PublicAPI, Pure]
    public static bool TileInStock(Entity<StockpileComponent> ent, Vector2i tile) => ent.Comp.Tiles.Contains(tile);

    /// <summary>
    /// Counts the current number of entities of a specific type in the stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="protoId">Entity prototype.</param>
    /// <returns></returns>
    [PublicAPI, Pure]
    public int GetTypeCount(Entity<StockpileComponent> ent, EntProtoId protoId)
        => ent.Comp.Stored.Count(x => protoId.Id == Prototype(x)?.ID);

    /// <summary>
    /// Checks whether one stockpile supplies another.
    /// </summary>
    /// <param name="supplier">Supplier stockpile entity.</param>
    /// <param name="supplied">Supplied stockpile entity.</param>
    [PublicAPI, Pure]
    public static bool HasSupplied(Entity<StockpileComponent> supplier, EntityUid supplied)
        => supplier.Comp.Supplied.Contains(supplied);

    /// <summary>
    /// Checks the target stockpile tile to see if anything can be stored there.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="tile">Target tile.</param>
    [PublicAPI, Pure]
    public bool IsTileFree(Entity<StockpileComponent> ent, TileRef tile)
    {
        if (_xform.GetGrid(ent.Owner) is not { } grid
            || grid != tile.GridUid
            || !ent.Comp.Tiles.Contains(tile.GridIndices))
            return true;

        var intersecting = new HashSet<Entity<StockpileContentComponent>>();
        _lookup.GetLocalEntitiesIntersecting(grid,
            tile.GridIndices,
            intersecting,
            flags: LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Uncontained);

        return intersecting.Count < ent.Comp.MaxTileEntities;
    }

    /// <summary>
    /// Returns a reverse supply chain starting from the root stockpile,
    /// to which only those stockpile where there is storage space for the target entity will be added.
    /// </summary>
    [PublicAPI, Pure]
    public List<Entity<StockpileComponent>> GetSupplyingChain(
        Entity<StockpileComponent> rootStock,
        EntityUid toInsert,
        bool includeRoot = false)
    {
        var stockpiles = new List<Entity<StockpileComponent>>();
        var queue = new Queue<Entity<StockpileComponent>>();
        var added = new HashSet<EntityUid>();

        if (!CanInsert(rootStock, Prototype(toInsert)?.ID))
            return stockpiles;

        queue.Enqueue(rootStock);

        while (queue.TryDequeue(out var stock))
        {
            if (!added.Add(stock))
                continue;

            foreach (var uid in stock.Comp.Supplied)
            {
                if (TryGetStock(uid, out var supplied))
                    queue.Enqueue(supplied.Value);
            }

            stockpiles.Add(stock);
        }

        if (!includeRoot)
            stockpiles.Remove(rootStock);

        stockpiles.Reverse();

        for (var i = 0; i < stockpiles.Count; i++)
        {
            if (CanInsert(stockpiles[i], Prototype(toInsert)?.ID))
                continue;

            stockpiles.RemoveAt(i);
            i--;
        }

        return stockpiles;
    }

    /// <summary>
    /// Returns the number of all stocks supplied from this one.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    [PublicAPI, Pure]
    public int GetTotalSupplied(Entity<StockpileComponent> ent)
    {
        var count = 0;
        var queue = new Queue<Entity<StockpileComponent>>();
        var added = new HashSet<EntityUid>();

        queue.Enqueue(ent);

        while (queue.TryDequeue(out var stock))
        {
            if (!added.Add(stock))
                continue;

            foreach (var uid in stock.Comp.Supplied)
            {
                if (TryGetStock(uid, out var supplied))
                    queue.Enqueue(supplied.Value);
            }

            count++;
        }

        return count - 1;
    }

    /// <summary>
    /// Searches for the available stockpile tile closest to the target coordinates.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="targetCoords">Target coordinates.</param>
    /// <param name="tileCoords">The coordinates of the center of the found tile.</param>
    /// <returns>True, if the tile is found.</returns>
    [PublicAPI, Pure]
    public bool TryFindClosestTile(
        Entity<StockpileComponent> ent,
        EntityCoordinates targetCoords,
        [NotNullWhen(true)] out EntityCoordinates? tileCoords)
    {
        var min = ((float)int.MaxValue, EntityCoordinates.Invalid);
        var grid = Transform(ent).Coordinates.EntityId;

        foreach (var ind in ent.Comp.FreeTiles)
        {
            var coords = new EntityCoordinates(grid, ind + new Vector2(0.5f));

            if (coords.TryDistance(EntityManager, _xform, targetCoords, out var dist)
                && dist < min.Item1)
                min = (dist, coords);
        }

        if (min.Invalid.IsValid(EntityManager))
        {
            tileCoords = min.Invalid;
            return true;
        }

        tileCoords = null;
        return false;
    }
}
