using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.World;

namespace Content.Shared._RF.Conversation.Requirements;

/// <summary>
/// Checks the current hour of the day.
/// </summary>
public sealed partial class DayHour : BaseConversationCondition<DayHour>
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

public sealed class DayHourConversationConditionSystem : ConversationConditionSystem<DayHour>
{
    [Dependency] private readonly SharedRimFortressWorldSystem _world = default!;

    protected override bool Check(EntityUid target, EntityUid? other, DayHour condition)
    {
        var hour = _world.WorldDateTime().Hours;
        return (condition.MoreThan == null || hour > condition.MoreThan)
               && (condition.LessThan == null || hour < condition.LessThan);
    }
}
