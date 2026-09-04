using Content.Server._RF.NPC.GOAP.Actions.Interaction;
using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Server._RF.NPC.Search.Systems;
using Content.Server._RF.NPC.Systems;
using Content.Server.Interaction;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
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

public sealed partial class StoringCrateGoapActionSystem : GoapActionSystem<StoringCrate>
{
    [Dependency] private InteractionSystem _interaction = default!;
    [Dependency] private NpcSearcherSystem _searcher = default!;
    [Dependency] private StockpileSystem _stockpile = default!;
    [Dependency] private NpcTimingSystem _npcTiming = default!;
    [Dependency] private MoveToActionSystem _moveTo = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private MovePullingGoapActionSystem _movePulling = default!;

    [Dependency] private EntityQuery<EntityStorageComponent> _storageQuery;
    [Dependency] private EntityQuery<PullerComponent> _pullerQuery;
    [Dependency] private EntityQuery<PullableComponent> _pullableQuery;

    protected override bool ActionStartup(Entity<GoapComponent> ent, StoringCrate action)
    {
        Remove(ent, action.StoringCoordinatesKey);

        if (!TryGet(ent, action.TargetKey, out _))
            return false;

        _searcher.TryGetBestResult(ent, action.StockQuery, out var result);

        if (!_stockpile.TryGetStock(result, out var stock))
        {
            CreateDump($"stockpile from query '{action.StockQuery}' not found");
            return false;
        }

        var ownerCoords = Transform(ent).Coordinates;

        if (!_stockpile.TryFindClosestTile(stock.Value, ownerCoords, out var ind, out var tileCoords))
        {
            CreateDump($"free tile not found in stockpile '{ToPrettyString(stock)}'");
            return false;
        }

        if (!_stockpile.ReserveTile(stock.Value, ind.Value, ent))
        {
            CreateDump($"failed to reserve tile {ind} in stock {ToPrettyString(stock)}");
            return false;
        }

        Set(ent, action.StockpileKey, result.Value);
        Set(ent, action.StoringCoordinatesKey, tileCoords.Value);
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, StoringCrate action)
    {
        if (Remove(ent, action.StockpileKey, out var uid)
            && _stockpile.TryGetStock(uid, out var stock))
            StockpileSystem.ClearReserve(stock.Value, ent);

        Remove(ent, action.StoringCoordinatesKey);
        NpcTimingSystem.ClearQueue(ent);
    }

    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, StoringCrate action, GoapPlanFinishReason reason)
    {
        if (TryGet(ent, action.TargetKey, out var target)
            && _pullableQuery.TryComp(target, out var comp))
            _pulling.TryStopPull(target, comp, ent);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, StoringCrate action)
    {
        var waitResult = _npcTiming.WaitQueue(ent, this);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        if (!TryGet(ent, action.TargetKey, out var crate)
            || !TryGet(ent, action.StoringCoordinatesKey, out var coords))
            return GoapActionResult.Failed;

        if (!_storageQuery.TryComp(crate, out var comp))
        {
            ComponentNotFound<EntityStorageComponent>(crate);
            return GoapActionResult.Failed;
        }

        if (!_pullerQuery.TryComp(ent, out var puller))
        {
            ComponentNotFound<PullerComponent>();
            return GoapActionResult.Failed;
        }

        if (!_pullableQuery.TryComp(crate, out var pullable))
        {
            ComponentNotFound<PullableComponent>(crate);
            return GoapActionResult.Failed;
        }

        if (pullable.Puller != null && pullable.Puller != ent)
        {
            CreateDump($"{ToPrettyString(crate)} currently pulled by other entity: {ToPrettyString(pullable.Puller)}");
            return GoapActionResult.Failed;
        }

        if (puller.Pulling != crate)
        {
            if (puller.Pulling != null)
            {
                if (!_pullableQuery.TryComp(puller.Pulling, out var pullingComp))
                {
                    ComponentNotFound<PullableComponent>(puller.Pulling);
                    return GoapActionResult.Failed;
                }

                if (!_pulling.TryStopPull(puller.Pulling.Value, pullingComp, ent))
                {
                    CreateDump($"failed to stop pulling {ToPrettyString(puller.Pulling)}");
                    return GoapActionResult.Failed;
                }
            }

            var result = _moveTo.Move(ent,
                this,
                Transform(crate).Coordinates,
                GoapState.InteractRange);

            if (result != GoapActionResult.Finished)
                return result;

            if (!_pulling.TryStartPull(ent, crate))
            {
                CreateDump($"failed to start pulling {ToPrettyString(crate)}");
                return GoapActionResult.Failed;
            }

            if (!_pullerQuery.TryComp(ent, out puller))
            {
                ComponentNotFound<PullerComponent>(); // Wtf
                return GoapActionResult.Failed;
            }

            // TODO: Containers may be locked, so the logic for opening them should be moved to a separate system where this can be handled.
            if (!comp.Open)
            {
                return _npcTiming.EnqueueWait(ent,
                    this,
                    (0.33f, 1f),
                    onFinish: () =>
                    {
                        _interaction.InteractionActivate(ent, crate);

                        if (comp.Open)
                            return true;

                        CreateDump($"failed to open {ToPrettyString(crate)}");
                        return false;
                    });
            }
        }

        var move = _moveTo.Move(ent, this, coords, GoapState.InteractRange);

        if (move != GoapActionResult.Finished)
            return move;

        var pulling = _movePulling.UpdatePulling(ent, this, coords);

        if (pulling != GoapActionResult.Finished)
            return pulling;

        _interaction.InteractionActivate(ent, crate);

        if (comp.Open)
        {
            CreateDump($"failed to close {ToPrettyString(crate)}");
            return GoapActionResult.Failed;
        }

        if (!_pulling.TryStopPull(crate, pullable, ent))
        {
            CreateDump($"failed to stop pulling {ToPrettyString(crate)}");
            return GoapActionResult.Failed;
        }

        return GoapActionResult.Finished;
    }
}
