using Content.Shared._RF.World;

namespace Content.Shared._RF.Conversation.Requirements;

/// <summary>
/// Checks the current hour of the day
/// </summary>
public sealed partial class DayHour : ConversationActorRequirement
{
    /// <summary>
    /// Minimum day hour
    /// </summary>
    [DataField]
    public int? MoreThan;

    /// <summary>
    /// Maximum day hour
    /// </summary>
    [DataField]
    public int? LessThan;

    public override bool Check(EntityUid author, EntityUid? actor, EntityManager entMan)
    {
        var hour = entMan.System<SharedRimFortressWorldSystem>().WorldDateTime().Hours;
        return (MoreThan == null || hour > MoreThan)
               && (LessThan == null || hour < LessThan);
    }
}
