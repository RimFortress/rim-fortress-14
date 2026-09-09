using System.Numerics;
using Content.Shared._RF.Zoning.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Systems;

public partial class ZoningSystem
{
    public bool TileValidCheck<T>(T condition, TileRef tile) where T : BaseZoneCondition<T>
    {
        var ev = new ZoneAddTileCheck<T>(condition, tile, true);
        RaiseLocalEvent(ref ev);
        return ev.Result;
    }

    public bool TileCheck<T>(T condition, IReadOnlySet<TileRef> tiles) where T : BaseZoneCondition<T>
    {
        var ev = new ZoneTileCheck<T>(condition, tiles, true);
        RaiseLocalEvent(ref ev);
        return ev.Result;
    }

    public bool EntityCheck<T>(T condition, IReadOnlySet<EntityUid> entities) where T : BaseZoneCondition<T>
    {
        var ev = new ZoneEntityCheck<T>(condition, entities, true);
        RaiseLocalEvent(ref ev);
        return ev.Result;
    }

    public ZoneConditionDesc ConditionDescription<T>(
        T condition,
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities)
        where T : BaseZoneCondition<T>
    {
        var ev = new GetZoneConditionDescription<T>(condition, tile, tiles, entities, new());
        RaiseLocalEvent(ref ev);
        return ev.Result;
    }

    /// <inheritdoc cref="IZoneConditionChecker.TileValidCheck"/>
    [PublicAPI, Pure]
    public bool TileValidCheck(ZoneCondition condition, TileRef tile)
        => condition.TileValidCheck(tile, this);

    /// <inheritdoc cref="IZoneConditionChecker.TileValidCheck"/>
    [PublicAPI, Pure]
    public bool TileValidCheck(IEnumerable<ZoneCondition> conditions, TileRef tile)
    {
        foreach (var condition in conditions)
        {
            if (!TileValidCheck(condition, tile))
                return false;
        }

        return true;
    }

    /// <inheritdoc cref="IZoneConditionChecker.TileValidCheck"/>
    [PublicAPI, Pure]
    public bool TileValidCheck(ZoneComponent comp, TileRef tile)
        => !comp.Conditions.TryGetValue(ZoneConditionType.TileAdd, out var conditions)
           || TileValidCheck(conditions, tile);

    /// <inheritdoc cref="IZoneConditionChecker.TileValidCheck"/>
    [PublicAPI, Pure]
    public bool TileValidCheck(EntProtoId<ZoneComponent> protoId, TileRef tile)
        => _proto.Resolve(protoId, out var proto)
           && proto.TryComp(out ZoneComponent? zone, EntityManager.ComponentFactory)
           && TileValidCheck(zone, tile);

    /// <inheritdoc cref="IZoneConditionChecker.TileValidCheck"/>
    [PublicAPI, Pure]
    public bool TileValidCheck(Entity<ZoneComponent> ent, TileRef tile) => TileValidCheck(ent.Comp, tile);

    /// <inheritdoc cref="IZoneConditionChecker.TileCheck"/>
    [PublicAPI, Pure]
    public bool TileCheck(ZoneCondition condition, IReadOnlySet<TileRef> tiles)
        => condition.TileCheck(tiles, this);

    /// <inheritdoc cref="IZoneConditionChecker.TileCheck"/>
    [PublicAPI, Pure]
    public bool TileCheck(IEnumerable<ZoneCondition> conditions, IReadOnlySet<TileRef> tiles)
    {
        foreach (var condition in conditions)
        {
            if (!TileCheck(condition, tiles))
                return false;
        }

        return true;
    }

    /// <inheritdoc cref="IZoneConditionChecker.TileCheck"/>
    [PublicAPI, Pure]
    public bool TileCheck(ZoneComponent comp, IReadOnlySet<TileRef> tiles)
        => !comp.Conditions.TryGetValue(ZoneConditionType.Tile, out var conditions)
           || TileCheck(conditions, tiles);

    /// <inheritdoc cref="IZoneConditionChecker.TileCheck"/>
    [PublicAPI, Pure]
    public bool TileCheck(Entity<ZoneComponent> ent)
    {
        if (_transform.GetGrid(ent.Owner) is not { } grid)
            return false;

        var tiles = new HashSet<TileRef>();

        foreach (var ind in ent.Comp.Tiles)
        {
            var coords = new EntityCoordinates(grid, ind + new Vector2(0.5f));

            if (_turf.TryGetTileRef(coords, out var @ref))
                tiles.Add(@ref.Value);
        }

        return TileCheck(ent.Comp, tiles);
    }

    /// <inheritdoc cref="IZoneConditionChecker.EntityCheck"/>
    [PublicAPI, Pure]
    public bool EntityCheck(ZoneCondition condition, IReadOnlySet<EntityUid> entities)
        => condition.EntityCheck(entities, this);

    /// <inheritdoc cref="IZoneConditionChecker.TileCheck"/>
    [PublicAPI, Pure]
    public bool EntityCheck(IEnumerable<ZoneCondition> conditions, IReadOnlySet<EntityUid> entities)
    {
        foreach (var condition in conditions)
        {
            if (!EntityCheck(condition, entities))
                return false;
        }

        return true;
    }

    /// <inheritdoc cref="IZoneConditionChecker.EntityCheck"/>
    [PublicAPI, Pure]
    public bool EntityCheck(ZoneComponent comp, IReadOnlySet<EntityUid> entities)
        => !comp.Conditions.TryGetValue(ZoneConditionType.Entity, out var conditions)
           || EntityCheck(conditions, entities);

    /// <inheritdoc cref="IZoneConditionChecker.EntityCheck"/>
    [PublicAPI, Pure]
    public bool EntityCheck(Entity<ZoneComponent> ent) => EntityCheck(ent.Comp, ent.Comp.Entities);

    /// <inheritdoc cref="IZoneConditionChecker.ConditionDescription"/>
    [PublicAPI, Pure]
    public ZoneConditionDesc ConditionDescription(
        ZoneCondition condition,
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities)
        => condition.ConditionDescription(tile, tiles, entities, this);

    /// <inheritdoc cref="IZoneConditionChecker.ConditionDescription"/>
    [PublicAPI, Pure]
    public List<ZoneConditionDesc> ConditionDescription(
        IEnumerable<ZoneCondition> conditions,
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities)
    {
        var result = new List<ZoneConditionDesc>();

        foreach (var condition in conditions)
        {
            result.Add(ConditionDescription(condition, tile, tiles, entities));
        }

        DebugTools.Assert(result.Count > 0);
        return result;
    }

    /// <inheritdoc cref="IZoneConditionChecker.ConditionDescription"/>
    [PublicAPI, Pure]
    public List<ZoneConditionDesc> ConditionDescription(Entity<ZoneComponent> ent)
    {
        var desc = new List<ZoneConditionDesc>();

        var tiles = GetTileRefs(ent);

        if (ent.Comp.Conditions.TryGetValue(ZoneConditionType.Tile, out var conditions))
        {
            foreach (var condition in conditions)
            {
                desc.Add(ConditionDescription(condition, null, tiles, ent.Comp.Entities));
            }
        }

        if (ent.Comp.Conditions.TryGetValue(ZoneConditionType.Entity, out conditions))
        {
            foreach (var condition in conditions)
            {
                desc.Add(ConditionDescription(condition, null, tiles, ent.Comp.Entities));
            }
        }

        return desc;
    }

    /// <summary>
    /// Returns a description of all conditions for adding a target tile to a zone.
    /// </summary>
    [PublicAPI, Pure]
    public List<ZoneConditionDesc> TileAddConditionsDescription(ZoneComponent comp, TileRef tile)
    {
        var desc = new List<ZoneConditionDesc>();

        if (!comp.Conditions.TryGetValue(ZoneConditionType.TileAdd, out var conditions))
            return desc;

        foreach (var condition in conditions)
        {
            desc.Add(ConditionDescription(condition, tile, new HashSet<TileRef>(), new HashSet<EntityUid>()));
        }

        return desc;
    }
}
