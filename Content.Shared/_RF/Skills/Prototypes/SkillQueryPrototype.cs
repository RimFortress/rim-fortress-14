using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Skills.Prototypes;

/// <summary>
/// A kind of skill check that the code asks for: what is being done, for example a melee attack or crafting.
/// The code knows only the query. Which skill is checked and by which <see cref="SkillCheckPrototype"/>
/// is up to the skills of the user, see <see cref="SkillPrototype.Checks"/>.
/// </summary>
[Prototype]
public sealed partial class SkillQueryPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;
}
