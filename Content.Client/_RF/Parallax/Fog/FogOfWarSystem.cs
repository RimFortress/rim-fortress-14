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
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _fogOfWarQuery = GetEntityQuery<FogOfWarComponent>();

        _overlay.AddOverlay(new FogOfWarOverlay());
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
