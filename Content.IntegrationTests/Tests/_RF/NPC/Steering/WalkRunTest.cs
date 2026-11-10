#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Steering.Components;
using Content.Server._RF.NPC.Steering.Systems;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared.CombatMode;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._RF.NPC.Steering;

/// <summary>
/// The agents walk, and run only when the order says so or when they are in the combat mode and the order has nothing to say about it.
/// </summary>
public sealed class WalkRunTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestWalkRunMob
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
    baseWalkSpeed: 2
    baseSprintSpeed: 5
  - type: CombatMode
  - type: RfSteerer
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedCombatModeSystem _combat = default!;
    [SidedDependency(Side.Server)] private readonly RfSteeringSystem _steering = default!;

    private async Task<float> TopSpeed(bool? sprint, bool combat)
    {
        var map = await Pair.CreateTestMap();
        EntityUid mob = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 40; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    _maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            mob = _entMan.SpawnAtPosition("TestWalkRunMob", new EntityCoordinates(map.Grid, 2.5f, 1.5f));
            _combat.SetInCombatMode(mob, combat);
            _entMan.GetComponent<RfSteererComponent>(mob).Sprint = sprint;

            var path = new List<PathNodeRef>();

            for (var x = 2; x < 35; x++)
            {
                path.Add(new PathNodeRef(map.Grid.Owner, default, 0, new Box2(x, 1, x + 1, 2)));
            }

            var result = new PathSearchResult(PathSearchStatus.Path, path, new());
            Assert.That(_steering.Follow(mob, result, new EntityCoordinates(map.Grid, 35f, 1.5f), 0.4f));
        });

        var top = 0f;

        for (var i = 0; i < 60; i++)
        {
            await Pair.RunTicksSync(2);
            await Pair.Server.WaitPost(() => top = Math.Max(top, _entMan.GetComponent<PhysicsComponent>(mob).LinearVelocity.Length()));
        }

        return top;
    }

    [Test]
    public async Task TestAgentWalksByDefault()
    {
        var speed = await TopSpeed(null, false);
        Assert.That(speed, Is.InRange(1.5f, 2.3f), $"the walking speed is 2 ({speed})");
    }

    [Test]
    public async Task TestAgentRunsWhenTheOrderSaysSo()
    {
        var speed = await TopSpeed(true, false);
        Assert.That(speed, Is.GreaterThan(3.5f), $"the running speed is 5 ({speed})");
    }

    [Test]
    public async Task TestAgentInCombatModeRunsByDefault()
    {
        var speed = await TopSpeed(null, true);
        Assert.That(speed, Is.GreaterThan(3.5f), $"it runs in the combat mode ({speed})");
    }

    [Test]
    public async Task TestOrderCanMakeAgentInCombatModeWalk()
    {
        var speed = await TopSpeed(false, true);
        Assert.That(speed, Is.InRange(1.5f, 2.3f), $"the order says walk ({speed})");
    }
}
