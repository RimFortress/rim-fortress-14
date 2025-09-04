using Content.Shared.Destructible.Thresholds;
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
    [DataField, AutoNetworkedField]
    public List<SkillData> Skills = new();

    /// <summary>
    /// Which skill levels should be randomized when adding a component
    /// </summary>
    [DataField]
    public List<ProtoId<SkillPrototype>> RandomizeSkills = new();

    /// <summary>
    /// Range of the number of levels that will be randomly distributed
    /// among the skills specified in <see cref="RandomizeSkills"/>
    /// </summary>
    [DataField]
    public MinMax RandomLevels;

    /// <summary>
    /// Maximum possible level of randomly distributed skill
    /// </summary>
    [DataField]
    public int MaxRandomLevel;
}

[DataDefinition, Serializable, NetSerializable]
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
}
