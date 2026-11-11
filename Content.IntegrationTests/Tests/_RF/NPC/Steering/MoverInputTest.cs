#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._RF.NPC.Steering;

/// <summary>
/// How a mob reacts to the input that an NPC gives to the mover, which is what the steering has to take into
/// account: whether the input can be partial, how long it lasts and how the floor changes the response.
/// </summary>
public sealed class MoverInputTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestSteerMob
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
";

    private const int FloorStart = 0;
    private const int IceStart = 40;

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly SharedPhysicsSystem _physics = default!;
    [SidedDependency(Side.Server)] private readonly IGameTiming _timing = default!;
    [SidedDependency(Side.Server)] private readonly ITileDefinitionManager _tileDefs = default!;

    private Entity<MapGridComponent> _grid;
    private EntityUid _mob;

    private async Task Setup()
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;

        await Pair.Server.WaitPost(() =>
        {
            var floor = new Tile(_tileDefs["Plating"].TileId);
            var ice = new Tile(_tileDefs["FloorIce"].TileId);

            for (var x = 0; x < 80; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    _maps.SetTile(_grid.Owner, _grid.Comp, new Vector2i(x, y), x < IceStart ? floor : ice);
                }
            }

            _mob = _entMan.SpawnAtPosition("TestSteerMob", new EntityCoordinates(_grid, 0.5f, 1.5f));
        });

        await Pair.RunTicksSync(5);
    }

    private async Task Place(float x)
    {
        await Pair.Server.WaitPost(() =>
        {
            _transform.SetCoordinates(_mob, new EntityCoordinates(_grid, x, 1.5f));
            _physics.SetLinearVelocity(_mob, Vector2.Zero);
        });

        await Pair.RunTicksSync(2);
    }

    private (Vector2 Position, Vector2 Velocity) State()
    {
        var position = _transform.GetWorldPosition(_mob);
        return (position, _entMan.GetComponent<PhysicsComponent>(_mob).LinearVelocity);
    }

    private void SetInput(Vector2 input)
    {
        var mover = _entMan.GetComponent<InputMoverComponent>(_mob);
        mover.CurTickSprintMovement = input;
        mover.LastInputTick = _timing.CurTick;
        mover.LastInputSubTick = ushort.MaxValue;
    }

    /// <summary>
    /// Holds the input for the given number of ticks, setting it again before every tick the way an NPC does.
    /// </summary>
    private async Task Hold(Vector2 input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            await Pair.Server.WaitPost(() => SetInput(input));
            await Pair.Server.WaitRunTicks(1);
        }
    }

    private async Task Idle(int ticks)
    {
        await Pair.Server.WaitRunTicks(ticks);
    }

    private float _speed;

    /// <summary>
    /// A partial input is a partial speed: the mover does not normalize the input, so the steering can ask
    /// for exactly the speed it wants and does not have to be limited to a few directions at full speed.
    /// </summary>
    [Test]
    public async Task TestInputIsAnalog()
    {
        await Setup();
        var dt = (float) _timing.TickPeriod.TotalSeconds;

        foreach (var scale in new[] { 1f, 0.5f, 0.25f })
        {
            await Place(FloorStart + 2f);
            await Hold(new Vector2(scale, 0f), (int) (2f / dt));
            await Pair.Server.WaitPost(() => _speed = State().Velocity.Length());

            Assert.That(_speed, Is.EqualTo(4.5f * scale).Within(0.05f), $"input {scale}");
        }
    }

    /// <summary>
    /// The input lasts for one tick, so it has to be set again every tick, even when the decision is not
    /// made every tick.
    /// </summary>
    [Test]
    public async Task TestInputLastsOneTick()
    {
        await Setup();
        var dt = (float) _timing.TickPeriod.TotalSeconds;

        await Place(FloorStart + 2f);
        await Pair.Server.WaitPost(() => SetInput(new Vector2(1f, 0f)));
        await Pair.Server.WaitRunTicks(1);
        await Pair.Server.WaitPost(() => _speed = State().Velocity.Length());
        Assert.That(_speed, Is.GreaterThan(0.5f), "the input must have had an effect");

        await Idle((int) (1f / dt));
        await Pair.Server.WaitPost(() => _speed = State().Velocity.Length());
        Assert.That(_speed, Is.LessThan(0.05f), "the mob must have stopped, nothing moved it for a second");
    }

}
