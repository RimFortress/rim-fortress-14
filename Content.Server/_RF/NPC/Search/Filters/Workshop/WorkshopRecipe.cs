using System.Linq;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Filters.Workshop;

/// <summary>
/// Filters workshops based on their ability to produce specific recipes.
/// </summary>
public sealed partial class WorkshopRecipe : BaseSearchFilter<WorkshopRecipe>
{
    /// <summary>
    /// The workshop's recipe table must match any table from this list to pass the filter.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<WorkshopRecipeTablePrototype>> Tables = new();
}

public sealed class WorkshopRecipeSearchFilterSystem : NpcSearchFilterSystem<WorkshopRecipe>
{
    [Dependency] private readonly EntityQuery<WorkshopComponent> _query = default!;

    protected override bool Filter(GoapState state, EntityUid target, WorkshopRecipe filter)
        => _query.TryComp(target, out var comp) && filter.Tables.Any(x => x == comp.Recipes);
}
