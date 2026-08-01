using System.Linq;
using Content.Server._RF.NPC.GOAP.Actions.Interaction;
using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server._RF.NPC.Systems;
using Content.Server.Construction.Components;
using Content.Server.Construction.Conditions;
using Content.Server.Interaction;
using Content.Server.NPC.Pathfinding;
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
using Content.Shared.Tools.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

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

    /// <summary>
    /// Set right after an interaction finishes, holding the tick at which it's safe to look
    /// for the next need again. <see cref="Content.Server.Construction.ConstructionSystem"/>
    /// applies an interaction's completion (deleting/storing the material, advancing
    /// StepIndex, possibly changing node) through a queued event rather than synchronously
    /// when the doAfter reports <c>Finished</c>, so there can be a short lag before the
    /// construction graph actually reflects what was just done. Without this pause,
    /// <see cref="NpcConstructionSystem.FindNextItem"/> can run against stale graph state one
    /// tick too early.
    /// </summary>
    public readonly StateKey<uint> ResumeAtTickKey = "ConstructionResumeAtTick";

    public readonly StateKey<GoapActionResult> LastWaitResultKey = "LastWaitResult";
}

public sealed class NpcConstructionSystem : GoapActionSystem<Construction>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ToolSystem _tool = default!;
    [Dependency] private readonly NpcHelperSystem _npcHelper = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly InteractWithSystem _interactWith = default!;
    [Dependency] private readonly MoveToSystem _moveTo = default!;
    [Dependency] private readonly PickupActionSystem _pickup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ProtoId<ToolQualityPrototype> AnchoringQuality = "Anchoring";
    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";

    /// <summary>
    /// Number of ticks to wait after an interaction finishes before looking for the next
    /// need, to give ConstructionSystem's queued completion processing time to actually apply
    /// the previous step's effects. See the remarks on <see cref="Construction.ResumeAtTickKey"/>.
    /// </summary>
    private const uint SettleTicks = 3;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Construction action) => 3f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Construction action)
    {
        ent.Comp.State.Remove(action.CurrentDoAfter);
        ent.Comp.State.Remove(action.CurrentItemKey);
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Construction action)
    {
        ent.Comp.State.Remove(action.CurrentDoAfter);
        ent.Comp.State.Remove(action.CurrentItemKey);
        _moveTo.ShutdownMovement(ent, action.PathfindKey);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Construction action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return GoapActionResult.Failed;

        // Give ConstructionSystem's queued interaction-completion processing (deleting/storing
        // the material, advancing StepIndex, possibly changing node) a couple of ticks to
        // actually land before we ask what's needed next - see ResumeAtTickKey's remarks.
        if (TryGetValue(ent, action, action.ResumeAtTickKey, out var resumeAtTick))
        {
            if (_timing.CurTick.Value < resumeAtTick)
                return GoapActionResult.Continuing;

            ent.Comp.State.Remove(action.ResumeAtTickKey);
        }

        var waitResult = _interactWith.Wait(ent, action, action.CurrentDoAfter, out _);

        if (TryGetValue(ent, action, action.LastWaitResultKey, out var lastWait)
            && waitResult != lastWait
            && waitResult == GoapActionResult.Finished)
        {
            ent.Comp.State.SetValue(action.ResumeAtTickKey, _timing.CurTick.Value + SettleTicks);
            ent.Comp.State.Remove(action.LastWaitResultKey);
            CreateDump(ent, action, $"started waiting for tick {_timing.CurTick.Value + SettleTicks}");
            return GoapActionResult.Continuing;
        }

        ent.Comp.State.SetValue(action.LastWaitResultKey, waitResult);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        // No item locked in yet for this round - figure out fresh, from the target's
        // current construction state, what's needed next.
        if (!TryGetValue(ent, action, action.CurrentItemKey, out var item))
        {
            var needResult = FindNextItem(ent, action, target, out var found);

            switch (needResult)
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
            CreateDump(ent, action, $"{ToPrettyString(item)} not exist");
            return GoapActionResult.Failed;
        }

        var itemCoords = Transform(item).Coordinates;
        var ownerCoords = Goap.GetValue(ent.Comp.State, GoapState.OwnerCoordinates);
        var targetCoords = Transform(target).Coordinates;
        float distance;

        if (!TryGetValue(ent, action, GoapState.ActiveHandEntity, out var heldEnt) || heldEnt != item)
        {
            if (!TryGetValue(ent, action, action.RangeKey, out var range)
                || !ownerCoords.TryDistance(EntityManager, itemCoords, out distance))
                return GoapActionResult.Failed;

            // Movement
            if (distance > range)
            {
                if (!_moveTo.StartedUp(ent))
                {
                    CreateDump(ent, action, $"started moving toward the item: {ToPrettyString(item)}");
                    _moveTo.StartupMovement(ent, action, itemCoords, true, action.PathfindKey, action.RangeKey, false);
                }

                var result = _moveTo.UpdateMovement(ent, action, itemCoords, action.PathfindKey, action.RangeKey, false);

                if (result != GoapActionResult.Finished)
                    return result;
            }
            else if (_moveTo.StartedUp(ent))
                _moveTo.ShutdownMovement(ent, action.PathfindKey);

            // Pick up the item
            if (!_pickup.Pickup(ent, item, action))
                return GoapActionResult.Failed;

            CreateDump(ent, action, $"{ToPrettyString(item)} picked up");

            // Turn on welder
            if (TryComp(item, out WelderComponent? welder) && !welder.Enabled)
            {
                CreateDump(ent, action, "turning on welder");
                _interaction.UserInteraction(ent, Transform(item).Coordinates, item);
            }
        }

        if (ownerCoords.TryDistance(EntityManager, targetCoords, out distance)
            && distance > Goap.GetValue(ent.Comp.State, GoapState.InteractRange))
        {
            if (!_moveTo.StartedUp(ent))
            {
                CreateDump(ent, action, $"started moving toward the target: {ToPrettyString(target)}");
                _moveTo.StartupMovement(ent, action, targetCoords, true, action.PathfindKey, action.RangeKey, false);
            }

            var result = _moveTo.UpdateMovement(ent, action, targetCoords, action.PathfindKey, action.RangeKey, false);

            if (result != GoapActionResult.Finished)
                return result;
        }
        else if (_moveTo.StartedUp(ent))
            _moveTo.ShutdownMovement(ent, action.PathfindKey);

        var interactResult = _interactWith.DoInteraction(ent, action, target, action.CurrentDoAfter, true);

        if (interactResult != GoapActionResult.Finished)
            return interactResult;

        return AdvanceToNextItem(ent, action);
    }

    /// <summary>
    /// Called once the interaction for the current item has finished successfully. Clears
    /// both the per-item doAfter tracking and the current-item lock, so the next Update call
    /// starts a fresh interaction and re-derives what's needed next from the target's
    /// (possibly now different) construction state.
    /// </summary>
    private GoapActionResult AdvanceToNextItem(Entity<GoapComponent> ent, Construction action)
    {
        ent.Comp.State.Remove(action.CurrentDoAfter);
        ent.Comp.State.Remove(action.CurrentItemKey);
        return GoapActionResult.Continuing;
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
    /// Looks at the target's construction graph exactly as it stands right now and returns
    /// the single next thing needed: the first unmet condition of the first relevant edge,
    /// or (if all of that edge's conditions are met) the next unapplied step of that edge.
    /// Unlike the old upfront batch collection, this never looks further ahead than the very
    /// next actionable need, so it can't go stale as soon as the target advances.
    /// </summary>
    private NeedResult FindNextItem(Entity<GoapComponent> ent, Construction action, EntityUid target, out EntityUid item)
    {
        item = default;
        var edges = GetEdges(target);

        if (edges.Count == 0)
            return NeedResult.Done;

        var query = _npcHelper.FreeOwnedEntities(ent);

        foreach (var edge in edges)
        {
            foreach (var condition in edge.Conditions)
            {
                if (condition.Condition(target, EntityManager))
                    continue;

                if (ConditionQuery(ent, action, query, condition, target) is not { } conditionUid)
                    return NeedResult.NotFound;

                item = conditionUid;
                return NeedResult.Found;
            }

            var stepIndex = TryComp(target, out ConstructionComponent? construct) ? construct.StepIndex : 0;

            for (var i = stepIndex; i < edge.Steps.Count; i++)
            {
                if (StepQuery(ent, action, query, edge.Steps[i]) is not { } stepUid)
                    return NeedResult.NotFound;

                item = stepUid;
                return NeedResult.Found;
            }
        }

        return NeedResult.Done;
    }

    private EntityUid? ConditionQuery(
        Entity<GoapComponent> ent,
        Construction action,
        List<EntityUid> query,
        IGraphCondition condition,
        EntityUid target)
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
                    return ToolQuery(ent, action, query, AnchoringQuality);
                case DoorWelded:
                case StorageWelded:
                    return ToolQuery(ent, action, query, WeldingQuality);
                case WirePanel:
                    return ToolQuery(ent, action, query, ScrewingQuality);
                case HasTag tag:
                    return TagQuery(ent, action, query, new() { tag.Tag }, true);
                case MachineFrameComplete:
                    if (!TryComp(target, out MachineFrameComponent? frame))
                    {
                        ComponentNotFound<MachineFrameComponent>(ent, action, target);
                        return null;
                    }

                    foreach (var (type, amount) in frame.MaterialRequirements)
                    {
                        if (!frame.MaterialProgress.TryGetValue(type, out var current) || current < amount)
                            return MaterialQuery(ent, action, query, type, amount);
                    }

                    foreach (var (compName, info) in frame.ComponentRequirements)
                    {
                        if (!frame.ComponentProgress.TryGetValue(compName, out var current) || current < info.Amount)
                            return ComponentQuery(ent, action, query, compName);
                    }

                    foreach (var (tagName, info) in frame.TagRequirements)
                    {
                        if (!frame.TagProgress.TryGetValue(tagName, out var current) || current < info.Amount)
                            return TagQuery(ent, action, query, new() { tagName }, true);
                    }

                    break;
                default:
                    CreateDump(ent, action, $"unsupported construction condition: {con}");
                    break;
            }
        }

        return null;
    }

    private EntityUid? StepQuery(
        Entity<GoapComponent> ent,
        Construction action,
        List<EntityUid> query,
        ConstructionGraphStep step)
    {
        switch (step)
        {
            case MaterialConstructionGraphStep insertMaterial:
                return MaterialQuery(ent, action, query, insertMaterial.MaterialPrototypeId, insertMaterial.Amount);
            case TagConstructionGraphStep insertTag:
                if (insertTag.Tag != null)
                    return TagQuery(ent, action, query, new() { insertTag.Tag }, true);

                break;
            case MultipleTagsConstructionGraphStep insertMultipleTags:
                if (insertMultipleTags.AnyTag != null)
                    return TagQuery(ent, action, query, insertMultipleTags.AnyTag, false);

                if (insertMultipleTags.AllTag != null)
                    return TagQuery(ent, action, query, insertMultipleTags.AllTag, true);

                break;
            case ToolConstructionGraphStep insertTool:
                return ToolQuery(ent, action, query, insertTool.Tool);
            case ComponentConstructionGraphStep insertComponent:
                return ComponentQuery(ent, action, query, insertComponent.Component);
            default:
                CreateDump(ent, action, $"unsupported construction step: {step}");
                break;
        }

        return null;
    }

    private EntityUid? TagQuery(
        Entity<GoapComponent> ent,
        Construction action,
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

        CreateDump(ent, action, $"entity with tags `{string.Join(", ", tags)} (requireAll: {requireAll}) not found`");
        return null;
    }

    private EntityUid? ToolQuery(
        Entity<GoapComponent> ent,
        Construction action,
        List<EntityUid> query,
        ProtoId<ToolQualityPrototype> quality)
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
        return null;
    }

    private EntityUid? ComponentQuery(
        Entity<GoapComponent> ent,
        Construction action,
        List<EntityUid> query,
        string component)
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
        return null;
    }

    private EntityUid? MaterialQuery(
        Entity<GoapComponent> ent,
        Construction action,
        List<EntityUid> query,
        ProtoId<StackPrototype> stack,
        int amount)
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

        CreateDump(ent, action, $"material `{stack}`, amount: {amount}, not found");
        return null;
    }

    /// <summary>
    /// Returns all edges to complete the construction of the target entity, computed fresh
    /// from the target's LIVE construction state every time it's called (never cached across
    /// calls) - as soon as a step advances <see cref="ConstructionComponent.StepIndex"/> or
    /// the current node, the very next call reflects that automatically.
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
