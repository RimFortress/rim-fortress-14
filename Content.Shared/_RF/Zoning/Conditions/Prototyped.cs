using System.Linq;
using Content.Shared._RF.Zoning.Systems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Conditions;

/// <summary>
/// Checks whether entities of a specific prototype are present in the zone.
/// </summary>
public sealed partial class Prototyped : BaseZoneCondition<Prototyped>
{
    public override ZoneConditionType Type => ZoneConditionType.Entity | ZoneConditionType.TileAdd;

    /// <summary>
    /// Prototypes list.
    /// </summary>
    [DataField]
    public List<EntProtoId> Types = new();
}

public sealed partial class PrototypedZoneConditionSystem : ZoningConditionSystem<Prototyped>
{
    [Dependency] private TurfSystem _turf = default!;

    protected override bool TileValidCheck(Prototyped condition, TileRef tile)
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

    protected override bool EntityCheck(Prototyped condition, IReadOnlySet<EntityUid> entities, int amount)
    {
        DebugTools.Assert(condition.Types.Count != 0);

        var count = 0;

        foreach (var uid in entities)
        {
            if (Prototype(uid) is not { } proto || !condition.Types.Contains(proto))
                continue;

            count++;

            if (count >= amount)
                return true;
        }

        return false;
    }

    protected override ZoneConditionDesc ConditionDescription(Prototyped condition, TileRef tile)
    {
        var typeNames = condition.Types.Select(x => $"[tooltip entProto=\"{x}\"]").ToArray();
        var inTile = _turf.GetEntitiesInTile(
                _turf.GetTileCenter(tile),
                LookupFlags.Static | LookupFlags.Dynamic)
            .Count(uid => condition.Types.Count == 0
                          || EntityManager.MetaQuery.TryComp(uid, out var meta)
                          && meta.EntityPrototype is { } proto
                          && condition.Types.Contains(proto.ID));

        return new ZoneConditionDesc(Loc.GetString("zone-condition-prototyped-tile-add-desc",
                ("invert", condition.Invert),
                ("amount", condition.Amount),
                ("types", typeNames.Length > 0 ? string.Join(", ", typeNames) : "empty")),
            condition.TileValidCheck(tile, Zoning),
            Progress: ZoneConditionDesc.ProgressText(inTile, condition.Amount));
    }

    protected override ZoneConditionDesc ConditionDescription(Prototyped condition, IReadOnlySet<EntityUid> entities)
    {
        var typeNames = condition.Types.Select(x => $"[tooltip entProto=\"{x}\"]").ToArray();
        var count = entities
            .Count(uid => EntityManager.MetaQuery.TryComp(uid, out var meta)
                          && meta.EntityPrototype is { } proto
                          && condition.Types.Contains(proto));

        return new ZoneConditionDesc(Loc.GetString("zone-condition-prototyped-entity-desc",
                ("invert", condition.Invert),
                ("amount", condition.Amount),
                ("types", string.Join(", ", typeNames))),
            condition.EntityCheck(entities, Zoning),
            Progress: ZoneConditionDesc.ProgressText(count, condition.Amount));
    }
}
