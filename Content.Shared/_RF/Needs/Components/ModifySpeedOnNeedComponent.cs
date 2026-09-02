using Content.Shared._RF.Needs.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Needs.Components;

/// <summary>
/// It is used to modify the speed of an entity's movement depending on current need threshold
/// </summary>
[RegisterComponent]
public sealed partial class ModifySpeedOnNeedComponent : Component
{
    /// <summary>
    /// Speed multipliers for each threshold of each need category.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<NeedCategoryPrototype>, Dictionary<ProtoId<NeedThresholdPrototype>, float>> Modifiers = new();
}
