using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.Zoning.Components;
using Content.Shared._RF.Zoning.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Systems;

public partial class ZoningSystem
{
    /// <summary>
    /// Creates a zone in the target tiles.
    /// </summary>
    /// <param name="protoId">A prototype of the zone that will be created.</param>
    /// <param name="tiles">The tiles on which the zone will be created.</param>
    /// <param name="zone">Created zone entity.</param>
    /// <returns>True, if the zone was successfully created.</returns>
    [PublicAPI]
    public bool TryCreateZone(
        EntProtoId<ZoneComponent> protoId,
        IReadOnlySet<TileRef> tiles,
        [NotNullWhen(true)] out Entity<ZoneComponent>? zone)
    {
        zone = null;

        if (tiles.Count == 0
            || !_proto.Resolve(protoId, out var proto)
            || !proto.TryComp(out ZoneComponent? zoneComp, EntityManager.ComponentFactory)
            || !_proto.Resolve(zoneComp.Proto, out var zoneProto))
            return false;

        var gridUid = tiles.First().GridUid;
        DebugTools.Assert(tiles.All(x => x.GridUid == gridUid));

        var coords = TilesCenter(tiles);
        var uid = Spawn(protoId, coords);
        zone = new(uid, EnsureComp<ZoneComponent>(uid));
        _meta.SetEntityName(uid, $"{Loc.GetString(zoneProto.Name)} #{uid.Id}");

        AddTile(zone.Value, tiles, false, false);

        if (zone.Value.Comp.Tiles.Count == 0)
        {
            Del(zone);
            zone = null;
            return false;
        }

        zone.Value.Comp.TilesValid = TileCheck(zone.Value);

        if (!zoneProto.InvalidCreation && !zone.Value.Comp.TilesValid)
        {
            Del(zone);
            zone = null;
            return false;
        }

        foreach (var tile in zone.Value.Comp.Tiles)
        {
            var tileCoords = new EntityCoordinates(gridUid, tile + new Vector2(0.5f));

            foreach (var tileEnt in _turf.GetEntitiesInTile(tileCoords, LookupFlags.All))
            {
                TryEnter(zone.Value, tileEnt, false, false);
            }
        }

        zone.Value.Comp.EntitiesValid = EntityCheck(zoneProto, zone.Value.Comp.Entities);

        if (!zoneProto.InvalidCreation && !zone.Value.Comp.EntitiesValid)
        {
            Del(zone);
            zone = null;
            return false;
        }

        UpdateFixtures(zone.Value, false);
        _physics.WakeBody(uid, force: true);
        Dirty(zone.Value.AsNullable());

        return true;
    }

    /// <summary>
    /// Removes the target zone.
    /// </summary>
    [PublicAPI]
    public void DeleteZone(Entity<ZoneComponent> ent)
    {
        TryLeave(ent, ent.Comp.Entities, false, false);
        Del(ent);
    }

    /// <summary>
    /// Adds a tile to the given zone.
    /// </summary>
    /// <param name="ent">Zone entity.</param>
    /// <param name="tile">Tile to add.</param>
    /// <returns>True, if the tile was successfully added.</returns>
    [PublicAPI]
    public bool AddTile(Entity<ZoneComponent> ent, TileRef tile)
        => AddTile(ent, tile, true, true, true);

    /// <summary>
    /// Adds a tiles to the given zone.
    /// </summary>
    /// <param name="ent">Zone entity.</param>
    /// <param name="tiles">Tiles to add.</param>
    [PublicAPI]
    public bool AddTile(Entity<ZoneComponent> ent, IReadOnlySet<TileRef> tiles)
        => AddTile(ent, tiles, true, true);

    /// <summary>
    /// Removes a tile from the given zone.
    /// </summary>
    /// <param name="ent">Zone entity.</param>
    /// <param name="tile">Tiles to remove.</param>
    /// <returns>True, if the tile was successfully removed.</returns>
    [PublicAPI]
    public bool RemoveTile(Entity<ZoneComponent> ent, TileRef tile)
        => RemoveTile(ent, tile, true, true, true);

    /// <summary>
    /// Removes a tile from the given zone.
    /// </summary>
    /// <param name="ent">Zone entity.</param>
    /// <param name="tiles">Tiles to remove.</param>
    [PublicAPI]
    public void RemoveTile(Entity<ZoneComponent> ent, IReadOnlySet<TileRef> tiles)
        => RemoveTile(ent, tiles, true, true);

    /// <summary>
    /// Returns the coordinates of the zone's center.
    /// </summary>
    /// <param name="ent">Zone entity.</param>
    [PublicAPI, Pure]
    public EntityCoordinates ZoneCenter(Entity<ZoneComponent> ent)
    {
        var coords = Transform(ent).Coordinates;

        if (ent.Comp.Tiles.Count == 0)
            return coords;

        var pos = Vector2.Zero;

        foreach (var ind in ent.Comp.Tiles)
        {
            pos += ind + new Vector2(0.5f);
        }

        pos /= ent.Comp.Tiles.Count;
        return new EntityCoordinates(coords.EntityId, pos);
    }

    /// <summary>
    /// Checks whether a tile belongs to any zone.
    /// </summary>
    /// <param name="tile">Tile.</param>
    /// <param name="type">The type of zone to search for. If null, any zone will be found.</param>
    [PublicAPI, Pure]
    public bool TileInZone(TileRef tile, ProtoId<ZonePrototype>? type = null) => TryGetZone(tile, out _, type);

    /// <summary>
    /// Checks whether a tile belongs to a target.
    /// </summary>
    [PublicAPI, Pure]
    public bool TileInZone(Entity<ZoneComponent> ent, TileRef tile)
        => _transform.GetGrid(ent.Owner) == tile.GridUid
           && ent.Comp.Tiles.Contains(tile.GridIndices);

    /// <summary>
    /// Searches for a zone located in given tile.
    /// </summary>
    /// <param name="tile">Tile.</param>
    /// <param name="zone">Found zone entities.</param>
    /// <param name="type">The type of zone to search for. If null, any zone will be found.</param>
    /// <returns>True, if found a zone to which this tile is assigned.</returns>
    [PublicAPI, Pure]
    public bool TryGetZone(
        TileRef tile,
        [NotNullWhen(true)] out HashSet<Entity<ZoneComponent>>? zone,
        ProtoId<ZonePrototype>? type = null)
        => TryGetZone(tile, out zone, type == null ? new HashSet<ProtoId<ZonePrototype>>() : new() { type.Value });

    /// <summary>
    /// Searches for a zone located in given tile.
    /// </summary>
    /// <param name="tile">Tile.</param>
    /// <param name="zone">Found zone entities.</param>
    /// <param name="types">The types of zone to search for. If empty, any zone will be found.</param>
    /// <returns>True, if found a zone to which this tile is assigned.</returns>
    public bool TryGetZone(
        TileRef tile,
        [NotNullWhen(true)] out HashSet<Entity<ZoneComponent>>? zone,
        HashSet<ProtoId<ZonePrototype>> types)
    {
        zone = new HashSet<Entity<ZoneComponent>>();

        _lookup.GetLocalEntitiesIntersecting(tile.GridUid, tile.GridIndices, zone);

        if (types.Count > 0)
            zone = zone.Where(x => types.Contains(x.Comp.Proto)).ToHashSet();

        return zone.Count != 0;
    }

    /// <summary>
    /// Searching for a zone at the specified coordinates.
    /// </summary>
    /// <param name="coords">Target coordinates.</param>
    /// <param name="zone">Found zone entities.</param>
    /// <returns>True, if found a zone in the target coordinates.</returns>
    [PublicAPI, Pure]
    public bool TryGetZone(
        EntityCoordinates coords,
        [NotNullWhen(true)] out HashSet<Entity<ZoneComponent>>? zone)
    {
        zone = null;
        return _turf.TryGetTileRef(coords, out var tile) && TryGetZone(tile.Value, out zone);
    }

    /// <summary>
    /// Returns the zone entity by its Net ID.
    /// </summary>
    /// <param name="netEnt">Zone Net ID.</param>
    /// <param name="stock">Zone entity.</param>
    [PublicAPI, Pure]
    public bool TryGetZone(
        [NotNullWhen(true)] NetEntity? netEnt,
        [NotNullWhen(true)] out Entity<ZoneComponent>? stock)
    {
        stock = null;
        return TryGetEntity(netEnt, out var uid) && TryGetZone(uid.Value, out stock);
    }

    [PublicAPI, Pure]
    public bool TryGetZone(
        [NotNullWhen(true)] EntityUid? uid,
        [NotNullWhen(true)] out Entity<ZoneComponent>? stock)
    {
        stock = null;

        if (!_zoneQuery.TryComp(uid, out var comp))
            return false;

        stock = new(uid.Value, comp);
        return true;
    }
}
