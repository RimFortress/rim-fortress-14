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
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.NPC.Pathfinding;

/// <summary>
/// The doors on the way: what the door is like, what the path through it asks of the agent.
/// </summary>
public sealed class RfPathfindingDoorTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: goapCompound
  id: TestDoorRoot
  tasks:
  - preconditions: []
    action: !type:TestNodeMove
    effects:
      Arrived: true

- type: entity
  id: TestDoorAgent
  components:
  - type: Goap
    rootTask: TestDoorRoot
  - type: PathProfiles
    profiles:
    - Walker
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

- type: goapCompound
  id: TestDoorWalkRoot
  tasks:
  - preconditions: []
    action: !type:MoveTo
    effects:
      Arrived: true
  - proto: Utility
  - proto: Door

- type: utilityAiGoal
  id: TestDoorGo
  tempKeys:
  - TargetCoordinates
  goalState:
    Arrived: true

- type: entity
  parent: MobHuman
  id: TestDoorWalker
  components:
  - type: ActiveNPC
  - type: Goap
    rootTask: TestDoorWalkRoot
  - type: UtilityAi
  - type: PathProfiles
    profiles:
    - Walker

# Opens by itself.
- type: entity
  parent: Airlock
  id: TestDoorPowered
  components:
  - type: ApcPowerReceiver
    needsPower: false

# Has to be opened by hand, and stays open.
- type: entity
  parent: Airlock
  id: TestDoorManual
  components:
  - type: ApcPowerReceiver
    needsPower: false
  - type: Door
    bumpOpen: false
  - type: Airlock
    autoClose: false
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedDoorSystem _door = default!;
    [SidedDependency(Side.Server)] private readonly RfPathfindingSystem _pathfinding = default!;
    [SidedDependency(Side.Server)] private readonly SharedHandsSystem _hands = default!;
    [SidedDependency(Side.Server)] private readonly SharedCombatModeSystem _combat = default!;
    [SidedDependency(Side.Server)] private readonly SharedUtilityAiSystem _uai = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly DoorPathSystem _doorPath = default!;

    private static readonly StateKey<bool> CloseDoors = "CloseDoors";
    private static readonly StateKey<EntityUid> Crowbar = "Query/Crowbar";

    private async Task<(Entity<MapGridComponent> Grid, EntityUid Agent, EntityUid Door)> Setup(
        string door,
        DoorState? state = null)
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;
        EntityUid doorUid = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 14; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            for (var y = 0; y < 14; y++)
            {
                if (y != 6)
                    _entMan.SpawnAtPosition("WallSolid", new EntityCoordinates(grid, 14.5f, y + 0.5f));
            }

            doorUid = _entMan.SpawnAtPosition(door, new EntityCoordinates(grid, 14.5f, 6.5f));

            if (state is { } set)
                _door.SetState(doorUid, set);

            agent = _entMan.SpawnAtPosition("TestDoorAgent", new EntityCoordinates(grid, 2.5f, 6.5f));
        });

        await Pair.RunTicksSync(10);
        return (grid, agent, doorUid);
    }

    private async Task<PathSearchResult> Search(EntityUid agent, bool closeDoors = false, bool crowbar = false)
    {
        await Pair.Server.WaitPost(() =>
        {
            var goap = _entMan.GetComponent<GoapComponent>(agent);
            goap.State.SetValue(CloseDoors, closeDoors);

            if (crowbar)
                goap.State.SetValue(Crowbar, agent);
        });

        PathSearchResult result = default;
        var found = false;

        for (var i = 0; i < 200 && !found; i++)
        {
            await Pair.Server.WaitPost(() =>
            {
                var ent = new Entity<PathProfilesComponent, GoapComponent>(
                    agent,
                    _entMan.GetComponent<PathProfilesComponent>(agent),
                    _entMan.GetComponent<GoapComponent>(agent));

                if (i == 0)
                {
                    var grid = _entMan.GetComponent<TransformComponent>(agent).GridUid!.Value;
                    Assert.That(_pathfinding.RequestPath(ent, "Travel", new EntityCoordinates(grid, 24.5f, 6.5f), 0.5f), Is.True);
                }

                found = _pathfinding.TryGetResult(ent, out result, out _);
            });

            await Pair.RunTicksSync(2);
        }

        Assert.That(found, Is.True);
        return result;
    }

    /// <summary>
    /// What the door is like depends on whether it has power, whether it opens when it is walked into, whether it is
    /// open and whether it is welded.
    /// </summary>
    [Test]
    public async Task TestKinds()
    {
        var (_, _, powered) = await Setup("TestDoorPowered");
        var (_, _, unpowered) = await Setup("Airlock");
        var (_, _, manual) = await Setup("TestDoorManual");
        var (_, _, open) = await Setup("TestDoorManual", DoorState.Open);
        var (_, _, welded) = await Setup("TestDoorPowered", DoorState.Welded);

        await Pair.Server.WaitAssertion(() =>
        {
            ProtoId<PathTagPrototype>? Kind(EntityUid door)
                => _doorPath.GetKind((door, _entMan.GetComponent<DoorComponent>(door)));

            Assert.That(_pathfinding.HasTag(powered, "Door"), Is.True, "every door has the tag of a door");
            Assert.That(_pathfinding.HasTag(unpowered, "Door"), Is.True);
            Assert.That(Kind(powered), Is.Null);
            Assert.That(Kind(unpowered), Is.EqualTo(DoorPathSystem.NeedsPryTag));
            Assert.That(Kind(manual), Is.EqualTo(DoorPathSystem.NeedsOpeningTag));
            Assert.That(Kind(open), Is.EqualTo(DoorPathSystem.CanCloseTag));
            Assert.That(Kind(welded), Is.EqualTo(DoorPathSystem.BlockedTag));
        });
    }

    /// <summary>
    /// A door that opens by itself costs the agent nothing: there is no goal on the way.
    /// </summary>
    [Test]
    public async Task TestSelfOpeningDoorIsFree()
    {
        var (_, agent, _) = await Setup("TestDoorPowered");
        var result = await Search(agent);

        Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Path));
        Assert.That(result.Goals, Is.Empty);
    }

    /// <summary>
    /// A door that does not open by itself is opened first: the goal is before its node, and the door is its entity.
    /// </summary>
    [Test]
    public async Task TestManualDoorIsOpened()
    {
        var (_, agent, _) = await Setup("TestDoorManual");
        var result = await Search(agent);

        Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Path));
        Assert.That(result.Goals, Has.Count.EqualTo(1));
        Assert.That(result.Goals[0].Phase, Is.EqualTo(PathGoalPhase.Before));
        Assert.That(result.Goals[0].Info.Goal.Id, Is.EqualTo("DoorOpen"));
        Assert.That(result.Goals[0].Info.EntityKey, Is.EqualTo(new StateKey<EntityUid>("DoorTarget")));
        Assert.That(result.Goals[0].Info.TargetKey, Is.EqualTo(new StateKey<EntityCoordinates>("DoorPoint")));
        Assert.That(result.Goals[0].Info.RangeKey, Is.EqualTo(new StateKey<float>("InteractRange")));
    }

    /// <summary>
    /// A door that has no power is pried if the agent has something to pry it with, and it is a wall if it has not.
    /// </summary>
    [Test]
    public async Task TestUnpoweredDoorNeedsCrowbar()
    {
        var (_, agent, _) = await Setup("Airlock");

        var without = await Search(agent);
        Assert.That(without.Status, Is.Not.EqualTo(PathSearchStatus.Path));

        var with = await Search(agent, crowbar: true);
        Assert.That(with.Status, Is.EqualTo(PathSearchStatus.Path));
        Assert.That(with.Goals, Has.Count.EqualTo(1));
        Assert.That(with.Goals[0].Info.Goal.Id, Is.EqualTo("DoorPry"));
    }

    [Test]
    public async Task TestWeldedDoorIsAWall()
    {
        var (_, agent, _) = await Setup("TestDoorPowered", DoorState.Welded);
        var result = await Search(agent, crowbar: true);

        Assert.That(result.Status, Is.Not.EqualTo(PathSearchStatus.Path));
    }

    /// <summary>
    /// An open door that stays open is closed after the agent, and only by the agent that closes the doors.
    /// </summary>
    [Test]
    public async Task TestOpenDoorIsClosedBehind([Values(false, true)] bool closeDoors)
    {
        var (_, agent, _) = await Setup("TestDoorManual", DoorState.Open);
        var result = await Search(agent, closeDoors);

        Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Path));

        if (!closeDoors)
        {
            Assert.That(result.Goals, Is.Empty);
            return;
        }

        Assert.That(result.Goals, Has.Count.EqualTo(1));
        Assert.That(result.Goals[0].Phase, Is.EqualTo(PathGoalPhase.After));
        Assert.That(result.Goals[0].Info.Goal.Id, Is.EqualTo("DoorClose"));
    }

    /// <summary>
    /// The agent walks to a place behind a door that has to be opened by hand: it stops at the door, opens it, goes
    /// through, and closes it if it is to. It does that in either direction.
    /// </summary>
    [Test]
    public async Task TestAgentGoesThroughManualDoor([Values(false, true)] bool closeDoors, [Values(false, true)] bool reverse)
        => await Walk(closeDoors, reverse, 0, !closeDoors);

    /// <summary>
    /// The hand of the agent is full, and it frees one to open the door: with the free hand it switches to, and if there
    /// is none, with a storage it puts what it holds into, or with the floor.
    /// </summary>
    [Test]
    public async Task TestAgentFreesHandForDoor([Values(1, 2)] int busyHands)
        => await Walk(false, false, busyHands, true);

    /// <summary>
    /// An agent in the combat mode is on an order and does not stop to close the doors behind it.
    /// </summary>
    [Test]
    public async Task TestAgentInCombatDoesNotCloseDoor()
        => await Walk(true, false, 0, true);

    private async Task Walk(bool closeDoors, bool reverse, int busyHands, bool combat)
    {
        var (grid, _, door) = await Setup("TestDoorManual");
        EntityUid agent = default;

        await Pair.Server.WaitPost(() =>
        {
            agent = _entMan.SpawnAtPosition("TestDoorWalker", new EntityCoordinates(grid, reverse ? 24.5f : 2.5f, 6.5f));

            for (var i = 0; i < busyHands; i++)
            {
                var item = _entMan.SpawnAtPosition("Pen", new EntityCoordinates(grid, 2.5f, 6.5f));
                Assert.That(_hands.TryPickupAnyHand(agent, item), Is.True);
            }

            var goap = _entMan.GetComponent<GoapComponent>(agent);
            _combat.SetInCombatMode(agent, combat);
            goap.State.SetValue(CloseDoors, closeDoors);
            goap.State.SetValue(new StateKey<EntityCoordinates>("TargetCoordinates"), new EntityCoordinates(grid, reverse ? 2.5f : 24.5f, 6.5f));
            _uai.SetGoal(agent, "TestDoorGo");
        });

        var arrived = false;

        for (var i = 0; i < 3000 && !arrived; i++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() =>
            {
                var x = _transform.GetWorldPosition(agent).X;
                arrived = reverse ? x < 3.5f : x > 23.5f;

                // The order is put aside for the door and goes on, it is not given up.
                if (!arrived && i > 5)
                    Assert.That(_uai.TryGetCurrentGoal(agent, out _), Is.True, "the order must not be given up on the way");
            });
        }

        Assert.That(arrived, Is.True, "the agent must have gone through the door");
        await Pair.Server.WaitAssertion(() =>
            Assert.That(_combat.IsInCombatMode(agent), Is.EqualTo(combat), "the combat mode is not changed at a door"));

        if (!closeDoors)
            return;

        if (combat)
        {
            await Pair.RunTicksSync(400);
            await Pair.Server.WaitAssertion(() =>
                Assert.That(_entMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Open)));
            return;
        }

        // The agent goes back to the door after a pause, the door does not react while it is opening.
        var closed = false;

        for (var i = 0; i < 900 && !closed; i++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() => closed = _entMan.GetComponent<DoorComponent>(door).State == DoorState.Closed);
        }

        Assert.That(closed, Is.True, "the door must be closed behind the agent");
    }
}
