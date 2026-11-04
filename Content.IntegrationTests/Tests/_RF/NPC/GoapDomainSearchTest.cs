#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Domains;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.NPC;

/// <summary>
/// The search queries follow the keys of the domains: the result of a query that is about a key of the owner of the situation of the
/// agent (<c>Engagement/Kind/Owner/Key</c>) is what the key is, whatever happens in the world to the key: the owner puts other
/// entities in it, the agent joins the situation and leaves it, the situation is given to another owner.
/// </summary>
public sealed class GoapDomainSearchTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: engagementRole
  id: TestDomainMember
  force: true

- type: entity
  id: TestDomain
  parent: BaseEngagement
  components:
  - type: Engagement
    dissolveInvalid: false
    roles:
    - TestDomainMember
  - type: Goap
    rootTask: Empty
    enabled: false

- type: entity
  id: TestDomainOwner
  components:
  - type: Goap
    rootTask: Empty
    enabled: false

- type: entity
  parent: MobHuman
  id: TestDomainAgent
  components:
  - type: Goap
    rootTask: Empty

# What the owner of the situation has in its key.
- type: searchQuery
  id: TestDomainOwnerTargets
  query: !type:KeyEntities
    key: Engagement/TestDomain/Owner/Targets

# What the situation itself has in its state.
- type: searchQuery
  id: TestDomainStateTargets
  query: !type:KeyEntities
    key: Engagement/TestDomain/State/Targets
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly EngagementSystem _engagement = default!;
    [SidedDependency(Side.Server)] private readonly OwnershipSystem _ownership = default!;
    [SidedDependency(Side.Server)] private readonly SharedGoapSystem _goap = default!;
    [SidedDependency(Side.Server)] private readonly SharedNpcSearcherSystem _searcher = default!;
    [SidedDependency(Side.Server)] private readonly GoapDomainSystem _domains = default!;

    private EntityUid _owner;
    private EntityUid _other;
    private EntityUid _agent;
    private Entity<EngagementComponent> _situation;
    private readonly List<EntityUid> _targets = new();

    private async Task Setup()
    {
        var map = await Pair.CreateTestMap();

        await Pair.Server.WaitPost(() =>
        {
            var coords = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
            _owner = _entMan.SpawnEntity("TestDomainOwner", coords);
            _other = _entMan.SpawnEntity("TestDomainOwner", coords);
            _agent = _entMan.SpawnEntity("TestDomainAgent", coords);

            for (var i = 0; i < 4; i++)
            {
                _targets.Add(_entMan.SpawnEntity(null, coords));
            }

            Assert.That(_engagement.TryStartEngagement("TestDomain", _agent, out var situation), Is.True);
            _situation = situation!.Value;
            _ownership.AddOwnership(_situation, owner: _owner);
            Assert.That(_engagement.TryJoinEngagement(_situation.AsNullable(), "TestDomainMember", _agent), Is.True);

            // The query is made once, the first time it is asked for.
            Assert.That(Results("TestDomainOwnerTargets"), Is.Empty);
        });
    }

    private void SetKey(EntityUid entity, IEnumerable<EntityUid> value)
        => _goap.SetValue(_entMan.GetComponent<GoapComponent>(entity).State, "Targets", value.ToHashSet());

    private IReadOnlyList<EntityUid> Results(string query)
        => _searcher.GetResults(new Entity<GoapComponent>(_agent, _entMan.GetComponent<GoapComponent>(_agent)), query);

    private async Task Settle() => await Pair.RunTicksSync(3);

    [Test]
    public async Task TestQueryFollowsTheKeyOfTheOwner()
    {
        await Setup();

        await Pair.Server.WaitPost(() => SetKey(_owner, _targets.Take(2)));
        await Settle();
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Results("TestDomainOwnerTargets"), Is.EquivalentTo(_targets.Take(2))));

        // The owner has other entities now: some leave the result, and some get into it.
        await Pair.Server.WaitPost(() => SetKey(_owner, _targets.Skip(1).Take(2)));
        await Settle();
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Results("TestDomainOwnerTargets"), Is.EquivalentTo(_targets.Skip(1).Take(2))));

        // The key of another entity that does not own the situation is of no concern.
        await Pair.Server.WaitPost(() => SetKey(_other, _targets));
        await Settle();
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Results("TestDomainOwnerTargets"), Is.EquivalentTo(_targets.Skip(1).Take(2))));
    }

    [Test]
    public async Task TestQueryFollowsTheAgentJoiningAndLeavingTheSituation()
    {
        await Setup();
        await Pair.Server.WaitPost(() => SetKey(_owner, _targets.Take(2)));
        await Settle();

        await Pair.Server.WaitPost(() =>
            Assert.That(_engagement.LeaveEngagement(_situation.AsNullable(), _agent, EngagementEndReason.Finished), Is.True));
        await Settle();
        await Pair.Server.WaitAssertion(() => Assert.That(Results("TestDomainOwnerTargets"), Is.Empty, "it is no longer in the situation"));

        await Pair.Server.WaitPost(() =>
            Assert.That(_engagement.TryJoinEngagement(_situation.AsNullable(), "TestDomainMember", _agent), Is.True));
        await Settle();
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Results("TestDomainOwnerTargets"), Is.EquivalentTo(_targets.Take(2)), "and it is in it again"));
    }

    [Test]
    public async Task TestQueryFollowsWhoOwnsTheSituation()
    {
        await Setup();
        await Pair.Server.WaitPost(() =>
        {
            SetKey(_owner, _targets.Take(1));
            SetKey(_other, _targets.Skip(2));
        });
        await Settle();
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Results("TestDomainOwnerTargets"), Is.EquivalentTo(_targets.Take(1))));

        // The situation is given to another owner.
        await Pair.Server.WaitPost(() =>
        {
            _ownership.RemoveOwnership(_situation.Owner, owner: _owner);
            _ownership.AddOwnership(_situation.Owner, owner: _other);
        });
        await Settle();
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Results("TestDomainOwnerTargets"), Is.EquivalentTo(_targets.Skip(2))));
    }

    [Test]
    public async Task TestQueryFollowsTheStateOfTheSituation()
    {
        await Setup();
        await Pair.Server.WaitPost(() => Assert.That(Results("TestDomainStateTargets"), Is.Empty));

        await Pair.Server.WaitPost(() => SetKey(_situation, _targets.Take(3)));
        await Settle();
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Results("TestDomainStateTargets"), Is.EquivalentTo(_targets.Take(3))));

        await Pair.Server.WaitPost(() => SetKey(_situation, _targets.Skip(3)));
        await Settle();
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Results("TestDomainStateTargets"), Is.EquivalentTo(_targets.Skip(3))));
    }

    [Test]
    public async Task TestChecksAreMadeOnceForManyChanges()
    {
        await Setup();
        var before = _domains.Notified;

        await Pair.Server.WaitPost(() =>
        {
            // Many changes of the same key before the tick ends, and the key is what it is at the end.
            SetKey(_owner, _targets.Take(1));
            SetKey(_owner, _targets.Take(2));
            SetKey(_owner, _targets.Take(3));
        });
        await Settle();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_domains.Notified - before, Is.EqualTo(1));
            Assert.That(Results("TestDomainOwnerTargets"), Is.EquivalentTo(_targets.Take(3)));
        });

        // The same entities again: nothing has changed, nobody is told.
        await Pair.Server.WaitPost(() => SetKey(_owner, _targets.Take(3)));
        await Settle();
        await Pair.Server.WaitAssertion(() => Assert.That(_domains.Notified - before, Is.EqualTo(1)));
    }
}
