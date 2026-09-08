using System.Linq;
using Content.Shared._RF.Zoning.Prototypes;
using Content.Shared._RF.Zoning.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Zoning.Conditions.TileAdd;

/// <summary>
/// They will check the tile for overlap with other zones.
/// </summary>
public sealed partial class InZone : BaseZoneCondition<InZone>
{
    public override ZoneConditionType Type => ZoneConditionType.TileAdd;

    /// <summary>
    /// Types of zones to check for tile overlap. If empty, a check will be performed for any zone.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ZonePrototype>> Types = new();
}

public sealed partial class InZoneZoningConditionSystem : ZoningConditionSystem<InZone>
{
    [Dependency] private ZoningSystem _zoning = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    protected override bool TileValidCheck(InZone condition, TileRef tile)
        => _zoning.TryGetZone(tile, out var zones, condition.Types)
           && zones.Count >= condition.Amount;

    protected override ZoneConditionDesc ConditionDescription(
        InZone condition,
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities)
    {
        _zoning.TryGetZone(tile!.Value, out var zones, condition.Types);

        var currentNames = zones?.Select(x => MetaData(x).EntityName).ToArray();
        var zoneNames = condition.Types.Select(x => Loc.GetString(_proto.Index(x).Name)).ToArray();

        return new ZoneConditionDesc(
            Loc.GetString(condition.Description,
                ("invert", condition.Invert),
                ("amount", condition.Amount),
                ("zones", zoneNames.Length > 0 ? string.Join(", ", zoneNames) : "empty"),
                ("current", currentNames?.Length > 0 ? string.Join(", ", currentNames) : "empty")),
            condition.TileValidCheck(tile!.Value, Zoning),
            Progress: ZoneConditionDesc.ProgressText(Loc, zones?.Count ?? 0, condition.Amount));
    }
}
