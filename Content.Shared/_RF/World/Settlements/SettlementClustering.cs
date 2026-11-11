using System.Numerics;
using JetBrains.Annotations;

namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// Finds the groups of points that are close to each other.
/// </summary>
public static class SettlementClustering
{
    /// <summary>
    /// Finds the groups of points: two points that are no farther from each other than the radius are in one group, and so
    /// are the points that are connected through others. A point that has no one close to it is in no group.
    /// </summary>
    /// <remarks>
    /// Points that are in one cell of a grid, whose diagonal is the radius, are all close to each other, so they are joined
    /// at once. Only the cells that are near each other, and not yet joined, are looked at for the points that are close, so
    /// a lot of points in a small place cost almost nothing.
    /// </remarks>
    /// <param name="points">The points.</param>
    /// <param name="radius">How far from each other two points can be to be in one group.</param>
    /// <returns>The indices of the points of each group, in the order of the points.</returns>
    [PublicAPI]
    public static List<List<int>> Cluster(IReadOnlyList<Vector2> points, float radius)
    {
        var groups = new List<List<int>>();

        if (points.Count < 2 || radius <= 0f)
            return groups;

        var cell = radius / MathF.Sqrt(2f);
        var cells = new Dictionary<(int, int), List<int>>();

        for (var i = 0; i < points.Count; i++)
        {
            var key = ((int) MathF.Floor(points[i].X / cell), (int) MathF.Floor(points[i].Y / cell));

            if (!cells.TryGetValue(key, out var list))
                cells[key] = list = new List<int>();

            list.Add(i);
        }

        var parent = new int[points.Count];

        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        // Two cells are close enough to have points that are close if they are no more than two cells apart.
        const int reach = 2;
        var radiusSquared = radius * radius;

        foreach (var ((x, y), list) in cells)
        {
            for (var i = 1; i < list.Count; i++)
            {
                Union(list[0], list[i]);
            }

            for (var dx = -reach; dx <= reach; dx++)
            {
                for (var dy = -reach; dy <= reach; dy++)
                {
                    // Every pair of cells is looked at once.
                    if (dx < 0 || dx == 0 && dy <= 0 || !cells.TryGetValue((x + dx, y + dy), out var other))
                        continue;

                    if (Find(list[0]) == Find(other[0]))
                        continue;

                    if (AnyClose(points, list, other, radiusSquared))
                        Union(list[0], other[0]);
                }
            }
        }

        var byRoot = new Dictionary<int, List<int>>();

        for (var i = 0; i < points.Count; i++)
        {
            var root = Find(i);

            if (!byRoot.TryGetValue(root, out var group))
                byRoot[root] = group = new List<int>();

            group.Add(i);
        }

        foreach (var group in byRoot.Values)
        {
            if (group.Count > 1)
                groups.Add(group);
        }

        groups.Sort((a, b) => a[0].CompareTo(b[0]));
        return groups;

        void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);

            if (a != b)
                parent[b] = a;
        }

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }
    }

    private static bool AnyClose(IReadOnlyList<Vector2> points, List<int> a, List<int> b, float radiusSquared)
    {
        foreach (var i in a)
        {
            foreach (var j in b)
            {
                if (Vector2.DistanceSquared(points[i], points[j]) <= radiusSquared)
                    return true;
            }
        }

        return false;
    }
}
