using Robust.Shared.Map;

namespace Content.Shared._RF.Zoning.Systems;

/// <summary>
/// System handles the logic of a <typeparamref name="T"/> zone condition.
/// </summary>
/// <typeparam name="T">Condition type.</typeparam>
public abstract class ZoningConditionSystem<T> : EntitySystem where T : BaseZoneCondition<T>
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAddTileCheck<T>>(OnZoneAddTileCheck);
        SubscribeLocalEvent<ZoneTileCheck<T>>(OnZoneTileCheck);
        SubscribeLocalEvent<ZoneEntityCheck<T>>(OnZoneEntityCheck);
        SubscribeLocalEvent<GetZoneConditionDescription<T>>(OnGetZoneConditionDescription);
    }

    private void OnZoneAddTileCheck(ref ZoneAddTileCheck<T> args)
    {
        var result = TileValidCheck(args.Condition, args.Tile);
        args.Result = result;
    }

    private void OnZoneTileCheck(ref ZoneTileCheck<T> args)
    {
        var result = TileCheck(args.Condition, args.Tiles);
        args.Result = result;
    }

    private void OnZoneEntityCheck(ref ZoneEntityCheck<T> args)
    {
        var result = EntityCheck(args.Condition, args.Entities);
        args.Result = result;
    }

    private void OnGetZoneConditionDescription(ref GetZoneConditionDescription<T> args)
    {
        var result = ConditionDescription(args.Condition, args.Tile, args.Tiles, args.Entities);
        args.Result = result;
    }

    /// <inheritdoc cref="ZoneCondition.TileValidCheck"/>
    protected virtual bool TileValidCheck(T condition, TileRef tile)
    {
        return false;
    }

    /// <inheritdoc cref="ZoneCondition.TileCheck"/>
    protected virtual bool TileCheck(T condition, IReadOnlySet<TileRef> tiles)
    {
        return false;
    }

    /// <inheritdoc cref="ZoneCondition.EntityCheck"/>
    protected virtual bool EntityCheck(T condition, IReadOnlySet<EntityUid> entities)
    {
        return false;
    }

    /// <inheritdoc cref="ZoneCondition.ConditionDescription"/>
    protected abstract ZoneConditionDesc ConditionDescription(
        T condition,
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities);
}
