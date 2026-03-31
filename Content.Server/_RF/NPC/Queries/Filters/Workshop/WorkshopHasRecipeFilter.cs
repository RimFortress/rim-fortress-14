using Content.Server._RF.Workshops.Systems;
using Content.Server.NPC;
using Content.Shared._RF.Workshops.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Filters.Workshop;

/// <summary>
/// Filters all workshops where a target recipe can be crafted.
/// </summary>
public sealed partial class WorkshopHasRecipeFilter : RfUtilityQueryFilter
{
    private WorkshopSystem _workshop;

    /// <summary>
    /// Target recipe prototype.
    /// </summary>
    [DataField]
    public ProtoId<WorkshopRecipePrototype>? Recipe;

    /// <summary>
    /// Target recipe prototype key.
    /// </summary>
    [DataField]
    public string? RecipeKey;

    private ProtoId<WorkshopRecipePrototype> _proto;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _workshop = EntityManager.System<WorkshopSystem>();
    }

    public override bool Startup(NPCBlackboard blackboard)
    {
        if (Recipe != null)
        {
            _proto = Recipe.Value;
            return true;
        }

        return RecipeKey != null && blackboard.TryGetValue(RecipeKey, out _proto, EntityManager);
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard) => _workshop.ContainsRecipe(uid, _proto);
}
