using Content.Client.Decals;
using Content.Client.Parallax;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Decals;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._RF.Parallax.Fog;

public sealed class FogOfWarSystem : SharedFogOfWarSystem
{
    [Dependency] private readonly BiomeSystem _biome  = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<FogOfWarComponent> _fogOfWarQuery;

    /// <inheritdoc/>
    public override void Initialize()
    {
        _xformQuery = GetEntityQuery<TransformComponent>();
        _fogOfWarQuery = GetEntityQuery<FogOfWarComponent>();

        _overlay.AddOverlay(new FogOfWarOverlay());

        SubscribeNetworkEvent<FogOfWarChunkAdded>(OnChunkAdded);
        SubscribeNetworkEvent<FogOfWarChunkRemoved>(OnChunkRemoved);
        SubscribeNetworkEvent<FogOfWarFullStateMessage>(OnFullStateRequest);

        SubscribeLocalEvent<FogOfWarComponent, ComponentStartup>(OnFogStartup);
    }

    private void OnFogStartup(EntityUid uid, FogOfWarComponent component, ComponentStartup args)
    {
        var xform = Transform(uid);
        var fowGrid = _mapMan.CreateGridEntity(xform.MapID);

        EnsureComp<RoofComponent>(fowGrid);
        EnsureComp<DecalGridComponent>(fowGrid);

        _transform.SetCoordinates(fowGrid, xform.Coordinates);
        component.FowGrid = fowGrid;

        if (_player.LocalEntity is not { Valid: true } player)
            return;

        var msg = new FogOfWarFullStateRequest(GetNetEntity(player), GetNetEntity(uid));
        RaiseNetworkEvent(msg);
    }

    private void OnChunkAdded(FogOfWarChunkAdded msg)
    {
        var grid = GetEntity(msg.Grid);

        if(!_fogOfWarQuery.TryComp(grid, out var comp) || !comp.FogChunks.Add(msg.Chunk))
            return;

        comp.ActiveChunks.Remove(msg.Chunk);
        LoadChunk(comp.FowGrid, grid, msg.ModifiedTiles, msg.Chunk);
    }

    private void OnChunkRemoved(FogOfWarChunkRemoved msg)
    {
        var grid = GetEntity(msg.Grid);

        if (!_fogOfWarQuery.TryComp(grid, out var comp) || !comp.ActiveChunks.Add(msg.Chunk))
            return;

        comp.FogChunks.Remove(msg.Chunk);
        var chunkSize = SharedBiomeSystem.ChunkSize;
        var tiles = new List<(Vector2i, Tile)>();

        for (var x = 0; x < chunkSize; x++)
        {
            for (var y = 0; y < chunkSize; y++)
            {
                var indices = new Vector2i(x + msg.Chunk.X, y + msg.Chunk.Y);
                tiles.Add((indices, Tile.Empty));
            }
        }

        _map.SetTiles(comp.FowGrid, Comp<MapGridComponent>(comp.FowGrid), tiles);

        if (comp.LoadedEntities.TryGetValue(msg.Chunk, out var entities))
        {
            foreach (var uid in entities)
            {
                Del(uid);
            }
        }

        if (!comp.LoadedDecal.TryGetValue(msg.Chunk, out var decals))
            return;

        foreach (var decal in decals)
        {
            _decals.RemoveDecal(grid, decal);
        }
    }

    private void OnFullStateRequest(FogOfWarFullStateMessage msg)
    {
        if (!TryComp(GetEntity(msg.Grid), out FogOfWarComponent? comp))
            return;

        comp.FogChunks = msg.VisitedChunks;
    }

    // Just copy-paste from BiomeSystem
    private void LoadChunk(
        Entity<MapGridComponent?> fowGrid,
        Entity<BiomeComponent?, MapGridComponent?, FogOfWarComponent?> source,
        HashSet<Vector2i> modified,
        Vector2i chunk)
    {
        if (!Resolve(source, ref source.Comp1)
            || !Resolve(source, ref source.Comp2)
            || !Resolve(source, ref source.Comp3)
            || !Resolve(fowGrid, ref fowGrid.Comp))
            return;

        var chunkSize = SharedBiomeSystem.ChunkSize;
        var biome = source.Comp1;
        var sourceGrid = source.Comp2;
        var fog = source.Comp3;

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

                if (!_biome.TryGetBiomeTile(indices, biome.Layers, biome.Seed, source, out var biomeTile))
                    continue;

                tiles.Add((indices, biomeTile.Value));
            }
        }

        _map.SetTiles(fowGrid, fowGrid.Comp, tiles);

        // Entities
        var entities = new HashSet<EntityUid>();
        for (var x = 0; x < chunkSize; x++)
        {
            for (var y = 0; y < chunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified.Contains(indices))
                    continue;

                // Don't mess with anything that's potentially anchored.
                var anchored = _map.GetAnchoredEntitiesEnumerator(source, sourceGrid, indices);

                if (anchored.MoveNext(out _) || !_biome.TryGetEntity(indices, biome, sourceGrid, out var entPrototype))
                    continue;

                // Just track loaded chunks for now.
                var ent = Spawn(entPrototype, _map.GridTileToLocal(source, sourceGrid, indices));

                // At least for now unless we do lookups or smth, only work with anchoring.
                if (_xformQuery.TryGetComponent(ent, out var xform) && !xform.Anchored)
                    _transform.AnchorEntity(new(ent, xform), new(source, sourceGrid), indices);

                entities.Add(ent);
            }
        }

        fog.LoadedEntities[chunk] = entities;

        // Decals
        var loadedDecals = new HashSet<uint>();
        for (var x = 0; x < chunkSize; x++)
        {
            for (var y = 0; y < chunkSize; y++)
            {
                var indices = new Vector2i(x + chunk.X, y + chunk.Y);

                if (modified.Contains(indices))
                    continue;

                // Don't mess with anything that's potentially anchored.
                var anchored = _map.GetAnchoredEntitiesEnumerator(source, sourceGrid, indices);

                if (anchored.MoveNext(out _)
                    || !_biome.TryGetDecals(indices, biome.Layers, biome.Seed, sourceGrid, out var decals))
                    continue;

                foreach (var decal in decals)
                {
                    _decals.TryAddDecal(fowGrid.Owner, decal.ID, decal.Position, out _);
                }
            }
        }

        fog.LoadedDecal[chunk] = loadedDecals;
    }

    public bool ChunkInFog(Entity<FogOfWarComponent?> grid, Vector2i chunk)
    {
        if (!Resolve(grid, ref grid.Comp))
            return false;

        return grid.Comp.FogChunks.Contains(chunk);
    }

    public bool ChunkActive(Entity<FogOfWarComponent?> grid, Vector2i chunk)
    {
        if (!Resolve(grid, ref grid.Comp))
            return false;

        return grid.Comp.ActiveChunks.Contains(chunk);
    }
}
