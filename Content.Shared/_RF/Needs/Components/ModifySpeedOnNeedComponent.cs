using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Needs.Components;

/// <summary>
/// It is used to modify the speed of an entity's movement depending on current need threshold
/// </summary>
[RegisterComponent]
public sealed partial class ModifySpeedOnNeedComponent : Component
{
    /// <summary>
    /// Speed multipliers for each threshold of each need
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<NeedPrototype>, Dictionary<string, float>> Modifiers = new();
}
