using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Needs.Components;

/// <summary>
/// This is used to change the level of satisfaction of a need when an entity sleeps
/// </summary>
[RegisterComponent]
public sealed partial class ModifyNeedOnSleepComponent : Component
{
    /// <summary>
    /// Multipliers for the decay rate of the need for each threshold
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<NeedPrototype>, Dictionary<string, float>> DecayRateModifiers = new();
}
