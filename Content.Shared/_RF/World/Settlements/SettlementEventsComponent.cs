namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// What the narrator keeps about the events of a settlement. The events depend on the settlement alone, not on the player that it
/// belongs to: every settlement waits for its own events, and has its own points.
/// </summary>
[RegisterComponent]
public sealed partial class SettlementEventsComponent : Component
{
    /// <summary>
    /// When the last event of the settlement was started, in the time of the round. The points of waiting are counted from it.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastEventTime;

    /// <summary>
    /// The points of waiting that were there when the narrator looked last: the chance of an event is rolled when they change.
    /// </summary>
    [ViewVariables]
    public int LastWaitPoints;

    /// <summary>
    /// The points that are added to those of the settlement, by whoever has the right to do it (the commands for debugging, for one).
    /// They are spent by the events as the others are.
    /// </summary>
    [ViewVariables]
    public int BonusPoints;
}
