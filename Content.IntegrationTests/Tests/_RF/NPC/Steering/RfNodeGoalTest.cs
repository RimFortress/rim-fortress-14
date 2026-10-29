#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Steering.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.NPC.Steering;

/// <summary>
/// The goal of a path node: the agent walks to a wall that is in its way, the wall is broken by the goal of the node,
/// and then the agent goes on to the target.
/// </summary>
public sealed class RfNodeGoalTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestNodeWall
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

- type: goapCompound
  id: TestNodeRoot
  tasks:
  - preconditions: []
    action: !type:TestNodeMove
    effects:
      Arrived: true
  - preconditions: []
    action: !type:TestNodeBreak
    effects:
      Broke: true

- type: utilityAiGoal
  id: TestNodeGo
  goalState:
    Arrived: true

- type: utilityAiGoal
  id: TestNodeBreakGoal
  goalState:
    Broke: true

- type: pathProfile
  id: TestNodeBreaker
  rules:
  - when:
    - !type:Blocked
    cost:
    - float: 5
    goal: TestNodeBreakGoal
    goalTargetKey: BreakTarget
    goalRangeKey: BreakRange

- type: pathProfile
  id: TestNodePlain
  algorithm: !type:AStar
    maxCost: 5000
    allowPartial: false
  rules:
  - when:
    - !type:TagInfluence
      tag: Obstacle
      moreThan: 0
    cost:
    - var: influence
    - mul:
      - float: 3
  - when:
    - !type:TagInfluence
      tag: Obstacle
      moreThan: 0.55
    cost:
    - float: 10000
  - when:
    - !type:Blocked
    cost:
    - float: 10000

- type: entity
  id: TestBigBlock
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
          bounds: ""-1.5,-1.5,1.5,1.5""
        mask:
        - MobMask
        layer:
        - Impassable

- type: entity
  id: TestRoundBlock
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 1.0
        mask:
        - MobMask
        layer:
        - Impassable

- type: entity
  id: TestSmallRound
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.4
        mask:
        - MobMask
        layer:
        - Impassable

- type: entity
  id: TestNodePlainAgent
  parent: TestNodeAgent
  components:
  - type: PathProfiles
    profiles:
    - TestNodePlain

- type: entity
  id: TestNodeAgent
  components:
  - type: ActiveNPC
  - type: Goap
    rootTask: TestNodeRoot
  - type: UtilityAi
  - type: PathProfiles
    profiles:
    - TestNodeBreaker
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
  - type: RfSteerer
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly SharedUtilityAiSystem _uai = default!;

    [Test]
    public async Task TestBreaksWallOnTheWay()
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            for (var y = 0; y < 8; y++)
            {
                _entMan.SpawnAtPosition("TestNodeWall", new EntityCoordinates(grid, 10.5f, y + 0.5f));
            }

            agent = _entMan.SpawnAtPosition("TestNodeAgent", new EntityCoordinates(grid, 2.5f, 3.5f));
        });

        await Pair.RunTicksSync(5);
        TestNodeBreakSystem.Broken = 0;

        await Pair.Server.WaitPost(() =>
        {
            var goap = _entMan.GetComponent<GoapComponent>(agent);
            goap.State.SetValue(new StateKey<float>("BreakRange"), 1.3f);
            goap.State.SetValue(TestNodeMoveSystem.Target, new EntityCoordinates(grid, 20.5f, 3.5f));
            _uai.SetGoal(agent, "TestNodeGo");
        });

        for (var i = 0; i < 1500; i++)
        {
            await Pair.RunTicksSync(1);

            var arrived = false;
            await Pair.Server.WaitPost(() => arrived = _transform.GetWorldPosition(agent).X > 19.5f);

            if (arrived)
                break;
        }

        await Pair.Server.WaitPost(() =>
        {
            Assert.That(TestNodeBreakSystem.Broken, Is.EqualTo(1), "the goal of the node must have been performed once");
            Assert.That(_transform.GetWorldPosition(agent).X, Is.GreaterThan(19.5f), "the agent must have got through");
        });
    }

    /// <summary>
    /// A solid target that takes up a lot of room cannot be reached at its center. The agent comes up to the side of
    /// it, the range is counted from the edge, and it does not fail.
    /// </summary>
    [Test]
    public async Task TestReachesBigSolidTarget()
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;
        EntityUid block = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            block = _entMan.SpawnAtPosition("TestBigBlock", new EntityCoordinates(grid, 15.5f, 3.5f));
            agent = _entMan.SpawnAtPosition("TestNodePlainAgent", new EntityCoordinates(grid, 2.5f, 3.5f));
        });

        await Pair.RunTicksSync(5);

        var result = GoapActionResult.Continuing;

        for (var i = 0; i < 1500 && result == GoapActionResult.Continuing; i++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() =>
            {
                result = _entMan.System<RfSteeringSystem>().Move(
                    new Entity<GoapComponent>(agent, _entMan.GetComponent<GoapComponent>(agent)),
                    block,
                    0.6f);
            });
        }

        await Pair.Server.WaitPost(() =>
        {
            Assert.That(result, Is.EqualTo(GoapActionResult.Finished));

            var position = _transform.GetWorldPosition(agent);
            Assert.That(position.X, Is.LessThan(14f), "the agent must be at the edge of the block, not inside it");
            Assert.That(position.X, Is.GreaterThan(12f));
        });
    }

    private async Task<(GoapActionResult Result, Vector2 Position, int Flips)> Walk(
        Entity<MapGridComponent> grid,
        EntityUid agent,
        EntityCoordinates target,
        float range = 0.4f)
    {
        var result = GoapActionResult.Continuing;
        var flips = 0;
        var lastSide = 0;

        for (var i = 0; i < 1500 && result == GoapActionResult.Continuing; i++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() =>
            {
                result = _entMan.System<RfSteeringSystem>().Move(
                    new Entity<GoapComponent>(agent, _entMan.GetComponent<GoapComponent>(agent)),
                    target,
                    range);

                // How often the agent changes the side it is going around something by.
                var velocity = _entMan.GetComponent<Robust.Shared.Physics.Components.PhysicsComponent>(agent).LinearVelocity.Y;

                if (Math.Abs(velocity) > 0.4f)
                {
                    var side = Math.Sign(velocity);

                    if (lastSide != 0 && side != lastSide)
                        flips++;

                    lastSide = side;
                }
            });
        }

        var position = Vector2.Zero;
        await Pair.Server.WaitPost(() => position = _transform.GetWorldPosition(agent));
        return (result, position, flips);
    }

    /// <summary>
    /// A corridor one tile wide is passable.
    /// </summary>
    [Test]
    public async Task TestWalksThroughNarrowCorridor([Values(3.5f, 3.3f, 3.15f)] float startY)
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            for (var x = 5; x < 20; x++)
            {
                _entMan.SpawnAtPosition("TestNodeWall", new EntityCoordinates(grid, x + 0.5f, 2.5f));
                _entMan.SpawnAtPosition("TestNodeWall", new EntityCoordinates(grid, x + 0.5f, 4.5f));
            }

            agent = _entMan.SpawnAtPosition("TestNodePlainAgent", new EntityCoordinates(grid, 2.5f, startY));
        });

        await Pair.RunTicksSync(5);

        var (result, position, _) = await Walk(grid, agent, new EntityCoordinates(grid, 25.5f, 3.5f));
        TestContext.Out.WriteLine($"MEASURE corridor {startY}: {result} at {position}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));
        Assert.That(position.X, Is.GreaterThan(24f));
    }

    /// <summary>
    /// A big round body in the way is gone around once, not twitching from side to side.
    /// </summary>
    [Test]
    public async Task TestGoesAroundRoundBody()
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 12; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            _entMan.SpawnAtPosition("TestRoundBlock", new EntityCoordinates(grid, 15.5f, 5.5f));
            agent = _entMan.SpawnAtPosition("TestNodePlainAgent", new EntityCoordinates(grid, 2.5f, 5.5f));
        });

        await Pair.RunTicksSync(5);

        var (result, position, flips) = await Walk(grid, agent, new EntityCoordinates(grid, 25.5f, 5.5f));
        TestContext.Out.WriteLine($"MEASURE round: {result} at {position}, side changes {flips}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));
        Assert.That(flips, Is.LessThanOrEqualTo(2), "the side must not change over and over");
    }

    /// <summary>
    /// The same corridor with the walls of the game.
    /// </summary>
    [Test]
    public async Task TestWalksThroughCorridorOfRealWalls()
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            for (var x = 5; x < 20; x++)
            {
                _entMan.SpawnAtPosition("WallSolid", new EntityCoordinates(grid, x + 0.5f, 2.5f));
                _entMan.SpawnAtPosition("WallSolid", new EntityCoordinates(grid, x + 0.5f, 4.5f));
            }

            agent = _entMan.SpawnAtPosition("TestNodePlainAgent", new EntityCoordinates(grid, 2.5f, 3.5f));
        });

        await Pair.RunTicksSync(5);

        var (result, position, _) = await Walk(grid, agent, new EntityCoordinates(grid, 25.5f, 3.5f));
        TestContext.Out.WriteLine($"MEASURE real corridor: {result} at {position}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));
    }

    /// <summary>
    /// A corridor one tile wide with turns, a staircase of them as in a cave.
    /// </summary>
    [Test]
    public async Task TestWalksThroughNarrowCorridorWithTurns()
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;

        // The tiles of the corridor, everything around them is a wall.
        var corridor = new HashSet<Vector2i>();

        for (var x = 2; x <= 10; x++)
        {
            corridor.Add(new Vector2i(x, 3));
        }

        for (var y = 3; y <= 9; y++)
        {
            corridor.Add(new Vector2i(10, y));
        }

        for (var x = 10; x <= 18; x++)
        {
            corridor.Add(new Vector2i(x, 9));
        }

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 22; x++)
            {
                for (var y = 0; y < 13; y++)
                {
                    var tile = new Vector2i(x, y);
                    _maps.SetTile(grid.Owner, grid.Comp, tile, new Tile(1));

                    if (!corridor.Contains(tile))
                        _entMan.SpawnAtPosition("WallSolid", new EntityCoordinates(grid, x + 0.5f, y + 0.5f));
                }
            }

            agent = _entMan.SpawnAtPosition("TestNodePlainAgent", new EntityCoordinates(grid, 2.5f, 3.5f));
        });

        await Pair.RunTicksSync(5);

        var (result, position, _) = await Walk(grid, agent, new EntityCoordinates(grid, 18.5f, 9.5f));
        TestContext.Out.WriteLine($"MEASURE turns: {result} at {position}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));
        Assert.That(position.X, Is.GreaterThan(17f));
    }

    /// <summary>
    /// In an open place the path is a straight line, the agent does not go by the steps of the grid.
    /// </summary>
    [Test]
    public async Task TestPathIsStraightInOpenField()
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 12; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            agent = _entMan.SpawnAtPosition("TestNodePlainAgent", new EntityCoordinates(grid, 2.5f, 2.5f));
        });

        await Pair.RunTicksSync(5);

        var nodes = -1;
        var result = GoapActionResult.Continuing;

        for (var i = 0; i < 1500 && result == GoapActionResult.Continuing; i++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() =>
            {
                result = _entMan.System<RfSteeringSystem>().Move(
                    new Entity<GoapComponent>(agent, _entMan.GetComponent<GoapComponent>(agent)),
                    new EntityCoordinates(grid, 25.5f, 9.5f),
                    0.4f);

                if (nodes < 0 && _entMan.TryGetComponent(agent, out Content.Server._RF.NPC.Steering.Components.RfSteeringComponent? steering))
                    nodes = steering.Nodes.Count;
            });
        }

        TestContext.Out.WriteLine($"MEASURE open field: {result}, nodes of the path {nodes}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));
        Assert.That(nodes, Is.EqualTo(2), "the path must be a straight line from where the agent is to where it goes");
    }

    /// <summary>
    /// A gap between two bodies that is narrower than the agent: the pathfinding does not know it is not passable, and
    /// the agent must not go to it, turn away, come back to it and so on. It goes around once.
    /// </summary>
    [Test]
    public async Task TestGoesAroundGapThatIsTooNarrow([Values(true, false)] bool wayAround)
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 14; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            // The gap between them is 0.5, the agent needs 0.7.
            _entMan.SpawnAtPosition("TestSmallRound", new EntityCoordinates(grid, 14.5f, 5.5f));
            _entMan.SpawnAtPosition("TestSmallRound", new EntityCoordinates(grid, 14.5f, 6.8f));

            // The wall that the gap is in: it has an end to go around, or has not and the way is closed.
            for (var y = 0; y < 14; y++)
            {
                if (y is 5 or 6 or 7 || wayAround && y is < 3 or > 10)
                    continue;

                _entMan.SpawnAtPosition("TestNodeWall", new EntityCoordinates(grid, 14.5f, y + 0.5f));
            }

            agent = _entMan.SpawnAtPosition("TestNodePlainAgent", new EntityCoordinates(grid, 2.5f, 6.15f));
        });

        await Pair.RunTicksSync(5);

        var result = GoapActionResult.Continuing;
        var repaths = 0;

        for (var i = 0; i < 1500 && result == GoapActionResult.Continuing; i++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() =>
            {
                result = _entMan.System<RfSteeringSystem>().Move(
                    new Entity<GoapComponent>(agent, _entMan.GetComponent<GoapComponent>(agent)),
                    new EntityCoordinates(grid, 25.5f, 6.15f),
                    0.4f);
                repaths = _entMan.GetComponent<Content.Server._RF.NPC.Steering.Components.RfSteererComponent>(agent).Repaths;
            });
        }

        TestContext.Out.WriteLine($"MEASURE gap (way around: {wayAround}): {result}, repaths {repaths}");

        // Where the way is closed the agent gives up, and does not dance in front of the gap.
        Assert.That(result, Is.EqualTo(wayAround ? GoapActionResult.Finished : GoapActionResult.Failed));
        Assert.That(repaths, Is.LessThanOrEqualTo(1));
    }

    /// <summary>
    /// A corridor one tile wide that goes by steps, as a cave does: the agent does not cut the corners of it.
    /// </summary>
    [Test]
    public async Task TestWalksThroughWindingCorridor(
        [Values(1, 2)] int stepLength,
        [Values(0f, 0.2f, -0.2f)] float offsetX,
        [Values(0f, 0.2f, -0.2f)] float offsetY)
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;

        var corridor = new HashSet<Vector2i>();
        var tile = new Vector2i(3, 3);
        corridor.Add(tile);

        // Right and up by turns.
        for (var k = 0; k < 7; k++)
        {
            for (var i = 0; i < stepLength; i++)
            {
                tile += new Vector2i(1, 0);
                corridor.Add(tile);
            }

            for (var i = 0; i < stepLength; i++)
            {
                tile += new Vector2i(0, 1);
                corridor.Add(tile);
            }
        }

        var end = tile;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 30; y++)
                {
                    var t = new Vector2i(x, y);
                    _maps.SetTile(grid.Owner, grid.Comp, t, new Tile(1));

                    if (!corridor.Contains(t))
                        _entMan.SpawnAtPosition("WallSolid", new EntityCoordinates(grid, x + 0.5f, y + 0.5f));
                }
            }

            agent = _entMan.SpawnAtPosition("TestNodePlainAgent", new EntityCoordinates(grid, 3.5f + offsetX, 3.5f + offsetY));
        });

        await Pair.RunTicksSync(5);

        var (result, position, _) = await Walk(grid, agent, new EntityCoordinates(grid, end.X + 0.5f, end.Y + 0.5f));
        var repaths = 0;
        await Pair.Server.WaitPost(() => repaths = _entMan.GetComponent<Content.Server._RF.NPC.Steering.Components.RfSteererComponent>(agent).Repaths);
        TestContext.Out.WriteLine($"MEASURE winding {stepLength} offset {offsetX},{offsetY}: {result} at {position}, repaths {repaths}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));
        Assert.That(repaths, Is.EqualTo(0), "the agent must not have got stuck on the way");
    }
}
