using System.Linq;
using System.Numerics;
using Content.Client.Decals;
using Content.Client.Parallax;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Decals;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;

namespace Content.Client._RF.Parallax.Fog;

public sealed partial class FogOfWarSystem : SharedFogOfWarSystem
{
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private EntityQuery<FogOfWarComponent> _fogQuery;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery;
    [Dependency] private EntityQuery<BiomeComponent> _biomeQuery;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery;

    private readonly Queue<(EntityUid Map, Vector2i Chunk, HashSet<Vector2i> Modified)> _pending = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new FogOfWarOverlay());
        _cfg.OnValueChanged(RfVars.FogMaxFrozenChunks, OnMaxFrozenChunksChanged);
    }

    #region Frozen chunks

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(RfVars.FogMaxFrozenChunks, OnMaxFrozenChunksChanged);
    }

    private void OnMaxFrozenChunksChanged(int value)
    {
        var query = EntityQueryEnumerator<FogOfWarComponent>();
        while (query.MoveNext(out var uid, out var fog))
        {
            EvictOldest((uid, fog));
        }
    }

    private void RequestFullState(Entity<FogOfWarComponent> ent)
    {
        if (!ent.Comp.PersistChunks || _player.LocalSession == null)
            return;

        RaiseNetworkEvent(new FogOfWarFullStateRequest(GetNetEntity(ent)));
    }

    [SubscribeLocalEvent]
    private void OnFogStartup(Entity<FogOfWarComponent> ent, ref ComponentStartup args)
    {
        RequestFullState(ent);
    }

    [SubscribeLocalEvent]
    private void OnFogShutdown(Entity<FogOfWarComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.FowGrid.IsValid() && Exists(ent.Comp.FowGrid) && LifeStage(ent.Comp.FowGrid) < EntityLifeStage.Terminating)
            Del(ent.Comp.FowGrid);

        ent.Comp.FowGrid = EntityUid.Invalid;
        ent.Comp.FrozenChunks.Clear();
        ent.Comp.FrozenOrder.Clear();
        ent.Comp.FrozenNodes.Clear();
        ent.Comp.FrozenEntities.Clear();
        ent.Comp.FrozenDecals.Clear();
    }

    [SubscribeNetworkEvent]
    private void OnFullState(FogOfWarFullStateMessage msg)
    {
        if (!TryGetEntity(msg.Map, out var map) || !_fogQuery.HasComp(map))
            return;

        var chunks = msg.Chunks.AsEnumerable();
        var max = _cfg.GetCVar(RfVars.FogMaxFrozenChunks);

        // Only the closest chunks fit, the rest would be evicted right away
        if (max > 0 && msg.Chunks.Count > max)
        {
            var center = _player.LocalEntity is { } player ? _transform.GetWorldPosition(player) : Vector2.Zero;
            chunks = chunks.OrderBy(c => (c.Key + new Vector2(SharedBiomeSystem.ChunkSize) / 2 - center).LengthSquared())
                .Take(max);
        }

        foreach (var (chunk, modified) in chunks)
        {
            _pending.Enqueue((map.Value, chunk, modified));
        }
    }

    [SubscribeNetworkEvent]
    private void OnChunkFrozen(FogOfWarChunkFrozen msg)
    {
        if (!TryGetEntity(msg.Map, out var map) || !_fogQuery.HasComp(map))
            return;

        _pending.Enqueue((map.Value, msg.Chunk, msg.ModifiedTiles));
    }

    [SubscribeNetworkEvent]
    private void OnChunkUnfrozen(FogOfWarChunkUnfrozen msg)
    {
        if (!TryGetEntity(msg.Map, out var map) || !_fogQuery.TryComp(map, out var fog))
            return;

        // The chunk may not have been copied yet
        var remaining = _pending.Where(p => !(p.Map == map.Value && p.Chunk == msg.Chunk)).ToArray();
        _pending.Clear();
        foreach (var item in remaining)
        {
            _pending.Enqueue(item);
        }

        UnfreezeChunk((map.Value, fog), msg.Chunk);
    }

    private void ProcessPending()
    {
        var perFrame = Math.Max(1, _cfg.GetCVar(RfVars.FogChunksPerFrame));

        for (var i = 0; i < perFrame && _pending.TryDequeue(out var item); i++)
        {
            if (!_fogQuery.TryComp(item.Map, out var fog)
                || !_biomeQuery.TryComp(item.Map, out var biome)
                || !_gridQuery.TryComp(item.Map, out var grid)
                || !fog.PersistChunks
                || !fog.FrozenChunks.Add(item.Chunk))
                continue;

            fog.FrozenNodes[item.Chunk] = fog.FrozenOrder.AddLast(item.Chunk);
            EvictOldest((item.Map, fog), item.Chunk);

            FreezeChunk((item.Map, fog, biome, grid), item.Chunk, item.Modified);
        }
    }

    /// <summary>
    /// Removes the oldest frozen chunks if there are more of them than the limit allows
    /// </summary>
    private void EvictOldest(Entity<FogOfWarComponent> ent, Vector2i? keep = null)
    {
        var max = _cfg.GetCVar(RfVars.FogMaxFrozenChunks);

        if (max <= 0)
            return;

        while (ent.Comp.FrozenChunks.Count > max && ent.Comp.FrozenOrder.First is { } oldest)
        {
            if (oldest.Value == keep)
                break;

            UnfreezeChunk(ent, oldest.Value);
        }
    }

    private bool EnsureFowGrid(Entity<FogOfWarComponent> ent)
    {
        if (ent.Comp.FowGrid.IsValid() && Exists(ent.Comp.FowGrid))
            return true;

        if (LifeStage(ent) >= EntityLifeStage.Terminating)
            return false;

        var fowGrid = _map.CreateGridEntity(ent.Owner);

        EnsureComp<RoofComponent>(fowGrid);

        ent.Comp.FowGrid = fowGrid;
        return true;
    }

    private void FreezeChunk(
        Entity<FogOfWarComponent, BiomeComponent, MapGridComponent> source,
        Vector2i chunk,
        HashSet<Vector2i> modified)
    {
        if (!EnsureFowGrid((source, source.Comp1)))
        {
            UnfreezeChunk((source, source.Comp1), chunk);
            return;
        }

        var fog = source.Comp1;
        var biome = source.Comp2;
        var sourceGrid = source.Comp3;
        var fowGrid = new Entity<MapGridComponent>(fog.FowGrid, Comp<MapGridComponent>(fog.FowGrid));
        var chunkSize = SharedBiomeSystem.ChunkSize;
        var sourceEnt = new Entity<MapGridComponent>(source, sourceGrid);

        // Tiles
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = 0; x < chunkSize; x++)
        {
            for (var y = 0; y < chunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (_map.TryGetTileRef(source, sourceGrid, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
                {
                    tiles.Add((indices, tileRef.Tile));
                    continue;
                }

                if (!_biome.TryGetBiomeTile(indices, biome.Layers, biome.Seed, sourceEnt, out var biomeTile))
                    continue;

                tiles.Add((indices, biomeTile.Value));
            }
        }

        SetTilesSafe(fowGrid, tiles);

        // Entities
        var entities = new HashSet<EntityUid>();
        fog.FrozenEntities[chunk] = entities;

        for (var x = 0; x < chunkSize; x++)
        {
            for (var y = 0; y < chunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified.Contains(indices))
                    continue;

                if (!_biome.TryGetEntity(indices, biome, sourceEnt, out var prototype))
                    continue;

                // Frozen entities live on the frozen grid, which is the one that has the tiles
                var ent = Spawn(prototype, _map.GridTileToLocal(fowGrid, fowGrid.Comp, indices));

                if (_xformQuery.TryComp(ent, out var xform) && !xform.Anchored)
                    _transform.AnchorEntity((ent, xform), fowGrid, indices);

                entities.Add(ent);
            }
        }

        // Decals
        var decals = new HashSet<DecalIndex>();
        fog.FrozenDecals[chunk] = decals;

        if (!fog.PersistDecals)
            return;

        for (var x = 0; x < chunkSize; x++)
        {
            for (var y = 0; y < chunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified.Contains(indices)
                    || !_biome.TryGetDecals(indices, biome.Layers, biome.Seed, sourceEnt, out var tileDecals))
                    continue;

                foreach (var (id, position) in tileDecals)
                {
                    var decal = new Decal(position, id, null, Angle.Zero, 0, false);

                    if (_decals.TryAddDecal(decal, new EntityCoordinates(fowGrid, position), out var index))
                        decals.Add(index);
                }
            }
        }
    }

    private void UnfreezeChunk(Entity<FogOfWarComponent> ent, Vector2i chunk)
    {
        var fog = ent.Comp;

        if (!fog.FrozenChunks.Remove(chunk))
            return;

        if (fog.FrozenNodes.Remove(chunk, out var node))
            fog.FrozenOrder.Remove(node);

        if (!fog.FowGrid.IsValid() || !Exists(fog.FowGrid))
            return;

        if (fog.FrozenEntities.Remove(chunk, out var entities))
        {
            foreach (var uid in entities)
            {
                if (Exists(uid))
                    Del(uid);
            }
        }

        if (fog.FrozenDecals.Remove(chunk, out var decals))
        {
            foreach (var decal in decals)
            {
                _decals.RemoveDecal(fog.FowGrid, decal);
            }
        }

        if (!_gridQuery.TryComp(fog.FowGrid, out var fowGrid))
            return;

        var chunkSize = SharedBiomeSystem.ChunkSize;
        var tiles = new List<(Vector2i, Tile)>(chunkSize * chunkSize);

        for (var x = 0; x < chunkSize; x++)
        {
            for (var y = 0; y < chunkSize; y++)
            {
                tiles.Add((new Vector2i(x + chunk.X, y + chunk.Y), Tile.Empty));
            }
        }

        SetTilesSafe((fog.FowGrid, fowGrid), tiles);
    }

    /// <summary>
    /// <see cref="SharedMapSystem.SetTiles(Entity{MapGridComponent}, List{ValueTuple{Vector2i, Tile}})"/> raises
    /// <see cref="TileChangedEvent"/> with the tile offset inside the chunk instead of the chunk index
    /// (SharedMapSystem.Grid.cs, SetTiles). Clyde uses the index to mark chunk meshes as dirty, so the chunk mesh is
    /// never updated, and when the chunk becomes empty <c>Clyde.CullEmptyChunks</c> throws KeyNotFoundException.
    /// SetTile raises the event correctly, so use it until the engine is fixed.
    /// </summary>
    private void SetTilesSafe(Entity<MapGridComponent> grid, List<(Vector2i, Tile)> tiles)
    {
        foreach (var (indices, tile) in tiles)
        {
            _map.SetTile(grid, indices, tile);
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ProcessPending();

        var updated = new HashSet<EntityUid>();
        var enumerator = EntityQueryEnumerator<FogOfWarClearerComponent, TransformComponent>();

        while (enumerator.MoveNext(out var comp, out var xform))
        {
            if (xform.MapUid is not { } map
                || !TryComp(map, out FogOfWarComponent? fog)
                || !fog.Enabled)
                continue;

            if (updated.Add(map))
                fog.LoadedChunks.Clear();

            var localPos = _transform.ToWorldPosition(xform.Coordinates);
            var current = (localPos / SharedBiomeSystem.ChunkSize).Floored();

            if (comp.CurrentChunk == current)
                continue;

            comp.LoadedChunks.Clear();
            comp.CurrentChunk = current;

            var size = new Vector2(comp.Range + SharedBiomeSystem.ChunkSize);
            var box = Box2.CenteredAround(localPos, size);
            var chunkEnumerator = new ChunkIndicesEnumerator(box, SharedBiomeSystem.ChunkSize);

            while (chunkEnumerator.MoveNext(out var chunk))
            {
                fog.LoadedChunks.Add(chunk.Value);
                comp.LoadedChunks.Add(chunk.Value);
            }
        }
    }
}
