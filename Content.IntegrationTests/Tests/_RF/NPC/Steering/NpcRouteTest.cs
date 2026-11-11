#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Steering.Systems;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.NPC.Executable.Systems;
using Content.Shared._RF.NPC.Pathfinding;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._RF.NPC.Steering;

/// <summary>
/// The overlay of the orders draws the way that an agent goes. The server tells it once for a path, and not while the agent goes along
/// it, and only to those who control the agent.
/// </summary>
public sealed class NpcRouteTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestRouteMob
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
  id: TestRouteControlledMob
  parent: TestRouteMob
  components:
  - type: ControllableNpc
    goals: []
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedExecutableGoalSystem _executable = default!;

    private Entity<MapGridComponent> _grid;

    private async Task<EntityUid> Setup(string proto)
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;
        EntityUid mob = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    _maps.SetTile(_grid.Owner, _grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            mob = _entMan.SpawnAtPosition(proto, new EntityCoordinates(_grid, 2.5f, 1.5f));
        });

        await Pair.RunTicksSync(5);
        return mob;
    }

    private bool Go(EntityUid mob, float endX)
    {
        var path = new List<PathNodeRef>();

        for (var x = 2; x < (int) endX; x++)
        {
            path.Add(new PathNodeRef(_grid.Owner, default, 0, new Box2(x, 1, x + 1, 2)));
        }

        var result = new PathSearchResult(PathSearchStatus.Path, path, new());
        return _entMan.System<RfSteeringSystem>().Follow(mob, result, new EntityCoordinates(_grid, endX, 1.5f), 0.4f);
    }

    [Test]
    public async Task TestTheSamePathIsNotToldAgain()
    {
        var mob = await Setup("TestRouteControlledMob");

        await Pair.Server.WaitAssertion(() =>
        {
            var path = new List<Vector2> { new(2.5f, 1.5f), new(8.5f, 1.5f), new(12.5f, 2.5f) };

            Assert.That(_executable.SetRoute(mob, _grid.Owner, path), Is.True, "the first path is told");

            // A path that is searched for again and is the same, a little different in the numbers, is not.
            Assert.That(_executable.SetRoute(mob, _grid.Owner, path), Is.False);
            Assert.That(_executable.SetRoute(mob, _grid.Owner, new List<Vector2> { new(2.51f, 1.5f), new(8.5f, 1.49f), new(12.5f, 2.5f) }), Is.False);

            // Another one is.
            Assert.That(_executable.SetRoute(mob, _grid.Owner, new List<Vector2> { new(2.5f, 1.5f), new(8.5f, 1.5f), new(12.5f, 0.5f) }), Is.True);

            // Cleared, and cleared again: told once.
            _executable.ClearRoute(mob);
            Assert.That(_entMan.GetComponent<ControllableNpcComponent>(mob).Route, Is.Empty);
            Assert.That(_executable.SetRoute(mob, EntityUid.Invalid, new List<Vector2>()), Is.False);
        });
    }

    [Test]
    public async Task TestOnlyThoseWhoControlTheAgentAreToldItsPath()
    {
        var mob = await Setup("TestRouteControlledMob");
        EntityUid player = default;

        await Pair.Server.WaitPost(() =>
        {
            // The player is next to the agent and sees it, but does not control it.
            player = _entMan.SpawnAtPosition("TestRouteMob", new EntityCoordinates(_grid, 3.5f, 1.5f));
            Pair.Server.PlayerMan.SetAttachedEntity(Pair.Player!, player);
        });
        await Pair.RunTicksSync(10);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_executable.GetControllerSessions(mob), Is.Empty);
            Assert.That(Go(mob, 20f));
        });
        await Pair.RunTicksSync(10);

        var net = Pair.Server.EntMan.GetNetEntity(mob);

        await Pair.Client.WaitAssertion(() =>
        {
            var agent = Pair.Client.EntMan.GetEntity(net);
            Assert.That(Pair.Client.EntMan.EntityExists(agent), Is.True, "the agent is in view of the player");
            Assert.That(Pair.Client.EntMan.GetComponent<ControllableNpcComponent>(agent).Route, Is.Empty, "and the player is not told its path");
        });

        // Now the player controls the agent: it gets the path that the agent follows now, and then every new one.
        await Pair.Server.WaitPost(() => _executable.AddController(player, mob));
        await Pair.RunTicksSync(10);

        await Pair.Server.WaitAssertion(() => Assert.That(_executable.GetControllerSessions(mob), Is.EqualTo(new[] { Pair.Player })));

        await Pair.Client.WaitAssertion(() =>
        {
            var agent = Pair.Client.EntMan.GetEntity(net);
            Assert.That(Pair.Client.EntMan.GetComponent<ControllableNpcComponent>(agent).Route, Has.Count.GreaterThanOrEqualTo(2));
        });

        // And when it does not control it any more, it is not told any more.
        await Pair.Server.WaitPost(() =>
        {
            _executable.RemoveController(player, mob);
            Assert.That(Go(mob, 24f));
        });
        await Pair.RunTicksSync(10);

        await Pair.Server.WaitAssertion(() => Assert.That(_executable.GetControllerSessions(mob), Is.Empty));
    }
}
