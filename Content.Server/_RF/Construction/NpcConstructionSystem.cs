using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._RF.NPC.Systems;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Construction.Conditions;
using Content.Server.Stack;
using Content.Server.Tools;
using Content.Shared._RF.Construction;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Construction;

/// <summary>
/// Helper system for the NPCs construction
/// </summary>
public sealed class NpcConstructionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ToolSystem _tool = default!;
    [Dependency] private readonly NpcHelperSystem _npcHelper = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly ConstructionSystem _construction = default!;

    private static readonly ProtoId<ToolQualityPrototype> AnchoringQuality = "Anchoring";
    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";

    /// <summary>
    /// Returns all items necessary for the user to advance in the construction of the entity.
    /// </summary>
    /// <param name="uid">Entity for construction.</param>
    /// <param name="user">User entity.</param>
    /// <param name="reason">The reason why the construction item could not be found.</param>
    public List<EntityUid>? GetConstructionItems(EntityUid uid, EntityUid user, out string reason)
    {
        reason = string.Empty;
        var entities = new List<EntityUid>();
        var commonQuery = _npcHelper.FreeOwnedEntities(user);

        foreach (var edge in GetEdges(uid))
        {
            foreach (var condition in edge.Conditions)
            {
                if (ConditionQuery(commonQuery, condition, uid, out reason) is { } query)
                    entities.AddRange(query);
                else
                    return null;
            }

            // Looking for the most suitable item for each step of construction
            foreach (var step in edge.Steps)
            {
                if (StepQuery(commonQuery, step, out reason) is { } ent)
                    entities.Add(ent);
                else
                    return null;
            }
        }

        return entities.Count == 0 ? null : entities;
    }

    /// <summary>
    /// Returns the next item from the user's inventory that is required to construct the entity
    /// </summary>
    /// <param name="uid">Target entity for construction</param>
    /// <param name="user">User entity</param>
    /// <param name="item">Entity of the item needed for construction</param>
    /// <param name="reason">The reason why the construction item could not be found</param>
    /// <returns>True, if the item for construction is found in the user's inventory</returns>
    public bool TryGetNextItem(
        EntityUid uid,
        EntityUid user,
        [NotNullWhen(true)] out EntityUid? item,
        [NotNullWhen(false)] out string? reason)
    {
        item = null;
        reason = null;

        var invEntities = _npcHelper.InventoryEntities(user);
        var edges = GetEdges(uid);
        var edge = _construction.GetCurrentEdge(uid) ?? edges[0];
        var step = _construction.GetCurrentStep(uid) ?? edges[0].Steps[0];

        foreach (var condition in edge.Conditions)
        {
            if (condition.Condition(uid, EntityManager))
                continue;

            var entities = ConditionQuery(invEntities, condition, uid, out reason);

            if (entities == null)
                return false;

            item = entities[0];
            return true;
        }

        // Looking for the most suitable item for each step of construction
        item = StepQuery(invEntities, step, out reason);

        return item != null;
    }

    private List<EntityUid>? ConditionQuery(List<EntityUid> query, IGraphCondition condition, EntityUid uid, out string reason)
    {
        reason = string.Empty;
        var entities = new List<EntityUid>();
        var conditions = new Queue<IGraphCondition>();
        conditions.Enqueue(condition);

        if (HasComp<CommonConstructionGhostComponent>(uid))
            return entities;

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
                    if (ToolQuery(query, AnchoringQuality, out reason) is { } anchoring)
                        entities.Add(anchoring);
                    else
                        return null;
                    break;
                case DoorWelded:
                case StorageWelded:
                    if (ToolQuery(query, WeldingQuality, out reason) is { } welding)
                        entities.Add(welding);
                    else
                        return null;
                    break;
                case WirePanel:
                    if (ToolQuery(query, ScrewingQuality, out reason) is { } screwing)
                        entities.Add(screwing);
                    else
                        return null;
                    break;
                case HasTag tag:
                    if (TagQuery(query, new() { tag.Tag }, true, out reason) is { } tagUid)
                        entities.Add(tagUid);
                    else
                        return null;
                    break;
                case MachineFrameComplete:
                    if (!TryComp(uid, out MachineFrameComponent? frame))
                        return null;

                    foreach (var (type, amount) in frame.MaterialRequirements)
                    {
                        if (MaterialQuery(query, type, amount, out reason) is { } material)
                            entities.Add(material);
                        else
                            return null;
                    }

                    foreach (var (compName, _) in frame.ComponentRequirements)
                    {
                        if (ComponentQuery(query, compName, out reason) is { } component)
                            entities.Add(component);
                        else
                            return null;
                    }

                    foreach (var (tagName, _) in frame.TagRequirements)
                    {
                        if (TagQuery(query, new() { tagName }, true, out reason) is { } machineTagUid)
                            entities.Add(machineTagUid);
                        else
                            return null;
                    }

                    break;
            }
        }

        return entities;
    }

    private EntityUid? StepQuery(List<EntityUid> query, ConstructionGraphStep step, out string reason)
    {
        reason = string.Empty;

        switch (step)
        {
            case MaterialConstructionGraphStep insertMaterial:
                return MaterialQuery(query, insertMaterial.MaterialPrototypeId, insertMaterial.Amount, out reason);
            case TagConstructionGraphStep insertTag:
                if (insertTag.Tag != null)
                    return TagQuery(query, new() { insertTag.Tag }, true, out reason);

                break;
            case MultipleTagsConstructionGraphStep insertMultipleTags:
                if (insertMultipleTags.AnyTag != null)
                    return TagQuery(query, insertMultipleTags.AnyTag, false, out reason);

                if (insertMultipleTags.AllTag != null)
                    return TagQuery(query, insertMultipleTags.AllTag, true, out reason);

                break;
            case ToolConstructionGraphStep insertTool:
                return ToolQuery(query, insertTool.Tool, out reason);
            case ComponentConstructionGraphStep insertComponent:
                return ComponentQuery(query, insertComponent.Component, out reason);
            default:
                Log.Error($"NPC attempts to perform an unsupported construction step: {step}");
                return null;
        }

        return null;
    }

    private EntityUid? TagQuery(List<EntityUid> query, List<ProtoId<TagPrototype>> tags, bool requireAll, out string reason)
    {
        reason = string.Empty;

        for (var i = 0; i < query.Count; i++)
        {
            var ent = query[i];

            if (requireAll && !_tag.HasAllTags(ent, tags)
                || !requireAll && !_tag.HasAnyTag(ent, tags))
                continue;

            query.RemoveAt(i);
            return ent;
        }

        reason = _npcHelper.TagNotFoundReason(tags);
        return null;
    }

    private EntityUid? ToolQuery(List<EntityUid> query, ProtoId<ToolQualityPrototype> quality, out string reason)
    {
        reason = string.Empty;

        for (var i = 0; i < query.Count; i++)
        {
            var ent = query[i];

            if (!_tool.HasQuality(ent, quality))
                continue;

            query.RemoveAt(i);
            return ent;
        }

        reason = _npcHelper.ToolNotFoundReason(quality);
        return null;
    }

    private EntityUid? ComponentQuery(List<EntityUid> query, string component, out string reason)
    {
        reason = string.Empty;

        var type = Factory.GetComponent(component).GetType();

        for (var i = 0; i < query.Count; i++)
        {
            var ent = query[i];

            if (!HasComp(ent, type))
                continue;

            query.RemoveAt(i);
            return ent;
        }

        reason = _npcHelper.ComponentNotFoundReason(component);
        return null;
    }

    private EntityUid? MaterialQuery(List<EntityUid> query, ProtoId<StackPrototype> stack, int amount, out string reason)
    {
        reason = string.Empty;

        for (var i = 0; i < query.Count; i++)
        {
            var ent = query[i];

            if (!TryComp(ent, out StackComponent? comp)
                || comp.StackTypeId != stack
                || comp.Count < amount)
                continue;

            if (comp.Count == amount)
            {
                query.RemoveAt(i);
                return ent;
            }

            // When searching for material, we create a new material entity
            // of the required quantity so as not to complicate the search logic
            if (_stack.Split(ent, amount, Transform(ent).Coordinates) is not { } split)
                continue;

            return split;
        }

        reason = _npcHelper.MaterialNotFoundReason(stack, amount);
        return null;
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
            if (!_proto.TryIndex(comp.Graph, out graph))
                return edges;

            if (comp.NodePathfinding == null || comp.NodePathfinding.Count == 0)
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
            if (!_proto.TryIndex(ghost.ConstructionProto, out var proto)
                || !_proto.TryIndex(proto.Graph, out graph))
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
