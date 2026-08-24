using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.World;

namespace Content.Server._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks the current hour of the day.
/// </summary>
public sealed partial class DayHour : BaseGoapCondition<DayHour>
{
    /// <summary>
    /// Minimum day hour.
    /// </summary>
    [DataField]
    public int? MoreThan;

    /// <summary>
    /// Maximum day hour.
    /// </summary>
    [DataField]
    public int? LessThan;
}

public sealed class DayHourGoapConditionSystem : GoapConditionSystem<DayHour>
{
    [Dependency] private readonly SharedRimFortressWorldSystem _world = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, DayHour condition)
    {
        var hour = _world.WorldDateTime().Hours;
        return (condition.MoreThan == null || hour > condition.MoreThan)
               && (condition.LessThan == null || hour < condition.LessThan);
    }
}
