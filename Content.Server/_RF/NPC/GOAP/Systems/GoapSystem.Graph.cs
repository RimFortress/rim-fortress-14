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
        _staticGraphs.Clear();
        Log.Info("building GOAP static dependency graphs...");

        foreach (var compound in _proto.EnumeratePrototypes<GoapCompoundPrototype>())
        {
            _staticGraphs[compound] = GetStaticGraph(compound);
        }

        Log.Info("graphs built");
    }

    [PublicAPI]
    public GoapStaticGraph GetStaticGraph(
        ProtoId<GoapCompoundPrototype> protoId,
        bool optimize = true)
    {
        if (!_proto.Resolve(protoId, out var compound))
            return new();

        var dummy = Spawn();
        var comp = Factory.GetComponent<GoapComponent>();
        comp.RootTask = DummyCompound;
        AddComp(dummy, comp);
        var nodes = GetExecutableTasks(compound).OrderBy(x => x.Id).ToList();
        var edges = new HashSet<GoapStaticGraphEdge>();
        var notConnected = new HashSet<int>();

        for (var fromInd = 0; fromInd < nodes.Count; fromInd++)
        {
            var from = nodes[fromInd];

            if (from.Preconditions.Any(x => x.EntityCondition))
            {
                notConnected.Add(from.Id);
                continue;
            }

            for (var toInd = 0; toInd < nodes.Count; toInd++)
            {
                var to = nodes[toInd];

                if (fromInd == toInd || notConnected.Contains(toInd))
                    continue;

                var hasConnection = false;
                var skip = new HashSet<int>();

                for (var i = 0; i < to.Preconditions.Count; i++)
                {
                    var condition = to.Preconditions[i];

                    // We perform two checks: the first when the state is empty,
                    // and the second on the node's effects.
                    // This is done to verify that the effects and conditions actually
                    // link the two nodes, rather than the second node simply having no conditions.
                    var dummyState = new GoapState();
                    dummyState.UseEntityDefaults = false;
                    dummyState.SetValue(GoapState.Owner, dummy);
                    var dummyCheck = CheckCondition(dummy, dummyState, condition);

                    var effectsState = from.Effects.ShallowClone();
                    effectsState.UseEntityDefaults = false;
                    effectsState.SetValue(GoapState.Owner, dummy);
                    var effectsCheck = CheckCondition(dummy, effectsState, condition);

                    if (!effectsCheck || effectsCheck == dummyCheck)
                        continue;

                    hasConnection = true;
                    skip.Add(i);
                }

                if (!hasConnection)
                    continue;

                edges.Add(new GoapStaticGraphEdge(fromInd, toInd, skip));
            }
        }

        var edgesList = edges.ToList();

        var outgoing = edgesList
            .GroupBy(x => x.FromNodeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var incoming = edgesList
            .GroupBy(x => x.ToNodeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var notConnectedArray = notConnected.ToArray();

        var rootCandidates = BuildRootCandidates(nodes, incoming, notConnectedArray);
        var candidatesByNodeId = BuildCandidatesByNodeId(nodes, outgoing, notConnectedArray);

        Del(dummy);

        return new GoapStaticGraph(
            Nodes: nodes,
            Edges: edgesList,
            OutgoingByNodeId: outgoing,
            IncomingByNodeId: incoming,
            NotConnected: notConnected,
            RootCandidates: rootCandidates,
            CandidatesByNodeId: candidatesByNodeId);
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

    private static GoapStaticGraphCandidate[] BuildRootCandidates(
        List<ExecutableGoapTask> nodes,
        Dictionary<int, List<GoapStaticGraphEdge>> incoming,
        int[] notConnected)
    {
        var result = new List<GoapStaticGraphCandidate>(nodes.Count);
        var seen = new HashSet<int>();

        foreach (var node in nodes)
        {
            if (incoming.ContainsKey(node.Id))
                continue;

            if (!seen.Add(node.Id))
                continue;

            result.Add(new GoapStaticGraphCandidate(node, null));
        }

        foreach (var id in notConnected)
        {
            if (!seen.Add(id))
                continue;

            result.Add(new GoapStaticGraphCandidate(nodes[id], null));
        }

        return result.ToArray();
    }

    private static Dictionary<int, GoapStaticGraphCandidate[]> BuildCandidatesByNodeId(
        List<ExecutableGoapTask> nodes,
        Dictionary<int, List<GoapStaticGraphEdge>> outgoing,
        int[] notConnected)
    {
        var result = new Dictionary<int, GoapStaticGraphCandidate[]>(nodes.Count);

        foreach (var node in nodes)
        {
            var list = new List<GoapStaticGraphCandidate>(8);
            var seen = new HashSet<int>();

            if (outgoing.TryGetValue(node.Id, out var edges))
            {
                foreach (var edge in edges)
                {
                    if (!seen.Add(edge.ToNodeId))
                        continue;

                    list.Add(new GoapStaticGraphCandidate(nodes[edge.ToNodeId], edge));
                }
            }

            foreach (var id in notConnected)
            {
                if (id == node.Id)
                    continue;

                if (!seen.Add(id))
                    continue;

                list.Add(new GoapStaticGraphCandidate(nodes[id], null));
            }

            result[node.Id] = list.ToArray();
        }

        return result;
    }
}
