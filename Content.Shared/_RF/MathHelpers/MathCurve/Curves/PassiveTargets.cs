using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Prototypes;
using Content.Shared._RF.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the number of passive targets.
/// </summary>
public sealed partial class PassiveTargets : BaseMathCurve<PassiveTargets>
{
    /// <summary>
    /// Only passive targets with this goal will be counted.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ExecutableGoalPrototype> Goal;

    /// <summary>
    /// If true, only targets created by users who are
    /// able to control the agent will be included in the count.
    /// </summary>
    [DataField]
    public bool CanControlOnly = true;
}

public sealed class PassiveTargetsCurveSystem : MathCurveSystem<PassiveTargets>
{
    [Dependency] private readonly SharedExecutableGoalSystem _executable = default!;

    protected override float Curve(PassiveTargets curve, float input, MathCurveContext ctx)
    {
        var enumerator = EntityQueryEnumerator<PassiveGoalTargetComponent>();
        var count = 0;

        while (enumerator.MoveNext(out var comp))
        {
            if (comp.Goal != curve.Goal)
                continue;

            if (!curve.CanControlOnly || ctx.User == null)
            {
                count++;
                continue;
            }

            if (_executable.CanControl(comp.User, ctx.User.Value))
                count++;
        }

        return count;
    }
}
