using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the user's skill level.
/// </summary>
public sealed partial class SkillLevel : BaseMathCurve<SkillLevel>
{
    /// <summary>
    /// Skill prototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SkillPrototype> Skill;

    /// <summary>
    /// If true, the outputs will be normalized relative to the maximum skill level.
    /// </summary>
    [DataField]
    public bool Normalize = true;
}

public sealed class SkillLevelSystem : MathCurveSystem<SkillLevel>
{
    [Dependency] private readonly SharedSkillsSystem _skills = default!;

    protected override float Curve(SkillLevel curve, float input, MathCurveContext ctx)
    {
        if (ctx.User == null)
            return input;

        var level = (float)_skills.GetLevel(ctx.User.Value, curve.Skill);

        if (level == 0)
            return 0;

        return curve.Normalize ? level / _skills.GetMaxLevel(curve.Skill) : level;
    }
}
