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
    /// Checks whether the target entity can be stored in the tile where it is located.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="toInsert">Entity to insert.</param>
    [PublicAPI, Pure]
    public bool CanInsert(Entity<StockpileComponent> ent, EntityUid toInsert)
        => _turf.GetTileRef(Transform(ent).Coordinates) is { } tile
           && CanInsert(ent, toInsert, tile.GridIndices);

    /// <summary>
    /// Checks whether given entity can be stored in the target tile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="toInsert">Entity to insert.</param>
    /// <param name="position">The target location where the entity should be stored.</param>
    [PublicAPI, Pure]
    public bool CanInsert(Entity<StockpileComponent> ent, EntityUid toInsert, Vector2i position)
    {
        if (Prototype(toInsert) is not { } proto)
            return false;

        var max = GetProtoMax(ent, proto);
        var current = GetTypeCount(ent, proto);

        if (max != -1 && current >= max)
            return false;

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
        NetEntity netEnt,
        [NotNullWhen(true)] out Entity<StockpileComponent>? stock)
    {
        stock = null;
        return TryGetEntity(netEnt, out var uid) && TryGetStock(uid.Value, out stock);
    }

    [PublicAPI, Pure]
    public bool TryGetStock(
        EntityUid uid,
        [NotNullWhen(true)] out Entity<StockpileComponent>? stock)
    {
        stock = null;

        if (!_stockQuery.TryComp(uid, out var comp))
            return false;

        stock = new(uid, comp);
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
    /// Searches for the last stockpiles in the supply chain to which the given entity can be stocked.
    /// </summary>
    public List<Entity<StockpileComponent>> FindLastSupplied(Entity<StockpileComponent> startStock, EntityUid toInsert)
    {
        var stockpiles = new List<Entity<StockpileComponent>>();
        var queue = new Queue<Entity<StockpileComponent>>();

        if (!CanInsert(startStock, toInsert))
            return stockpiles;

        queue.Enqueue(startStock);

        while (queue.TryDequeue(out var stock))
        {
            var valid = true;

            foreach (var uid in stock.Comp.Supplied)
            {
                if (!_stockQuery.TryComp(uid, out var supplied)
                    || !CanInsert(new(uid, supplied), toInsert))
                    continue;

                valid = false;
                queue.Enqueue(new(uid, supplied));
            }

            if (valid)
                stockpiles.Add(stock);
        }

        return stockpiles;
    }
}
