using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Skills;

[Prototype]
public sealed class SkillPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<SkillPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [AbstractDataField, NeverPushInheritance]
    public bool Abstract { get; private set; }

    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// Human-readable skill name
    /// </summary>
    [DataField("name")]
    public LocId Name;

    /// <summary>
    /// Path to the skill icon that will be displayed in the interface
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Maximum skill level
    /// </summary>
    [DataField]
    public int MaxLevel;

    /// <summary>
    /// Basic amount of experience points required to increase the skill level
    /// </summary>
    [DataField]
    public int LevelUpExp;

    /// <summary>
    /// The multiplier by which <see cref="LevelUpExp"/> will be increased with each level
    /// </summary>
    [DataField]
    public float LevelExpMultiplier;

    /// <summary>
    /// The effects that will be applied to an entity when specific levels are reached
    /// </summary>
    /// <remarks>
    /// The effects for level zero are applied every level up
    /// </remarks>
    [DataField]
    public Dictionary<int, List<EntityEffect>> LevelUpEffects = new();
}
