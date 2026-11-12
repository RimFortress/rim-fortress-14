using Content.Shared._RF.Skills.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Skills.Components;

/// <summary>
/// How hard it is to perform skill checks with, on or by the owner: a tool, a target or the user itself.
/// The values are passed to the formulas of the check, see <see cref="SkillCheckPrototype"/>.
/// The component knows neither the skills nor the checks.
/// </summary>
[RegisterComponent]
public sealed partial class SkillDifficultyComponent : Component
{
    /// <summary>
    /// The difficulty for each query. The queries that are not listed count as 0, and an interaction in which
    /// none of the participants lists the query is not a skill check at all.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SkillQueryPrototype>, float> Difficulty = new();
}
