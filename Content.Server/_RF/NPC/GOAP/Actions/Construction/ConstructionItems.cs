using System.Linq;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server._RF.NPC.Systems;
using Content.Server.Construction.Components;
using Content.Server.Construction.Conditions;
using Content.Server.Stack;
using Content.Server.Tools;
using Content.Shared._RF.Construction;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Construction;

/// <summary>
/// Searches for all the items needed to complete construction edges
/// (every unmet edge condition plus every remaining step of that edge) and stores
/// them all at once in state as a single list.
/// </summary>
public sealed partial class ConstructionItems : BaseGoapAction<ConstructionItems>
{
    /// <summary>
    /// The key that contains the entity to be constructed.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// A key that stores a list of all items needed for construction.
    /// </summary>
    [DataField]
    public StateKey<List<EntityUid>> ResultKey = "ConstructionItems";
}

public sealed class ConstructionItemsSystem : GoapActionSystem<ConstructionItems>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ToolSystem _tool = default!;
    [Dependency] private readonly NpcHelperSystem _npcHelper = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    private static readonly ProtoId<ToolQualityPrototype> AnchoringQuality = "Anchoring";
    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";

    protected override bool ActionStartup(Entity<GoapComponent> ent, ConstructionItems action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return false;

        var query = _npcHelper.FreeOwnedEntities(ent);
        var edges = GetEdges(target);

        if (edges.Count == 0)
        {
            CreateDump(ent, action, "no construction edges found for target");
            return false;
        }

        var reason = string.Empty; // TODO
        var result = new List<EntityUid>();

        foreach (var edge in edges)
        {
            // Resolve every edge condition that isn't already satisfied - these must all be
            // met before the edge's steps can even be attempted.
            if (!HasComp<CommonConstructionGhostComponent>(target))
            {
                foreach (var condition in edge.Conditions)
                {
                    if (condition.Condition(target, EntityManager))
                        continue;

                    if (ConditionQuery(condition, target) is not { } conditionUid)
                        return false;

                    result.Add(conditionUid);
                }
            }

            // Resolve every remaining step of the current edge, starting from whichever step
            // construction is currently on, rather than stopping after the first one.
            var stepIndex = TryComp(target, out ConstructionComponent? construct) ? construct.StepIndex : 0;

            for (var i = stepIndex; i < edge.Steps.Count; i++)
            {
                if (StepQuery(edge.Steps[i]) is not { } stepUid)
                    return false;

                result.Add(stepUid);
            }
        }

        ent.Comp.State.SetValue(action.ResultKey, result);
        return true;

        EntityUid? ConditionQuery(IGraphCondition condition, EntityUid uid)
        {
            var conditions = new Queue<IGraphCondition>();
            conditions.Enqueue(condition);

            // Check the conditions; if they are not met, look for an item that can be used to fulfill them.
            while (conditions.TryDequeue(out var con))
            {
                if (con.Condition(uid, EntityManager))
                    continue;

                switch (con)
                {
                    case AllConditions all:
                        conditions.Clear();
                        foreach (var cond in all.Conditions)
                        {
                            conditions.Enqueue(cond);
                        }
                        break;
                    case AnyConditions any:
                        conditions.Clear();
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

                            conditions.Enqueue(cond);
                            break;
                        }
                        break;
                    case EntityAnchored:
                        return ToolQuery(AnchoringQuality);
                    case DoorWelded:
                    case StorageWelded:
                        return ToolQuery(WeldingQuality);
                    case WirePanel:
                        return ToolQuery(ScrewingQuality);
                    case HasTag tag:
                        return TagQuery(new() { tag.Tag }, true);
                    case MachineFrameComplete:
                        if (!TryComp(uid, out MachineFrameComponent? frame))
                        {
                            ComponentNotFound<MachineFrameComponent>(ent, action, uid);
                            return null;
                        }

                        foreach (var (type, amount) in frame.MaterialRequirements)
                        {
                            if (!frame.MaterialProgress.TryGetValue(type, out var current) || current < amount)
                                return MaterialQuery(type, amount);
                        }

                        foreach (var (compName, info) in frame.ComponentRequirements)
                        {
                            if (!frame.ComponentProgress.TryGetValue(compName, out var current) || current < info.Amount)
                                return ComponentQuery(compName);
                        }

                        foreach (var (tagName, info) in frame.TagRequirements)
                        {
                            if (!frame.TagProgress.TryGetValue(tagName, out var current) || current < info.Amount)
                                return TagQuery(new() { tagName }, true);
                        }

                        break;
                    default:
                        CreateDump(ent, action, $"unsupported construction condition: {con}");
                        break;
                }
            }

            return null;
        }

        EntityUid? StepQuery(ConstructionGraphStep step)
        {
            switch (step)
            {
                case MaterialConstructionGraphStep insertMaterial:
                    return MaterialQuery(insertMaterial.MaterialPrototypeId, insertMaterial.Amount);
                case TagConstructionGraphStep insertTag:
                    if (insertTag.Tag != null)
                        return TagQuery(new() { insertTag.Tag }, true);

                    break;
                case MultipleTagsConstructionGraphStep insertMultipleTags:
                    if (insertMultipleTags.AnyTag != null)
                        return TagQuery(insertMultipleTags.AnyTag, false);

                    if (insertMultipleTags.AllTag != null)
                        return TagQuery(insertMultipleTags.AllTag, true);

                    break;
                case ToolConstructionGraphStep insertTool:
                    return ToolQuery(insertTool.Tool);
                case ComponentConstructionGraphStep insertComponent:
                    return ComponentQuery(insertComponent.Component);
                default:
                    CreateDump(ent, action, $"unsupported construction step: {step}");
                    break;
            }

            return null;
        }

        EntityUid? TagQuery(List<ProtoId<TagPrototype>> tags, bool requireAll)
        {
            for (var i = 0; i < query.Count; i++)
            {
                var uid = query[i];

                if (requireAll && !_tag.HasAllTags(uid, tags)
                    || !requireAll && !_tag.HasAnyTag(uid, tags))
                    continue;

                query.RemoveAt(i);
                return uid;
            }

            CreateDump(ent, action, $"entity with tags `{string.Join(", ", tags)} (requireAll: {requireAll}) not found`");
            reason = _npcHelper.TagNotFoundReason(tags);
            return null;
        }

        EntityUid? ToolQuery(ProtoId<ToolQualityPrototype> quality)
        {
            for (var i = 0; i < query.Count; i++)
            {
                var uid = query[i];

                if (!_tool.HasQuality(uid, quality))
                    continue;

                query.RemoveAt(i);
                return uid;
            }

            CreateDump(ent, action, $"tool `{quality}` not found");
            reason = _npcHelper.ToolNotFoundReason(quality);
            return null;
        }

        EntityUid? ComponentQuery(string component)
        {
            var type = Factory.GetComponent(component).GetType();

            for (var i = 0; i < query.Count; i++)
            {
                var uid = query[i];

                if (!HasComp(uid, type))
                    continue;

                query.RemoveAt(i);
                return uid;
            }

            CreateDump(ent, action, $"entity with component `{component}` not found");
            reason = _npcHelper.ComponentNotFoundReason(component);
            return null;
        }

        EntityUid? MaterialQuery(ProtoId<StackPrototype> stack, int amount)
        {
            for (var i = 0; i < query.Count; i++)
            {
                var uid = query[i];

                if (!TryComp(uid, out StackComponent? comp)
                    || comp.StackTypeId != stack
                    || comp.Count < amount)
                    continue;

                if (comp.Count == amount)
                {
                    query.RemoveAt(i);
                    return uid;
                }

                // When searching for material, we create a new material entity
                // of the required quantity so as not to complicate the search logic
                if (_stack.Split(uid, amount, Transform(ent).Coordinates) is not { } split)
                {
                    CreateDump(ent, action, $"failed to split {ToPrettyString(uid)}, amount: {amount}");
                    continue;
                }

                return split;
            }

            CreateDump(ent, action, $"material `{stack}`, amount: {amount}, not found");
            reason = _npcHelper.MaterialNotFoundReason(stack, amount);
            return null;
        }
    }

    /// <summary>
    /// Returns all edges to complete the construction of the target entity
    /// </summary>
    private List<ConstructionGraphEdge> GetEdges(EntityUid uid)
    {
        var edges = new List<ConstructionGraphEdge>();
        var path = new List<string>();
        string? start = null;
        ConstructionGraphPrototype? graph = null;

        // Searching for a path of nodes for an existing structure
        if (TryComp(uid, out ConstructionComponent? comp))
        {
            if (!_proto.Resolve(comp.Graph, out graph)
                || comp.NodePathfinding == null
                || comp.NodePathfinding.Count == 0)
                return edges;

            start = graph.Start;
            path = comp.NodePathfinding.ToList();

            // If there is one node left to execute,
            // add the current node to the path for correct edge search
            if (path.Count == 1)
                path.Insert(0, comp.Node);
        }
        // Searching for a path of nodes for a construction ghost
        else if (TryComp(uid, out CommonConstructionGhostComponent? ghost))
        {
            if (!_proto.Resolve(ghost.ConstructionProto, out var proto)
                || !_proto.Resolve(proto.Graph, out graph))
                return edges;

            start = proto.StartNode;
            path = graph.PathId(proto.StartNode, proto.TargetNode)?.ToList();
        }

        if (path == null || graph == null || start == null)
            return edges;

        path.Insert(0, start);

        for (var i = 0; i < path.Count - 1; i++)
        {
            if (graph.Edge(path[i], path[i + 1]) is not { } edge)
                continue;

            edges.Add(edge);
        }

        return edges;
    }
}
