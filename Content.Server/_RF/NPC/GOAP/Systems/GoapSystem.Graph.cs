using System.Collections.Frozen;
using System.Diagnostics;
using System.Linq;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Systems;

public partial class GoapSystem
{
    private void BuildGraphs()
    {
        Log.Info("building GOAP static dependency graphs...");
        var sw = Stopwatch.StartNew();
        var staticGraphs = new Dictionary<ProtoId<GoapCompoundPrototype>, GoapStaticGraph>();

        var dummy = Spawn();
        var comp = Factory.GetComponent<GoapComponent>();
        comp.RootTask = DummyCompound;
        AddComp(dummy, comp);

        foreach (var compound in _proto.EnumeratePrototypes<GoapCompoundPrototype>())
        {
            staticGraphs[compound] = GetStaticGraph(compound, dummy);
        }

        StaticGraphs = staticGraphs.ToFrozenDictionary();

        // Updating debug info
        foreach (var (uid, sessions) in DebugSubscriptions)
        {
            foreach (var session in sessions)
            {
                SendDebug(session, uid);
            }
        }

        Log.Info($"{StaticGraphs.Count} graphs built in {sw.Elapsed}");
    }

    /// <summary>
    /// Builds the static dependency graph for a compound prototype.
    /// </summary>
    [PublicAPI]
    public GoapStaticGraph GetStaticGraph(
        ProtoId<GoapCompoundPrototype> protoId,
        EntityUid? dummy = null)
    {
        if (!_proto.Resolve(protoId, out var compound))
            return new();

        if (dummy == null)
        {
            dummy = Spawn();
            var comp = Factory.GetComponent<GoapComponent>();
            comp.RootTask = DummyCompound;
            AddComp(dummy.Value, comp);
        }

        var nodes = GetExecutableTasks(compound).OrderBy(x => x.Id).ToList();
        var edges = new HashSet<GoapStaticGraphEdge>();
        var notConnected = new HashSet<int>();

        for (var fromInd = 0; fromInd < nodes.Count; fromInd++)
        {
            var from = nodes[fromInd];

            // A node with at least one effect on an entity-default key has some effect whose
            // real runtime value can't be trusted from a dummy-entity probe. It still
            // participates in normal edge building below (for its other, static effects), but
            // it also needs to remain in the fallback pool so it's still considered as a
            // possible dynamic contributor everywhere - see the NotConnected remarks above.
            if (from.Effects.Any(x => GoapState.EntityDefaults.Contains(x.Key)))
                notConnected.Add(from.Id);

            for (var toInd = 0; toInd < nodes.Count; toInd++)
            {
                var to = nodes[toInd];

                if (fromInd == toInd)
                    continue;

                var hasConnection = false;

                foreach (var condition in to.Preconditions)
                {
                    // Conditions that read live ECS state can't be reliably predicted from a
                    // dummy entity at graph-build time - always leave these for the planner to
                    // resolve dynamically (via the NotConnected fallback pool) instead of risking
                    // a false static edge based on the dummy's unrepresentative state.
                    if (condition.EntityCondition)
                        continue;

                    // We perform two checks: the first when the state is empty,
                    // and the second on the node's effects.
                    // This is done to verify that the effects and conditions actually
                    // link the two nodes, rather than the second node simply having no conditions.
                    var dummyState = new GoapState();
                    dummyState.UseEntityDefaults = false;
                    dummyState.SetValue(GoapState.Owner, dummy.Value);
                    var dummyCheck = CheckCondition(dummy.Value, dummyState, condition);

                    var effectsState = new GoapState();
                    effectsState.UseEntityDefaults = false;
                    effectsState.SetValue(GoapState.Owner, dummy.Value);

                    foreach (var (key, value) in from.Effects)
                    {
                        // Skip effect keys whose real runtime value is entity-derived - only
                        // probe with effects we can actually trust to be what's declared, so a
                        // static edge is never created based on an untrustworthy dummy value.
                        if (GoapState.EntityDefaults.Contains(key))
                            continue;

                        effectsState.SetValue(key, value);
                    }

                    var effectsCheck = CheckCondition(dummy.Value, effectsState, condition);

                    if (!effectsCheck || effectsCheck == dummyCheck)
                        continue;

                    hasConnection = true;
                }

                if (!hasConnection)
                    continue;

                edges.Add(new GoapStaticGraphEdge(fromInd, toInd));
            }
        }

        var edgesList = edges.ToList();

        var outgoing = edgesList
            .GroupBy(x => x.FromNodeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var incoming = edgesList
            .GroupBy(x => x.ToNodeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var candidatesByNodeId = BuildCandidatesByNodeId(nodes, incoming, notConnected.ToArray());

        var nodesByEffect = new Dictionary<(string, object), List<ExecutableGoapTask>>();
        foreach (var node in nodes)
        {
            foreach (var (k, v) in node.Effects)
            {
                var tuple = (k, v);

                if (nodesByEffect.TryGetValue(tuple, out var list))
                    list.Add(node);
                else
                    nodesByEffect[tuple] = new() { node };
            }
        }

        Del(dummy);

        return new GoapStaticGraph(
            Nodes: nodes,
            Edges: edgesList,
            OutgoingByNodeId: outgoing,
            IncomingByNodeId: incoming,
            CandidatesByNodeId: candidatesByNodeId,
            NodesByEffect: nodesByEffect);
    }

    private List<ExecutableGoapTask> GetExecutableTasks(ProtoId<GoapCompoundPrototype> protoId)
    {
        var nextId = 0;
        return GetExecutableTasks(protoId, ref nextId);
    }

    private List<ExecutableGoapTask> GetExecutableTasks(
        ProtoId<GoapCompoundPrototype> protoId,
        ref int nextId)
    {
        if (!_proto.Resolve(protoId, out var proto))
            return new();

        var tasks = new List<ExecutableGoapTask>();

        foreach (var task in proto.Tasks)
        {
            switch (task)
            {
                case GoapActionTask action:
                    tasks.Add(new(
                        nextId++,
                        new List<GoapAction> { action.Action },
                        action.Preconditions,
                        action.Effects,
                        protoId));
                    break;

                case GoapCompoundTask compound:
                    tasks.Add(new(
                        nextId++,
                        compound.Actions,
                        compound.Preconditions,
                        compound.Effects,
                        protoId));
                    break;

                case GoapCompoundPrototypeTask protoCompound:
                    tasks.AddRange(GetExecutableTasks(protoCompound.Proto, ref nextId));
                    break;

                default:
                    throw new InvalidOperationException();
            }
        }

        return tasks;
    }

    private static Dictionary<int, ExecutableGoapTask[]> BuildCandidatesByNodeId(
        List<ExecutableGoapTask> nodes,
        Dictionary<int, List<GoapStaticGraphEdge>> incoming,
        int[] notConnected)
    {
        var result = new Dictionary<int, ExecutableGoapTask[]>(nodes.Count);

        foreach (var node in nodes)
        {
            var list = new List<ExecutableGoapTask>(8);
            var seen = new HashSet<int>();

            if (incoming.TryGetValue(node.Id, out var edges))
            {
                foreach (var edge in edges)
                {
                    if (!seen.Add(edge.FromNodeId))
                        continue;

                    list.Add(nodes[edge.FromNodeId]);
                }
            }

            foreach (var id in notConnected)
            {
                if (id == node.Id)
                    continue;

                if (!seen.Add(id))
                    continue;

                list.Add(nodes[id]);
            }

            result[node.Id] = list.ToArray();
        }

        return result;
    }
}
