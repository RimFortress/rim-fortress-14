#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.GOAP.Systems;
using static NUnit.Framework.Assert;

namespace Content.IntegrationTests.Tests._RF.NPC.GOAP;

/// <summary>
/// Structural regression tests for the prototype -&gt; <see cref="Content.Shared._RF.NPC.GOAP.ExecutableGoapTask"/>
/// -&gt; <see cref="Content.Shared._RF.NPC.GOAP.GoapStaticGraph"/> pipeline, using the actual
/// <c>MoveTo</c>/<c>Combat</c>/<c>Dig</c> compounds from the repo (re-registered under
/// test-only IDs to avoid clashing with the real prototypes of the same name).
/// </summary>
/// <remarks>
/// These deliberately avoid asserting anything about *edges* between nodes (that depends on
/// the semantics of the concrete condition/comparison classes, which aren't in this context),
/// and instead assert only on things that are fully determined by YAML structure: how many
/// tasks a compound flattens into, and how many actions/preconditions/effects each task keeps.
/// A regression here (e.g. the implicit-data-definition discriminator between the singular
/// `action:` field and the plural `actions:` field misfiring, or a shared-list/off-by-one bug
/// while assigning sequential node Ids) would not throw - it would just quietly merge, drop,
/// or misalign tasks, which is exactly the kind of thing that's hard to notice by playing the
/// game but easy to notice with a count assertion.
/// </remarks>
[TestOf(typeof(GoapSystem))]
public sealed class GoapStaticGraphPrototypeTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: goapCompound
  id: TestMoveToGraph
  tasks:
  - preconditions:
    - InContainer == false
    - Buckled == false
    - Pulled == false
    action: !type:MoveTo
    effects:
      NearTargetPlace: true

- type: goapCompound
  id: TestDigGraph
  tasks:
  - preconditions:
    - InContainer == false
    - Buckled == false
    - Pulled == false
    - Query/Pickaxe == true
    actions:
    - !type:EntityCoords
      targetKey: Query/Pickaxe
    - !type:MoveTo
      rangeKey: InteractRange
    - !type:Pickup
      targetKey: Query/Pickaxe
    - !type:Wield
      targetState: true
    effects:
      PickaxePickedUp: true

  - preconditions:
    - Query/DigTarget != null
    - PickaxePickedUp == true
    actions:
    - !type:Melee
      targetKey: Query/DigTarget
    - !type:MarkOwnerInRange
      range: 3
      whitelist:
        components:
        - Log
    effects:
      TargetDug: true

- type: goapCompound
  id: TestCombatGraph
  tasks:
  # Search
  - preconditions:
    - Query/InventoryGuns || Query/NearbyGuns != null
    actions:
    - !type:CaptureQuery
      capture: Query/InventoryGuns || Query/NearbyGuns
      targetKey: WeaponTarget
    effects:
      GunFound: true
      WeaponTarget: true

  - preconditions:
    - Query/InventoryMelees || Query/NearbyMelees != null
    actions:
    - !type:CaptureQuery
      capture: Query/InventoryMelees || Query/NearbyMelees
      targetKey: WeaponTarget
      costMultiplier: 2
    effects:
      MeleeFound: true
      WeaponTarget: true

  # Pickup
  - preconditions:
    - InContainer == false
    - Buckled == false
    - Pulled == false
    - WeaponTarget != null
    actions:
    - !type:EntityCoords
      targetKey: WeaponTarget
    - !type:MoveTo
      rangeKey: InteractRange
    - !type:Pickup
      targetKey: WeaponTarget
    - !type:Wield
      targetState: true
    effects:
      WeaponPickedUp: true

  # Gun
  - preconditions:
    - GunFound == true
    - WeaponPickedUp == true
    - Query/AttackTarget != null
    actions:
    - !type:EntityCoords
      targetKey: Query/AttackTarget
    - !type:MoveTo
      rangeKey: MeleeRange
      stopOnLineOfSight: true
    - !type:Juke
      jukeType: Away
    - !type:Gun
      targetKey: Query/AttackTarget
      removeCombatMode: false
    effects:
      TargetAttacked: true

  # Melee
  - preconditions:
    - MeleeFound == true
    - WeaponPickedUp == true
    - Query/AttackTarget != null
    actions:
    - !type:Wield
      targetState: true
    - !type:EntityCoords
      targetKey: Query/AttackTarget
    - !type:MoveTo
      rangeKey: MeleeRange
    - !type:Juke
    - !type:Melee
      targetKey: Query/AttackTarget
      removeCombatMode: false
      costMultiplier: 4
    effects:
      TargetAttacked: true

  # Fistfight
  - preconditions:
    - WeaponTarget == null
    - Query/AttackTarget != null
    actions:
    - !type:EntityCoords
      targetKey: Query/AttackTarget
    - !type:MoveTo
      rangeKey: MeleeRange
    - !type:Juke
    - !type:Melee
      targetKey: Query/AttackTarget
      removeCombatMode: false
      costMultiplier: 100 # A fistfight is the least desirable option
    effects:
      TargetAttacked: true
";

    [SidedDependency(Side.Server)] private readonly GoapSystem _goap = null!;

    /// <summary>
    /// A task declared via the singular <c>action:</c> field (<c>GoapActionTask</c>) must
    /// flatten into an <c>ExecutableGoapTask</c> whose Actions list has exactly one element -
    /// as opposed to the plural <c>actions:</c> list form (<c>GoapCompoundTask</c>) exercised
    /// by the other two tests below. This is the simplest possible case for the
    /// implicit-data-definition discriminator that distinguishes the two task kinds.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestSingleActionTaskFlattensToOneAction()
    {
        var graph = _goap.GetStaticGraph("TestMoveToGraph");

        using (EnterMultipleScope())
        {
            That(graph.Nodes, Has.Count.EqualTo(1));
            That(graph.Nodes[0].Id, Is.EqualTo(0));
            That(graph.Nodes[0].Actions, Has.Count.EqualTo(1));
            That(graph.Nodes[0].Preconditions, Has.Count.EqualTo(3));
            That(graph.Nodes[0].Effects.Count, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// A two-task, plural-<c>actions:</c>-form compound must flatten each task
    /// independently, in declaration order, each keeping its own action/precondition/effect
    /// counts. A mistake here (e.g. a shared list instance between tasks, or an off-by-one
    /// while assigning sequential node Ids) would not throw - it would just quietly merge or
    /// misalign the two tasks.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestMultiTaskCompoundFlattensEachTaskIndependently()
    {
        var graph = _goap.GetStaticGraph("TestDigGraph");

        using (EnterMultipleScope())
        {
            That(graph.Nodes, Has.Count.EqualTo(2));

            // Task 0: pickaxe pickup.
            That(graph.Nodes[0].Id, Is.Zero);
            That(graph.Nodes[0].Actions, Has.Count.EqualTo(4));
            That(graph.Nodes[0].Preconditions, Has.Count.EqualTo(4));
            That(graph.Nodes[0].Effects.Count, Is.EqualTo(1));

            // Task 1: dig.
            That(graph.Nodes[1].Id, Is.EqualTo(1));
            That(graph.Nodes[1].Actions, Has.Count.EqualTo(2));
            That(graph.Nodes[1].Preconditions, Has.Count.EqualTo(2));
            That(graph.Nodes[1].Effects.Count, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// A larger, realistic six-task compound with mixed action counts, YAML comment lines,
    /// and "or"-key preconditions on domain queries. Broader smoke test that the whole
    /// prototype -&gt; executable-task pipeline holds together under real content rather than
    /// a minimal synthetic example: every task's own action/precondition/effect count must
    /// survive unchanged and in declaration order, and building the static graph for it must
    /// not throw.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestCombatCompoundStructuralIntegrity()
    {
        var graph = _goap.GetStaticGraph("TestCombatGraph");

        var expectedActionCounts = new[] { 1, 1, 4, 4, 5, 4 };
        var expectedPreconditionCounts = new[] { 1, 1, 4, 3, 3, 2 };
        var expectedEffectCounts = new[] { 2, 2, 1, 1, 1, 1 };

        That(graph.Nodes, Has.Count.EqualTo(6));

        using (EnterMultipleScope())
        {
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                That(node.Id, Is.EqualTo(i), $"node {i} id mismatch");
                That(node.Actions, Has.Count.EqualTo(expectedActionCounts[i]), $"node {i} action count");
                That(node.Preconditions, Has.Count.EqualTo(expectedPreconditionCounts[i]), $"node {i} precondition count");
                That(node.Effects.Count, Is.EqualTo(expectedEffectCounts[i]), $"node {i} effect count");
            }
        }
    }

    /// <summary>
    /// Every node produced for a given compound must carry that compound's own ProtoId in
    /// <c>ExecutableGoapTask.Compound</c> - this is what lets debug tooling (and nested-compound
    /// flattening via <c>GoapCompoundPrototypeTask</c>) trace a flattened node back to its
    /// source prototype. A copy/paste bug that hardcodes or forwards the wrong ProtoId
    /// wouldn't throw - it would just misattribute nodes in the debugger.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestNodesCarryOwningCompoundProtoId()
    {
        var graph = _goap.GetStaticGraph("TestDigGraph");

        using (EnterMultipleScope())
        {
            foreach (var node in graph.Nodes)
            {
                That(node.Compound.ToString(), Is.EqualTo("TestDigGraph"));
            }
        }
    }
}
