using Content.Server._RF.Kitchen;
using Content.Server.NPC;
using Content.Shared.Kitchen;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Filters.Cooking;

/// <summary>
/// Filters entities that result from preparing a given recipe.
/// </summary>
public sealed partial class RecipeResultFilter : RfUtilityQueryFilter
{
    private NpcKitchenSystem _kitchen;

    /// <summary>
    /// Prototype of the target recipe.
    /// </summary>
    [DataField]
    public ProtoId<FoodRecipePrototype>? Proto;

    /// <summary>
    /// The key storing the target recipe prototype.
    /// </summary>
    [DataField]
    public string? RecipeKey;

    private ProtoId<FoodRecipePrototype>? _recipe;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _kitchen = entManager.System<NpcKitchenSystem>();
    }

    public override bool Startup(NPCBlackboard blackboard)
    {
        if (RecipeKey != null && blackboard.TryGetValue(RecipeKey, out _recipe, EntityManager))
            return true;

        if (Proto == null)
            return false;

        _recipe = Proto;
        return true;
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard) => _kitchen.IsResult(uid, _recipe!.Value);
}
