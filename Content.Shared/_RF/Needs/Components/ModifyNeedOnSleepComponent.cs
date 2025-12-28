using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Needs.Components;

/// <summary>
/// This is used to change the level of satisfaction of a need when an entity sleeps
/// </summary>
[RegisterComponent]
public sealed partial class ModifyNeedOnSleepComponent : Component
{
    /// <summary>
    /// The values for each need and each threshold by which the need will be increased with each update
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<NeedPrototype>, Dictionary<string, float>> Modifiers = new();

    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextUpdate;
}
