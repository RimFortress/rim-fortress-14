using Content.Shared._RF.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Social.Components;

/// <summary>
/// This is used to modify the skill level of an entity during checks depending on the mood level
/// </summary>
[RegisterComponent]
public sealed partial class ModifySkillLevelOnMoodComponent : Component
{
    /// <summary>
    /// A modifier that will be multiplied by the mood level,
    /// and then the skill level will be multiplied by this value.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, float> Modifiers = new();
}
