using System.Linq;
using Content.Shared._RF.Zoning.Components;
using Content.Shared._RF.Zoning.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Zoning.Conditions;

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
    public HashSet<EntProtoId<ZoneComponent>> Types = new();

    /// <summary>
    /// If true, the search will be only in valid zones.
    /// </summary>
    [DataField]
    public bool ValidOnly;
}

public sealed partial class InZoneZoningConditionSystem : ZoningConditionSystem<InZone>
{
    [Dependency] private ZoningSystem _zoning = default!;

    protected override bool TileValidCheck(InZone condition, TileRef tile)
        => _zoning.TryGetZone(tile, out var zones, condition.Types, condition.ValidOnly)
           && zones.Count >= condition.Amount;

    protected override ZoneConditionDesc ConditionDescription(InZone condition, TileRef tile)
    {
        _zoning.TryGetZone(tile, out var zones, condition.Types, condition.ValidOnly);

        var currentNames = zones?.Select(x => $"[tooltip netUid={GetNetEntity(x).Id}]").ToArray();
        var zoneNames = condition.Types.Select(x => $"[tooltip entProto=\"{x}\"]").ToArray();

        return new ZoneConditionDesc(
            Loc.GetString("zone-condition-in-zone-tile-add-desc",
                ("invert", condition.Invert),
                ("amount", condition.Amount),
                ("zones", zoneNames.Length > 0 ? string.Join(", ", zoneNames) : "empty"),
                ("current", currentNames?.Length > 0 ? string.Join(", ", currentNames) : "empty")),
            condition.TileValidCheck(tile, Zoning),
            Progress: ZoneConditionDesc.ProgressText(zones?.Count ?? 0, condition.Amount));
    }
}
