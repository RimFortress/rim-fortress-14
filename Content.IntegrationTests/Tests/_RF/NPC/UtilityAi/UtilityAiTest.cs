#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.NPC.UtilityAi;

[TestOf(typeof(SharedUtilityAiSystem))]
public sealed class UtilityTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: goapCompound
  id: TestUaiScoringRootCompound
  tasks:
  - preconditions: []
    action: !type:MoveTo
    effects:
      Noop: true

- type: utilityAiGoal
  id: TestUaiScoreLow
  scoreCurves:
  - float: 0.30

- type: utilityAiGoal
  id: TestUaiScoreHigh
  scoreCurves:
  - float: 0.90

- type: utilityAiGoal
  id: TestUaiScoreNearMax
  scoreCurves:
  - float: 0.99

- type: utilityAiGoal
  id: TestUaiScoreIncumbentBoost
  scoreCurves:
  - float: 0.90
  incumbentBonus:
  - add: [float: 0.05]
  - mul: [float: 1.10]

- type: utilityAiGoal
  id: TestUaiParentGoal
  failPenalty: 0.5
  scoreCurves:
  - float: 0.42

- type: utilityAiGoal
  id: TestUaiChildInherits
  parent: TestUaiParentGoal

- type: utilityAiGoal
  id: TestUaiChildOverrides
  parent: TestUaiParentGoal
  scoreCurves:
  - float: 0.77

- type: entity
  id: TestUaiScoringAgentPlain
  name: uai scoring test agent (plain)
  components:
  - type: Goap
    rootTask: TestUaiScoringRootCompound
  - type: UtilityAi

- type: entity
  id: TestUaiScoringAgentTwoGoals
  name: uai scoring test agent (two goals, different scores)
  components:
  - type: Goap
    rootTask: TestUaiScoringRootCompound
  - type: UtilityAi
    goals:
    - TestUaiScoreLow
    - TestUaiScoreHigh

- type: entity
  id: TestUaiScoringAgentIncumbentVsChallenger
  name: uai scoring test agent (incumbent vs challenger)
  components:
  - type: Goap
    rootTask: TestUaiScoringRootCompound
  - type: UtilityAi
    goals:
    - TestUaiScoreIncumbentBoost
    - TestUaiScoreNearMax
";

    [SidedDependency(Side.Server)] private readonly SharedUtilityAiSystem _uai = default!;
    [SidedDependency(Side.Server)] private readonly UaiScoreModifierTestSystem _scoreModifier = default!;

    /// <summary>
    /// A bare <c>scoreCurves: [float: X]</c> goal, when it is NOT the agent's current goal,
    /// must score exactly X - the incumbent-bonus branch in <c>GetScore</c> must not fire for
    /// goals the agent isn't already running.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestGetScoreReturnsRawCurveValueWhenNotIncumbent()
    {
        var ent = SSpawn("TestUaiScoringAgentPlain");

        Assert.That(_uai.GetScore(ent, "TestUaiScoreHigh"), Is.EqualTo(0.90f).Within(0.0001f));
    }

    /// <summary>
    /// Once a goal is set as the agent's current goal, <c>GetScore</c> must run the score
    /// through <c>IncumbentBonus</c> instead of returning the raw curve value.
    /// <c>base.yml</c>'s <c>incumbentBonus: [add: 0.05, mul: 1.10]</c> pattern is a sequential
    /// chain (each curve consumes the previous result), so for a base score of 0.90 the
    /// expected value is <c>(0.90 + 0.05) * 1.10 == 1.045</c>, which then gets clamped down to
    /// 1 by <c>GetScore</c>'s final <c>Math.Clamp</c>. Getting either the chain order or the
    /// clamp wrong would silently make an incumbent goal look worse (or artificially perfect)
    /// without ever throwing.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestGetScoreAppliesIncumbentBonusChainAndClamps()
    {
        var ent = SSpawn("TestUaiScoringAgentPlain");
        _uai.SetGoal(ent, "TestUaiScoreIncumbentBoost");

        Assert.That(_uai.GetScore(ent, "TestUaiScoreIncumbentBoost"), Is.EqualTo(1f));
    }

    /// <summary>
    /// <c>TryGetGoal</c> must return the goal with the strictly higher score regardless of
    /// which order the two candidates happen to be visited in (<c>Goals</c> is a
    /// <c>HashSet</c>, so iteration order isn't something the caller controls). This is the
    /// core max-selection contract (<c>max.Value.Score &lt; score</c>), exercised with two
    /// curve values whose relative ordering is unambiguous.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestTryGetGoalPicksHigherScoringCandidate()
    {
        var ent = SSpawn("TestUaiScoringAgentTwoGoals");

        Assert.That(_uai.TryGetGoal(ent, out var protoId), Is.True);

        ProtoId<UtilityAiGoalPrototype> expected = "TestUaiScoreHigh";
        Assert.That(protoId, Is.EqualTo(expected));
    }

    /// <summary>
    /// An incumbent goal whose boosted score (1.0, from the previous test's math) beats a
    /// non-incumbent challenger's higher *raw* curve value (0.99) must still win. This proves
    /// the incumbent bonus is actually wired into <c>TryGetGoal</c>'s comparison loop, not
    /// just observable in isolation via <c>GetScore</c>.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestTryGetGoalPrefersBoostedIncumbentOverHigherRawChallenger()
    {
        var ent = SSpawn("TestUaiScoringAgentIncumbentVsChallenger");
        _uai.SetGoal(ent, "TestUaiScoreIncumbentBoost");

        Assert.That(_uai.TryGetGoal(ent, out var protoId), Is.True);

        ProtoId<UtilityAiGoalPrototype> expected = "TestUaiScoreIncumbentBoost";
        Assert.That(protoId, Is.EqualTo(expected));
    }

    /// <summary>
    /// <see cref="UtilityAiGoalScoreModify"/> subscribers can push the score arbitrarily far
    /// out of range, and <c>GetScore</c> must still clamp the final result to [0, 1] in both
    /// directions. This is the only test in this file that depends on a locally-defined
    /// <see cref="UaiScoreModifierTestSystem"/> subscribing to the event - Robust's
    /// entity-system manager auto-discovers and initializes any public non-abstract
    /// <c>EntitySystem</c> in a loaded assembly, which includes the test assembly itself.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestGetScoreClampsAfterEventModification()
    {
        var ent = SSpawn("TestUaiScoringAgentPlain");

        try
        {
            _scoreModifier.Delta = 10f;
            Assert.That(_uai.GetScore(ent, "TestUaiScoreHigh"), Is.EqualTo(1f));

            _scoreModifier.Delta = -10f;
            Assert.That(_uai.GetScore(ent, "TestUaiScoreHigh"), Is.Zero);
        }
        finally
        {
            // Reset so this doesn't leak into another test sharing the pooled server.
            _scoreModifier.Delta = 0f;
        }
    }

    /// <summary>
    /// A child goal prototype that doesn't declare its own <c>scoreCurves</c>/<c>failPenalty</c>
    /// must inherit the parent's values rather than falling back to the type's own C# defaults
    /// (<c>FailPenalty = 0.2f</c>, empty <c>ScoreCurves</c>). Asserting against the parent's
    /// deliberately-non-default values (0.5 vs the 0.2 default, 0.42 vs an empty curve list)
    /// is what makes this a real inheritance check rather than a coincidental default match.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestChildGoalInheritsUnsetFieldsFromParent()
    {
        var ent = SSpawn("TestUaiScoringAgentPlain");
        var protoId = new ProtoId<UtilityAiGoalPrototype>("TestUaiChildInherits");
        var proto = SProtoMan.Index(protoId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(proto.FailPenalty, Is.EqualTo(0.5f));
            Assert.That(_uai.GetScore(ent, "TestUaiChildInherits"), Is.EqualTo(0.42f).Within(0.0001f));
        }
    }

    /// <summary>
    /// A child goal prototype that DOES declare its own <c>scoreCurves</c> must use its own
    /// value, not the parent's - inheritance must not leave a stale/merged value behind for a
    /// field the child explicitly overrode.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestChildGoalOverridesParentField()
    {
        var ent = SSpawn("TestUaiScoringAgentPlain");

        Assert.That(_uai.GetScore(ent, "TestUaiChildOverrides"), Is.EqualTo(0.77f).Within(0.0001f));
    }
}

/// <summary>
/// Test-only system that lets a test control exactly how <see cref="UtilityAiGoalScoreModify"/>
/// perturbs a score, without needing to know anything about real gameplay systems that would
/// normally subscribe to it.
/// </summary>
public sealed class UaiScoreModifierTestSystem : EntitySystem
{
    public float Delta;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UtilityAiComponent, UtilityAiGoalScoreModify>(OnScoreModify);
    }

    private void OnScoreModify(Entity<UtilityAiComponent> ent, ref UtilityAiGoalScoreModify args)
    {
        args.Score += Delta;
    }
}
