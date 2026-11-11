#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.NPC.Engagement;

/// <summary>
/// An invite is kept in the participant component of the one who is invited, so the component stays while the invite does, even when
/// the one who is invited leaves every situation that it is in. Otherwise ending the situation that invited it finds no component.
/// </summary>
public sealed class EngagementInviteTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: engagementRole
  id: TestInviteMember
  force: true

- type: engagementRole
  id: TestInviteGuest
  joinAfterStart: true

- type: entity
  id: TestInviteMembers
  parent: BaseEngagement
  components:
  - type: Engagement
    dissolveInvalid: false
    roles:
    - TestInviteMember

- type: entity
  id: TestInviteGuests
  parent: BaseEngagement
  components:
  - type: Engagement
    dissolveInvalid: false
    roles:
    - TestInviteGuest
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly EngagementSystem _engagement = default!;

    [Test]
    public async Task TestInviteOutlivesLeavingAnotherSituation()
    {
        var map = await Pair.CreateTestMap();

        await Pair.Server.WaitAssertion(() =>
        {
            var coords = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
            var actor = _entMan.SpawnEntity("MobHuman", coords);
            var inviter = _entMan.SpawnEntity("MobHuman", coords);
            var members = _entMan.SpawnEntity("TestInviteMembers", MapCoordinates.Nullspace);
            var guests = _entMan.SpawnEntity("TestInviteGuests", MapCoordinates.Nullspace);

            Assert.That(_engagement.TryJoinEngagement(members, "TestInviteMember", actor), Is.True);
            Assert.That(_engagement.InviteToEngagement(guests, "TestInviteGuest", actor, inviter), Is.True);

            // It leaves the only situation that it is in, with the invite pending.
            Assert.That(_engagement.LeaveEngagement(members, actor, EngagementEndReason.Finished), Is.True);
            Assert.That(_entMan.HasComponent<EngagementParticipantComponent>(actor), Is.True, "it is still invited");

            // The situation that invited it ends: the invite goes with it, and no error is logged.
            _engagement.EndEngagement(guests, EngagementEndReason.Finished);
            Assert.That(_entMan.Deleted(guests), Is.True);
        });
    }
}
