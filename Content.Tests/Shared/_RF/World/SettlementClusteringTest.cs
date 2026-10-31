using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.World.Settlements;
using NUnit.Framework;

namespace Content.Tests.Shared._RF.World;

[TestFixture]
public sealed class SettlementClusteringTest
{
    [Test]
    public void TestPointsThatAreCloseAreOneGroup()
    {
        var groups = SettlementClustering.Cluster(
            new[] { new Vector2(0, 0), new Vector2(5, 0), new Vector2(0, 5) },
            10f);

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0], Is.EquivalentTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void TestAPointWithNobodyCloseIsInNoGroup()
    {
        var groups = SettlementClustering.Cluster(
            new[] { new Vector2(0, 0), new Vector2(5, 0), new Vector2(500, 500) },
            10f);

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0], Is.EquivalentTo(new[] { 0, 1 }));
    }

    [Test]
    public void TestPointsAreConnectedThroughOthers()
    {
        // The first and the last are farther from each other than the radius, the middle one connects them.
        var groups = SettlementClustering.Cluster(
            new[] { new Vector2(0, 0), new Vector2(15, 0), new Vector2(30, 0) },
            20f);

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0], Has.Count.EqualTo(3));
    }

    [Test]
    public void TestSeparateGroups()
    {
        var groups = SettlementClustering.Cluster(
            new[] { new Vector2(0, 0), new Vector2(3, 0), new Vector2(100, 0), new Vector2(103, 0), new Vector2(50, 300) },
            10f);

        Assert.That(groups, Has.Count.EqualTo(2));
        Assert.That(groups[0], Is.EquivalentTo(new[] { 0, 1 }));
        Assert.That(groups[1], Is.EquivalentTo(new[] { 2, 3 }));
    }

    [Test]
    public void TestPointsAtTheSamePlaceAreNotOne()
    {
        // The pops that stand on one tile are not the same point.
        var groups = SettlementClustering.Cluster(new[] { new Vector2(1, 1), new Vector2(1, 1) }, 5f);

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0], Has.Count.EqualTo(2));
    }

    [Test]
    public void TestTheRadiusIsTheLimit()
    {
        Assert.That(SettlementClustering.Cluster(new[] { new Vector2(0, 0), new Vector2(10, 0) }, 10f), Has.Count.EqualTo(1));
        Assert.That(SettlementClustering.Cluster(new[] { new Vector2(0, 0), new Vector2(10.01f, 0) }, 10f), Is.Empty);
    }

    [Test]
    public void TestSameAsSlowComparison()
    {
        // The result is the same as the one of comparing every pair.
        var random = new System.Random(42);

        for (var round = 0; round < 20; round++)
        {
            var points = new List<Vector2>();

            for (var i = 0; i < 80; i++)
            {
                points.Add(new Vector2(random.NextSingle() * 300f, random.NextSingle() * 300f));
            }

            var radius = 8f + random.NextSingle() * 25f;
            var fast = SettlementClustering.Cluster(points, radius)
                .Select(g => string.Join(",", g.OrderBy(x => x)))
                .OrderBy(x => x)
                .ToList();
            var slow = Slow(points, radius)
                .Select(g => string.Join(",", g.OrderBy(x => x)))
                .OrderBy(x => x)
                .ToList();

            Assert.That(fast, Is.EqualTo(slow), $"round {round}, radius {radius}");
        }
    }

    private static List<List<int>> Slow(List<Vector2> points, float radius)
    {
        var seen = new bool[points.Count];
        var groups = new List<List<int>>();

        for (var start = 0; start < points.Count; start++)
        {
            if (seen[start])
                continue;

            var group = new List<int> { start };
            seen[start] = true;

            for (var i = 0; i < group.Count; i++)
            {
                for (var j = 0; j < points.Count; j++)
                {
                    if (seen[j] || Vector2.Distance(points[group[i]], points[j]) > radius)
                        continue;

                    seen[j] = true;
                    group.Add(j);
                }
            }

            if (group.Count > 1)
                groups.Add(group);
        }

        return groups;
    }
}
