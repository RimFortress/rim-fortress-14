using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the number of agents currently performing the Utility AI goal.
/// </summary>
public sealed partial class GoalPerformers : BaseMathCurve<GoalPerformers>
{
    /// <summary>
    /// Utility AI goal.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<UtilityAiGoalPrototype> Goal;

    /// <summary>
    /// If true, only agents that share the same owner as the user will be included in the count.
    /// </summary>
    [DataField]
    public bool SameOwnerOnly = true;
}

public sealed class GoalPerformersCurveSystem : MathCurveSystem<GoalPerformers>
{
    [Dependency] private readonly OwnershipSystem _ownership = default!;

    protected override float Curve(GoalPerformers curve, float input, EntityUid? user)
    {
        var enumerator = EntityQueryEnumerator<UtilityAiComponent>();
        var count = 0;

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (comp.CurrentGoal != curve.Goal)
                continue;

            if (!curve.SameOwnerOnly || user == null)
            {
                count++;
                continue;
            }

            if (_ownership.HasSameOwner(user.Value, uid))
                count++;
        }

        return count;
    }
}
