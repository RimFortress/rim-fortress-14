using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.World;

public abstract class SharedRimFortressWorldSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] protected readonly TurfSystem Turf = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IConfigurationManager _cvar = default!;
    [Dependency] private readonly SharedBiomeSystem _biome = default!;

    protected RimFortressRuleComponent? Rule;
    protected EntityUid? WorldMap;

    protected const byte ChunkSize = SharedBiomeSystem.ChunkSize;

    protected EntityQuery<RimFortressPlayerComponent> PlayerQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private int _maxSettlementRadius = 100;
    private int _minSettlementMembers = 2;
    private int _playerSafeRadius = 100;
    protected int SpawnAreaRadius = 20;
    protected int MinSpawnAreaTiles = 100;

    public override void Initialize()
    {
        base.Initialize();
        PlayerQuery = GetEntityQuery<RimFortressPlayerComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_cvar, RfVars.MaxSettlementRadius, value => _maxSettlementRadius = value, true);
        Subs.CVar(_cvar, RfVars.MinSettlementMembers, value => _minSettlementMembers = value, true);
        Subs.CVar(_cvar, RfVars.PlayerSafeRadius, value => _playerSafeRadius = value, true);
        Subs.CVar(_cvar, RfVars.SpawnAreaRadius, value => SpawnAreaRadius = value, true);
        Subs.CVar(_cvar, RfVars.MinSpawnAreaTiles, value => MinSpawnAreaTiles = value, true);
    }

    #region Spawning

    /// <summary>
    /// Spawns entities in random free tiles around a given center
    /// </summary>
    /// <param name="targetCoords">Spawning coordinates</param>
    /// <param name="popProto">Prototype of the entity to be spawned</param>
    /// <param name="amount">Amount of entities to be spawned</param>
    /// <param name="entities">Entities spawned elsewhere previously that will be used in place of the prototype spawning</param>
    /// <returns>List of spawned entities</returns>
    public List<EntityUid> SpawnPop(
        EntityCoordinates targetCoords,
        EntProtoId? popProto = null,
        int amount = 1,
        List<EntityUid>? entities = null)
    {
        DebugTools.Assert(popProto != null || entities?.Count == amount);

        var spawned = new List<EntityUid>();
        var freeTiles = GetSpawnTiles(targetCoords, amount);

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

    protected HashSet<TileRef> GetSpawnTiles(int amount)
    {
        if (WorldMap is not { } worldMap)
            return new();

        var settlements = AllSettlements();
        var coords = settlements.Count > 0
            ? _random.Pick(settlements)
            : new EntityCoordinates(worldMap, Vector2.Zero);

        return GetSpawnTiles(coords, amount);
    }

    protected HashSet<TileRef> GetSpawnTiles(EntityCoordinates targetCoordinates, int amount)
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

    protected HashSet<TileRef> GetSpawnTiles(EntityCoordinates targetCoordinates)
    {
        if (WorldMap is not { } worldMap)
            return new();

        return GetSpawnTiles(worldMap,
            targetCoordinates,
            _playerSafeRadius,
            SpawnAreaRadius,
            MinSpawnAreaTiles);
    }

    /// <summary>
    /// Returns the given number of free tails to spawn around the given area
    /// </summary>
    /// <param name="grid">Grid entity</param>
    /// <param name="targetCoords">Spawning center coordinates</param>
    /// <param name="radiusFromPlayers">The minimum radius beyond which the tile must be from any player</param>
    /// <param name="spawnAreaRadius">The radius from the target point at which spawning tiles can be searched for</param>
    /// <param name="minSpawnAreaTiles">The minimum number of free tiles to which a spawning tile must be connected.</param>
    protected HashSet<TileRef> GetSpawnTiles(
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

    /// <summary>
    /// Returns the coordinates of the player's settlements
    /// </summary>
    public List<EntityCoordinates> GetPlayerSettlements(Entity<RimFortressPlayerComponent?> player)
    {
        if (!Resolve(player, ref player.Comp)
            || !_xformQuery.TryComp(player, out var playerXform)
            || player.Comp.Pops.Count == 0)
            return new();

        // Collect a list of the coordinates of all the player's pops
        var points = new List<Vector2>();
        foreach (var pop in player.Comp.Pops)
        {
            if (!_xformQuery.TryComp(pop, out var xform))
                continue;

            points.Add(xform.Coordinates.Position);
        }

        var grid = playerXform.Coordinates.EntityId;
        var coords = new List<EntityCoordinates>();

        // Divide pop coordinates into clusters
        var (clusters, _) = DbScan.Cluster(points, _maxSettlementRadius, _minSettlementMembers);

        // Find the center of mass of all clusters of points
        foreach (var cluster in clusters)
        {
            var massCenter = Vector2.Zero;

            foreach (var point in cluster)
            {
                massCenter += point;
            }

            massCenter /= cluster.Count;
            coords.Add(new EntityCoordinates(grid, massCenter));
        }

        return coords;
    }

    public List<EntityCoordinates> AllSettlements()
    {
        var settlements = new List<EntityCoordinates>();
        var enumerator = EntityQueryEnumerator<RimFortressPlayerComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            settlements.AddRange(GetPlayerSettlements(new(uid, comp)));
        }

        return settlements;
    }

    public Dictionary<EntityUid, List<EntityCoordinates>> AllPlayersSettlements()
    {
        var settlements = new Dictionary<EntityUid, List<EntityCoordinates>>();
        var enumerator = EntityQueryEnumerator<RimFortressPlayerComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            settlements.Add(uid, GetPlayerSettlements(new(uid, comp)));
        }

        return settlements;
    }

    #endregion

    public List<EntityUid>? GetPLayerPops(EntityUid uid)
    {
        if (!PlayerQuery.TryComp(uid, out var player))
            return null;

        return player.Pops.Count == 0 ? null : player.Pops;
    }

    public ProtoId<JobPrototype>? PickPopJob(IReadOnlyDictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities)
    {
        if (TryPick(JobPriority.High, out var picked))
            return picked;

        if (TryPick(JobPriority.Medium, out picked))
            return picked;

        if (TryPick(JobPriority.Low, out picked))
            return picked;

        return null;

        bool TryPick(JobPriority priority, [NotNullWhen(true)] out ProtoId<JobPrototype>? jobId)
        {
            var filtered = jobPriorities
                .Where(p => p.Value == priority)
                .Select(p => p.Key)
                .ToList();

            if (filtered.Count != 0)
            {
                jobId = _random.Pick(filtered);
                return true;
            }

            jobId = null;
            return false;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Rule is not { } rule)
            return;

        var query = EntityQueryEnumerator<RimFortressPlayerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextEventTime)
                continue;

            comp.NextEventTime = _timing.CurTime + TimeSpan.FromSeconds(rule.MinMaxEventTiming.Next(_random));
            RaiseLocalEvent(new PlayerAvailableForEvent { Player = new(uid, comp) });
        }
    }
}

public sealed class PlayerAvailableForEvent : HandledEntityEventArgs
{
    public Entity<RimFortressPlayerComponent> Player;
}

[Serializable, NetSerializable]
public sealed class SettlementCoordinatesMessage(Dictionary<NetEntity, List<NetCoordinates>> coords) : EntityEventArgs
{
    public Dictionary<NetEntity, List<NetCoordinates>> Coords = coords;
}

[Serializable, NetSerializable]
public sealed class WorldDebugInfoRequest : EntityEventArgs
{
}

#region Helpers

/// <summary>
/// A class helper that extracts clusters and noise among an array of points using the DBSCAN algorithm
/// </summary>
public static class DbScan
{
    /// <summary>
    /// Selects clusters and individual noise points among an array of points
    /// </summary>
    /// <param name="points">List of points from which to select a cluster</param>
    /// <param name="radius">Maximum radius of one cluster</param>
    /// <param name="minPts">Minimum required number of neighboring points to be considered as a cluster and not as noise</param>
    /// <returns>List of point clusters and list of noisy points</returns>
    public static (List<List<Vector2>> Clusters, List<Vector2> Noise) Cluster(
        List<Vector2> points,
        float radius,
        int minPts)
    {
        List<List<Vector2>> clusters = new();
        HashSet<Vector2> visited = new();
        List<Vector2> noise = new();

        foreach (var point in points)
        {
            if (!visited.Add(point))
                continue;

            var neighbors = GetNeighbors(points, point, radius);

            if (neighbors.Count < minPts)
            {
                noise.Add(point);
                continue;
            }

            List<Vector2> cluster = new();
            ExpandCluster(points, point, neighbors, cluster, visited, radius, minPts);
            clusters.Add(cluster);
        }

        return (clusters, noise);
    }

    private static void ExpandCluster(
        List<Vector2> points,
        Vector2 point,
        List<Vector2> neighbors,
        List<Vector2> cluster,
        HashSet<Vector2> visited,
        float radius,
        int minPts)
    {
        cluster.Add(point);

        for (var i = 0; i < neighbors.Count; i++)
        {
            var neighbor = neighbors[i];

            if (visited.Add(neighbor))
            {
                var newNeighbors = GetNeighbors(points, neighbor, radius);

                if (newNeighbors.Count >= minPts)
                    neighbors.AddRange(newNeighbors);
            }

            if (!cluster.Contains(neighbor))
                cluster.Add(neighbor);
        }
    }

    private static List<Vector2> GetNeighbors(List<Vector2> points, Vector2 point, float radius)
    {
        return points.Where(p => Vector2.Distance(p, point) <= radius).ToList();
    }
}

#endregion
