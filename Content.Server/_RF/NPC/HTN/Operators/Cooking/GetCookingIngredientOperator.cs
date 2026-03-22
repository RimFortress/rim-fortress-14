using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Kitchen;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Kitchen;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Operators.Cooking;

/// <summary>
/// Looks for an ingredient needed for a food recipe that hasn't been collected yet.
/// </summary>
public sealed partial class GetCookingIngredientOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;
    private NpcKitchenSystem _npcKitchen;

    /// <summary>
    /// The key stores the kitchen for cooking.
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    /// <summary>
    /// The key that stores the food recipe.
    /// </summary>
    [DataField]
    public string TargetRecipeKey = "TargetRecipe";

    /// <summary>
    /// The key in which the found ingredient will be stored.
    /// </summary>
    [DataField(required: true)]
    public string ResultKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _npcKitchen = sysManager.GetEntitySystem<NpcKitchenSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue(TargetKey, out EntityUid? target, _entity)
            || !blackboard.TryGetValue(TargetRecipeKey, out ProtoId<FoodRecipePrototype>? protoId, _entity)
            || !_npcKitchen.TryGetNextCookingIngredient(owner, target.Value, protoId.Value, out var uid))
            return (false, null);

        return (true, new() { {ResultKey, uid} });
    }
}
