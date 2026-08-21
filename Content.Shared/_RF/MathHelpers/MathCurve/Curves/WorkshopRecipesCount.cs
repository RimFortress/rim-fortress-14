using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the number of active recipes in workshops sharing the same owner as the user.
/// </summary>
public sealed partial class WorkshopRecipesCount : BaseMathCurve<WorkshopRecipesCount>
{
    /// <summary>
    /// Workshops with these recipe tables will be included in the count.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<WorkshopRecipeTablePrototype>> Tables = new();
}

public sealed class WorkshopRecipesCountMathCurveSystem : MathCurveSystem<WorkshopRecipesCount>
{
    [Dependency] private readonly OwnershipSystem _ownership = default!;

    protected override float Curve(WorkshopRecipesCount curve, float input, MathCurveContext ctx)
    {
        if (ctx.User == null)
            return 0f;

        var count = 0f;
        var enumerator = _ownership.GetEntitiesEnumerator<WorkshopComponent>(ctx.User.Value);
        while (enumerator.MoveNext(out var comp))
        {
            if (!curve.Tables.Contains(comp.Recipes))
                continue;

            foreach (var entry in comp.Queue.Queue)
            {
                if (!entry.Suspended)
                    count++;
            }
        }

        return count;
    }
}
