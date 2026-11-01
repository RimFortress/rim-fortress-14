#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.GameTicking;

/// <summary>
/// What a game rule is made of: variables, reactions to triggers, metrics, outcomes, the search queries of the rule and its
/// situations. They are the tools that are there already (triggers, entity effects and conditions, search queries,
/// <c>Engagement</c>), put together in YAML.
/// </summary>
public sealed class RuleTriggersTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: MobHuman
  id: TestRuleAgent
  components:
  - type: Goap
    rootTask: Empty

- type: engagementRole
  id: TestRuleMember
  force: true
  stayConditions:
  - !type:MobState
    targetState: Dead
    invert: true

- type: engagement
  id: TestRuleSquad
  dissolveInvalid: false
  roles:
  - TestRuleMember

# The fixed set of the entities that the rule is about, told apart by what they are like.
- type: searchQuery
  id: TestRuleTargets
  query: !type:KeyEntities
    key: Targets

- type: searchQuery
  id: TestRuleTargetsAlive
  query: !type:KeyEntities
    key: Targets
  filters:
  - !type:IsAlive

- type: searchQuery
  id: TestRuleTargetsDead
  notify: true
  query: !type:KeyEntities
    key: Targets
  filters:
  - !type:IsDead

- type: entity
  id: TestRuleTriggersRule
  components:
  - type: WorldRule
  - type: Goap
    rootTask: Empty
    enabled: false
  - type: MathVariables
  - type: TriggerOnSearchResult
  - type: TriggerOnEngagement
  # It is measured while the rule goes on.
  - type: ActiveGameRuleTriggerCondition
    keys: [ tick ]
  - type: RepeatingTrigger
    delay: 0.1
    keyOut: tick
  - type: KeyedEntityEffectOnTrigger
    effects:
      started:
      - !type:SetMathVariable
        key: startedAt
        value:
        - !type:CurTimeSeconds
      - !type:SetMathVariable
        key: kills
        value:
        - float: 0
      - !type:SetMathVariable
        key: losses
        value:
        - float: 0
      - !type:SetMathVariable
        key: targetsTotal
        value:
        - !type:SearchQueryCount
          query: TestRuleTargets
      tick:
      # What is measured: the effects are applied in order.
      - !type:SetMathVariable
        key: time
        value:
        - !type:CurTimeSeconds
        - minus:
          - var: startedAt
      - !type:SetMathVariable
        key: deadFraction
        value:
        - !type:SearchQueryCount
          query: TestRuleTargetsDead
        - div:
          - var: targetsTotal
      - !type:SetMathVariable
        key: members
        value:
        - !type:EngagementActors
          engagement: TestRuleSquad
          role: TestRuleMember
      - !type:SetMathVariable
        key: aliveMembers
        value:
        - !type:EngagementActors
          engagement: TestRuleSquad
          conditions:
          - !type:MobState
            targetState: Alive
      - !type:SetMathVariable
        key: killsAndLosses
        value:
        - var: kills
        - add:
          - var: losses
      # How the rule ends.
      - !type:EndWorldRule
        result: Won
        conditions:
        - !type:MathCurveCondition
          curves:
          - var: deadFraction
          - !type:Threshold
            moreThan: 0.5
      TestRuleTargetsDead.added:
      - !type:SetMathVariable
        key: kills
        value:
        - add:
          - float: 1
      TestRuleSquad.left.TestRuleMember:
      - !type:SetMathVariable
        key: losses
        value:
        - add:
          - float: 1
      ended.Won:
      - !type:SetMathVariable
        key: celebrated
        value:
        - float: 1
";

    private static readonly StateKey<HashSet<EntityUid>> Targets = "Targets";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly RimFortressRuleSystem _rules = default!;
    [SidedDependency(Side.Server)] private readonly MathVariablesSystem _variables = default!;
    [SidedDependency(Side.Server)] private readonly SharedGoapSystem _goap = default!;
    [SidedDependency(Side.Server)] private readonly SharedNpcSearcherSystem _searcher = default!;
    [SidedDependency(Side.Server)] private readonly EngagementSystem _engagement = default!;
    [SidedDependency(Side.Server)] private readonly OwnershipSystem _ownership = default!;
    [SidedDependency(Side.Server)] private readonly MobStateSystem _mobState = default!;
    [SidedDependency(Side.Server)] private readonly TriggerSystem _trigger = default!;

    private EntityUid _rule;
    private EntityCoordinates _coords;
    private readonly List<EntityUid> _targets = new();

    // The rule is made like those of the game are: in nullspace, and started when it is time.
    private async Task Setup(int targets = 4, bool start = true)
    {
        var map = await Pair.CreateTestMap();
        _coords = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
        _targets.Clear();

        await Pair.Server.WaitPost(() =>
        {
            _rule = _entMan.SpawnEntity("TestRuleTriggersRule", MapCoordinates.Nullspace);

            for (var i = 0; i < targets; i++)
            {
                _targets.Add(_entMan.SpawnEntity("MobHuman", _coords));
            }

            SetTargets(_targets);

            if (start)
                Start();
        });
    }

    private void SetTargets(IEnumerable<EntityUid> targets)
        => _goap.SetValue(_entMan.GetComponent<GoapComponent>(_rule).State, Targets, targets.ToHashSet());

    private void Start()
    {
        Assert.That(
            _rules.StartWorldRule(
                new Entity<WorldRuleComponent?>(_rule, _entMan.GetComponent<WorldRuleComponent>(_rule)),
                _targets[0],
                _coords,
                ignoreDelay: true),
            Is.True);
    }

    private async Task Run(float seconds) => await Pair.RunTicksSync((int) (seconds * 60) + 2);

    private float Variable(string key) => _variables.Get(_rule, key);

    private bool Has(string key)
        => _variables.GetVariables(new Entity<MathVariablesComponent?>(_rule, _entMan.GetComponent<MathVariablesComponent>(_rule)))
            .ContainsKey(key);

    private int Count(string query)
        => _searcher.GetResultsCount(new Entity<GoapComponent>(_rule, _entMan.GetComponent<GoapComponent>(_rule)), query);

    [Test]
    public async Task TestClassesOfTheEntities()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("targetsTotal"), Is.EqualTo(4f), "it was found when the rule started");
            Assert.That(Count("TestRuleTargets"), Is.EqualTo(4));
            Assert.That(Count("TestRuleTargetsAlive"), Is.EqualTo(4));
        });

        await Pair.Server.WaitPost(() => _mobState.ChangeMobState(_targets[0], MobState.Dead));
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Count("TestRuleTargetsDead"), Is.EqualTo(1));
            Assert.That(Count("TestRuleTargetsAlive"), Is.EqualTo(3));
        });

        await Pair.Server.WaitPost(() => _entMan.DeleteEntity(_targets[1]));
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Count("TestRuleTargets"), Is.EqualTo(3), "it is gone");
            Assert.That(Variable("targetsTotal"), Is.EqualTo(4f), "the copy is what it was");
        });
    }

    /// <summary>
    /// The results of the queries follow the value of the key: the entities that are not in it anymore leave, and the new ones get in.
    /// </summary>
    [Test]
    public async Task TestQueriesFollowTheKey()
    {
        await Setup();
        EntityUid another = default;

        await Pair.Server.WaitAssertion(() => Assert.That(Count("TestRuleTargets"), Is.EqualTo(4)));

        await Pair.Server.WaitPost(() =>
        {
            another = _entMan.SpawnEntity("MobHuman", _coords);
            _mobState.ChangeMobState(another, MobState.Dead);
            SetTargets(new[] { _targets[2], _targets[3], another });
        });

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Count("TestRuleTargets"), Is.EqualTo(3));
            Assert.That(Count("TestRuleTargetsAlive"), Is.EqualTo(2));
            Assert.That(Count("TestRuleTargetsDead"), Is.EqualTo(1), "the new one is told apart as the others are");
        });

        await Pair.Server.WaitPost(() => _goap.RemoveKey(_entMan.GetComponent<GoapComponent>(_rule).State, Targets));
        await Pair.Server.WaitAssertion(() => Assert.That(Count("TestRuleTargets"), Is.EqualTo(0)));
    }

    /// <summary>
    /// The rule reacts when it starts, it measures every tick (the time of it is the game time minus when it started), a
    /// trigger of the search query adds up, and an outcome ends the rule with a result, which is a trigger too.
    /// </summary>
    [Test]
    public async Task TestMetricsAndOutcome()
    {
        await Setup();

        await Pair.Server.WaitPost(() => _mobState.ChangeMobState(_targets[0], MobState.Dead));
        await Run(0.3f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("time"), Is.GreaterThan(0.2f), "it has been going on since it started");
            Assert.That(Variable("deadFraction"), Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(Variable("kills"), Is.EqualTo(1f), "the trigger has been applied once");
            Assert.That(_entMan.HasComponent<EndedGameRuleComponent>(_rule), Is.False, "not yet enough of them");
        });

        await Pair.Server.WaitPost(() =>
        {
            _mobState.ChangeMobState(_targets[1], MobState.Dead);
            _mobState.ChangeMobState(_targets[2], MobState.Dead);
        });
        await Run(0.3f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_entMan.HasComponent<EndedGameRuleComponent>(_rule), Is.True);
            Assert.That(_entMan.HasComponent<ActiveGameRuleComponent>(_rule), Is.False);
            Assert.That(_entMan.GetComponent<WorldRuleComponent>(_rule).Result, Is.EqualTo("Won"));
            Assert.That(Variable("kills"), Is.EqualTo(3f));
            Assert.That(Variable("celebrated"), Is.EqualTo(1f), "the end is a trigger too");
        });

        // It is not measured when it is over.
        var time = 0f;
        await Pair.Server.WaitPost(() => time = Variable("time"));
        await Run(0.5f);
        await Pair.Server.WaitAssertion(() => Assert.That(Variable("time"), Is.EqualTo(time)));
    }

    /// <summary>
    /// A rule that has not started is not measured, even if what it needs for an outcome is there already.
    /// </summary>
    [Test]
    public async Task TestRuleThatHasNotStartedIsNotMeasured()
    {
        await Setup(start: false);

        await Pair.Server.WaitPost(() =>
        {
            foreach (var target in _targets)
            {
                _mobState.ChangeMobState(target, MobState.Dead);
            }
        });
        await Run(0.5f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Has("deadFraction"), Is.False, "it has measured nothing");
            Assert.That(_entMan.HasComponent<EndedGameRuleComponent>(_rule), Is.False);
        });

        await Pair.Server.WaitPost(Start);
        await Run(0.3f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_entMan.HasComponent<EndedGameRuleComponent>(_rule), Is.True, "and it ends once it has started");
            Assert.That(_entMan.GetComponent<WorldRuleComponent>(_rule).Result, Is.EqualTo("Won"));
        });
    }

    /// <summary>
    /// The participants of the situations of the rule are counted by the kind, the role and what they are like, and leaving a
    /// role is a trigger. A situation belongs to the rule when the rule owns it.
    /// </summary>
    [Test]
    public async Task TestEngagementActors()
    {
        await Setup();
        var members = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            for (var i = 0; i < 3; i++)
            {
                members.Add(_entMan.SpawnEntity("TestRuleAgent", _coords));
            }

            Assert.That(_engagement.TryStartEngagement("TestRuleSquad", members[0], out var squad), Is.True);
            _ownership.AddOwnership(squad!.Value, owner: _rule);

            foreach (var member in members)
            {
                Assert.That(_engagement.TryJoinEngagement(squad.Value.AsNullable(), "TestRuleMember", member), Is.True);
            }
        });

        await Run(0.3f);
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("members"), Is.EqualTo(3f));
            Assert.That(Variable("aliveMembers"), Is.EqualTo(3f));
        });

        // One of them is down: it is still a member, but not an alive one. The state that is forced does not last, so the
        // rule is triggered at once.
        await Pair.Server.WaitPost(() =>
        {
            _mobState.ChangeMobState(members[0], MobState.Critical);
            _trigger.Trigger(_rule, null, "tick", predicted: false);
        });
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("members"), Is.EqualTo(3f));
            Assert.That(Variable("aliveMembers"), Is.EqualTo(2f));
        });

        // And one of them is dead: it is not a member anymore, and it is a trigger.
        await Pair.Server.WaitPost(() => _mobState.ChangeMobState(members[1], MobState.Dead));
        await Run(0.3f);
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("members"), Is.EqualTo(2f));
            Assert.That(Variable("losses"), Is.EqualTo(1f));
            Assert.That(Variable("killsAndLosses"), Is.EqualTo(1f), "the effects see what has been set before them");
        });
    }

    /// <summary>
    /// The situations that the rule does not own are not its own.
    /// </summary>
    [Test]
    public async Task TestOtherSituationsAreNotCounted()
    {
        await Setup();
        EntityUid member = default;

        await Pair.Server.WaitPost(() =>
        {
            member = _entMan.SpawnEntity("TestRuleAgent", _coords);
            Assert.That(_engagement.TryStartEngagement("TestRuleSquad", member, out var squad), Is.True);
            Assert.That(_engagement.TryJoinEngagement(squad!.Value.AsNullable(), "TestRuleMember", member), Is.True);
        });

        await Run(0.3f);
        await Pair.Server.WaitAssertion(() => Assert.That(Variable("members"), Is.EqualTo(0f)));
    }
}
