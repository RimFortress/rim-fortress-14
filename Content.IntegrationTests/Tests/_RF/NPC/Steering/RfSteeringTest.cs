#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Steering.Components;
using Content.Server._RF.NPC.Steering.Systems;
using Content.Shared._RF.NPC.Pathfinding;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._RF.NPC.Steering;

/// <summary>
/// The steering has to bring an agent to the end of a path and stop it there, on any floor.
/// </summary>
public sealed class RfSteeringTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestRfSteerMob
  components:
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

- type: entity
  id: TestRfBlock
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
        - WallLayer
";

    private const int IceStart = 40;

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly ITileDefinitionManager _tileDefs = default!;

    private Entity<MapGridComponent> _grid;
    private EntityUid _mob;

    private async Task Setup(string tile)
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 80; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    var id = x < IceStart ? "Plating" : tile;
                    _maps.SetTile(_grid.Owner, _grid.Comp, new Vector2i(x, y), new Tile(_tileDefs[id].TileId));
                }
            }

            _mob = _entMan.SpawnAtPosition("TestRfSteerMob", new EntityCoordinates(_grid, 2.5f, 1.5f));
        });

        await Pair.RunTicksSync(5);
    }

    private void Go(float endX, int interval, float range, bool detour = false) => Go(_mob, 2, endX, interval, range, detour);

    /// <summary>
    /// Sends the agent along the row of tiles between the two places, in either direction.
    /// </summary>
    private void Go(EntityUid mob, int startX, float endX, int interval, float range, bool detour = false)
    {
        var path = new List<PathNodeRef>();
        var step = endX > startX ? 1 : -1;

        for (var x = startX; step > 0 ? x < (int) endX : x > (int) endX; x += step)
        {
            // The path of the agent does not lead through what is in the way, it leads around it.
            var y = detour && x is >= 14 and <= 16 ? 2 : 1;
            path.Add(new PathNodeRef(_grid.Owner, default, 0, new Box2(x, y, x + 1, y + 1)));
        }

        _entMan.GetComponent<RfSteererComponent>(mob).UpdateInterval = interval;

        // The tests are about the steering at the speed of a run, the walk is the business of WalkRunTest.
        _entMan.GetComponent<RfSteererComponent>(mob).Sprint = true;

        var coordinates = new EntityCoordinates(_grid, endX, 1.5f);
        var result = new PathSearchResult(PathSearchStatus.Path, path, new());
        Assert.That(_entMan.System<RfSteeringSystem>().Follow(mob, result, coordinates, range));
    }

    /// <summary>
    /// Runs until the steering reports the arrival, remembers the farthest the agent went.
    /// </summary>
    private async Task<(RfSteeringStatus Status, float Max, float X, float Speed)> Run(int maxTicks)
    {
        var max = 0f;
        var status = RfSteeringStatus.Moving;
        var x = 0f;
        var speed = 0f;

        for (var i = 0; i < maxTicks && status == RfSteeringStatus.Moving; i++)
        {
            await Pair.Server.WaitRunTicks(1);
            await Pair.Server.WaitPost(() =>
            {
                status = _entMan.GetComponent<RfSteeringComponent>(_mob).Status;
                x = _transform.GetWorldPosition(_mob).X;
                speed = _entMan.GetComponent<PhysicsComponent>(_mob).LinearVelocity.Length();
                max = Math.Max(max, x);
            });
        }

        return (status, max, x, speed);
    }

    [Test]
    public async Task TestStopsOnFloor([Values(1, 5)] int interval)
    {
        await Setup("Plating");
        await Pair.Server.WaitPost(() => Go(30.5f, interval, 0.3f));

        var (status, max, x, speed) = await Run(1000);

        Assert.That(status, Is.EqualTo(RfSteeringStatus.InRange));
        Assert.That(x, Is.EqualTo(30.5f).Within(0.3f));
        Assert.That(speed, Is.LessThan(0.3f));
        Assert.That(max, Is.LessThan(30.5f + 0.3f), "must not overshoot");
    }

    /// <summary>
    /// The goal of the whole steering: an agent that runs onto ice and has to stop at the target there.
    /// </summary>
    [Test]
    public async Task TestStopsOnIce([Values(1, 5)] int interval)
    {
        await Setup("FloorIce");
        await Pair.Server.WaitPost(() => Go(60.5f, interval, 0.3f));

        var (status, max, x, speed) = await Run(1500);

        TestContext.Out.WriteLine($"MEASURE ice interval {interval}: {status}, at {x:F2}, max {max:F2}, speed {speed:F2}");

        Assert.That(status, Is.EqualTo(RfSteeringStatus.InRange));
        Assert.That(x, Is.EqualTo(60.5f).Within(0.3f));
        Assert.That(max, Is.LessThan(60.5f + 0.5f), "must not fly past the target");
    }

    [Test]
    public async Task TestStopReleasesAgent()
    {
        await Setup("FloorIce");
        await Pair.Server.WaitPost(() => Go(60.5f, 1, 0.3f));
        await Pair.Server.WaitRunTicks(90);
        await Pair.Server.WaitPost(() => _entMan.System<RfSteeringSystem>().Stop(_mob));
        await Pair.Server.WaitRunTicks(120);

        await Pair.Server.WaitPost(() =>
        {
            Assert.That(_entMan.HasComponent<RfSteeringComponent>(_mob), Is.False, "released once it has stopped");
            Assert.That(_entMan.GetComponent<PhysicsComponent>(_mob).LinearVelocity.Length(), Is.LessThan(0.3f));
        });
    }

    /// <summary>
    /// A body stands on the path: the agent goes around it and gets to the end.
    /// </summary>
    [Test]
    public async Task TestGoesAroundObstacle([Values(1, 5)] int interval)
    {
        await Setup("Plating");
        await Pair.Server.WaitPost(() => _entMan.SpawnAtPosition("TestRfBlock", new EntityCoordinates(_grid, 15.5f, 1.5f)));
        await Pair.Server.WaitPost(() => Go(30.5f, interval, 0.3f, detour: true));

        var (status, _, x, _) = await Run(1000);

        Assert.That(status, Is.EqualTo(RfSteeringStatus.InRange));
        Assert.That(x, Is.EqualTo(30.5f).Within(0.4f));
    }

    /// <summary>
    /// The end of the path is where the agent cannot stand (somebody or something is there): it gets as close as it can, and stops there.
    /// It is not left in that state for good, as it does not try to move any more and so is never taken for being stuck, but it is not
    /// in range either.
    /// </summary>
    [Test]
    public async Task TestAgentThatStopsShortOfTheEndIsStuck([Values(1, 5)] int interval)
    {
        await Setup("Plating");
        await Pair.Server.WaitPost(() => _entMan.SpawnAtPosition("TestRfBlock", new EntityCoordinates(_grid, 20.5f, 1.5f)));

        // The end is in the middle of the block, the range is small.
        await Pair.Server.WaitPost(() => Go(20.5f, interval, 0.3f));

        var (status, _, x, _) = await Run(1500);

        Assert.That(status, Is.EqualTo(RfSteeringStatus.Stuck), "the agent that cannot get closer is told that it is stuck");
        Assert.That(x, Is.LessThan(20f));
    }

    /// <summary>
    /// Two agents go toward each other along the same row, they pass and both get to their ends.
    /// </summary>
    [Test]
    public async Task TestAgentsPassEachOther([Values(1, 5)] int interval)
    {
        await Setup("Plating");

        EntityUid other = default;
        await Pair.Server.WaitPost(() =>
        {
            other = _entMan.SpawnAtPosition("TestRfSteerMob", new EntityCoordinates(_grid, 30.5f, 1.5f));
            Go(_mob, 2, 30.5f, interval, 0.3f);
            Go(other, 30, 2.5f, interval, 0.3f);
        });

        var statuses = (RfSteeringStatus.Moving, RfSteeringStatus.Moving);

        for (var i = 0; i < 1000 && (statuses.Item1 == RfSteeringStatus.Moving || statuses.Item2 == RfSteeringStatus.Moving); i++)
        {
            await Pair.Server.WaitRunTicks(1);
            await Pair.Server.WaitPost(() =>
            {
                statuses = (_entMan.GetComponent<RfSteeringComponent>(_mob).Status,
                    _entMan.GetComponent<RfSteeringComponent>(other).Status);
            });
        }

        await Pair.Server.WaitPost(() =>
        {
            Assert.That(statuses.Item1, Is.EqualTo(RfSteeringStatus.InRange));
            Assert.That(statuses.Item2, Is.EqualTo(RfSteeringStatus.InRange));
            Assert.That(_transform.GetWorldPosition(_mob).X, Is.EqualTo(30.5f).Within(0.4f));
            Assert.That(_transform.GetWorldPosition(other).X, Is.EqualTo(2.5f).Within(0.4f));
        });
    }

    /// <summary>
    /// An agent arrives with its face to where it was going, braking with the input must not turn it around.
    /// </summary>
    [Test]
    public async Task TestFacesForwardOnArrival([Values("Plating", "FloorIce")] string floor)
    {
        await Setup(floor);
        await Pair.Server.WaitPost(() => Go(floor == "Plating" ? 30.5f : 60.5f, 1, 0.3f));

        var (status, _, _, _) = await Run(1500);

        await Pair.Server.WaitPost(() =>
        {
            Assert.That(status, Is.EqualTo(RfSteeringStatus.InRange));

            var facing = _transform.GetWorldRotation(_mob).ToWorldVec();
            Assert.That(facing.X, Is.GreaterThan(0.9f), "the agent must look where it went, not back");
        });
    }

    /// <summary>
    /// An agent that is pressed against something it cannot pass must not twitch, and must give up after a while.
    /// </summary>
    [Test]
    public async Task TestDoesNotTwitchAgainstWall()
    {
        await Setup("Plating");

        EntityUid block = default;
        await Pair.Server.WaitPost(() =>
        {
            block = _entMan.SpawnAtPosition("TestRfBlock", new EntityCoordinates(_grid, 15.5f, 1.5f));

            var path = new List<PathNodeRef>();

            for (var x = 2; x < 30; x++)
            {
                path.Add(new PathNodeRef(_grid.Owner, default, 0, new Box2(x, 1, x + 1, 2)));
            }

            // The wall is on the way and the agent is told not to avoid it, as if it were the target.
            var result = new PathSearchResult(PathSearchStatus.Path, path, new());
            Assert.That(_entMan.System<RfSteeringSystem>().Follow(
                _mob, result, new EntityCoordinates(_grid, 30.5f, 1.5f), 0.3f, null, block));
        });

        var flips = 0;
        var last = 0f;
        var status = RfSteeringStatus.Moving;

        for (var i = 0; i < 300; i++)
        {
            await Pair.Server.WaitRunTicks(1);
            await Pair.Server.WaitPost(() =>
            {
                status = _entMan.GetComponent<RfSteeringComponent>(_mob).Status;
                var input = _entMan.GetComponent<Content.Shared.Movement.Components.InputMoverComponent>(_mob)
                    .CurTickSprintMovement.X;

                // Only after the agent has got to the wall.
                if (i > 100 && Math.Abs(input) > 0.05f)
                {
                    if (last != 0f && Math.Sign(input) != Math.Sign(last))
                        flips++;

                    last = input;
                }
            });
        }

        TestContext.Out.WriteLine($"MEASURE wall: status {status}, input flips {flips}");
        Assert.That(flips, Is.LessThan(5), "the input must not jump from side to side");
        Assert.That(status, Is.EqualTo(RfSteeringStatus.Stuck));
    }

    /// <summary>
    /// Two agents are to stand close to each other, as when they talk. They must get there and stop.
    /// </summary>
    [Test]
    public async Task TestAgentsStandCloseTogether()
    {
        await Setup("Plating");

        EntityUid other = default;
        await Pair.Server.WaitPost(() =>
        {
            other = _entMan.SpawnAtPosition("TestRfSteerMob", new EntityCoordinates(_grid, 24.5f, 1.5f));
            Go(_mob, 2, 12.0f, 1, 0.3f);
            Go(other, 24, 12.9f, 1, 0.3f);
        });

        var statuses = (RfSteeringStatus.Moving, RfSteeringStatus.Moving);

        for (var i = 0; i < 600 && (statuses.Item1 == RfSteeringStatus.Moving || statuses.Item2 == RfSteeringStatus.Moving); i++)
        {
            await Pair.Server.WaitRunTicks(1);
            await Pair.Server.WaitPost(() =>
            {
                statuses = (_entMan.GetComponent<RfSteeringComponent>(_mob).Status,
                    _entMan.GetComponent<RfSteeringComponent>(other).Status);
            });
        }

        TestContext.Out.WriteLine($"MEASURE together: {statuses}");
        Assert.That(statuses.Item1, Is.Not.EqualTo(RfSteeringStatus.Moving));
        Assert.That(statuses.Item2, Is.Not.EqualTo(RfSteeringStatus.Moving));
    }

    /// <summary>
    /// On the ground an agent goes at full speed to the last moment and stops short, it does not slow down early as if
    /// it were on ice.
    /// </summary>
    [Test]
    public async Task TestKeepsSpeedUntilTheEndOnFloor()
    {
        await Setup("Plating");
        await Pair.Server.WaitPost(() => Go(30.5f, 1, 0.3f));

        var speedBefore = 0f;
        var status = RfSteeringStatus.Moving;

        for (var i = 0; i < 1000 && status == RfSteeringStatus.Moving; i++)
        {
            await Pair.Server.WaitRunTicks(1);
            await Pair.Server.WaitPost(() =>
            {
                status = _entMan.GetComponent<RfSteeringComponent>(_mob).Status;

                // A meter and a half before the end.
                if (_transform.GetWorldPosition(_mob).X < 29f)
                    speedBefore = _entMan.GetComponent<PhysicsComponent>(_mob).LinearVelocity.Length();
            });
        }

        TestContext.Out.WriteLine($"MEASURE floor speed 1.5 m before the end: {speedBefore:F2}");
        Assert.That(speedBefore, Is.GreaterThan(4f), "the agent must not have begun to slow down yet");
    }
}
