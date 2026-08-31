using System.Linq;
using System.Numerics;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Stockpile.Systems;

public partial class StockpileSystem
{
    /// <summary>
    /// Creates a stockpile on specified tiles.
    /// </summary>
    public void CreateStockpile(HashSet<TileRef> tiles, EntityUid owner)
    {
        DebugTools.Assert(tiles.Count > 0 && tiles.First().GridUid.IsValid());

        var tilesList = new HashSet<TileRef>();
        var gridUid = tiles.First().GridUid;

        DebugTools.Assert(tiles.All(x => x.GridUid == gridUid));

        foreach (var tile in tiles)
        {
            if (!TryGetStock(tile, out _)
                && !_turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                tilesList.Add(tile);
        }

        if (tilesList.Count == 0)
            return;

        if (_net.IsClient)
        {
            RaiseNetworkEvent(new StockpileCreateRequest(
                GetNetEntity(gridUid),
                tilesList.Select(x => x.GridIndices).ToHashSet()));
            return;
        }

        var uid = Spawn(StockProto, new EntityCoordinates(gridUid, tilesList.First().GridIndices));
        var ent = new Entity<StockpileComponent>(uid, EnsureComp<StockpileComponent>(uid));

        _ownership.AddOwnership(uid, owner: owner);
        _meta.SetEntityName(uid, $"{Loc.GetString(DefaultStockName)} #{uid.Id}");
        AddTiles(ent, tilesList);
        _physics.WakeBody(uid, force: true);
    }

    /// <summary>
    /// Deletes the stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    public void DeleteStockpile(Entity<StockpileComponent> ent)
    {
        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileDeleted(GetNetEntity(ent)));
        else
            Del(ent);
    }

    /// <summary>
    /// Adds a tiles to the given stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="tiles">Tiles list.</param>
    /// <returns>True, if the tile was successfully added.</returns>
    [PublicAPI]
    public void AddTiles(Entity<StockpileComponent> ent, HashSet<TileRef> tiles)
    {
        foreach (var tile in tiles)
        {
            AddTile(ent, tile, false);
        }

        if (_net.IsClient)
        {
            RaiseNetworkEvent(new StockpileTileAdded(
                GetNetEntity(ent),
                tiles.Select(x => x.GridIndices).ToHashSet()));
        }

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Tiles));
        DirtyField(ent.AsNullable(), nameof(StockpileComponent.FreeTiles));
    }

    /// <summary>
    /// Adds a tile to the given stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="tile">Tile.</param>
    /// <param name="dirty"></param>
    /// <returns>True, if the tile was successfully added.</returns>
    [PublicAPI]
    public bool AddTile(Entity<StockpileComponent> ent, TileRef tile, bool dirty = true)
    {
        if (TileInStock(tile)
            || _turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
            || _xform.GetGrid(ent.Owner) != tile.GridUid)
            return false;

        var id = $"{tile.GridIndices.X},{tile.GridIndices.Y}";
        var shape = new PolygonShape();
        var offset = tile.GridIndices - Transform(ent).Coordinates.Position + new Vector2(0.5f);
        shape.SetAsBox(0.5f, 0.5f, offset, 0f);

        if (!_fixture.TryCreateFixture(ent,
                shape,
                id,
                density: 0f,
                hard: false,
                collisionLayer: (int)CollisionGroup.Impassable,
                collisionMask: (int)CollisionGroup.Impassable))
            return false;

        ent.Comp.TileFixtures[tile.GridIndices] = id;
        ent.Comp.Tiles.Add(tile.GridIndices);
        ent.Comp.FreeTiles.Add(tile.GridIndices);

        if (!dirty)
            return true;

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileTileAdded(GetNetEntity(ent), new() { tile.GridIndices }));

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Tiles));
        DirtyField(ent.AsNullable(), nameof(StockpileComponent.FreeTiles));
        return true;
    }

    /// <summary>
    /// Removes the tile from those assigned to the stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="tiles">Tiles list.</param>
    /// <returns>True, if the tile was successfully removed.</returns>
    [PublicAPI]
    public void RemoveTile(Entity<StockpileComponent> ent, HashSet<TileRef> tiles)
    {
        foreach (var tile in tiles)
        {
            RemoveTile(ent, tile, false);
        }

        if (_net.IsClient)
        {
            RaiseNetworkEvent(new StockpileTileRemoved(GetNetEntity(ent),
                tiles.Select(x => x.GridIndices).ToHashSet()));
        }

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Tiles));
        DirtyField(ent.AsNullable(), nameof(StockpileComponent.FreeTiles));
        ValidateStockEntities(ent);
    }

    /// <summary>
    /// Removes the tile from those assigned to the stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="tile">Tile.</param>
    /// <param name="dirty"></param>
    /// <returns>True, if the tile was successfully removed.</returns>
    [PublicAPI]
    public bool RemoveTile(Entity<StockpileComponent> ent, TileRef tile, bool dirty = true)
    {
        if (!ent.Comp.Tiles.Remove(tile.GridIndices))
            return false;

        if (ent.Comp.Tiles.Count == 0)
        {
            Del(ent);
            return true;
        }

        ent.Comp.FreeTiles.Remove(tile.GridIndices);

        if (ent.Comp.TileFixtures.Remove(tile.GridIndices, out var id))
            _fixture.DestroyFixture(ent, id);

        if (!dirty)
            return true;

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileTileRemoved(GetNetEntity(ent), new() { tile.GridIndices }));

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Tiles));
        DirtyField(ent.AsNullable(), nameof(StockpileComponent.FreeTiles));
        ValidateStockEntities(ent);
        return true;
    }

    /// <summary>
    /// Changes the color used to display the stock in the UI.
    /// </summary>
    [PublicAPI]
    public void SetStockColor(Entity<StockpileComponent> ent, Color color)
    {
        ent.Comp.Color = color;
        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Color));

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileColorSet(GetNetEntity(ent), color));
    }

    /// <summary>
    /// Adds a stock to the list of those supplied by another stock.
    /// </summary>
    /// <param name="supplier">Supplier stockpile entity.</param>
    /// <param name="supplied">Supplied stockpile entity.</param>
    [PublicAPI]
    public void AddSuppliedStock(Entity<StockpileComponent?> supplier, Entity<StockpileComponent?> supplied)
    {
        if (!Resolve(supplier, ref supplier.Comp)
            || !Resolve(supplied, ref supplied.Comp)
            || !supplier.Comp.Supplied.Add(supplied)
            || !supplied.Comp.Suppliers.Add(supplier))
            return;

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileSuppliedAdded(GetNetEntity(supplier), GetNetEntity(supplied)));

        DirtyField(supplier.AsNullable(), nameof(StockpileComponent.Supplied));
        DirtyField(supplied.AsNullable(), nameof(StockpileComponent.Suppliers));

        var ev = new StockpileSupplyingAdded(supplier, supplied);
        RaiseLocalEvent(supplier, ev);
        RaiseLocalEvent(supplied, ev);
    }

    /// <summary>
    /// Removes a stock from the list of those supplied by another stock.
    /// </summary>
    /// <param name="supplier">Supplier stockpile entity.</param>
    /// <param name="supplied">Supplied stockpile entity.</param>
    [PublicAPI]
    public void RemoveSuppliedStock(Entity<StockpileComponent?> supplier, Entity<StockpileComponent?> supplied)
    {
        if (!Resolve(supplier, ref supplier.Comp)
            || !Resolve(supplied, ref supplied.Comp)
            || !supplier.Comp.Supplied.Remove(supplied)
            || !supplied.Comp.Suppliers.Remove(supplier))
            return;

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileSuppliedRemoved(GetNetEntity(supplier), GetNetEntity(supplied)));

        DirtyField(supplier.AsNullable(), nameof(StockpileComponent.Supplied));
        DirtyField(supplied.AsNullable(), nameof(StockpileComponent.Suppliers));

        var ev = new StockpileSupplyingRemoved(supplier, supplied);
        RaiseLocalEvent(supplier, ev);
        RaiseLocalEvent(supplied, ev);
    }

    /// <summary>
    /// Removes the entity from the stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="uid">Entity to remove.</param>
    /// <returns>True, if the entity has been removed.</returns>
    [PublicAPI]
    public bool RemoveEntity(Entity<StockpileComponent> ent, EntityUid uid)
    {
        if (!ent.Comp.Stored.Remove(uid))
            return false;

        RemoveRecursively(uid);

        if (_turf.TryGetTileRef(Transform(uid).Coordinates, out var tile)
            && IsTileFree(ent, tile.Value))
        {
            ent.Comp.FreeTiles.Add(tile.Value.GridIndices);
            DirtyField(ent.AsNullable(), nameof(StockpileComponent.FreeTiles));
        }

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Stored));

        if (_net.IsServer)
            RaiseNetworkEvent(new StockpileContentUpdated(GetNetEntity(ent)));

        return true;

        void RemoveRecursively(EntityUid toRemove)
        {
            if (!RemComp<StockpileContentComponent>(toRemove)
                || !_containerQuery.TryComp(toRemove, out var contComp))
                return;

            var ev = new StockEntityRemoved(ent, toRemove);
            RaiseLocalEvent(ent, ev);
            RaiseLocalEvent(toRemove, ev);

            foreach (var container in _container.GetAllContainers(toRemove, contComp))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    RemoveRecursively(contained);
                }
            }
        }
    }

    /// <summary>
    /// Sets the maximum number of items of a specific type that can be stored in the stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="protoId">Entity type.</param>
    /// <param name="max">Limit on the number of entities of this type. -1 means there is no limit.</param>
    [PublicAPI]
    public void SetProtoMax(Entity<StockpileComponent> ent, EntProtoId protoId, int max)
    {
        if ((ent.Comp.Settings.TryGetValue(protoId, out var current)
            || _defaultSettings.TryGetValue(protoId, out current))
            && current == max)
            return;

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileSettingUpdated(GetNetEntity(ent), protoId, max));

        ent.Comp.Settings[protoId] = max;
        var ev = new StockSettingsChanged(ent, protoId, current, max);
        RaiseLocalEvent(ent, ev, true);
        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Settings));
        ValidateStockEntities(ent);
    }

    /// <summary>
    /// Sets the maximum number of items of a specific type that can be stored in the stockpile.
    /// </summary>
    /// <param name="ent">Stockpile entity.</param>
    /// <param name="settings">A dictionary containing the entity type and its maximum quantity in stock.</param>
    [PublicAPI]
    public void SetProtoMax(Entity<StockpileComponent> ent, Dictionary<EntProtoId, int> settings)
    {
        foreach (var (proto, value) in settings)
        {
            var old = ent.Comp.Settings.GetValueOrDefault(proto, 0);

            if (old == value)
                continue;

            ent.Comp.Settings[proto] = value;
            var ev = new StockSettingsChanged(ent, proto, old, value);
            RaiseLocalEvent(ent, ev, true);
        }

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Settings));
        ValidateStockEntities(ent);

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileSettingsUpdated(GetNetEntity(ent), settings));
    }

    [PublicAPI]
    public bool ReserveTile(Entity<StockpileComponent> ent, Vector2i tile, EntityUid user)
    {
        if (ent.Comp.ReservedTiles.ContainsKey(tile)
            || !_ownership.HasSameOwner(ent.Owner, user))
            return false;

        ent.Comp.ReservedTiles[tile] = user;
        return true;
    }

    [PublicAPI]
    public bool ReserveEntity(Entity<StockpileComponent> ent, EntityUid target, EntityUid user)
    {
        if (ent.Comp.ReservedEntities.ContainsKey(target)
            || !_ownership.HasSameOwner(ent.Owner, user))
            return false;

        ent.Comp.ReservedEntities[target] = user;
        return true;
    }

    [PublicAPI]
    public static void ClearReserve(Entity<StockpileComponent> ent, EntityUid user)
    {
        ent.Comp.ReservedTiles = ent.Comp.ReservedTiles.Where(x => x.Value != user).ToDictionary();
        ent.Comp.ReservedEntities = ent.Comp.ReservedEntities.Where(x => x.Value != user).ToDictionary();
    }
}
