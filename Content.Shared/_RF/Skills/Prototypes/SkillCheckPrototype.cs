using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared.EntityEffects;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.Skills.Prototypes;

/// <summary>
/// A scheme of a skill check: when it applies, how its outcome is calculated and what it does.
/// It knows neither the skill that is checked nor the query: skills name the checks they perform for a query,
/// see <see cref="SkillPrototype.Checks"/>.
/// </summary>
/// <remarks>
/// <para>
/// A formula is a list of curves that are applied to the input one after another. An empty formula returns the input
/// as it is. The formulas have these variables: <c>level</c> (level of the skill of the user with all modifiers),
/// <c>maxLevel</c>, <c>usedDifficulty</c>, <c>targetDifficulty</c> and <c>userDifficulty</c> (see
/// <see cref="Components.SkillDifficultyComponent"/>, 0 if an entity has none). All the formulas except
/// <see cref="Difficulty"/> also have <c>difficulty</c> (the result of that formula) and <c>delta</c>, which is
/// <c>level - difficulty</c>.
/// </para>
/// </remarks>
[Prototype]
public sealed partial class SkillCheckPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<SkillCheckPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc/>
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    #region Conditions

    /// <summary>
    /// When several checks fit a query, the one with the highest priority is performed.
    /// If their priorities are equal, the first one in the list of the skill wins, then the one of the skill
    /// with the lesser id. The checks with conditions are to have the priority higher than the ones without.
    /// </summary>
    [DataField]
    public int Priority;

    /// <summary>
    /// Whitelist for entities involved in skill checks.
    /// If the conditions are not met, this check cannot be performed.
    /// </summary>
    [DataField]
    public Dictionary<SkillCheckTarget, EntityWhitelist> Whitelist = new();

    /// <summary>
    /// Blacklist for entities involved in skill checks.
    /// If the conditions are not met, this check cannot be performed.
    /// </summary>
    [DataField]
    public Dictionary<SkillCheckTarget, EntityWhitelist> Blacklist = new();

    #endregion

    #region Formulas

    /// <summary>
    /// How hard the check is. The input is 0.
    /// </summary>
    [DataField]
    public List<MathCurve> Difficulty = new();

    /// <summary>
    /// The chance of the additional success, from 0 to 1. The input is 0.
    /// </summary>
    [DataField]
    public MathCurve[] SuccessChance = [];

    /// <summary>
    /// The chance of failure, from 0 to 1. It is rolled if there is no additional success. The input is 0.
    /// </summary>
    [DataField]
    public MathCurve[] FailChance = [];

    /// <summary>
    /// How long the action takes. The input is the usual time of the action, in seconds.
    /// </summary>
    [DataField]
    public MathCurve[] Delay = [];

    /// <summary>
    /// How the result of the action changes, for example the damage of a hit or the spread of a shot.
    /// The input is the usual value.
    /// </summary>
    [DataField]
    public MathCurve[] Result = [];

    /// <summary>
    /// How will the user experience change depending on the skill check results?
    /// </summary>
    [DataField]
    public Dictionary<SkillCheckResult, MathCurve[]> Experience = new();

    #endregion

    /// <summary>
    /// Effects that will be applied to the participants in the check depending on its results.
    /// </summary>
    [DataField(serverOnly: true)]
    public Dictionary<SkillCheckTarget, Dictionary<SkillCheckResult, EntityEffect[]>> Effects = [];
}
