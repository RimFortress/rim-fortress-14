namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// An event that affects the whole world
/// </summary>
[RegisterComponent]
public sealed partial class GlobalWorldRuleComponent : Component
{
    /// <summary>
    /// Cost of starting an event in event points
    /// </summary>
    [DataField]
    public int Cost;

    /// <summary>
    /// Cost of one minute of action of this event in event points
    /// </summary>
    [DataField]
    public float TimeCost;

    /// <summary>
    /// Modifier of the chance of this event triggering, from 0 to 1
    /// </summary>
    [DataField]
    public float ChanceMod = 1f;

    [DataField]
    public TimeSpan StartedAt;

    [ViewVariables]
    public TimeSpan EndAt;
}
