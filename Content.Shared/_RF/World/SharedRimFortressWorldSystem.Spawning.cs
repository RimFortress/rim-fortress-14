using System.Linq;
using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._RF.World;

public partial class SharedRimFortressWorldSystem
{
    /// <summary>
    /// Spawns entities in random free tiles around a given center
    /// </summary>
    /// <param name="targetCoords">Spawning coordinates</param>
    /// <param name="popPrototypes">Entity prototypes for spawning</param>
    /// <param name="radius">The minimum distance that should be from the spawn point to any player settlement</param>
    /// <returns>List of spawned entities</returns>
    public List<EntityUid> SpawnPop(
        EntityCoordinates targetCoords,
        List<EntProtoId> popPrototypes,
        int? radius = null)
    {
        var spawned = new List<EntityUid>();
        var freeTiles = GetSpawnTiles(targetCoords, popPrototypes.Count, radius ?? _playerSafeRadius).ToList();

        // Spawn the entities on a random free tile
        while (popPrototypes.Count > 0)
        {
            var spawnCoords = Turf.GetTileCenter(_random.Pick(freeTiles));
            var spawnedUid = Spawn(popPrototypes.Pop(), spawnCoords);

            spawned.Add(spawnedUid);
        }

        return spawned;
    }

    /// <summary>
    /// Spawns entities in random free tiles around a given center
    /// </summary>
    /// <param name="targetCoords">Spawning coordinates</param>
    /// <param name="popProto">Prototype of the entity to be spawned</param>
    /// <param name="amount">Amount of entities to be spawned</param>
    /// <param name="entities">Entities spawned elsewhere previously that will be used in place of the prototype spawning</param>
    /// <param name="radius">The minimum distance that should be from the spawn point to any player settlement</param>
    /// <returns>List of spawned entities</returns>
    public List<EntityUid> SpawnPop(
        EntityCoordinates targetCoords,
        EntProtoId? popProto = null,
        int amount = 1,
        List<EntityUid>? entities = null,
        int? radius = null)
    {
        DebugTools.Assert(popProto != null || entities?.Count == amount);

        var spawned = new List<EntityUid>();
        var freeTiles = GetSpawnTiles(targetCoords, amount, radius ?? _playerSafeRadius);

        if (freeTiles.Count == 0)
            return spawned;

        // Spawn the entities on a random free tile
        while (amount > 0)
        {
            var spawnCoords = Turf.GetTileCenter(_random.Pick(freeTiles));

            // If we already have entities ready to go, we simply move them to the free places
            if (entities?.Count > 0)
            {
                var entity = entities.Pop();
                _transform.AttachToGridOrMap(entity);
                _transform.SetCoordinates(entity, spawnCoords);
                amount--;
                continue;
            }

            var spawnedUid = Spawn(popProto, spawnCoords);
            spawned.Add(spawnedUid);
            amount--;
        }

        return spawned;
    }

    public HashSet<TileRef> GetSpawnTiles(int amount)
    {
        if (WorldMap is not { } worldMap)
            return new();

        var settlements = AllSettlements();
        var coords = settlements.Count > 0
            ? _random.Pick(settlements)
            : new EntityCoordinates(worldMap, Vector2.Zero);

        return GetSpawnTiles(coords, amount);
    }

    public HashSet<TileRef> GetSpawnTiles(EntityCoordinates targetCoordinates, int amount)
    {
        var tiles = GetSpawnTiles(targetCoordinates);
        var result = new HashSet<TileRef>();

        while (result.Count < amount && tiles.Count > 0)
        {
            var randomTile = _random.Pick(tiles);
            tiles.Remove(randomTile);
            result.Add(randomTile);
        }

        return result;
    }

    public HashSet<TileRef> GetSpawnTiles(EntityCoordinates targetCoordinates)
    {
        if (WorldMap is not { } worldMap)
            return new();

        return GetSpawnTiles(worldMap,
            targetCoordinates,
            _playerSafeRadius,
            SpawnAreaRadius,
            MinSpawnAreaTiles);
    }

    public HashSet<TileRef> GetSpawnTiles(EntityCoordinates targetCoordinates, int amount, int radius)
    {
        if (WorldMap is not { } worldMap)
            return new();

        var tiles = GetSpawnTiles(worldMap,
            targetCoordinates,
            radius,
            SpawnAreaRadius,
            MinSpawnAreaTiles);
        var result = new HashSet<TileRef>();

        while (result.Count < amount && tiles.Count > 0)
        {
            var randomTile = _random.Pick(tiles);
            tiles.Remove(randomTile);
            result.Add(randomTile);
        }

        return result;
    }

    /// <summary>
    /// Returns the given number of free tails to spawn around the given area
    /// </summary>
    /// <param name="grid">Grid entity</param>
    /// <param name="targetCoords">Spawning center coordinates</param>
    /// <param name="radiusFromPlayers">The minimum radius beyond which the tile must be from any player</param>
    /// <param name="spawnAreaRadius">The radius from the target point at which spawning tiles can be searched for</param>
    /// <param name="minSpawnAreaTiles">The minimum number of free tiles to which a spawning tile must be connected.</param>
    public HashSet<TileRef> GetSpawnTiles(
        Entity<MapGridComponent?> grid,
        EntityCoordinates targetCoords,
        int radiusFromPlayers,
        int spawnAreaRadius,
        int minSpawnAreaTiles)
    {
        if (!Resolve(grid, ref grid.Comp))
            return new();

        var angle = Angle.FromDegrees(_random.NextFloat(360f));
        var distance = radiusFromPlayers;
        var settlements = AllSettlements();

        while (true)
        {
            var pos = new Vector2((float) Math.Cos(angle), (float) Math.Sin(angle)) * distance + targetCoords.Position;
            var valid = true;

            distance += radiusFromPlayers / 2;

            foreach (var settlement in settlements)
            {
                if (Vector2.Distance(pos, settlement.Position) >= radiusFromPlayers)
                    continue;

                valid = false;
                break;
            }

            if (!valid)
                continue;

            var box = Box2.CenteredAround(pos, new Vector2(spawnAreaRadius));
            var tiles = GetFreeTiles(grid, box, minSpawnAreaTiles);

            if (tiles.Count == 0)
                continue;

            return tiles;
        }
    }

    /// <summary>
    /// Returns all free tiles in the biome chunk
    /// </summary>
    protected HashSet<TileRef> GetFreeTiles(Entity<MapGridComponent?, BiomeComponent?> grid, Box2 area, int areaMinSize)
    {
        if (!Resolve(grid, ref grid.Comp1) || !Resolve(grid, ref grid.Comp2))
            return new();

        var tileEnumerator = _map.GetTilesEnumerator(grid, grid.Comp1, area, ignoreEmpty: false);
        var freeTiles = new HashSet<TileRef>();

        // Find all free tiles in the specified area
        while (tileEnumerator.MoveNext(out var tileRef))
        {
            if (tileRef.IsSpace())
            {
                if (_biome.TryGetEntity(tileRef.GridIndices, grid.Comp2, grid.Comp1, out _))
                    continue;
            }
            else if (Turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
            {
                continue;
            }

            freeTiles.Add(tileRef);
        }

        while (freeTiles.Count > 0)
        {
            var randomTile = _random.Pick(freeTiles);
            freeTiles.Remove(randomTile);

            if (ConnectedTilesCount(randomTile, areaMinSize, freeTiles, out var tiles))
            {
                freeTiles = freeTiles.Where(tile => tiles.Contains(tile)).ToHashSet();
                break;
            }

            foreach (var visited in tiles)
            {
                freeTiles.Remove(visited);
            }
        }

        return freeTiles;
    }

    /// <summary>
    /// Checks the number of free tiles connected to the given one.
    /// </summary>
    /// <param name="tileRef">tile from which the check will start</param>
    /// <param name="moreThan">The minimum number of free tiles connected to the given one that we expect to be available</param>
    /// <param name="freeTilesCache">A list of free tiles that have been checked before, to prevent double-checking</param>
    /// <param name="visited">list of visited tiles</param>
    /// <returns>True, if the number of connected free tiles is greater than moreThan</returns>
    private bool ConnectedTilesCount(TileRef tileRef, int moreThan, HashSet<TileRef> freeTilesCache, out HashSet<TileRef> visited)
    {
        var directions = new[] { Vector2i.Left, Vector2i.Right, Vector2i.Up, Vector2i.Down };
        var queue = new Queue<TileRef>();

        queue.Enqueue(tileRef);
        visited = new();

        var gridUid = tileRef.GridUid;
        var gridComp = Comp<MapGridComponent>(gridUid);

        while (queue.TryDequeue(out var node))
        {
            if (!visited.Add(node))
                continue;

            if (queue.Count > moreThan)
                return true;

            if (!freeTilesCache.Contains(node)
                && Turf.IsTileBlocked(node, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                continue;

            foreach (var offset in directions)
            {
                var nextTile = _map.GetTileRef((gridUid, gridComp), node.GridIndices + offset);
                queue.Enqueue(nextTile);
            }
        }

        return false;
    }
}
