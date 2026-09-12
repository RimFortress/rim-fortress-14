using System.Linq;
using Content.Shared._RF.Zoning.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Conditions;

/// <summary>
/// Check that any condition from the list is met.
/// </summary>
public sealed partial class Or : BaseZoneCondition<Or>
{
    public override ZoneConditionType Type => ZoneConditionType.All;

    /// <summary>
    /// Conditions list.
    /// </summary>
    [DataField(required: true)]
    public List<ZoneCondition> Conditions = new();
}

public sealed partial class OrZoneConditionSystem : ZoningConditionSystem<Or>
{
    protected override bool TileValidCheck(Or condition, TileRef tile)
    {
        DebugTools.Assert(condition.Amount == 1, "amount property not supported for this condition");

        foreach (var con in condition.Conditions)
        {
            if (Zoning.TileValidCheck(con, tile))
                return true;
        }

        return false;
    }

    protected override bool TileCheck(Or condition, IReadOnlySet<TileRef> tiles, int amount)
    {
        DebugTools.Assert(condition.Amount == 1, "amount property not supported for this condition");

        foreach (var con in condition.Conditions)
        {
            if (Zoning.TileCheck(con, tiles))
                return true;
        }

        return false;
    }

    protected override bool EntityCheck(Or condition, IReadOnlySet<EntityUid> entities, int amount)
    {
        DebugTools.Assert(condition.Amount == 1, "amount property not supported for this condition");

        foreach (var con in condition.Conditions)
        {
            if (Zoning.EntityCheck(con, entities))
                return true;
        }

        return false;
    }

    protected override ZoneConditionDesc ConditionDescription(Or condition, TileRef tile)
    {
        return new ZoneConditionDesc(
            Loc.GetString("zone-condition-or-desc", ("invert", condition.Invert)),
            Zoning.TileValidCheck(condition, tile),
            SubDescriptions: Zoning.ConditionDescription(condition.Conditions, tile)
                .Select(x => (IZoneConditionDesc)x)
                .ToList());
    }

    protected override ZoneConditionDesc ConditionDescription(Or condition, IReadOnlySet<TileRef> tiles)
    {
        return new ZoneConditionDesc(
            Loc.GetString("zone-condition-or-desc", ("invert", condition.Invert)),
            Zoning.TileCheck(condition, tiles),
            SubDescriptions: Zoning.ConditionDescription(condition.Conditions, tiles)
                .Select(x => (IZoneConditionDesc)x)
                .ToList());
    }

    protected override ZoneConditionDesc ConditionDescription(Or condition, IReadOnlySet<EntityUid> entities)
    {
        return new ZoneConditionDesc(
            Loc.GetString("zone-condition-or-desc", ("invert", condition.Invert)),
            Zoning.EntityCheck(condition, entities),
            SubDescriptions: Zoning.ConditionDescription(condition.Conditions, entities)
                .Select(x => (IZoneConditionDesc)x)
                .ToList());
    }
}
