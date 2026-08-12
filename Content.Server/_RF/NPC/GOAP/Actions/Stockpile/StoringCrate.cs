using Content.Server._RF.NPC.GOAP.Actions.Interaction;
using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server._RF.NPC.Search.Systems;
using Content.Server._RF.NPC.Systems;
using Content.Server.Interaction;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.Stockpile.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Stockpile;

/// <summary>
/// Handles all the logic for storing large items in the stockpiles.
/// </summary>
public sealed partial class StoringCrate : BaseGoapAction<StoringCrate>
{
    /// <summary>
    /// The target entity to be stored.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// Prototype of a stockpile search query.
    /// </summary>
    [DataField]
    public ProtoId<SearchQueryPrototype> StockQuery = "StockpileToStore";

    /// <summary>
    /// The key under which the found stockpile will be saved.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> StockpileKey = "StoringStockpile";

    /// <summary>
    /// The key under which the coordinates of the storage location will be saved.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> StoringCoordinatesKey = "StoringCoordinates";
}

public sealed class StoringCrateGoapActionSystem : GoapActionSystem<StoringCrate>
{
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly NpcSearcherSystem _searcher = default!;
    [Dependency] private readonly StockpileSystem _stockpile = default!;
    [Dependency] private readonly NpcTimingSystem _npcTiming = default!;
    [Dependency] private readonly MoveToActionSystem _moveTo = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly MovePullingGoapActionSystem _movePulling = default!;

    [Dependency] private readonly EntityQuery<EntityStorageComponent> _storageQuery = default!;
    [Dependency] private readonly EntityQuery<PullerComponent> _pullerQuery = default!;
    [Dependency] private readonly EntityQuery<PullableComponent> _pullableQuery = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, StoringCrate action)
    {
        Remove(ent, action, action.StoringCoordinatesKey);

        if (!TryGetValue(ent, action, action.TargetKey, out _))
            return false;

        _searcher.TryGetBestResult(ent, action.StockQuery, out var result);

        if (!_stockpile.TryGetStock(result, out var stock))
        {
            CreateDump(ent, action, $"stockpile from query '{action.StockQuery}' not found");
            return false;
        }

        var ownerCoords = Transform(ent).Coordinates;

        if (!_stockpile.TryFindClosestTile(stock.Value, ownerCoords, out var tileCoords))
        {
            CreateDump(ent, action, $"free tile not found in stockpile '{ToPrettyString(stock)}'");
            return false;
        }

        Set(ent, action, action.StockpileKey, result.Value);
        Set(ent, action, action.StoringCoordinatesKey, tileCoords.Value);
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, StoringCrate action)
    {
        Remove(ent, action, action.StockpileKey);
        Remove(ent, action, action.StoringCoordinatesKey);
    }

    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, StoringCrate action, GoapPlanFinishReason reason)
    {
        if (TryGetValue(ent, action, action.TargetKey, out var target)
            && _pullableQuery.TryComp(target, out var comp))
            _pulling.TryStopPull(target, comp, ent);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, StoringCrate action)
    {
        var waitResult = _npcTiming.WaitQueue(ent, action);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        if (!TryGetValue(ent, action, action.TargetKey, out var crate)
            || !TryGetValue(ent, action, action.StoringCoordinatesKey, out var coords))
            return GoapActionResult.Failed;

        if (!_storageQuery.TryComp(crate, out var comp))
        {
            ComponentNotFound<EntityStorageComponent>(ent, action, crate);
            return GoapActionResult.Failed;
        }

        if (!_pullerQuery.TryComp(ent, out var puller) || puller.Pulling != crate)
        {
            var result = _moveTo.Move(ent,
                action,
                Transform(crate).Coordinates,
                GoapState.InteractRange);

            if (result != GoapActionResult.Finished)
                return result;

            if (!_pulling.TryStartPull(ent, crate))
            {
                CreateDump(ent, action, $"failer to start pulling {ToPrettyString(crate)}");
                return GoapActionResult.Failed;
            }

            if (!_pullerQuery.TryComp(ent, out puller))
            {
                ComponentNotFound<PullerComponent>(ent, action); // Wtf
                return GoapActionResult.Failed;
            }

            // TODO: Containers may be locked, so the logic for opening them should be moved to a separate system where this can be handled.
            if (!comp.Open)
            {
                return _npcTiming.EnqueueWait(ent,
                    action,
                    (0.33f, 1f),
                    onFinish: () =>
                    {
                        _interaction.InteractionActivate(ent, crate);

                        if (comp.Open)
                            return true;

                        CreateDump(ent, action, $"failed to open {ToPrettyString(crate)}");
                        return false;
                    });
            }
        }

        var move = _moveTo.Move(ent, action, coords, GoapState.InteractRange);

        if (move != GoapActionResult.Finished)
            return move;

        var pulling = _movePulling.UpdatePulling(ent, action, coords);

        if (pulling != GoapActionResult.Finished)
            return pulling;

        _interaction.InteractionActivate(ent, crate);

        if (comp.Open)
        {
            CreateDump(ent, action, $"failed to close {ToPrettyString(crate)}");
            return GoapActionResult.Failed;
        }

        if (!_pullableQuery.TryComp(crate, out var pullable))
        {
            ComponentNotFound<PullableComponent>(ent, action, crate);
            return GoapActionResult.Failed;
        }

        if (!_pulling.TryStopPull(crate, pullable, ent))
        {
            CreateDump(ent, action, $"failed to stop pulling {ToPrettyString(crate)}");
            return GoapActionResult.Failed;
        }

        return GoapActionResult.Finished;
    }
}
