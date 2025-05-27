using System.Linq;
using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared._RF.World;

public partial class SharedRimFortressWorldSystem
{
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

    /// <summary>
    /// Returns a list of all settlements of all players
    /// </summary>
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

    /// <summary>
    /// Returns a list of all settlements of all players
    /// </summary>
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
}

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
