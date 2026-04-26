using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Conditions;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._RF.NPC.GOAP;

[TestFixture]
public sealed partial class GoapGraphGameTest : GameTest
{
    [SidedDependency(Side.Server)]
    private GoapSystem _goap = default!;

    [Test]
    public void Build_CreatesEdgeWhenProducerSatisfiesConsumer()
    {
        var producerEffects = new GoapState();
        producerEffects.SetValue<bool>("HasFood", true);

        var consumerPreconditions = new List<GoapCondition>
        {
            new EqualsBool { Key = "HasFood", Value = true }
        };

        var tasks = new List<ExecutableGoapTask>
        {
            new(
                Actions: new List<GoapAction> { new TestNoopAction() },
                Preconditions: new List<GoapCondition>(),
                Effects: producerEffects),

            new(
                Actions: new List<GoapAction> { new TestNoopAction() },
                Preconditions: consumerPreconditions,
                Effects: new GoapState())
        };

        var graph = _goap.Build(EntityUid.Invalid, tasks);
        var edge = graph.Edges[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(graph.Nodes, Has.Count.EqualTo(2));
            Assert.That(graph.Edges, Has.Count.EqualTo(1));
            Assert.That(graph.Issues, Is.Empty);
            Assert.That(graph.OutgoingByNodeId[0], Has.Count.EqualTo(1));
            Assert.That(graph.IncomingByNodeId[1], Has.Count.EqualTo(1));

            Assert.That(edge.FromNodeId, Is.Zero);
            Assert.That(edge.ToNodeId, Is.EqualTo(1));
            Assert.That(edge.ConditionIndex, Is.Zero);
            Assert.That(edge.ConditionType, Is.EqualTo(nameof(EqualsBool)));
        }

    }

    [Test]
    public void Build_ReportsMissingProducerAsIssue()
    {
        var consumerPreconditions = new List<GoapCondition>
        {
            new EqualsInt { Key = "Energy", Value = 5 }
        };

        var tasks = new List<ExecutableGoapTask>
        {
            new(
                Actions: new List<GoapAction> { new TestNoopAction() },
                Preconditions: new List<GoapCondition>(),
                Effects: new GoapState()),

            new(
                Actions: new List<GoapAction> { new TestNoopAction() },
                Preconditions: consumerPreconditions,
                Effects: new GoapState())
        };

        var graph = _goap.Build(EntityUid.Invalid, tasks);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(graph.Edges, Is.Empty);
            Assert.That(graph.Issues, Has.Count.EqualTo(1));
            Assert.That(graph.Issues[0].NodeId, Is.EqualTo(1));
            Assert.That(graph.Issues[0].ConditionType, Is.EqualTo(nameof(EqualsInt)));
        }
    }

    [Test]
    public void Build_CanIncludeSelfEdges_WhenRequested()
    {
        var effects = new GoapState();
        effects.SetValue<bool>("Ready", true);

        var task = new ExecutableGoapTask(
            Actions: new List<GoapAction> { new TestNoopAction() },
            Preconditions: new List<GoapCondition>
            {
                new EqualsBool { Key = "Ready", Value = true }
            },
            Effects: effects);

        var tasks = new List<ExecutableGoapTask> { task };

        var withoutSelfEdges = _goap.Build(EntityUid.Invalid, tasks, includeSelfEdges: false);
        var withSelfEdges = _goap.Build(EntityUid.Invalid, tasks, includeSelfEdges: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withoutSelfEdges.Edges, Is.Empty);
            Assert.That(withoutSelfEdges.Issues, Has.Count.EqualTo(1));
            Assert.That(withSelfEdges.Edges, Has.Count.EqualTo(1));
            Assert.That(withSelfEdges.Issues, Is.Empty);
            Assert.That(withSelfEdges.Edges[0].FromNodeId, Is.Zero);
            Assert.That(withSelfEdges.Edges[0].ToNodeId, Is.Zero);
        }
    }

    private sealed partial class TestNoopAction : GoapAction
    {
        public override float Cost(EntityUid target, GoapState state, IGoapActionPerformer performer) => 1f;

        public override GoapActionResult Update(EntityUid target, IGoapActionPerformer performer, out GoapDebugDump? dump)
        {
            dump = new GoapDebugDump("noop update", new GoapStateDebugDump(new()));
            return GoapActionResult.Finished;
        }

        public override bool Startup(EntityUid target, IGoapActionPerformer performer, out GoapDebugDump? dump)
        {
            dump = new GoapDebugDump("noop startup", new GoapStateDebugDump(new()));
            return true;
        }

        public override void Shutdown(EntityUid target, IGoapActionPerformer performer, out GoapDebugDump? dump)
        {
            dump = new GoapDebugDump("noop shutdown", new GoapStateDebugDump(new()));
        }
    }
}
