#nullable enable
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Server._RF.NPC.Steering.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.NPC.Steering;

/// <summary>
/// A step is a short walk to a place close by with no path searched for: it is made only if the way is clear, and the agent that makes it
/// keeps facing the enemy it steps away from.
/// </summary>
public sealed class StepTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestStepMob
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
  id: TestStepWall
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

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly RfPathfindingSystem _pathfinding = default!;
    [SidedDependency(Side.Server)] private readonly RfSteeringSystem _steering = default!;

    private Entity<MapGridComponent> _grid;
    private EntityUid _mob;
    private EntityUid _enemy;

    private async Task Setup()
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    _maps.SetTile(_grid.Owner, _grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            _entMan.SpawnAtPosition("TestStepWall", new EntityCoordinates(_grid, 6.5f, 3.5f));
            _mob = _entMan.SpawnAtPosition("TestStepMob", new EntityCoordinates(_grid, 4.5f, 3.5f));
            _enemy = _entMan.SpawnAtPosition("TestStepMob", new EntityCoordinates(_grid, 3.5f, 3.5f));
        });

        await Pair.RunTicksSync(5);
        await Pair.Server.WaitPost(() => _pathfinding.EnsureChunk(_grid, Vector2i.Zero));
    }

    [Test]
    public async Task TestStepIsMadeFacingTheEnemy()
    {
        await Setup();
        var started = false;

        // Away from the enemy, a meter to the right, with the wall behind the place
        await Pair.Server.WaitPost(() =>
        {
            started = _steering.Step(_mob, _transform.GetWorldPosition(_mob) + new Vector2(0.8f, 0.8f), _enemy);
            Assert.That(_steering.IsStepping(_mob), Is.EqualTo(started));
        });
        Assert.That(started, Is.True, "the way is clear");

        await Pair.RunTicksSync(45);

        await Pair.Server.WaitPost(() =>
        {
            var position = _transform.GetWorldPosition(_mob);
            Assert.That(Vector2.Distance(position, _transform.GetWorldPosition(_grid) + new Vector2(5.3f, 4.3f)), Is.LessThan(0.5f), $"the agent got to the place ({position})");

            // The enemy is on the left of it and the agent looks there although it went the other way
            var toEnemy = _transform.GetWorldPosition(_enemy) - position;
            var difference = Angle.ShortestDistance(_transform.GetWorldRotation(_mob), toEnemy.ToWorldAngle());
            Assert.That(Math.Abs(difference.Degrees), Is.LessThan(25), "it keeps facing the enemy");
        });
    }

    [Test]
    public async Task TestStepIntoTheWallIsNotMade()
    {
        await Setup();

        await Pair.Server.WaitPost(() =>
        {
            Assert.That(_steering.Step(_mob, _transform.GetWorldPosition(_mob) + new Vector2(2f, 0f), _enemy), Is.False, "the wall is in the way");
            Assert.That(_steering.IsStepping(_mob), Is.False);
            Assert.That(_steering.Step(_mob, _transform.GetWorldPosition(_mob) + new Vector2(0f, 20f), _enemy), Is.False, "the place is out of the floor");
        });
    }
}
