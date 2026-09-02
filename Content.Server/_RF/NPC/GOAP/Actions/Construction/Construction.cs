using System.Linq;
using Content.Server._RF.NPC.GOAP.Actions.Interaction;
using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Server._RF.NPC.Systems;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Construction.Conditions;
using Content.Server.Interaction;
using Content.Server.NPC.Pathfinding;
using Content.Server.Tools;
using Content.Shared._RF.Construction;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Construction;
using Content.Shared.Construction.Steps;
using Content.Shared.DoAfter;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Construction;

/// <summary>
/// A complex operator responsible for the entire construction logic. Given only the
/// construction target, it figures out - one item at a time, from the target's LIVE
/// construction graph state - what the very next thing needed is (an unmet edge condition,
/// or the next unapplied step of the current edge), finds a matching entity the agent owns,
/// walks over, picks it up, walks to the target and uses it there, then repeats.
/// </summary>
public sealed partial class Construction : BaseGoapAction<Construction>
{
    /// <summary>
    /// Key that contains the target entity being constructed.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = "MovementPathfinding";

    /// <summary>
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField]
    public StateKey<float> RangeKey = GoapState.InteractRange;

    /// <summary>
    /// The item currently being fetched/used to satisfy whatever the next unmet
    /// condition/step turns out to be. Cleared as soon as that item has been successfully
    /// used, so the following Update call re-derives the next need from the target's
    /// current (possibly changed) construction state instead of trusting a stale value.
    /// </summary>
    public readonly StateKey<EntityUid> CurrentItemKey = "CurrentConstructionItem";

    /// <summary>
    /// The key where the ID of the current `doAfter` is stored.
    /// </summary>
    public readonly StateKey<ushort> CurrentDoAfter = "CurrentConstructionInteractDoAfter";
}

public sealed partial class NpcConstructionSystem : GoapActionSystem<Construction>
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ToolSystem _tool = default!;
    [Dependency] private NpcHelperSystem _npcHelper = default!;
    [Dependency] private InteractionSystem _interaction = default!;
    [Dependency] private InteractWithSystem _interactWith = default!;
    [Dependency] private MoveToActionSystem _moveTo = default!;
    [Dependency] private PickupActionSystem _pickup = default!;
    [Dependency] private ConstructionSystem _construction = default!;
    [Dependency] private NpcTimingSystem _npcTiming = default!;

    [Dependency] private readonly EntityQuery<ActiveDoAfterComponent> _activeDoAfterQuery = default!;
    [Dependency] private readonly EntityQuery<ConstructionComponent> _constructionQuery = default!;

    private static readonly ProtoId<ToolQualityPrototype> AnchoringQuality = "Anchoring";
    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Construction action) => 3f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Construction action)
    {
        Remove(ent, action.CurrentDoAfter);
        Remove(ent, action.CurrentItemKey);
        NpcTimingSystem.ClearQueue(ent);
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Construction action)
    {
        Remove(ent, action.CurrentDoAfter);
        Remove(ent, action.CurrentItemKey);
        NpcTimingSystem.ClearQueue(ent);
        _moveTo.ShutdownMovement(ent, this, action.PathfindKey);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Construction action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return GoapActionResult.Failed;

        var waitResult = _interactWith.Wait(ent, action, action.CurrentDoAfter, out _);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        var queuerResult = _npcTiming.WaitQueue(ent, this);

        if (queuerResult != GoapActionResult.Finished)
            return queuerResult;

        if (_activeDoAfterQuery.HasComp(ent)
            || _constructionQuery.TryComp(target, out var comp)
            && comp.InteractionQueue.Count > 0)
            return GoapActionResult.Continuing;

        // No item locked in yet for this round - figure out fresh, from the target's
        // current construction state, what's needed next.
        if (!TryGet(ent, action.CurrentItemKey, out var item))
        {
            switch (FindNextItem(ent, target, out var found))
            {
                case NeedResult.Done:
                    return GoapActionResult.Finished;
                case NeedResult.NotFound:
                    return GoapActionResult.Failed;
                case NeedResult.Found:
                    item = found;
                    ent.Comp.State.SetValue(action.CurrentItemKey, item);
                    break;
            }
        }

        // No interaction in flight for this item yet, so it genuinely should still exist -
        // we're about to walk to it / pick it up.
        if (Deleted(item))
        {
            CreateDump($"{ToPrettyString(item)} not exist");
            return GoapActionResult.Failed;
        }

        var itemCoords = Transform(item).Coordinates;
        var ownerCoords = Get(ent.Comp.State, GoapState.OwnerCoordinates);
        var targetCoords = Transform(target).Coordinates;
        float distance;

        if (!TryGet(ent, GoapState.ActiveHandEntity, out var heldEnt) || heldEnt != item)
        {
            if (!TryGet(ent, action.RangeKey, out var range)
                || !ownerCoords.TryDistance(EntityManager, itemCoords, out distance))
                return GoapActionResult.Failed;

            // Movement
            if (distance > range)
            {
                if (!_moveTo.StartedUp(ent))
                {
                    CreateDump($"started moving toward the item: {ToPrettyString(item)}");
                    _moveTo.StartupMovement(ent, this, itemCoords, true, action.PathfindKey, action.RangeKey);
                }

                var result = _moveTo.UpdateMovement(ent, this, itemCoords, action.PathfindKey, action.RangeKey);

                if (result != GoapActionResult.Finished)
                    return result;
            }
            else if (_moveTo.StartedUp(ent))
                _moveTo.ShutdownMovement(ent, this, action.PathfindKey);

            return _npcTiming.EnqueueWait(ent,
                this,
                (0.33f, 0.66f),
                onFinish:() =>
                {
                    // Pick up the item
                    if (!_pickup.Pickup(ent, item, action))
                        return false;

                    CreateDump($"{ToPrettyString(item)} picked up");

                    // Turn on welder
                    if (TryComp(item, out WelderComponent? welder) && !welder.Enabled)
                    {
                        CreateDump("turning on welder");
                        _interaction.UserInteraction(ent, Transform(item).Coordinates, item);
                    }

                    return true;
                });
        }

        if (ownerCoords.TryDistance(EntityManager, targetCoords, out distance)
            && distance > Get(ent.Comp.State, GoapState.InteractRange))
        {
            if (!_moveTo.StartedUp(ent))
            {
                CreateDump($"started moving toward the target: {ToPrettyString(target)}");
                _moveTo.StartupMovement(ent, this, targetCoords, true, action.PathfindKey, action.RangeKey);
            }

            var result = _moveTo.UpdateMovement(ent, this, targetCoords, action.PathfindKey, action.RangeKey);

            if (result != GoapActionResult.Finished)
                return result;
        }
        else if (_moveTo.StartedUp(ent))
            _moveTo.ShutdownMovement(ent, this, action.PathfindKey);

        return _npcTiming.EnqueueWait(ent,
            this,
            (0.33f, 0.66f),
            onFinish:() =>
            {
                _interactWith.DoInteraction(ent, action, target, action.CurrentDoAfter, false);
                AdvanceToNextItem(ent, action);
            });
    }

    /// <summary>
    /// Called once the interaction for the current item has finished successfully. Clears
    /// both the per-item doAfter tracking and the current-item lock, so the next Update call
    /// starts a fresh interaction and re-derives what's needed next from the target's
    /// (possibly now different) construction state.
    /// </summary>
    private void AdvanceToNextItem(Entity<GoapComponent> ent, Construction action)
    {
        Remove(ent, action.CurrentDoAfter);
        Remove(ent, action.CurrentItemKey);
    }

    private enum NeedResult
    {
        /// <summary>
        /// An item was found and should be used to satisfy the next need.
        /// </summary>
        Found,

        /// <summary>
        /// Nothing is left to do - construction of the target is complete.
        /// </summary>
        Done,

        /// <summary>
        /// Something is still needed, but no matching entity could be found for it.
        /// </summary>
        NotFound,
    }

    /// <summary>
    /// Looks at the target's construction state exactly as it stands right now and returns
    /// the single next thing needed: the first unmet condition of the relevant edge, or (if
    /// all of that edge's conditions are met) the next unapplied step of that edge.
    /// </summary>
    /// <remarks>
    /// For an entity that already has a <see cref="ConstructionComponent"/>, "the relevant
    /// edge" is asked directly from <see cref="ConstructionSystem"/> - the SAME authority
    /// <c>HandleEvent</c> itself uses to validate interactions - rather than being
    /// reconstructed from <see cref="ConstructionComponent.NodePathfinding"/>.
    /// <c>NodePathfinding</c> is a lookahead path computed for pathfinding/UI purposes and can
    /// legitimately point further ahead than <see cref="ConstructionComponent.EdgeIndex"/> /
    /// <see cref="ConstructionComponent.StepIndex"/> - the actual, authoritative position in
    /// the graph - which is exactly what caused items to be requested for a step construction
    /// wasn't actually waiting on yet.
    /// </remarks>
    private NeedResult FindNextItem(Entity<GoapComponent> ent,
        EntityUid target,
        out EntityUid item)
        => TryComp(target, out ConstructionComponent? construct)
            ? FindNextItemForStructure(ent, target, construct, out item)
            : FindNextItemForGhost(ent, target, out item);

    /// <summary>
    /// Finds the next need for an entity that already has a live <see cref="ConstructionComponent"/>,
    /// using <see cref="ConstructionSystem"/>'s own authoritative current node/edge/step
    /// instead of reconstructing them.
    /// </summary>
    private NeedResult FindNextItemForStructure(
        Entity<GoapComponent> ent,
        EntityUid target,
        ConstructionComponent construct,
        out EntityUid item)
    {
        item = default;

        if (_construction.GetCurrentNode(target, construct) is not { } node)
            return NeedResult.Done;

        ConstructionGraphEdge edge;
        int stepIndex;

        if (_construction.GetCurrentEdge(target, construct) is { } currentEdge)
        {
            // We've already entered an edge - construction.StepIndex is authoritative for
            // exactly how far into it we are.
            edge = currentEdge;
            stepIndex = construct.StepIndex;
        }
        else if (construct.TargetEdgeIndex is { } targetEdgeIndex && targetEdgeIndex < node.Edges.Count)
        {
            // Not inside an edge yet, but UpdatePathfinding already picked which of this
            // node's edges we should be taking next - that's the one HandleNode will accept
            // an interaction for once its conditions are satisfied.
            edge = node.Edges[targetEdgeIndex];
            stepIndex = 0;
        }
        else
        {
            // No edge in progress and no pathfinding target chosen yet. This is usually
            // transient (resolves itself once UpdatePathfinding runs) rather than "done".
            return NeedResult.Done;
        }

        var query = _npcHelper.FreeOwnedEntities(ent);

        foreach (var condition in edge.Conditions)
        {
            if (condition.Condition(target, EntityManager))
                continue;

            if (ConditionQuery(query, condition, target) is not { } conditionUid)
                return NeedResult.NotFound;

            item = conditionUid;
            return NeedResult.Found;
        }

        for (;stepIndex < edge.Steps.Count;)
        {
            if (StepQuery(query, edge.Steps[stepIndex]) is not { } stepUid)
                return NeedResult.NotFound;

            item = stepUid;
            return NeedResult.Found;
        }

        return NeedResult.Done;
    }

    /// <summary>
    /// Finds the next need for a construction ghost, which has no live
    /// <see cref="ConstructionComponent"/> (and therefore no authoritative EdgeIndex/StepIndex)
    /// yet - the whole start-to-target path is still ahead of us, so we walk it from the
    /// beginning using the ghost's static prototype data.
    /// </summary>
    private NeedResult FindNextItemForGhost(Entity<GoapComponent> ent, EntityUid target, out EntityUid item)
    {
        item = default;

        if (!TryComp(target, out CommonConstructionGhostComponent? ghost)
            || !_proto.Resolve(ghost.ConstructionProto, out var proto)
            || !_proto.Resolve(proto.Graph, out var graph))
            return NeedResult.Done;

        var path = graph.PathId(proto.StartNode, proto.TargetNode)?.ToList();

        if (path == null)
            return NeedResult.Done;

        path.Insert(0, proto.StartNode);

        var query = _npcHelper.FreeOwnedEntities(ent);

        for (var i = 0; i < path.Count - 1; i++)
        {
            if (graph.Edge(path[i], path[i + 1]) is not { } edge)
                continue;

            foreach (var condition in edge.Conditions)
            {
                if (condition.Condition(target, EntityManager))
                    continue;

                if (ConditionQuery(query, condition, target) is not { } conditionUid)
                    return NeedResult.NotFound;

                item = conditionUid;
                return NeedResult.Found;
            }

            // A ghost hasn't started this edge yet - there's no StepIndex to speak of, so we
            // always begin at step 0 of the first edge with an unmet need.
            foreach (var step in edge.Steps)
            {
                if (StepQuery(query, step) is not { } stepUid)
                    return NeedResult.NotFound;

                item = stepUid;
                return NeedResult.Found;
            }
        }

        return NeedResult.Done;
    }

    private EntityUid? ConditionQuery(List<EntityUid> query, IGraphCondition condition, EntityUid target)
    {
        var conditions = new Queue<IGraphCondition>();
        conditions.Enqueue(condition);

        // Check the conditions; if they are not met, look for an item that can be used to fulfill them.
        while (conditions.TryDequeue(out var con))
        {
            if (con.Condition(target, EntityManager))
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
                    return ToolQuery(query, AnchoringQuality);
                case DoorWelded:
                case StorageWelded:
                    return ToolQuery(query, WeldingQuality);
                case WirePanel:
                    return ToolQuery(query, ScrewingQuality);
                case HasTag tag:
                    return TagQuery(query, new() { tag.Tag }, true);
                case MachineFrameComplete:
                    if (!TryComp(target, out MachineFrameComponent? frame))
                    {
                        ComponentNotFound<MachineFrameComponent>(target);
                        return null;
                    }

                    foreach (var (type, amount) in frame.MaterialRequirements)
                    {
                        if (!frame.MaterialProgress.TryGetValue(type, out var current) || current < amount)
                            return MaterialQuery(query, type, amount);
                    }

                    foreach (var (compName, info) in frame.ComponentRequirements)
                    {
                        if (!frame.ComponentProgress.TryGetValue(compName, out var current) || current < info.Amount)
                            return ComponentQuery(query, compName);
                    }

                    foreach (var (tagName, info) in frame.TagRequirements)
                    {
                        if (!frame.TagProgress.TryGetValue(tagName, out var current) || current < info.Amount)
                            return TagQuery(query, new() { tagName }, true);
                    }

                    break;
                default:
                    CreateDump($"unsupported construction condition: {con}");
                    break;
            }
        }

        return null;
    }

    private EntityUid? StepQuery(List<EntityUid> query, ConstructionGraphStep step)
    {
        switch (step)
        {
            case MaterialConstructionGraphStep insertMaterial:
                return MaterialQuery(query, insertMaterial.MaterialPrototypeId, insertMaterial.Amount);
            case TagConstructionGraphStep insertTag:
                if (insertTag.Tag != null)
                    return TagQuery(query, new() { insertTag.Tag }, true);

                break;
            case MultipleTagsConstructionGraphStep insertMultipleTags:
                if (insertMultipleTags.AnyTag != null)
                    return TagQuery(query, insertMultipleTags.AnyTag, false);

                if (insertMultipleTags.AllTag != null)
                    return TagQuery(query, insertMultipleTags.AllTag, true);

                break;
            case ToolConstructionGraphStep insertTool:
                return ToolQuery(query, insertTool.Tool);
            case ComponentConstructionGraphStep insertComponent:
                return ComponentQuery(query, insertComponent.Component);
            default:
                CreateDump($"unsupported construction step: {step}");
                break;
        }

        return null;
    }

    private EntityUid? TagQuery(
        List<EntityUid> query,
        List<ProtoId<TagPrototype>> tags,
        bool requireAll)
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

        CreateDump($"entity with tags `{string.Join(", ", tags)} (requireAll: {requireAll}) not found`");
        return null;
    }

    private EntityUid? ToolQuery(List<EntityUid> query, ProtoId<ToolQualityPrototype> quality)
    {
        for (var i = 0; i < query.Count; i++)
        {
            var uid = query[i];

            if (!_tool.HasQuality(uid, quality))
                continue;

            query.RemoveAt(i);
            return uid;
        }

        CreateDump($"tool `{quality}` not found");
        return null;
    }

    private EntityUid? ComponentQuery(List<EntityUid> query, string component)
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

        CreateDump($"entity with component `{component}` not found");
        return null;
    }

    private EntityUid? MaterialQuery(List<EntityUid> query, ProtoId<StackPrototype> stack, int amount)
    {
        for (var i = 0; i < query.Count; i++)
        {
            var uid = query[i];

            if (!TryComp(uid, out StackComponent? comp)
                || comp.StackTypeId != stack
                || comp.Count < amount)
                continue;

            query.RemoveAt(i);
            return uid;
        }

        CreateDump($"material `{stack}`, amount: {amount}, not found");
        return null;
    }
}
