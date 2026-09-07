using System.Linq;
using Content.Shared._RF.Zoning.Systems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Zoning.Conditions.Tile;

/// <summary>
/// Checks whether there are tiles of a specific type in the zone.
/// </summary>
public sealed partial class Tile : BaseZoneCondition<Tile>
{
    public override ZoneConditionType Type => ZoneConditionType.Tile;

    /// <summary>
    /// Types of tiles to check for. If empty, the checks will verify the count of any tiles.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ContentTileDefinition>> Types = new();
}

public sealed partial class TileZoneConditionSystem : ZoningConditionSystem<Tile>
{
    [Dependency] private IPrototypeManager _proto = default!;

    protected override bool TileCheck(Tile condition, IReadOnlySet<TileRef> tiles)
    {
        if (condition.Types.Count == 0)
            return tiles.Count >= condition.Amount;

        var types = condition.Types.Select(x => _proto.Index(x).TileId).ToHashSet();
        var count = 0;

        foreach (var tile in tiles)
        {
            if (!types.Contains((ushort)tile.Tile.TypeId))
                continue;

            count++;

            if (count >= condition.Amount)
                return true;
        }

        return false;
    }

    protected override ZoneConditionDesc ConditionDescription(
        Tile condition,
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities)
    {
        var types = condition.Types.Select(x => _proto.Index(x).TileId).ToHashSet();
        var count = tiles.Count(x => types.Count == 0 || types.Contains((ushort)x.Tile.TypeId));
        return new(
            Loc.GetString("zone-condition-tile-desc",
                ("amount", condition.Amount),
                ("invert", condition.Invert)),
            count >= condition.Amount,
            Progress: ZoneConditionDesc.ProgressText(Loc, count, condition.Amount),
            TileIcon: condition.Types.ToList());
    }
}
