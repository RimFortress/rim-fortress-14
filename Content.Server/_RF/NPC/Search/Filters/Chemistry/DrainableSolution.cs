using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC.Search.Filters.Chemistry;

/// <summary>
/// Filters drainable solutions.
/// </summary>
public sealed partial class DrainableSolution : BaseSearchFilter<DrainableSolution>
{
    /// <summary>
    /// Prototype of a filtered reagent.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    /// <summary>
    /// Minimum amount of reagent required to trigger the filter.
    /// </summary>
    [DataField]
    public float? ReagentMoreThan;

    /// <summary>
    /// Maximum amount of reagent required to trigger the filter.
    /// </summary>
    [DataField]
    public float? ReagentLessThan;

    /// <summary>
    /// Should this reagent be the only one in the solution?
    /// </summary>
    [DataField]
    public bool OnlyReagent;

    /// <summary>
    /// The maximum volume of the solution must be greater than the specified value to trigger the filter.
    /// </summary>
    [DataField]
    public float? SolutionMaxMoreThan;

    /// <summary>
    /// The maximum volume of the solution must be less than the specified value to trigger the filter.
    /// </summary>
    [DataField]
    public float? SolutionMaxLessThan;

    /// <summary>
    /// Does the solution must be empty for the filter to trigger?
    /// </summary>
    [DataField]
    public bool? SolutionEmpty;
}

public sealed class DrainableSolutionFilterSystem : NpcSearchFilterSystem<DrainableSolution>
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeTrackedDirty<SearchTrackedComponent, SolutionChangedEvent>();
    }

    protected override bool Filter(GoapState state, EntityUid target, DrainableSolution filter)
    {
        if (!_solution.TryGetDrainableSolution(target, out _, out var solution))
            return false;

        if (filter.SolutionEmpty != null && filter.SolutionEmpty != (solution.Volume == 0))
            return false;

        if (filter.SolutionMaxMoreThan != null && solution.MaxVolume < filter.SolutionMaxMoreThan
            || filter.SolutionMaxLessThan != null && solution.MaxVolume > filter.SolutionMaxLessThan)
            return false;

        if (filter.Reagent == null)
            return true;

        if (solution.Contents.FirstOrNull(x => x.Reagent.Prototype == filter.Reagent) is not { } reagent)
            return false;

        if (filter.OnlyReagent && solution.Contents.Count > 1)
            return false;

        return (filter.ReagentMoreThan == null || reagent.Quantity > filter.ReagentMoreThan)
               && (filter.ReagentLessThan == null || reagent.Quantity < filter.ReagentLessThan);
    }
}
