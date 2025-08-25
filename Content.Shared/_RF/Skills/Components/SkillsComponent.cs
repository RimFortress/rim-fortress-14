using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Skills.Components;

/// <summary>
/// A component containing information about the entity's skills
/// </summary>
[Access(typeof(SharedSkillsSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkillsComponent : Component
{
    /// <summary>
    /// The factor by which all experience gained is multiplied
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ExpFactor = 1;

    [DataField, AutoNetworkedField]
    public List<SkillData> Skills = new();
}

[DataDefinition, NetSerializable]
public sealed partial class SkillData
{
    /// <summary>
    /// Skill prototype
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SkillPrototype> Id;

    /// <summary>
    /// Current skill level
    /// </summary>
    [DataField]
    public int CurrentLevel;

    /// <summary>
    /// Current skill experience points
    /// </summary>
    [DataField]
    public int CurrentExp;

    /// <summary>
    /// The number of experience points required to increase the skill level
    /// </summary>
    public int LevelUpExp;

    /// <summary>
    /// Minimum amount of experience required to stay at the current skill level
    /// </summary>
    public int MinLevelExp;

    /// <summary>
    /// The factor by which all experience gained for this skill is multiplied
    /// </summary>
    [DataField]
    public float ExpFactor = 1;
}
