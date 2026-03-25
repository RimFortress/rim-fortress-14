using Content.Server._RF.Kitchen;
using Content.Server.NPC;
using Content.Shared.Kitchen;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions.Cooking;

/// <summary>
/// Checks whether it's possible to start preparing the recipe in the kitchen.
/// </summary>
public sealed partial class CanStartCookingPrecondition : InvertiblePrecondition
{
    [Dependency] private readonly IEntityManager _entity = default!;
    private NpcKitchenSystem _kitchen;

    /// <summary>
    /// The key to storing the kitchen entity.
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    /// <summary>
    /// The key that stores the food recipe.
    /// </summary>
    [DataField]
    public string TargetRecipeKey = "Recipe";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _kitchen = sysManager.GetEntitySystem<NpcKitchenSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
        => blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entity)
           && blackboard.TryGetValue<ProtoId<FoodRecipePrototype>>(TargetRecipeKey, out var recipe, _entity)
           && _kitchen.CanStartCooking(target, recipe);
}
