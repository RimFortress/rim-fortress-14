#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.NPC.Engagement;

/// <summary>
/// An agent stays in a role of a situation while the conditions of the role hold, so which states of the mob make it leave
/// is up to the role: one role is left when the mob is not alive, another one only when it is dead, a third one never.
/// </summary>
public sealed class EngagementStayConditionsTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: goapCompound
  id: TestStayRoot
  tasks: []

- type: entity
  parent: MobHuman
  id: TestStayAgent
  components:
  - type: Goap
    rootTask: TestStayRoot

- type: engagementRole
  id: TestStayAlive
  force: true
  stayConditions:
  - !type:MobState
    targetState: Alive

- type: engagementRole
  id: TestStayNotDead
  force: true
  stayConditions:
  - !type:MobState
    targetState: Dead
    invert: true

- type: engagementRole
  id: TestStayAlways
  force: true

- type: engagement
  id: TestStay
  dissolveInvalid: false
  roles:
  - TestStayAlive
  - TestStayNotDead
  - TestStayAlways
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly EngagementSystem _engagement = default!;
    [SidedDependency(Side.Server)] private readonly MobStateSystem _mobState = default!;

    private Entity<EngagementComponent> _situation;
    private EntityUid _alive;
    private EntityUid _notDead;
    private EntityUid _always;

    private async Task Setup()
    {
        var map = await Pair.CreateTestMap();

        await Pair.Server.WaitPost(() =>
        {
            var coords = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
            _alive = _entMan.SpawnEntity("TestStayAgent", coords);
            _notDead = _entMan.SpawnEntity("TestStayAgent", coords);
            _always = _entMan.SpawnEntity("TestStayAgent", coords);

            Assert.That(_engagement.TryStartEngagement("TestStay", _alive, out var situation), Is.True);
            _situation = situation!.Value;

            Assert.That(_engagement.TryJoinEngagement(_situation.AsNullable(), "TestStayAlive", _alive), Is.True);
            Assert.That(_engagement.TryJoinEngagement(_situation.AsNullable(), "TestStayNotDead", _notDead), Is.True);
            Assert.That(_engagement.TryJoinEngagement(_situation.AsNullable(), "TestStayAlways", _always), Is.True);
        });
    }

    private bool In(EntityUid uid) => _engagement.IsMember(_situation.AsNullable(), uid);

    [Test]
    public async Task TestEveryoneLeavesWhenTheirConditionsStopHolding()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(In(_alive), Is.True);
            Assert.That(In(_notDead), Is.True);
            Assert.That(In(_always), Is.True);
        });

        // A mob that is down is not alive, but it is not dead: only the role that asks for it to be alive is left.
        await Pair.Server.WaitPost(() =>
        {
            _mobState.ChangeMobState(_alive, MobState.Critical);
            _mobState.ChangeMobState(_notDead, MobState.Critical);
            _mobState.ChangeMobState(_always, MobState.Critical);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(In(_alive), Is.False, "it has to be alive");
            Assert.That(In(_notDead), Is.True, "it only must not be dead");
            Assert.That(In(_always), Is.True, "it is not asked anything");
        });

        await Pair.Server.WaitPost(() =>
        {
            _mobState.ChangeMobState(_notDead, MobState.Dead);
            _mobState.ChangeMobState(_always, MobState.Dead);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(In(_notDead), Is.False, "it is dead now");
            Assert.That(In(_always), Is.True, "nothing makes it leave");
        });
    }

    [Test]
    public async Task TestCannotTakeARoleItCannotStayIn()
    {
        await Setup();

        await Pair.Server.WaitPost(() =>
        {
            var dead = _entMan.SpawnEntity("TestStayAgent", new EntityCoordinates(_entMan.GetComponent<TransformComponent>(_alive).ParentUid, 0.5f, 0.5f));
            _mobState.ChangeMobState(dead, MobState.Dead);

            Assert.That(_engagement.TryJoinEngagement(_situation.AsNullable(), "TestStayAlive", dead), Is.False);
            Assert.That(_engagement.TryJoinEngagement(_situation.AsNullable(), "TestStayAlways", dead), Is.True);
        });
    }
}
