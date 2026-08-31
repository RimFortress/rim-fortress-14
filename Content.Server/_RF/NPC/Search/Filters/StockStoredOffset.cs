using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared.Maps;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities that are within a specified distance from the center of the tile.
/// </summary>
public sealed partial class StockStoredOffset : BaseSearchFilter<StockStoredOffset>
{
    /// <summary>
    /// Entities that are farther away from the center of the tile will be filtered out.
    /// </summary>
    [DataField]
    public float? MoreThan;

    /// <summary>
    /// Entities that are less distant from the center of the tile will be filtered out.
    /// </summary>
    [DataField]
    public float? LessThan;
}

public sealed class TileOffsetSearchFilterSystem : NpcSearchFilterSystem<StockStoredOffset>
{
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StockpileContentComponent, MoveEvent>((ent, ref _) => DirtyFilter(ent.Owner));
    }

    protected override bool Filter(GoapState state, EntityUid target, StockStoredOffset filter)
    {
        var coords = Transform(target).Coordinates;

        if (!_turf.TryGetTileRef(coords, out var tileRef)
            || !coords.TryDistance(EntityManager, _turf.GetTileCenter(tileRef.Value), out var dist))
            return false;

        return dist > filter.MoreThan || dist < filter.LessThan;
    }
}
