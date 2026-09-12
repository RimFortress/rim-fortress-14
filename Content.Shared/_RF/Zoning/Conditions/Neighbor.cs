using System.Linq;
using System.Numerics;
using Content.Shared._RF.Zoning.Systems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Conditions;

/// <summary>
/// Checks the conditions relative to the tiles neighboring with the target tile.
/// </summary>
public sealed partial class Neighbor : BaseZoneCondition<Neighbor>
{
    public override ZoneConditionType Type => ZoneConditionType.TileAdd;

    /// <summary>
    /// Conditions list.
    /// </summary>
    [DataField(required: true)]
    public List<ZoneCondition> Conditions = new();
}

public sealed partial class NeighborZoneConditionSystem : ZoningConditionSystem<Neighbor>
{
    [Dependency] private TurfSystem _turf = default!;

    private static readonly Vector2[] Directions = [Vector2i.Left, Vector2i.Up, Vector2i.Right, Vector2i.Down];

    protected override bool TileValidCheck(Neighbor condition, TileRef tile)
    {
        DebugTools.Assert(condition.Amount != 0);
        DebugTools.Assert(condition.Amount <= 4, "there can't be more than 4 neighboring tiles.");

        var coords = _turf.GetTileCenter(tile);
        var count = 0;

        foreach (var dir in Directions)
        {
            if (Check(condition, coords, dir))
                count++;

            if (count >= condition.Amount)
                return true;
        }

        return false;
    }

    private bool Check(Neighbor condition, EntityCoordinates coords, Vector2 offset)
        => _turf.TryGetTileRef(new EntityCoordinates(coords.EntityId, coords.Position + offset), out var tileRef)
           && Zoning.TileValidCheck(condition.Conditions, tileRef.Value);

    protected override ZoneConditionDesc ConditionDescription(Neighbor condition, TileRef tile)
    {
        var tileCoords = _turf.GetTileCenter(tile);
        var count = Directions.Count(x => Check(condition, tileCoords, x));

        return new ZoneConditionDesc(
            Loc.GetString("zone-condition-neighbor-tile-add-desc",
                ("invert", condition.Invert),
                ("amount", condition.Amount)),
            condition.TileValidCheck(tile, Zoning),
            Progress: ZoneConditionDesc.ProgressText(count, condition.Amount),
            SubDescriptions: Zoning.ConditionDescription(condition.Conditions, tile)
                .Select(x => (IZoneConditionDesc)x)
                .ToList());
    }
}
