#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.Phases;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.Phases;

/// <summary>
/// What an event is made of that goes in stages: the metrics that are measured in order before anything else, and the transitions
/// between the phases that wait for the conditions, for the time of the phase, and for the conditions to hold for a while.
/// </summary>
public sealed class ScenarioTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestScenario
  components:
  - type: MathVariables
    variables:
      x: [float: 3]
    metrics:
      double:
      - var: x
      - mul: [float: 2]
      quad:
      - var: double
      - mul: [float: 2]
  - type: Phases
    phases:
      A:
        transitions:
        - to: B
          when:
          - !type:MathCurveCondition
            curves:
            - var: double
            - !type:Threshold
              moreThan: 10
      B:
        transitions:
        - to: C
          after:
          - float: 3
      C:
        transitions:
        - to: D
          delay: 2
          when:
          - !type:MathCurveCondition
            curves:
            - var: x
            - !type:Threshold
              moreThan: 100
      D: {}
  - type: KeyedEntityEffectOnTrigger
    effects:
      tick:
      - !type:SetMathVariable
        key: seen
        value:
        - var: double
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly MathVariablesSystem _variables = default!;
    [SidedDependency(Side.Server)] private readonly PhasesSystem _phases = default!;
    [SidedDependency(Side.Server)] private readonly TriggerSystem _trigger = default!;

    private EntityUid _scenario;

    private async Task Setup()
    {
        var map = await Pair.CreateTestMap();

        await Pair.Server.WaitPost(() =>
        {
            _scenario = _entMan.SpawnEntity("TestScenario", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            Assert.That(_phases.SetPhase(_scenario, "A"), Is.True);
        });
    }

    private void Tick() => _trigger.Trigger(_scenario, null, "tick", predicted: false);

    private float Var(string key) => _variables.Get(_scenario, key);

    private async Task Run(float seconds) => await Pair.RunSeconds(seconds);

    [Test]
    public async Task TestMetricsAreMeasuredInOrderBeforeTheEffects()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Var("double"), Is.Zero, "a metric is 0 until it is measured");

            Tick();

            Assert.That(Var("double"), Is.EqualTo(6f));
            Assert.That(Var("quad"), Is.EqualTo(12f), "the one after sees the one before, as it is now");
            Assert.That(Var("seen"), Is.EqualTo(6f), "the effects of the entity see the fresh numbers");

            _variables.Set(_scenario, "x", 4f);
            Tick();

            Assert.That(Var("seen"), Is.EqualTo(8f));
        });
    }

    [Test]
    public async Task TestTransitionWaitsForItsConditions()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Tick();
            Assert.That(_phases.GetPhase(_scenario), Is.EqualTo("A"));

            _variables.Set(_scenario, "x", 6f);
            Tick();
            Assert.That(_phases.GetPhase(_scenario), Is.EqualTo("B"), "double is 12 now, more than 10");
        });
    }

    [Test]
    public async Task TestTransitionWaitsForTheTimeOfThePhase()
    {
        await Setup();
        await Pair.Server.WaitAssertion(() => _phases.SetPhase(_scenario, "B"));

        await Run(1f);
        await Pair.Server.WaitPost(Tick);
        await Pair.Server.WaitAssertion(() => Assert.That(_phases.GetPhase(_scenario), Is.EqualTo("B"), "3 seconds have not passed"));

        await Run(3f);
        await Pair.Server.WaitPost(Tick);
        await Pair.Server.WaitAssertion(() => Assert.That(_phases.GetPhase(_scenario), Is.EqualTo("C")));
    }

    [Test]
    public async Task TestTransitionWaitsForTheConditionsToHoldForAWhile()
    {
        await Setup();
        await Pair.Server.WaitAssertion(() =>
        {
            _phases.SetPhase(_scenario, "C");
            _variables.Set(_scenario, "x", 200f);
            Tick();
            Assert.That(_phases.GetPhase(_scenario), Is.EqualTo("C"), "it has just begun to hold");
        });

        await Run(1f);

        // A break: it begins again.
        await Pair.Server.WaitAssertion(() =>
        {
            _variables.Set(_scenario, "x", 3f);
            Tick();
            _variables.Set(_scenario, "x", 200f);
            Tick();
        });

        await Run(1.5f);
        await Pair.Server.WaitPost(Tick);
        await Pair.Server.WaitAssertion(() => Assert.That(_phases.GetPhase(_scenario), Is.EqualTo("C"), "1.5 seconds since the break"));

        await Run(1f);
        await Pair.Server.WaitPost(Tick);
        await Pair.Server.WaitAssertion(() => Assert.That(_phases.GetPhase(_scenario), Is.EqualTo("D")));
    }
}
