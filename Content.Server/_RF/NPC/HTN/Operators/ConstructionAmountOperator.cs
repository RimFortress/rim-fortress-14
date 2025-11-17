using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Construction.Conditions;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._RF.Construction;
using Content.Shared.Construction;
using Content.Shared.Construction.Steps;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Obtains information on the amount of material needed for construction and stores it in a key
/// </summary>
public sealed partial class ConstructionAmountOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _proto  = default!;

    private ConstructionSystem _construction;

    /// <summary>
    /// The key that contains the entity to be constructed
    /// </summary>
    [DataField(required: true)]
    public string TargetKey;

    /// <summary>
    /// A key into which the amount of material required will be stored
    /// </summary>
    [DataField]
    public string AmountKey = "Amount";

    /// <summary>
    /// Whether to consider a task failed if no material is currently required for construction
    /// </summary>
    [DataField]
    public bool FailIfNoFound;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _construction = sysManager.GetEntitySystem<ConstructionSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entityManager))
            return (false, null);

        ConstructionGraphEdge? edge = null;
        ConstructionGraphNode? node;

        if (_entityManager.TryGetComponent(uid, out ConstructionComponent? comp))
        {
            (node, edge) = _construction.GetCurrentNodeAndEdge(uid.Value, comp);

            if (node == null || edge == null
                && (comp.NodePathfinding == null || comp.NodePathfinding.Count == 0))
                return (false, null);

            edge ??= node.GetEdge(comp.NodePathfinding!.ToList().First());
        }
        else if (_entityManager.TryGetComponent(uid, out CommonConstructionGhostComponent? ghost))
        {
            if (!_proto.TryIndex(ghost.ConstructionProto, out var proto)
                || !_proto.TryIndex(proto.Graph, out var graph))
                return (false, null);

            node = _construction.GetNodeFromGraph(graph, proto.StartNode);
            var path = graph.PathId(proto.StartNode, proto.TargetNode);

            if (path == null || path.Length == 0)
                return (false, null);

            edge ??= node?.GetEdge(path[0]);
        }

        if (edge == null)
            return (false, null);

        // Check conditions
        var queue = new Queue<IGraphCondition>(edge.Conditions);
        while (queue.TryDequeue(out var condition))
        {
            if (condition.Condition(uid.Value, _entityManager))
                continue;

            switch (condition)
            {
                case AllConditions all:
                    queue.Clear();
                    foreach (var cond in all.Conditions)
                    {
                        queue.Enqueue(cond);
                    }
                    break;
                case AnyConditions any:
                    queue.Clear();
                    foreach (var cond in any.Conditions)
                    {
                        // Take the first supported condition
                        if (cond is not (AllConditions
                            or AnyConditions
                            or EntityAnchored
                            or DoorWelded
                            or StorageWelded
                            or WirePanel
                            or HasTag
                            or MachineFrameComplete))
                            continue;

                        queue.Enqueue(cond);
                        break;
                    }
                    break;
                case MachineFrameComplete:
                    if (!_entityManager.TryGetComponent(uid, out MachineFrameComponent? frame))
                        break;

                    foreach (var (_, amount) in frame.MaterialRequirements)
                    {
                        return (true, new() { { AmountKey, amount } });
                    }

                    break;
                default:
                    return (!FailIfNoFound, null);
            }
        }

        if (edge.Steps[comp?.StepIndex ?? 0] is MaterialConstructionGraphStep step)
            return (true, new() { { AmountKey, step.Amount } });

        return (!FailIfNoFound, null);
    }
}
