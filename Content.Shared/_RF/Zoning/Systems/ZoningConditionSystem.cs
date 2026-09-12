using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Systems;

/// <summary>
/// System handles the logic of a <typeparamref name="T"/> zone condition.
/// </summary>
/// <typeparam name="T">Condition type.</typeparam>
public abstract partial class ZoningConditionSystem<T> : EntitySystem where T : BaseZoneCondition<T>
{
    [Dependency] protected ZoningSystem Zoning = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAddTileCheck<T>>(OnZoneAddTileCheck);
        SubscribeLocalEvent<ZoneTileCheck<T>>(OnZoneTileCheck);
        SubscribeLocalEvent<ZoneEntityCheck<T>>(OnZoneEntityCheck);
        SubscribeLocalEvent<GetZoneTileAddConditionDescription<T>>(OnGetZoneTileAddConditionDescription);
        SubscribeLocalEvent<GetZoneTileConditionDescription<T>>(OnGetZoneTileConditionDescription);
        SubscribeLocalEvent<GetZoneEntityConditionDescription<T>>(OnGetZoneEntityConditionDescription);
    }

    private void OnZoneAddTileCheck(ref ZoneAddTileCheck<T> args)
    {
        DebugTools.Assert(args.Condition.Amount != 0);
        var result = TileValidCheck(args.Condition, args.Tile);
        args.Result = result;
    }

    private void OnZoneTileCheck(ref ZoneTileCheck<T> args)
    {
        DebugTools.Assert(args.Condition.Amount != 0);
        var amount = args.Condition.Invert
            ? args.Tiles.Count - args.Condition.Amount + 1
            : args.Condition.Amount;

        if (amount <= 0)
        {
            args.Result = true;
            return;
        }

        var result = TileCheck(args.Condition, args.Tiles, amount);
        args.Result = result;
    }

    private void OnZoneEntityCheck(ref ZoneEntityCheck<T> args)
    {
        DebugTools.Assert(args.Condition.Amount != 0);
        var amount = args.Condition.Invert
            ? args.Entities.Count - args.Condition.Amount + 1
            : args.Condition.Amount;

        if (amount <= 0)
        {
            args.Result = true;
            return;
        }

        var result = EntityCheck(args.Condition, args.Entities, amount);
        args.Result = result;
    }

    private void OnGetZoneTileAddConditionDescription(ref GetZoneTileAddConditionDescription<T> args)
    {
        var result = ConditionDescription(args.Condition, args.Tile);
        args.Result = result;
    }

    private void OnGetZoneTileConditionDescription(ref GetZoneTileConditionDescription<T> args)
    {
        var result = ConditionDescription(args.Condition, args.Tiles);
        args.Result = result;
    }

    private void OnGetZoneEntityConditionDescription(ref GetZoneEntityConditionDescription<T> args)
    {
        var result = ConditionDescription(args.Condition, args.Entities);
        args.Result = result;
    }

    /// <inheritdoc cref="ZoneCondition.TileValidCheck"/>
    protected virtual bool TileValidCheck(T condition, TileRef tile) => false;

    /// <inheritdoc cref="ZoneCondition.TileCheck"/>
    protected virtual bool TileCheck(T condition, IReadOnlySet<TileRef> tiles, int amount) => false;

    /// <inheritdoc cref="ZoneCondition.EntityCheck"/>
    protected virtual bool EntityCheck(T condition, IReadOnlySet<EntityUid> entities, int amount) => false;

    /// <inheritdoc cref="ZoneCondition.ConditionDescription(TileRef, IZoneConditionChecker)"/>
    protected virtual ZoneConditionDesc ConditionDescription(T condition, TileRef tile) => new();

    /// <inheritdoc cref="ZoneCondition.ConditionDescription(IReadOnlySet{TileRef}, IZoneConditionChecker)"/>
    protected virtual ZoneConditionDesc ConditionDescription( T condition, IReadOnlySet<TileRef> tiles) => new();

    /// <inheritdoc cref="ZoneCondition.ConditionDescription(IReadOnlySet{EntityUid}, IZoneConditionChecker)"/>
    protected virtual ZoneConditionDesc ConditionDescription(T condition, IReadOnlySet<EntityUid> entities) => new();
}
