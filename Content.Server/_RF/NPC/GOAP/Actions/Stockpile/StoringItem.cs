using Content.Server._RF.NPC.GOAP.Actions.Interaction;
using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Server._RF.NPC.Search.Systems;
using Content.Server._RF.NPC.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Interaction;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Stockpile;

/// <summary>
/// Handles all the logic for transferring small, handheld items to the stockpile by an agent.
/// </summary>
public sealed partial class StoringItem : BaseGoapAction<StoringItem>
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

    /// <summary>
    /// The key in which the storage container will be saved.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> StoringCrateKey = "StoringCrate";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = "MovementPathfinding";
}

public sealed partial class StoringItemGoapActionSystem : GoapActionSystem<StoringItem>
{
    [Dependency] private InteractionSystem _interaction = default!;
    [Dependency] private NpcSearcherSystem _searcher = default!;
    [Dependency] private StockpileSystem _stockpile = default!;
    [Dependency] private NpcTimingSystem _npcTiming = default!;
    [Dependency] private MoveToActionSystem _moveTo = default!;
    [Dependency] private PickupActionSystem _pickup = default!;
    [Dependency] private HandsSystem _hands = default!;

    [Dependency] private EntityQuery<EntityStorageComponent> _storageQuery;
    [Dependency] private EntityQuery<StockpileContentComponent> _contentQuery;

    protected override bool ActionStartup(Entity<GoapComponent> ent, StoringItem action)
    {
        Remove(ent, action.StoringCoordinatesKey);
        Remove(ent, action.StoringCrateKey);
        Remove(ent, action.PathfindKey);

        if (!TryGet(ent, action.TargetKey, out var target))
            return false;

        _searcher.TryGetBestResult(ent, action.StockQuery, out var result);

        if (!_stockpile.TryGetStock(result, out var stock))
        {
            CreateDump($"stockpile from query '{action.StockQuery}' not found");
            return false;
        }

        var ownerCoords = Transform(ent).Coordinates;

        if (_stockpile.TryFindClosestContainerToInsert(stock.Value, ownerCoords, target, out var container))
        {
            if (!_stockpile.ReserveEntity(stock.Value, container.Value, ent))
            {
                CreateDump($"failed to reserve entity {ToPrettyString(container)} in stock {ToPrettyString(stock)}");
                return false;
            }

            Set(ent, action.StockpileKey, result.Value);
            Set(ent, action.StoringCrateKey, container.Value);
            return true;
        }

        CreateDump($"free entity storage not found in stockpile '{ToPrettyString(stock)}'");

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

    protected override void ActionShutdown(Entity<GoapComponent> ent, StoringItem action)
    {
        if (Remove(ent, action.StockpileKey, out var uid)
            && _stockpile.TryGetStock(uid, out var stock))
            StockpileSystem.ClearReserve(stock.Value, ent);

        Remove(ent, action.StoringCoordinatesKey);
        Remove(ent, action.StoringCrateKey);
        Remove(ent, action.PathfindKey);
        NpcTimingSystem.ClearQueue(ent);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, StoringItem action)
    {
        var waitResult = _npcTiming.WaitQueue(ent, this);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        if (!TryGet(ent, action.TargetKey, out var item))
            return GoapActionResult.Failed;

        if (_contentQuery.HasComp(item))
            return GoapActionResult.Finished;

        if (!TryGet(ent, GoapState.ActiveHandEntity, out var held) || held != item)
        {
            var move = _moveTo.Move(ent, this, Transform(item).Coordinates, GoapState.InteractRange);

            if (move != GoapActionResult.Finished)
                return move;

            if (!_pickup.Pickup(ent, item, action))
                return GoapActionResult.Failed;
        }

        if (TryGet(ent, action.StoringCrateKey, out var crate))
            return CrateUpdate(ent, item, crate);

        if (TryGet(ent, action.StoringCoordinatesKey, out var coords))
            return TileUpdate(ent, item, coords);

        return GoapActionResult.Failed;
    }

    private GoapActionResult CrateUpdate(Entity<GoapComponent> ent, EntityUid item, EntityUid crate)
    {
        if (!_storageQuery.TryComp(crate, out var comp))
        {
            ComponentNotFound<EntityStorageComponent>(crate);
            return GoapActionResult.Failed;
        }

        var coords = Transform(crate).Coordinates;

        var result = _moveTo.Move(ent, this, coords, GoapState.InteractRange);

        if (result != GoapActionResult.Finished)
            return result;

        // TODO: Containers may be locked, so the logic for opening them should be moved to a separate system where this can be handled.
        if (!comp.Open)
        {
            _npcTiming.EnqueueWait(ent,
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

        _npcTiming.EnqueueWait(ent,
            this,
            (0.33f, 1f),
            onFinish: () =>
            {
                if (_hands.TryDrop(ent.Owner, coords))
                    return true;

                CreateDump($"failed to drop {ToPrettyString(item)} in {ToPrettyString(crate)}");
                return false;
            });

        _npcTiming.EnqueueWait(ent,
            this,
            (0.33f, 1f),
            onFinish: () =>
            {
                _interaction.InteractionActivate(ent, crate);

                if (!comp.Open)
                    return true;

                CreateDump($"failed to close {ToPrettyString(crate)}");
                return false;
            });

        return GoapActionResult.Continuing;
    }

    private GoapActionResult TileUpdate(
        Entity<GoapComponent> ent,
        EntityUid item,
        EntityCoordinates coords)
    {
        var result = _moveTo.Move(ent, this, coords, GoapState.InteractRange);

        if (result != GoapActionResult.Finished)
            return result;

        if (_hands.TryDrop(ent.Owner, coords))
            return GoapActionResult.Finished;

        CreateDump($"failed to drop {ToPrettyString(item)} at {coords}");
        return GoapActionResult.Failed;
    }
}
