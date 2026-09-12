using System.Linq;
using System.Numerics;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Zoning.Components;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Systems;

/// <summary>
/// A system that provides an API for zoning.
/// </summary>
public sealed partial class ZoningSystem : EntitySystem, IZoneConditionChecker
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private FixtureSystem _fixture = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private OwnershipSystem _ownership = default!;
    [Dependency] private EntityQuery<ZoneComponent> _zoneQuery;
    [Dependency] private EntityQuery<ZoneCreatorComponent> _zoneCreatorQuery;
    [Dependency] private EntityQuery<ZoneVisualsComponent> _zoneVisualsQuery;

    /// <summary>
    /// An event invoked on the client whenever the zone controlled by the player changes.
    /// </summary>
    public event Action<Entity<ZoneComponent>>? OnZoneUpdated;

    /// <summary>
    /// An event raised on the client each time <see cref="ZoneComponent"/> is initialized.
    /// </summary>
    public event Action<Entity<ZoneComponent>>? OnZoneInit;

    private void UpdateFixtures(Entity<ZoneComponent> ent, bool dirty = true)
    {
        var coords = Transform(ent).Coordinates;

        switch (ent.Comp.CollisionMode)
        {
            case ZoneCollisionMode.None:
                DebugTools.Assert(ent.Comp.MonoFixtures.Count == 0);
                DebugTools.Assert(ent.Comp.TileFixtures.Count == 0);
                break;
            case ZoneCollisionMode.Mono:
                DebugTools.Assert(ent.Comp.TileFixtures.Count == 0);

                foreach (var fix in ent.Comp.MonoFixtures)
                {
                    _fixture.DestroyFixture(ent, fix, updates: false);
                }

                ent.Comp.MonoFixtures.Clear();

                foreach (var region in GetRegions(ent.Comp.Tiles))
                {
                    foreach (var rect in DecomposeIntoRectangles(region))
                    {
                        var id = ZoneComponent.MonoFixtureId + $"{ent.Comp.MonoFixtures.Count}";

                        if (_fixture.TryCreateFixture(ent,
                                GetRectShape(rect, coords.Position),
                                id,
                                density: 0f,
                                hard: false,
                                collisionLayer: (int)ent.Comp.Layer,
                                collisionMask: (int)ent.Comp.Mask,
                                updates: false))
                            ent.Comp.MonoFixtures.Add(id);
                    }
                }

                _fixture.FixtureUpdate(ent);
                _physics.WakeBody(ent, force: true);

                if (dirty)
                    DirtyField(ent.AsNullable(), nameof(ZoneComponent.MonoFixtures));
                break;
            case ZoneCollisionMode.Tile:
                DebugTools.Assert(ent.Comp.MonoFixtures.Count == 0);
                var toRemove = new HashSet<Vector2i>();

                foreach (var (tile, fix) in ent.Comp.TileFixtures)
                {
                    if (ent.Comp.Tiles.Contains(tile))
                        continue;

                    _fixture.DestroyFixture(ent, fix);
                    toRemove.Add(tile);
                }

                foreach (var tile in toRemove)
                {
                    ent.Comp.TileFixtures.Remove(tile);
                }

                foreach (var tile in ent.Comp.Tiles)
                {
                    if (ent.Comp.TileFixtures.ContainsKey(tile))
                        continue;

                    var id = ZoneComponent.MonoFixtureId + $"{tile.X}_{tile.Y}";

                    if (_fixture.TryCreateFixture(ent,
                            GetTileShape(tile, coords.Position),
                            id,
                            density: 0f,
                            hard: false,
                            collisionLayer: (int)ent.Comp.Layer,
                            collisionMask: (int)ent.Comp.Mask))
                        ent.Comp.TileFixtures[tile] = id;
                }

                if (dirty)
                    DirtyField(ent.AsNullable(), nameof(ZoneComponent.MonoFixtures));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ent.Comp.CollisionMode));
        }
    }

    private static HashSet<HashSet<Vector2i>> GetRegions(IReadOnlySet<Vector2i> tiles)
    {
        var regions = new HashSet<HashSet<Vector2i>>();
        var notAdded = new HashSet<Vector2i>(tiles);

        while (notAdded.Count > 0)
        {
            var region = new HashSet<Vector2i>();
            var queue = new Queue<Vector2i>();
            queue.Enqueue(notAdded.First());

            while (queue.TryDequeue(out var tile))
            {
                if (!notAdded.Remove(tile))
                    continue;

                region.Add(tile);
                queue.Enqueue(tile + Vector2i.Up);
                queue.Enqueue(tile + Vector2i.Right);
                queue.Enqueue(tile + Vector2i.Down);
                queue.Enqueue(tile + Vector2i.Left);
            }

            DebugTools.Assert(region.Count > 0);
            regions.Add(region);
        }

        return regions;
    }

    /// <summary>
    /// Decomposes a set of tiles into a minimal-ish set of maximal axis-aligned rectangles
    /// (greedy "grow width, then grow height while the whole row stays free" sweep). Each
    /// rectangle is trivially convex, so it can become a single PolygonShape fixture -
    /// PolygonShape.Set() computes a convex hull internally and silently drops any concave
    /// vertices, so a single polygon can never represent an arbitrary tile region; this
    /// sidesteps that by never handing it anything but boxes.
    /// </summary>
    private static List<Box2i> DecomposeIntoRectangles(IReadOnlySet<Vector2i> tiles)
    {
        var rects = new List<Box2i>();
        var remaining = new HashSet<Vector2i>(tiles);

        while (remaining.Count > 0)
        {
            // Deterministic starting point (lowest Y, then lowest X) so the same tile set
            // always decomposes into the same rectangles - makes fixture diffs/tests stable.
            var origin = remaining.OrderBy(t => t.Y).ThenBy(t => t.X).First();

            var width = 1;

            while (remaining.Contains(origin + new Vector2i(width, 0)))
            {
                width++;
            }

            var height = 1;
            var canGrow = true;

            while (canGrow)
            {
                for (var dx = 0; dx < width; dx++)
                {
                    if (remaining.Contains(origin + new Vector2i(dx, height)))
                        continue;

                    canGrow = false;
                    break;
                }

                if (canGrow)
                    height++;
            }

            rects.Add(new Box2i(origin.X, origin.Y, origin.X + width, origin.Y + height));

            for (var dx = 0; dx < width; dx++)
            {
                for (var dy = 0; dy < height; dy++)
                {
                    remaining.Remove(origin + new Vector2i(dx, dy));
                }
            }
        }

        return rects;
    }

    private static PolygonShape GetRectShape(Box2i rect, Vector2 center)
    {
        var shape = new PolygonShape();
        shape.SetAsBox(new Box2(
            rect.Left - center.X,
            rect.Bottom - center.Y,
            rect.Right - center.X,
            rect.Top - center.Y));
        return shape;
    }

    private static PolygonShape GetTileShape(Vector2i tile, Vector2 center)
    {
        var shape = new PolygonShape();
        var offset = tile - center + new Vector2(0.5f);
        shape.SetAsBox(0.5f, 0.5f, offset, 0f);
        return shape;
    }

    private static EntityCoordinates TilesCenter(IReadOnlySet<TileRef> tiles)
    {
        var pos = Vector2.Zero;

        foreach (var ind in tiles)
        {
            pos += ind.GridIndices + new Vector2(0.5f);
        }

        pos /= tiles.Count;
        return new EntityCoordinates(tiles.FirstOrNull()?.GridUid ?? EntityUid.Invalid, pos);
    }

    private bool AddTile(Entity<ZoneComponent> ent, TileRef tile, bool updateFixture, bool validate, bool dirty)
    {
        if (!TileValidCheck(ent, tile))
            return false;

        ent.Comp.Tiles.Add(tile.GridIndices);
        RaiseLocalEvent(ent, new ZoneTileAdded(tile));

        if (updateFixture)
            UpdateFixtures(ent, dirty);

        if (validate)
        {
            ValidateSplit(ent, dirty);
            ValidZoneTiles(ent, TileCheck(ent));
        }

        if (!dirty)
            return true;

        DirtyField(ent.AsNullable(), nameof(ZoneComponent.Tiles));

        switch (ent.Comp.CollisionMode)
        {
            case ZoneCollisionMode.Tile:
                DirtyField(ent.AsNullable(), nameof(ZoneComponent.TileFixtures));
                break;
            case ZoneCollisionMode.Mono:
                DirtyField(ent.AsNullable(), nameof(ZoneComponent.MonoFixtures));
                break;
        }

        return true;
    }

    private void AddTile(Entity<ZoneComponent> ent, IReadOnlySet<TileRef> tiles, bool validate, bool dirty)
    {
        foreach (var tile in tiles)
        {
            AddTile(ent, tile, false, false, false);
        }

        UpdateFixtures(ent, dirty);

        if (validate)
        {
            ValidateSplit(ent, dirty);
            ValidZoneTiles(ent, TileCheck(ent));
        }

        if (!dirty)
            return;

        DirtyField(ent.AsNullable(), nameof(ZoneComponent.Tiles));

        switch (ent.Comp.CollisionMode)
        {
            case ZoneCollisionMode.Tile:
                DirtyField(ent.AsNullable(), nameof(ZoneComponent.TileFixtures));
                break;
            case ZoneCollisionMode.Mono:
                DirtyField(ent.AsNullable(), nameof(ZoneComponent.MonoFixtures));
                break;
        }
    }

    private bool RemoveTile(Entity<ZoneComponent> ent, TileRef tile, bool updateFixture, bool validate, bool dirty)
    {
        if (_transform.GetGrid(ent.Owner) != tile.GridUid
            || !ent.Comp.Tiles.Remove(tile.GridIndices))
            return false;

        RaiseLocalEvent(ent, new ZoneTileRemoved(tile));

        if (updateFixture)
            UpdateFixtures(ent, dirty);

        if (validate)
        {
            ValidateSplit(ent, dirty);
            ValidZoneTiles(ent, TileCheck(ent));
        }

        if (!dirty)
            return true;

        DirtyField(ent.AsNullable(), nameof(ZoneComponent.Tiles));
        return true;
    }

    private void RemoveTile(Entity<ZoneComponent> ent, IReadOnlySet<TileRef> tiles, bool validate, bool dirty)
    {
        foreach (var tile in tiles)
        {
            RemoveTile(ent, tile, false, false, false);
        }

        UpdateFixtures(ent, dirty);

        if (validate)
        {
            ValidateSplit(ent, dirty);
            ValidZoneTiles(ent, TileCheck(ent));
        }

        if (!dirty)
            return;

        DirtyField(ent.AsNullable(), nameof(ZoneComponent.Tiles));

        switch (ent.Comp.CollisionMode)
        {
            case ZoneCollisionMode.Tile:
                DirtyField(ent.AsNullable(), nameof(ZoneComponent.TileFixtures));
                break;
            case ZoneCollisionMode.Mono:
                DirtyField(ent.AsNullable(), nameof(ZoneComponent.MonoFixtures));
                break;
        }
    }

    private bool TryEnter(Entity<ZoneComponent> ent, EntityUid toEnter, bool validate = true, bool dirty = true)
    {
        if (ent.Comp.Entities.Contains(toEnter)
            || !_whitelist.CheckBoth(toEnter,
                whitelist: ent.Comp.CollisionWhitelist,
                blacklist: ent.Comp.CollisionBlacklist))
            return false;

        var tile = ent.Comp.CollisionMode == ZoneCollisionMode.Tile
            ? _turf.GetTileRef(Transform(toEnter).Coordinates)
            : null;
        var ev = new BeforeZoneEnter(toEnter, tile);
        RaiseLocalEvent(ent, ref ev);

        if (ev.Canceled)
            return false;

        ent.Comp.Entities.Add(toEnter);
        var ev1 = new EntityEnteredZone(ent, tile, toEnter);
        RaiseLocalEvent(ent, ev1);
        RaiseLocalEvent(toEnter, ev1);

        if (validate)
            ValidZoneEntities(ent, EntityCheck(ent));

        if (!dirty)
            return true;

        DirtyField(ent.AsNullable(), nameof(ZoneComponent.Entities));
        return true;
    }

    private void TryEnter(Entity<ZoneComponent> ent, IEnumerable<EntityUid> toEnter)
    {
        foreach (var uid in toEnter)
        {
            TryEnter(ent, uid, false, false);
        }

        ValidZoneEntities(ent, EntityCheck(ent));
        DirtyField(ent.AsNullable(), nameof(ZoneComponent.Entities));
    }

    private bool TryLeave(Entity<ZoneComponent> ent, EntityUid toLeave, bool validate = true, bool dirty = true)
    {
        if (!ent.Comp.Entities.Remove(toLeave))
            return false;

        var tile = ent.Comp.CollisionMode == ZoneCollisionMode.Tile
            ? _turf.GetTileRef(Transform(toLeave).Coordinates)
            : null;
        var ev1 = new EntityLeavedZone(ent, tile, toLeave);
        RaiseLocalEvent(ent, ev1);
        RaiseLocalEvent(toLeave, ev1);

        if (validate)
            ValidZoneEntities(ent, EntityCheck(ent));

        if (!dirty)
            return true;

        DirtyField(ent.AsNullable(), nameof(ZoneComponent.Entities));
        return true;
    }

    private void TryLeave(Entity<ZoneComponent> ent, IEnumerable<EntityUid> toLeave, bool validate = true, bool dirty = true)
    {
        foreach (var uid in toLeave)
        {
            TryLeave(ent, uid, false, false);
        }

        if (validate)
            ValidZoneEntities(ent, EntityCheck(ent));

        if (!dirty)
            return;

        DirtyField(ent.AsNullable(), nameof(ZoneComponent.Entities));
    }

    private void ValidZoneTiles(Entity<ZoneComponent> ent, bool valid)
    {
        var old = ent.Comp.Valid;

        if (ent.Comp.TilesValid == valid)
            return;

        ent.Comp.TilesValid = valid;
        DirtyField(ent.AsNullable(), nameof(ZoneComponent.TilesValid));

        if (old && !ent.Comp.Valid)
            RaiseLocalEvent(ent, new ZoneInvalid());

        if (!old && ent.Comp.Valid)
            RaiseLocalEvent(ent, new ZoneValid());
    }

    private void ValidZoneEntities(Entity<ZoneComponent> ent, bool valid)
    {
        var old = ent.Comp.Valid;

        if (ent.Comp.EntitiesValid == valid)
            return;

        ent.Comp.EntitiesValid = valid;
        DirtyField(ent.AsNullable(), nameof(ZoneComponent.EntitiesValid));

        if (old && !ent.Comp.Valid)
            RaiseLocalEvent(ent, new ZoneInvalid());

        if (!old && ent.Comp.Valid)
            RaiseLocalEvent(ent, new ZoneValid());
    }

    private void ValidateSplit(Entity<ZoneComponent> ent, bool dirty = true)
    {
        if (ent.Comp.SplitMode == ZoneSplitMode.None)
            return;

        var regions = GetRegions(ent.Comp.Tiles);

        if (regions.Count <= 1)
            return;

        var largest = regions.MaxBy(x => x.Count)!;
        regions.Remove(largest);

        ent.Comp.Tiles = largest;
        UpdateFixtures(ent, dirty);

        foreach (var region in regions)
        {
            var tileRefs = GetTileRefs(ent, region);

            foreach (var tile in tileRefs)
            {
                RaiseLocalEvent(ent, new ZoneTileRemoved(tile));
            }

            if (ent.Comp.SplitMode == ZoneSplitMode.Split
                && Prototype(ent) is { } entProto)
                TryCreateZone(entProto.ID, tileRefs, out _);
        }

        if (dirty)
            DirtyField(ent.AsNullable(), nameof(ZoneComponent.Tiles));
    }

    private HashSet<TileRef> GetTileRefs(Entity<ZoneComponent> ent) => GetTileRefs(ent, ent.Comp.Tiles);

    private HashSet<TileRef> GetTileRefs(Entity<ZoneComponent> ent, IReadOnlySet<Vector2i> tiles)
        => _transform.GetGrid(ent.Owner) is not { } grid ? new() : GetTileRefs(grid, tiles);

    private HashSet<TileRef> GetTileRefs(EntityUid grid, IReadOnlySet<Vector2i> tiles)
    {
        var refs = new HashSet<TileRef>();

        if (!HasComp<MapGridComponent>(grid))
            return refs;

        foreach (var tile in tiles)
        {
            var coords = new EntityCoordinates(grid, tile + new Vector2(0.5f));

            if (_turf.TryGetTileRef(coords, out var @ref))
                refs.Add(@ref.Value);
        }

        return refs;
    }

    private bool CanControl(ICommonSession session, Entity<ZoneComponent> zone)
        => session.AttachedEntity is { } player
           && _zoneCreatorQuery.TryComp(player, out var creator)
           && Prototype(zone) is { } proto
           && creator.Zones.Contains(proto.ID)
           && _ownership.HasOwner(zone.Owner, player);
}

public interface IZoneConditionChecker
{
    /// <inheritdoc cref="ZoneCondition.TileValidCheck"/>
    bool TileValidCheck<T>(T condition, TileRef tile) where T : BaseZoneCondition<T>;

    /// <inheritdoc cref="ZoneCondition.TileCheck"/>
    bool TileCheck<T>(T condition, IReadOnlySet<TileRef> tiles) where T : BaseZoneCondition<T>;

    /// <inheritdoc cref="ZoneCondition.EntityCheck"/>
    bool EntityCheck<T>(T condition, IReadOnlySet<EntityUid> entities) where T : BaseZoneCondition<T>;

    /// <inheritdoc cref="ZoneCondition.ConditionDescription(TileRef, IZoneConditionChecker)"/>
    ZoneConditionDesc ConditionDescription<T>(T condition, TileRef tile) where T : BaseZoneCondition<T>;

    /// <inheritdoc cref="ZoneCondition.ConditionDescription(IReadOnlySet{TileRef}, IZoneConditionChecker)"/>
    ZoneConditionDesc ConditionDescription<T>(T condition, IReadOnlySet<TileRef> tiles) where T : BaseZoneCondition<T>;

    /// <inheritdoc cref="ZoneCondition.ConditionDescription(IReadOnlySet{EntityUid}, IZoneConditionChecker)"/>
    ZoneConditionDesc ConditionDescription<T>(T condition, IReadOnlySet<EntityUid> entities) where T : BaseZoneCondition<T>;
}
