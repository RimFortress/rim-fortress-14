using System.Linq;
using Content.Shared._RF.Zoning.Systems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Conditions.TileAdd;

/// <summary>
/// Checks for entities in the tile.
/// </summary>
public sealed partial class TileEntities : BaseZoneCondition<TileEntities>
{
    public override ZoneConditionType Type => ZoneConditionType.TileAdd;

    /// <summary>
    /// The type of entities to check for. If empty, check for the presence of any entities.
    /// </summary>
    [DataField]
    public List<EntProtoId> Types = new();
}

public sealed partial class TileEntitiesZoneConditionSystem : ZoningConditionSystem<TileEntities>
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private TurfSystem _turf = default!;

    protected override bool TileValidCheck(TileEntities condition, TileRef tile)
    {
        var entities = _turf.GetEntitiesInTile(
            _turf.GetTileCenter(tile),
            LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sensors);
        var count = 0;

        if (condition.Types.Count == 0)
            return entities.Count >= condition.Amount;

        foreach (var uid in entities)
        {
            if (Prototype(uid) is not { } proto || !condition.Types.Contains(proto.ID))
                continue;

            count++;

            if (count >= condition.Amount)
                return true;
        }

        return false;
    }

    protected override ZoneConditionDesc ConditionDescription(TileEntities condition,
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities)
    {
        DebugTools.AssertNotNull(tile);
        var typeNames = condition.Types.Select(x => _proto.Index(x).Name).ToArray();
        var inTile = _turf.GetEntitiesInTile(
                _turf.GetTileCenter(tile!.Value),
                LookupFlags.Static | LookupFlags.Dynamic)
            .Count(uid => condition.Types.Count == 0
                          || Prototype(uid) is { } proto && condition.Types.Contains(proto.ID));

        return new ZoneConditionDesc(Loc.GetString("zone-condition-tile-entities-desc",
                ("invert", condition.Invert),
                ("amount", condition.Amount),
                ("types", typeNames.Length > 0 ? string.Join(", ", typeNames) : "empty")),
            TileValidCheck(condition, tile!.Value),
            Progress: ZoneConditionDesc.ProgressText(Loc, inTile, condition.Amount),
            EntIcon: condition.Types);
    }
}
