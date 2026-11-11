#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.MathHelpers.Assignment;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Curves;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.MathHelpers;

/// <summary>
/// The distribution of items between buckets, and the evaluation of a set of entities by curves.
/// </summary>
public sealed class AssignmentTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly AssignmentSystem _assignment = default!;
    [SidedDependency(Side.Server)] private readonly EntityScoringSystem _scoring = default!;

    private static List<MathCurve> Curves(params MathCurve[] curves) => curves.ToList();

    [Test]
    public void TestHeaviestAreGivenFirstToTheCheapest()
    {
        var buckets = new List<AssignmentBucket<string>> { new("a"), new("b") };
        var items = new[] { ("x", 5f), ("y", 4f), ("z", 3f), ("w", 2f) };

        // The cost is the load: the items are shared evenly.
        var result = Assignment.Assign(items, buckets, (_, _, bucket) => bucket.Load);

        Assert.That(result.Taken["a"], Is.EqualTo(new[] { "x", "w" }));
        Assert.That(result.Taken["b"], Is.EqualTo(new[] { "y", "z" }));
        Assert.That(result.Unassigned, Is.Empty);
    }

    [Test]
    public void TestWhatDoesNotFitOrCannotBeGivenIsLeft()
    {
        var buckets = new List<AssignmentBucket<string>> { new("a") { MaxItems = 2 }, new("b") { MaxLoad = 3f } };
        var items = new[] { ("x", 5f), ("y", 4f), ("z", 3f), ("w", 2f) };

        // The cost is infinite for z, nobody takes it. b can take only 3, so nothing but w (2) fits, and a is full with two.
        var result = Assignment.Assign(items, buckets, (item, _, bucket) => item == "z" ? float.PositiveInfinity : bucket.Load);

        Assert.That(result.Taken["a"], Is.EqualTo(new[] { "x", "y" }));
        Assert.That(result.Taken["b"], Is.EqualTo(new[] { "w" }));
        Assert.That(result.Unassigned, Is.EqualTo(new[] { "z" }));
    }

    [Test]
    public void TestWhoHasABucketKeepsIt()
    {
        var buckets = new List<AssignmentBucket<string>> { new("a"), new("b") };
        var items = new[] { ("x", 5f), ("y", 4f), ("z", 3f) };

        // x is b's although a is the cheaper: it stays, and the load of b is counted when y and z are given.
        var result = Assignment.Assign(items, buckets, (_, _, bucket) => bucket.Load,
            (string item, out string key) =>
            {
                key = "b";
                return item == "x";
            });

        Assert.That(result.Taken["b"], Does.Contain("x"));
        Assert.That(result.Taken["a"], Is.EquivalentTo(new[] { "y", "z" }));
    }

    [Test]
    public void TestSomeoneWhoseBucketIsGoneIsGivenAgain()
    {
        var buckets = new List<AssignmentBucket<string>> { new("a") };
        var result = Assignment.Assign(new[] { ("x", 1f) }, buckets, (_, _, _) => 0f,
            (string _, out string key) =>
            {
                key = "gone";
                return true;
            });

        Assert.That(result.Taken["a"], Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public async Task TestEntitiesGoToTheCloserBucket()
    {
        var map = await Pair.CreateTestMap();
        var items = new List<EntityUid>();
        var buckets = new List<AssignmentBucket<EntityUid>>();

        await Pair.Server.WaitAssertion(() =>
        {
            foreach (var x in new[] { 1.5f, 2.5f, 20.5f, 21.5f })
            {
                items.Add(_entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, x, 0.5f)));
            }

            buckets.Add(new AssignmentBucket<EntityUid>(_entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 0.5f, 0.5f)))
            {
                Position = new System.Numerics.Vector2(0.5f, 0.5f),
            });
            buckets.Add(new AssignmentBucket<EntityUid>(_entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 25.5f, 0.5f)))
            {
                Position = new System.Numerics.Vector2(25.5f, 0.5f),
            });

            // Every item weighs 1 (the variable), and costs the distance.
            var result = _assignment.AssignEntities(
                items,
                buckets,
                Curves(new Variable { Var = "one" }),
                Curves(new Variable { Var = "distance" }),
                variables: new() { ["one"] = 1f });

            Assert.That(result.Taken[buckets[0].Key], Is.EquivalentTo(items.Take(2)));
            Assert.That(result.Taken[buckets[1].Key], Is.EquivalentTo(items.Skip(2)));
            Assert.That(buckets[0].Load, Is.EqualTo(2f));
        });
    }

    [Test]
    public async Task TestScoringFoldsASetOfEntities()
    {
        var map = await Pair.CreateTestMap();

        await Pair.Server.WaitAssertion(() =>
        {
            var set = Enumerable.Range(0, 3)
                .Select(_ => _entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 0.5f, 0.5f)))
                .ToList();

            // The score of each is 2, and the score is the same for all of them: the sum, the mean and the greatest differ.
            var curves = Curves(new FloatCurve { Float = 2f });

            Assert.That(_scoring.Aggregate(set, curves, EntityAggregate.Sum), Is.EqualTo(6f));
            Assert.That(_scoring.Aggregate(set, curves, EntityAggregate.Average), Is.EqualTo(2f));
            Assert.That(_scoring.Aggregate(set, curves, EntityAggregate.Max), Is.EqualTo(2f));
            Assert.That(_scoring.Aggregate(set, curves, EntityAggregate.Count), Is.EqualTo(3f));
            Assert.That(_scoring.Aggregate(set.Take(0), curves, EntityAggregate.Sum), Is.EqualTo(0f));
            Assert.That(_scoring.Best(set.Take(0), curves), Is.Null);
            Assert.That(_scoring.Best(set, curves), Is.EqualTo(set[0]), "the first one if all are equal");

            // The score is 0 for all: they are not counted, if only the positive are.
            var nothing = Curves(new FloatCurve { Float = 0f });
            Assert.That(_scoring.Score(set, nothing, positiveOnly: true), Is.Empty);

            // And the one that is gone is not counted.
            _entMan.DeleteEntity(set[1]);
            Assert.That(_scoring.Aggregate(set, curves, EntityAggregate.Sum), Is.EqualTo(4f));
        });
    }
}
