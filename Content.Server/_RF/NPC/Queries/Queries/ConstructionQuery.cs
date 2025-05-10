using System.Linq;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Construction.Conditions;
using Content.Server.NPC;
using Content.Shared.Construction;
using Content.Shared.Construction.Steps;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// A queue of tools required to build an entity.
/// Only the first currently required step or condition to be executed is selected
/// </summary>
public sealed partial class ConstructionQuery : RfUtilityQuery
{
    private ConstructionSystem _construction;
    private TagSystem _tag;

    private EntityQuery<TransformComponent> _xformQuery;

    [ValidatePrototypeId<ToolQualityPrototype>]
    private readonly ProtoId<ToolQualityPrototype> _anchoringQuality = "Anchoring";

    [ValidatePrototypeId<ToolQualityPrototype>]
    private readonly ProtoId<ToolQualityPrototype> _weldingQuality = "Welding";

    [ValidatePrototypeId<ToolQualityPrototype>]
    private readonly ProtoId<ToolQualityPrototype> _screwingQuality = "Screwing";

    /// <summary>
    /// The key that contains the entity to be constructed
    /// </summary>
    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _construction = entManager.System<ConstructionSystem>();
        _tag = entManager.System<TagSystem>();

        _xformQuery = entManager.GetEntityQuery<TransformComponent>();
    }

    public override HashSet<EntityUid> Query(NPCBlackboard blackboard)
    {
        var query = new HashSet<EntityUid>();
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var mapId = EntityManager.GetComponent<TransformComponent>(owner).MapID;

        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
            || !EntityManager.TryGetComponent(uid, out ConstructionComponent? comp))
            return query;

        var (node, edge) = _construction.GetCurrentNodeAndEdge(uid.Value, comp);

        if (node == null || edge == null
            && (comp.NodePathfinding == null || comp.NodePathfinding.Count == 0))
            return query;

        edge ??= node.GetEdge(comp.NodePathfinding!.ToList().First());
        var step = edge?.Steps[comp.StepIndex];

        if (edge != null && ConditionQuery(mapId, uid.Value, edge.Conditions) is { } conQuery)
            return conQuery;

        if (step != null)
            return StepQuery(mapId, step);

        return query;
    }

    private HashSet<EntityUid>? ConditionQuery(MapId mapId, EntityUid uid, IReadOnlyList<IGraphCondition> conditions)
    {
        var cons = new Queue<IGraphCondition>(conditions);

        while (cons.TryDequeue(out var condition))
        {
            if (condition.Condition(uid, EntityManager))
                continue;

            switch (condition)
            {
                case AllConditions all:
                    cons.Clear();
                    foreach (var cond in all.Conditions)
                    {
                        cons.Enqueue(cond);
                    }
                    break;
                case AnyConditions any:
                    cons.Clear();
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

                        cons.Enqueue(cond);
                        break;
                    }
                    break;
                case EntityAnchored:
                    return ToolQuery(mapId, _anchoringQuality);
                case DoorWelded:
                case StorageWelded:
                    return ToolQuery(mapId, _weldingQuality);
                case WirePanel:
                    return ToolQuery(mapId, _screwingQuality);
                case HasTag tag:
                    return TagQuery(mapId, new() { tag.Tag }, true);
                case MachineFrameComplete:
                    if (!EntityManager.TryGetComponent(uid, out MachineFrameComponent? frame))
                        break;

                    foreach (var (type, amount) in frame.MaterialRequirements)
                    {
                        return MaterialQuery(mapId, type, amount);
                    }

                    foreach (var (compName, _) in frame.ComponentRequirements)
                    {
                        return ComponentQuery(mapId, compName);
                    }

                    foreach (var (tagName, _) in frame.TagRequirements)
                    {
                        return TagQuery(mapId, new() { tagName }, true);
                    }

                    break;
            }
        }

        return null;
    }

    private HashSet<EntityUid> StepQuery(MapId mapId, ConstructionGraphStep step)
    {
        var query = new HashSet<EntityUid>();

        switch (step)
        {
            case MaterialConstructionGraphStep insertMaterial:
                query = MaterialQuery(mapId, insertMaterial.MaterialPrototypeId, insertMaterial.Amount);
                break;
            case TagConstructionGraphStep insertTag:
                if (insertTag.Tag != null)
                    query = TagQuery(mapId, new() { insertTag.Tag }, true);

                break;
            case MultipleTagsConstructionGraphStep insertMultipleTags:
                if (insertMultipleTags.AnyTag != null)
                    query.UnionWith(TagQuery(mapId, insertMultipleTags.AnyTag, false));

                if (insertMultipleTags.AllTag != null)
                    query.UnionWith(TagQuery(mapId, insertMultipleTags.AllTag, true));

                break;
            case ToolConstructionGraphStep insertTool:
                query = ToolQuery(mapId, insertTool.Tool);
                break;
            case ComponentConstructionGraphStep insertComponent:
                query = ComponentQuery(mapId, insertComponent.Component);
                break;
        }

        return query;
    }

    private HashSet<EntityUid> MaterialQuery(MapId mapId, ProtoId<StackPrototype> stack, int amount)
    {
        var query = new HashSet<EntityUid>();
        var enumerator = EntityManager.AllEntityQueryEnumerator<StackComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (!_xformQuery.TryComp(uid, out var xform)
                || xform.MapID != mapId
                || comp.StackTypeId != stack
                || comp.Count < amount)
                continue;

            query.Add(uid);
        }

        return query;
    }

    private HashSet<EntityUid> TagQuery(MapId mapId, List<ProtoId<TagPrototype>> tags, bool requireAll)
    {
        var query = new HashSet<EntityUid>();
        var enumerator = EntityManager.AllEntityQueryEnumerator<TagComponent>();

        while (enumerator.MoveNext(out var uid, out _))
        {
            if (!_xformQuery.TryComp(uid, out var xform)
                || xform.MapID != mapId)
                continue;

            if (requireAll && !_tag.HasAllTags(uid, tags)
                || !requireAll && !_tag.HasAnyTag(uid, tags))
                continue;

            query.Add(uid);
        }

        return query;
    }

    private HashSet<EntityUid> ToolQuery(MapId mapId, ProtoId<ToolQualityPrototype> quality)
    {
        var query = new HashSet<EntityUid>();
        var enumerator = EntityManager.AllEntityQueryEnumerator<ToolComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (!_xformQuery.TryComp(uid, out var xform)
                || xform.MapID != mapId
                || !comp.Qualities.ToList().Contains(quality))
                continue;

            query.Add(uid);
        }

        return query;
    }

    private HashSet<EntityUid> ComponentQuery(MapId mapId, string component)
    {
        var query = new HashSet<EntityUid>();
        var enumerator = EntityManager.AllEntityQueryEnumerator(EntityManager.ComponentFactory.GetComponent(component).GetType());

        while (enumerator.MoveNext(out var uid, out _))
        {
            if (!_xformQuery.TryComp(uid, out var xform) || xform.MapID != mapId)
                continue;

            query.Add(uid);
        }

        return query;
    }
}
