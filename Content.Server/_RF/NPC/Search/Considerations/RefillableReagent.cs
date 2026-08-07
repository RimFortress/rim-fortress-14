using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Evaluates refillable solutions based on the amount of reagent they contain.
/// </summary>
public sealed partial class RefillableReagent : BaseSearchConsideration<RefillableReagent>
{
    /// <summary>
    /// A prototype reagent for evaluation.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>
    /// Will the evaluation be normalized relative to the maximum size of the solution?
    /// </summary>
    [DataField]
    public bool Normalize = true;
}

public sealed class RefillableReagentConsiderationSystem : NpcSearchConsiderationSystem<RefillableReagent>
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    protected override float GetScore(GoapState state, EntityUid target, RefillableReagent con)
    {
        if (!_solution.TryGetRefillableSolution(target, out _, out var solution)
            || solution.Contents.FirstOrNull(x => x.Reagent.Prototype == con.Reagent) is not { } reagent)
            return 0f;

        return !con.Normalize ? reagent.Quantity.Float() : (reagent.Quantity / solution.MaxVolume).Float();
    }
}
