using System.Linq;
using Content.Shared._RF.Zoning.Systems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Zoning.Conditions;

/// <summary>
/// Checks whether there are tiles of a specific type in the zone.
/// </summary>
public sealed partial class Tile : BaseZoneCondition<Tile>
{
    public override ZoneConditionType Type => ZoneConditionType.Tile | ZoneConditionType.TileAdd;

    /// <summary>
    /// Types of tiles to check for. If empty, the checks will verify the count of any tiles.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ContentTileDefinition>> Types = new();
}

public sealed partial class TileZoneConditionSystem : ZoningConditionSystem<Tile>
{
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    protected override bool TileValidCheck(Tile condition, TileRef tile)
        => condition.Types.Count == 0
           || condition.Types.Contains(_turf.GetContentTileDefinition(tile));

    protected override bool TileCheck(Tile condition, IReadOnlySet<TileRef> tiles, int amount)
    {
        if (condition.Types.Count == 0)
            return tiles.Count >= amount;

        var types = condition.Types.Select(x => _proto.Index(x).TileId).ToHashSet();
        var count = 0;

        foreach (var tile in tiles)
        {
            if (!types.Contains((ushort)tile.Tile.TypeId))
                continue;

            count++;

            if (count >= amount)
                return true;
        }

        return false;
    }

    protected override ZoneConditionDesc ConditionDescription(Tile condition, TileRef tile)
    {
        var names = condition.Types.Select(x => $"[tooltip tile=\"{x}\"]").ToHashSet();
        return new(
            Loc.GetString("zone-condition-tile-tile-add-desc",
                ("invert", condition.Invert),
                ("types", names.Count > 0 ? string.Join(", ", names) : "empty")),
            condition.TileValidCheck(tile, Zoning));
    }

    protected override ZoneConditionDesc ConditionDescription(Tile condition, IReadOnlySet<TileRef> tiles)
    {
        var types = condition.Types.Select(x => _proto.Index(x).TileId).ToHashSet();
        var names = condition.Types.Select(x => $"[tooltip tile=\"{x}\"]").ToHashSet();
        var count = tiles.Count(x => types.Count == 0 || types.Contains((ushort)x.Tile.TypeId));
        return new(
            Loc.GetString("zone-condition-tile-tile-desc",
                ("amount", condition.Amount),
                ("invert", condition.Invert),
                ("types", string.Join(", ", names))),
            condition.TileCheck(tiles, Zoning),
            Progress: ZoneConditionDesc.ProgressText(count, condition.Amount));
    }
}
