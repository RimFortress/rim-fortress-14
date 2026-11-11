#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.NPC.Pathfinding;

/// <summary>
/// The dynamic path tags: the profile has rules for them like for any other tag, and the system works out that they are not baked
/// into the navigation map, but made of where the sources are when the path is searched for. The shape of a tag is the same one for both
/// kinds, a source counts for what the tag says, and a tag can be only where the source sees.
/// </summary>
public sealed class PathDynamicTagTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
# A wall that stops projectiles.
- type: entity
  id: TestDynamicWall
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.5,-0.5,0.5,0.5""
        mask:
        - MobMask
        layer:
        - Impassable
        - BulletImpassable

# Linear, 1 at the source and 0 at the radius.
- type: pathTag
  id: TestDynamic
  dynamic: true
  influence: 4
  influenceCurve:
  - div: [var: radius]
  - mul: [float: -1]
  - add: [float: 1]

- type: pathTag
  id: TestDynamicHeavy
  dynamic: true
  influence: 4

- type: pathTag
  id: TestDynamicSight
  dynamic: true
  influence: 8

- type: pathProfile
  id: TestDynamicProfile
  rules:
  - when:
    - !type:TagInfluence
      tag: TestDynamic
      moreThan: 0
    cost:
    - var: influence
  # The source counts for what the rule says.
  - when:
    - !type:TagInfluence
      tag: TestDynamicHeavy
      weight:
      - float: 3
      moreThan: 0
    cost:
    - var: influence
  # Only where the source sees.
  - when:
    - !type:TagInfluence
      tag: TestDynamicSight
      sight: true
      moreThan: 0
    cost:
    - var: influence
    - mul: [float: 100]

- type: entity
  id: TestDynamicAgent
  components:
  - type: Goap
    rootTask: Empty
  - type: PathProfiles
    profiles:
    - TestDynamicProfile
";

    [SidedDependency(Side.Server)] private readonly RfPathfindingSystem _pathfinding = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;

    private Entity<MapGridComponent> _grid;
    private Entity<PathProfilesComponent, GoapComponent> _agent;

    private EntityCoordinates At(float x, float y) => new(_grid, x, y);

    private async Task Setup()
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 16; x++)
            {
                for (var y = 0; y < 16; y++)
                {
                    _maps.SetTile(_grid.Owner, _grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            // A wall of 3 tiles between the shooter at (2.5, 5.5) and the places behind it.
            for (var y = 4; y <= 6; y++)
            {
                _entMan.SpawnAtPosition("TestDynamicWall", At(5.5f, y + 0.5f));
            }

            var agent = _entMan.SpawnAtPosition("TestDynamicAgent", At(12.5f, 12.5f));
            _agent = new Entity<PathProfilesComponent, GoapComponent>(
                agent,
                _entMan.GetComponent<PathProfilesComponent>(agent),
                _entMan.GetComponent<GoapComponent>(agent));
        });

        await Pair.RunTicksSync(5);
        await Pair.Server.WaitPost(() => _pathfinding.EnsureChunk(_grid, Vector2i.Zero));
    }

    private EntityUid Source(string tag, float x, float y)
    {
        var uid = _entMan.SpawnAtPosition("TestDynamicWall", At(x, y));
        _entMan.DeleteEntity(uid);
        uid = _entMan.SpawnEntity(null, At(x, y));
        _pathfinding.AddTag(uid, tag);
        return uid;
    }

    /// <summary>
    /// What the rules of the dynamic tags add to a node at a point, the sources being where they are now.
    /// </summary>
    private float Cost(float x, float y)
    {
        Assert.That(_pathfinding.BeginRequest(_agent, "TestDynamicProfile"), Is.True);
        return _pathfinding.EvaluateDynamic(_agent, default, false, _grid, new Vector2(x, y));
    }

    [Test]
    public async Task TestDynamicTagIsNotBaked()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_pathfinding.TryGetTagIndex("TestDynamic", out _), Is.False, "it is not in the facts of the nodes");
            Source("TestDynamic", 2.5f, 10.5f);

            // The chunk is not marked to be rebuilt for a tag that is not in it, and moving the source does not mark it either.
            Assert.That(_entMan.GetComponent<RfGridPathfindingComponent>(_grid).DirtyChunks, Is.Empty);
        });
    }

    [Test]
    public async Task TestInfluenceFallsByTheDistanceAndEndsAtTheRadius()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Source("TestDynamic", 2.5f, 10.5f);

            Assert.That(Cost(2.5f, 10.5f), Is.EqualTo(1f).Within(0.01f));
            Assert.That(Cost(4.5f, 10.5f), Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(Cost(6.5f, 10.5f), Is.Zero);
        });
    }

    [Test]
    public async Task TestSourcesAreSummedAndFollowTheMoves()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var first = Source("TestDynamic", 2.5f, 10.5f);
            Source("TestDynamic", 3.5f, 10.5f);

            var both = Cost(3f, 10.5f);
            Assert.That(both, Is.GreaterThan(1.2f), "two of them are more than one");

            // It is where the source is when the path is searched for, not where it was.
            _entMan.GetComponent<TransformComponent>(first).LocalPosition = new Vector2(12.5f, 2.5f);
            Assert.That(Cost(3f, 10.5f), Is.LessThan(both));

            // And one that has lost the tag is not a source.
            _pathfinding.RemoveTag(first, "TestDynamic");
            Cost(3f, 10.5f);
            Assert.That(_agent.Comp1.Dynamic!.Sources.Single(x => x.Key.Tag == "TestDynamic").Value, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task TestSourceCountsForWhatTheTagSays()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Source("TestDynamicHeavy", 2.5f, 10.5f);

            Assert.That(Cost(2.5f, 10.5f), Is.EqualTo(3f).Within(0.01f), "it counts for what the rule says: 3");
        });
    }

    [Test]
    public async Task TestSightTagIsOnlyWhereTheSourceSees()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Source("TestDynamicSight", 2.5f, 5.5f);

            Assert.That(Cost(4.5f, 5.5f), Is.GreaterThan(0f), "this side of the wall it is seen");
            Assert.That(Cost(7.5f, 5.5f), Is.Zero, "the wall stops the shots");
            Assert.That(Cost(4.5f, 8.5f), Is.GreaterThan(0f), "round the end of the wall");
        });
    }
}
