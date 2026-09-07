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
    public bool TileValidCheck(ZonePrototype proto, TileRef tile)
        => !proto.Conditions.TryGetValue(ZoneConditionType.TileAdd, out var conditions)
           || TileValidCheck(conditions, tile);

    /// <inheritdoc cref="IZoneConditionChecker.TileValidCheck"/>
    [PublicAPI, Pure]
    public bool TileValidCheck(EntProtoId<ZoneComponent> protoId, TileRef tile)
        => _proto.Resolve(protoId, out var proto)
           && proto.TryComp(out ZoneComponent? zone, EntityManager.ComponentFactory)
           && _proto.Resolve(zone.Proto, out var zoneProto)
           && TileValidCheck(zoneProto, tile);

    /// <inheritdoc cref="IZoneConditionChecker.TileValidCheck"/>
    [PublicAPI, Pure]
    public bool TileValidCheck(Entity<ZoneComponent> ent, TileRef tile)
        => _proto.Resolve(ent.Comp.Proto, out var proto) && TileValidCheck(proto, tile);

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
    public bool TileCheck(ZonePrototype proto, IReadOnlySet<TileRef> tiles)
        => !proto.Conditions.TryGetValue(ZoneConditionType.Tile, out var conditions)
           || TileCheck(conditions, tiles);

    /// <inheritdoc cref="IZoneConditionChecker.TileCheck"/>
    [PublicAPI, Pure]
    public bool TileCheck(Entity<ZoneComponent> ent)
    {
        if (!_proto.Resolve(ent.Comp.Proto, out var proto)
            || _transform.GetGrid(ent.Owner) is not { } grid)
            return false;

        var tiles = new HashSet<TileRef>();

        foreach (var ind in ent.Comp.Tiles)
        {
            var coords = new EntityCoordinates(grid, ind + new Vector2(0.5f));

            if (_turf.TryGetTileRef(coords, out var @ref))
                tiles.Add(@ref.Value);
        }

        return TileCheck(proto, tiles);
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
    public bool EntityCheck(ZonePrototype proto, IReadOnlySet<EntityUid> entities)
        => !proto.Conditions.TryGetValue(ZoneConditionType.Entity, out var conditions)
           || EntityCheck(conditions, entities);

    /// <inheritdoc cref="IZoneConditionChecker.EntityCheck"/>
    [PublicAPI, Pure]
    public bool EntityCheck(Entity<ZoneComponent> ent)
        => _proto.Resolve(ent.Comp.Proto, out var proto)
           && EntityCheck(proto, ent.Comp.Entities);

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

    /// <summary>
    /// Returns a description of all unmet zone validity conditions.
    /// </summary>
    [PublicAPI, Pure]
    public List<ZoneConditionDesc> UnmetConditionsDescription(Entity<ZoneComponent> ent)
    {
        var desc = new List<ZoneConditionDesc>();

        if (!_proto.Resolve(ent.Comp.Proto, out var proto))
            return desc;

        var tiles = GetTileRefs(ent);

        if (proto.Conditions.TryGetValue(ZoneConditionType.Tile, out var conditions))
        {
            foreach (var condition in conditions)
            {
                if (!TileCheck(condition, tiles))
                    desc.Add(ConditionDescription(condition, null, tiles, ent.Comp.Entities));
            }
        }

        if (proto.Conditions.TryGetValue(ZoneConditionType.Entity, out conditions))
        {
            foreach (var condition in conditions)
            {
                if (!EntityCheck(condition, ent.Comp.Entities))
                    desc.Add(ConditionDescription(condition, null, tiles, ent.Comp.Entities));
            }
        }

        return desc;
    }

    /// <summary>
    /// Returns a description of all unmet conditions for adding a target tile to a zone.
    /// </summary>
    [PublicAPI, Pure]
    public List<ZoneConditionDesc> UnmetTileAddConditionsDescription(Entity<ZoneComponent> ent, TileRef tile)
    {
        var desc = new List<ZoneConditionDesc>();

        if (!_proto.Resolve(ent.Comp.Proto, out var proto))
            return desc;

        var tiles = GetTileRefs(ent);

        if (!proto.Conditions.TryGetValue(ZoneConditionType.TileAdd, out var conditions))
            return desc;

        foreach (var condition in conditions)
        {
            if (!TileCheck(condition, tiles))
                desc.Add(ConditionDescription(condition, tile, tiles, ent.Comp.Entities));
        }

        return desc;
    }
}
