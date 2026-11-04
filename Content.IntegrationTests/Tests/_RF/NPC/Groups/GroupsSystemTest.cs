#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Groups.Systems;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.Engagement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.NPC.Groups;

/// <summary>
/// The groups of an owner: divided so that they are about as strong as each other, filled by the places in the roles, joined,
/// and a role that is empty is filled from the group.
/// </summary>
public sealed class GroupsSystemTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: MobHuman
  id: TestGroupAgent
  components:
  - type: Goap
    rootTask: Empty

- type: engagementRole
  id: TestGroupLeader
  force: true
  minCount: 1
  maxCount: 1
  weight: 0

- type: engagementRole
  id: TestGroupMember
  force: true
  maxCount: 2
  weight: 1

- type: entity
  id: TestGroup
  parent: BaseEngagement
  components:
  - type: Engagement
    dissolveInvalid: false
    roles:
    - TestGroupLeader
    - TestGroupMember
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly GroupsSystem _groups = default!;
    [SidedDependency(Side.Server)] private readonly EngagementSystem _engagement = default!;

    private EntityUid _owner;
    private readonly List<EntityUid> _agents = new();

    private async Task Setup(int agents)
    {
        var map = await Pair.CreateTestMap();

        await Pair.Server.WaitPost(() =>
        {
            var coords = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
            _owner = _entMan.SpawnEntity("TestGroupAgent", coords);

            for (var i = 0; i < agents; i++)
            {
                _agents.Add(_entMan.SpawnEntity("TestGroupAgent", coords));
            }
        });
    }

    [Test]
    public void TestPartitionIsEven()
    {
        // 9 5 4 3 2 1: the strongest are the first of the lists, every other goes to the weakest.
        var items = new[] { 9f, 5f, 4f, 3f, 2f, 1f }.Select(x => (x, x));
        var lists = GroupsSystem.Partition(items, 2);

        Assert.That(lists, Has.Count.EqualTo(2));
        Assert.That(lists[0][0], Is.EqualTo(9f), "the strongest is the first of the first");
        Assert.That(lists[1][0], Is.EqualTo(5f));
        Assert.That(lists.Sum(x => x.Sum()), Is.EqualTo(24f));
        Assert.That(Math.Abs(lists[0].Sum() - lists[1].Sum()), Is.LessThanOrEqualTo(4f));
        Assert.That(GroupsSystem.Partition(items, 10), Has.Count.EqualTo(6), "not more lists than there are items");
    }

    [Test]
    public async Task TestFormDividesMembersAndSeatsLeaders()
    {
        await Setup(6);

        await Pair.Server.WaitAssertion(() =>
        {
            var weighted = _agents.Select((x, i) => (x, (float) (i + 1))).ToList();
            var formed = _groups.Form(_owner, "TestGroup", weighted, 2);

            Assert.That(formed, Has.Count.EqualTo(2));
            Assert.That(_groups.GetGroups(_owner, "TestGroup"), Has.Count.EqualTo(2));
            Assert.That(_groups.GetMembers(_owner, "TestGroup"), Is.EquivalentTo(_agents));

            foreach (var group in formed)
            {
                Assert.That(_groups.GetMembers(group.Comp, "TestGroupLeader"), Has.Count.EqualTo(1), "every group has a leader");
            }

            Assert.That(_groups.IsFormed(_groups.GetGroups(_owner, "TestGroup"), _agents, 2), Is.True);
            Assert.That(_groups.IsFormed(_groups.GetGroups(_owner, "TestGroup"), _agents, 3), Is.False);
        });
    }

    [Test]
    public async Task TestPlaceFillsGroupsInTurn()
    {
        await Setup(7);

        await Pair.Server.WaitAssertion(() =>
        {
            // A group takes one leader and two members, the next member goes to another.
            foreach (var agent in _agents)
            {
                Assert.That(_groups.Place(_owner, "TestGroup", agent), Is.True);
            }

            var groups = _groups.GetGroups(_owner, "TestGroup");
            Assert.That(groups.Sum(x => _groups.GetMembers(x.Comp).Count), Is.EqualTo(7));
            Assert.That(groups.All(x => _groups.GetMembers(x.Comp).Count <= 3), Is.True);
            Assert.That(groups, Has.Count.EqualTo(3));
            Assert.That(_groups.TryGetGroup(_owner, _agents[0], "TestGroup", out var first), Is.True);
            Assert.That(_groups.GetMembers(first.Comp), Does.Contain(_agents[0]));
        });
    }

    [Test]
    public async Task TestMergeMovesEveryoneAndEndsTheGroup()
    {
        await Setup(4);

        await Pair.Server.WaitAssertion(() =>
        {
            // A leader and a member in each.
            var formed = _groups.Form(_owner, "TestGroup", _agents.Select(x => (x, 1f)), 2);
            Assert.That(formed, Has.Count.EqualTo(2));
            var (a, b) = (formed[0], formed[1]);
            var moved = _groups.GetMembers(b.Comp);

            _groups.Merge(b, a);

            Assert.That(_entMan.Deleted(b.Owner), Is.True, "the group that is joined is over");
            Assert.That(_groups.GetGroups(_owner, "TestGroup"), Has.Count.EqualTo(1));
            Assert.That(_groups.GetMembers(a.Comp, "TestGroupLeader"), Has.Count.EqualTo(1), "the leader is the one it has had");
            Assert.That(_groups.GetMembers(a.Comp).Count, Is.EqualTo(3), "one leader and two members: there are no more places");
            Assert.That(moved.Count(x => _groups.GetMembers(a.Comp).Contains(x)), Is.GreaterThan(0), "somebody has moved");
        });
    }

    [Test]
    public async Task TestFillRoleTakesTheBest()
    {
        await Setup(3);

        await Pair.Server.WaitAssertion(() =>
        {
            var group = _groups.Form(_owner, "TestGroup", _agents.Select(x => (x, 1f)), 1)[0];
            var leader = _groups.GetMembers(group.Comp, "TestGroupLeader").Single();
            Assert.That(_groups.FillRole(group, "TestGroupLeader"), Is.False, "the role is not empty");

            // The leader is gone: the best of the others by the rank takes his place.
            Assert.That(_engagement.LeaveEngagement(group.AsNullable(), leader, EngagementEndReason.Finished), Is.True);
            var best = _groups.GetMembers(group.Comp).MaxBy(x => _agents.IndexOf(x));

            Assert.That(_groups.FillRole(group, "TestGroupLeader", x => _agents.IndexOf(x)), Is.True);
            Assert.That(_groups.GetMembers(group.Comp, "TestGroupLeader"), Is.EquivalentTo(new[] { best }));
        });
    }

    [Test]
    public async Task TestPruneEndsEmptyGroups()
    {
        await Setup(1);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_groups.Place(_owner, "TestGroup", _agents[0]), Is.True);
            var group = _groups.GetGroups(_owner, "TestGroup").Single();

            Assert.That(_groups.Prune(_owner, "TestGroup"), Has.Count.EqualTo(1), "the group has someone");

            _engagement.LeaveEngagement(group.AsNullable(), _agents[0], EngagementEndReason.Finished);
            Assert.That(_groups.Prune(_owner, "TestGroup"), Is.Empty);
            Assert.That(_entMan.Deleted(group.Owner), Is.True);
        });
    }
}
