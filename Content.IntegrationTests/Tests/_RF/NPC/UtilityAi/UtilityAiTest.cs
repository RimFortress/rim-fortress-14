#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using static NUnit.Framework.Assert;

namespace Content.IntegrationTests.Tests._RF.NPC.UtilityAi;

[TestOf(typeof(UtilityAiGoalPrototype))]
public sealed class UtilityAiTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: goapCompound
  id: TestUaiInheritRootCompound
  tasks:
  - preconditions: []
    action: !type:MoveTo
    effects:
      Noop: true

- type: entity
  id: TestUaiInheritAgent
  name: uai inherit test agent
  components:
  - type: Goap
    rootTask: TestUaiInheritRootCompound
  - type: UtilityAi

- type: entity
  id: TestUaiInheritDeconstructAgent
  name: uai deconstruct test agent
  components:
  - type: Goap
    rootTask: TestUaiInheritRootCompound
  - type: UtilityAi
    goals:
    - TestDeconstruct

- type: utilityAiGoal
  id: TestBaseUtilityAiGoal
  conditions:
  - CombatMode == false
  incumbentBonus:
  - add: [float: 0.05]
  - mul: [float: 1.10]

- type: utilityAiGoal
  id: TestBaseExecutableGoal
  scoreCurves:
  - float: 1

- type: utilityAiGoal
  id: TestAttack
  parent: TestBaseUtilityAiGoal
  color: ""#FF0000""
  conditions:
  - CombatMode == true
  - !type:Or
    conditions:
    - AttackTarget == null
    - !type:MobState
      targetKey: AttackTarget
      targetState: Dead
      invert: true
  scoreCurves:
  - float: 0.90
  capture:
  - Query/AttackTarget
  goalState:
    TargetAttacked: true

- type: utilityAiGoal
  id: TestConstruction
  parent: TestBaseUtilityAiGoal
  color: ""#FFFF00""
  scoreCurves:
  - preset: BaseUAIScore
    variables:
      skill:
      - !type:SkillLevel
        skill: Construction
      targetsCount:
      - !type:SearchQueryCount
        query: Construction
      performersCount:
      - !type:GoalPerformers
        goal: Construction
      importance: [float: 0.55]
  goalState:
    TargetConstructed: true

- type: utilityAiGoal
  id: TestDeconstruct
  color: ""#945A1C""
  scoreCurves:
  - float: 1
  goalState:
    TargetDeconstruct: true
";

    private static readonly ProtoId<UtilityAiGoalPrototype> Attack = "TestAttack";
    private static readonly ProtoId<UtilityAiGoalPrototype> Construction = "TestConstruction";
    private static readonly ProtoId<UtilityAiGoalPrototype> Deconstruct = "TestDeconstruct";

    [SidedDependency(Side.Server)] private readonly SharedUtilityAiSystem _uai = null!;

    /// <summary>
    /// A child prototype that redeclares <c>conditions:</c> must fully replace the parent's
    /// list, not append to it - <c>Attack</c> declares 2 conditions of its own
    /// ("CombatMode == true" plus the Or-clause), which directly contradicts the parent's
    /// single "CombatMode == false" condition, so a merge would be nonsensical content-wise.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestChildOverridesConditionsListInsteadOfMerging()
    {
        var attack = SProtoMan.Index(Attack);

        That(attack.Conditions, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Each goal's <c>goalState:</c> must parse into a <c>GoapState</c> with exactly the
    /// declared key set to <c>true</c> - one key per goal here, but three different goals, so
    /// this also guards against state leaking or being shared between prototype instances.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestGoalStateParsesDistinctPerPrototype()
    {
        var attack = SProtoMan.Index(Attack);
        var construction = SProtoMan.Index(Construction);
        var deconstruct = SProtoMan.Index(Deconstruct);

        using (EnterMultipleScope())
        {
            That(attack.GoalState, Has.Count.EqualTo(1));
            That(attack.GoalState.GetValue(new StateKey<bool>("TargetAttacked")), Is.True);

            That(construction.GoalState, Has.Count.EqualTo(1));
            That(construction.GoalState.GetValue(new StateKey<bool>("TargetConstructed")), Is.True);

            That(deconstruct.GoalState, Has.Count.EqualTo(1));
            That(deconstruct.GoalState.GetValue(new StateKey<bool>("TargetDeconstruct")), Is.True);
        }
    }

    /// <summary>
    /// <c>Attack</c>'s single <c>capture:</c> entry must parse to exactly the one declared
    /// key.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestCaptureKeyParsesForAttack()
    {
        var attack = SProtoMan.Index(Attack);

        using (EnterMultipleScope())
        {
            That(attack.Capture, Has.Count.EqualTo(1));
            That(attack.Capture, Does.Contain(new StateKey<EntityUid>("Query/AttackTarget")));
        }
    }

    /// <summary>
    /// Every <c>scoreCurves:</c> declared here is a single top-level list entry, regardless of
    /// how much internal complexity that one entry carries (a plain constant, or - for
    /// <c>Construction</c> - a preset curve with nested per-variable sub-curve lists). This is
    /// purely a count of top-level list items, so it doesn't depend on knowing what any of the
    /// curve types actually compute.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestScoreCurvesTopLevelCountIsOnePerGoal()
    {
        var attack = SProtoMan.Index(Attack);
        var construction = SProtoMan.Index(Construction);
        var deconstruct = SProtoMan.Index(Deconstruct);

        using (EnterMultipleScope())
        {
            That(attack.ScoreCurves, Has.Count.EqualTo(1));
            That(construction.ScoreCurves, Has.Count.EqualTo(1));
            That(deconstruct.ScoreCurves, Has.Count.EqualTo(1));
        }
    }

    /// <summary>
    /// <c>Attack</c>'s conditions must be reported as unmet on a fresh, non-combat entity -
    /// via the very first condition ("CombatMode == true") already failing. Relies on the
    /// short-circuit-AND assumption documented on the class: if conditions were ANDed lazily,
    /// this never touches the nested Or/MobState clause at all.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestAttackConditionsNotMetOutsideCombatMode()
    {
        var ent = SSpawn("TestUaiInheritAgent");
        SEntMan.GetComponent<GoapComponent>(ent).State.SetValue(GoapState.Owner, ent);

        That(_uai.ConditionsMet(ent, Attack), Is.False);
    }

    /// <summary>
    /// <c>BaseUtilityAiGoal</c>'s own single condition ("CombatMode == false") must be
    /// reported as met on a fresh, non-combat entity.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestBaseGoalConditionsMetOutsideCombatMode()
    {
        var ent = SSpawn("TestUaiInheritAgent");
        SEntMan.GetComponent<GoapComponent>(ent).State.SetValue(GoapState.Owner, ent);

        That(_uai.ConditionsMet(ent, "BaseUtilityAiGoal"), Is.True);
    }

    /// <summary>
    /// End-to-end scoring case: with only <c>Deconstruct</c> in the goal set (no conditions,
    /// <c>scoreCurves: [float: 1]</c>, no prior goal/penalty history), <c>GetScore</c> should
    /// compute exactly 1.0 with no incumbent bonus and no penalty applied, hitting
    /// <c>TryGetGoal</c>'s "score == 1f" immediate-return path. Relies on the "float: X is a
    /// constant curve" assumption documented on the class.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestDeconstructScoresMaximumAndWinsTryGetGoal()
    {
        var ent = SSpawn("TestUaiInheritDeconstructAgent");
        SEntMan.GetComponent<GoapComponent>(ent).State.SetValue(GoapState.Owner, ent);

        using (EnterMultipleScope())
        {
            That(_uai.TryGetGoal(ent, out var protoId), Is.True);
            That(protoId, Is.EqualTo(Deconstruct));
        }
    }
}
