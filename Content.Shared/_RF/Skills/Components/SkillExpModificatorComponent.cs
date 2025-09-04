using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Skills.Components;

/// <summary>
/// A component that modifies the amount of skill experience gained
/// if the owner of the component is part of a skill check, as a user, checker, or target
/// </summary>
[RegisterComponent]
public sealed partial class SkillExpModificatorComponent : Component
{
    /// <summary>
    /// A modifier for each skill that will be added to the skill experience gained
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, int> FlatModificators = new();

    /// <summary>
    /// Multipliers for each skill, by which the received skill experience will be multiplied
    /// </summary>
    /// <remarks>
    /// These multipliers will be applied after all flat modifiers have been added
    /// </remarks>
    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, float> Multipliers = new();
}
