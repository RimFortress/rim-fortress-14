using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Kitchen;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Kitchen;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Operators.Cooking;

/// <summary>
/// Looking for a container with the reagent needed to cook the recipe.
/// </summary>
public sealed partial class GetCookingReagentOperator : HTNOperator
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
    /// The key to storing the found container of reagent.
    /// </summary>
    [DataField(required: true)]
    public string ResultKey;

    /// <summary>
    /// The key in which the reagent quantity will be stored.
    /// </summary>
    [DataField(required: true)]
    public string ResultReagentKey;

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
            || !_npcKitchen.TryGetNextCookingReagent(owner, target.Value, protoId.Value, out var uid, out var reagent))
            return (false, null);

        return (true, new()
        {
            {ResultKey, uid},
            {ResultReagentKey, reagent},
        });
    }
}
