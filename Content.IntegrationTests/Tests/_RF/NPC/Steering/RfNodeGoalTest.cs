#nullable enable
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
}
