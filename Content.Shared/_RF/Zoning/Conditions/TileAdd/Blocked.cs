using Content.Shared._RF.Zoning.Systems;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Conditions.TileAdd;

/// <summary>
/// Checks whether the tile is blocked.
/// </summary>
public sealed partial class Blocked : BaseZoneCondition<Blocked>
{
    public override ZoneConditionType Type => ZoneConditionType.TileAdd;

    /// <summary>
    /// Collision groups that will be used to check for blocking.
    /// </summary>
    [DataField]
    public CollisionGroup Mask = CollisionGroup.Impassable | CollisionGroup.HighImpassable;
}

public sealed partial class BlockedZoneConditionSystem : ZoningConditionSystem<Blocked>
{
    [Dependency] private TurfSystem _turf = default!;

    protected override bool TileValidCheck(Blocked condition, TileRef tile)
        => _turf.IsTileBlocked(tile, condition.Mask);

    protected override ZoneConditionDesc ConditionDescription(
        Blocked condition,
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities)
    {
        DebugTools.AssertNotNull(tile);
        return new ZoneConditionDesc(
            Loc.GetString("zone-condition-blocked-desc", ("invert", condition.Invert)),
            TileValidCheck(condition, tile!.Value));
    }
}
