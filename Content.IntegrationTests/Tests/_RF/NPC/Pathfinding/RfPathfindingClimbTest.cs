#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.NPC.Pathfinding;

/// <summary>
/// What can be climbed onto, such as a table, is climbed onto by an agent that has a way over it, and is not by an agent
/// that only looks for a place to wander to.
/// </summary>
public sealed class RfPathfindingClimbTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: goapCompound
  id: TestClimbRoot
  tasks:
  - preconditions: []
    action: !type:TestNodeMove
    effects:
      Arrived: true
  - proto: Climb

- type: utilityAiGoal
  id: TestClimbGo
  goalState:
    Arrived: true

- type: entity
  id: TestClimbAgent
  components:
  - type: ActiveNPC
  - type: Goap
    rootTask: TestClimbRoot
  - type: UtilityAi
  - type: PathProfiles
    profiles:
    - Walker
  - type: Climbing
  - type: DoAfter
  - type: Physics
    bodyType: KinematicController
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
        density: 50
        mask:
        - MobMask
        layer:
        - MobLayer
  - type: InputMover
  - type: MobMover
  - type: MovementSpeedModifier
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly SharedUtilityAiSystem _uai = default!;
    [SidedDependency(Side.Server)] private readonly RfPathfindingSystem _pathfinding = default!;

    private async Task<(Entity<MapGridComponent> Grid, EntityUid Agent)> Setup(IEnumerable<Vector2i> tables, Vector2 agentAt)
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;
        var places = tables.ToList();

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 14; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            foreach (var place in places)
            {
                _entMan.SpawnAtPosition("Table", new EntityCoordinates(grid, place.X + 0.5f, place.Y + 0.5f));
            }

            agent = _entMan.SpawnAtPosition("TestClimbAgent", new EntityCoordinates(grid, agentAt.X, agentAt.Y));
        });

        await Pair.RunTicksSync(5);
        return (grid, agent);
    }

    /// <summary>
    /// Every body that can be climbed onto has the tag.
    /// </summary>
    [Test]
    public async Task TestClimbablesAreMarked()
    {
        var (grid, _) = await Setup(new[] { new Vector2i(10, 5) }, new Vector2(2.5f, 6.5f));

        await Pair.Server.WaitAssertion(() =>
        {
            var chunk = _pathfinding.EnsureChunk(grid, new Vector2i(1, 0))!;
            var poly = chunk.GetPolys(RfPathfindingChunk.TileIndex(2, 5))[0];

            Assert.That(_pathfinding.HasTag(poly.Data.Facts, "Climbable"), Is.True);
        });
    }

    /// <summary>
    /// The way is closed by a row of tables, and the agent climbs over it: the goal of the rule is performed at the node of
    /// the table, and then the way goes on.
    /// </summary>
    [Test]
    public async Task TestClimbsOverTablesOnTheWay()
    {
        var row = Enumerable.Range(0, 14).Select(y => new Vector2i(14, y)).ToList();
        var (grid, agent) = await Setup(row, new Vector2(2.5f, 6.5f));

        await Pair.Server.WaitPost(() =>
        {
            var goap = _entMan.GetComponent<GoapComponent>(agent);
            goap.State.SetValue(new StateKey<EntityCoordinates>("TestNodeMoveTarget"), new EntityCoordinates(grid, 24.5f, 6.5f));
            _uai.SetGoal(agent, "TestClimbGo");
        });

        var arrived = false;

        for (var i = 0; i < 2500 && !arrived; i++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() => arrived = _transform.GetWorldPosition(agent).X > 23.5f);
        }

        Assert.That(arrived, Is.True, "the agent must have climbed over the row of tables");
    }

    /// <summary>
    /// An agent that only looks for a place to go to does not climb: it is in a place that is closed by tables, and the
    /// place is chosen in it.
    /// </summary>
    [Test]
    public async Task TestWanderingDoesNotClimb()
    {
        var ring = new List<Vector2i>();

        for (var x = 8; x <= 14; x++)
        {
            for (var y = 3; y <= 9; y++)
            {
                if (x is 8 or 14 || y is 3 or 9)
                    ring.Add(new Vector2i(x, y));
            }
        }

        var (_, agent) = await Setup(ring, new Vector2(11.5f, 6.5f));
        var inside = 0;

        for (var attempt = 0; attempt < 15; attempt++)
        {
            PathSearchResult result = default;
            var found = false;

            for (var i = 0; i < 100 && !found; i++)
            {
                await Pair.Server.WaitPost(() =>
                {
                    var ent = new Entity<PathProfilesComponent, GoapComponent>(
                        agent,
                        _entMan.GetComponent<PathProfilesComponent>(agent),
                        _entMan.GetComponent<GoapComponent>(agent));

                    if (i == 0)
                        Assert.That(_pathfinding.RequestPath(ent, "Wander", null, 9f), Is.True);

                    found = _pathfinding.TryGetResult(ent, out result, out _);
                });

                await Pair.RunTicksSync(2);
            }

            Assert.That(found, Is.True);
            var place = result.Path[^1].Box.Center;

            if (place.X is > 9f and < 14f && place.Y is > 4f and < 9f)
                inside++;

            await Pair.Server.WaitPost(() => _pathfinding.CancelPath(
                new Entity<PathProfilesComponent?>(agent, _entMan.GetComponent<PathProfilesComponent>(agent))));
        }

        Assert.That(inside, Is.EqualTo(15));
    }
}
