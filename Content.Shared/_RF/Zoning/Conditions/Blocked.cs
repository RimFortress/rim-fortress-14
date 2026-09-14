using System.Linq;
using Content.Shared._RF.Zoning.Systems;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Conditions;

/// <summary>
/// Checks whether the tile is blocked.
/// </summary>
public sealed partial class Blocked : BaseZoneCondition<Blocked>
{
    public override ZoneConditionType Type => ZoneConditionType.TileAdd | ZoneConditionType.Tile;

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

    protected override bool TileCheck(Blocked condition, IReadOnlySet<TileRef> tiles, int amount)
    {
        DebugTools.Assert(condition.Amount != 0);

        var count = 0;

        foreach (var tile in tiles)
        {
            if (!_turf.IsTileBlocked(tile, condition.Mask))
                continue;

            count++;

            if (count >= amount)
                return true;
        }

        return false;
    }

    protected override ZoneConditionDesc ConditionDescription(Blocked condition, IReadOnlySet<TileRef> tiles)
    {
        var blocked = tiles.Count(tile => _turf.IsTileBlocked(tile, condition.Mask));

        // Progress should track whatever the localized text is actually describing:
        // blocked-tile count for "are blocked", not-blocked count for "are not blocked".
        var count = condition.Invert ? tiles.Count - blocked : blocked;

        return new(
            Loc.GetString("zone-condition-blocked-tile-desc",
                ("amount", condition.Amount),
                ("invert", condition.Invert)),
            condition.TileCheck(tiles, Zoning),
            Progress: ZoneConditionDesc.ProgressText(count, condition.Amount));
    }

    protected override ZoneConditionDesc ConditionDescription(Blocked condition, TileRef tile)
    {
        return new ZoneConditionDesc(
            Loc.GetString("zone-condition-blocked-tile-add-desc", ("invert", condition.Invert)),
            condition.TileValidCheck(tile, Zoning));
    }
}
