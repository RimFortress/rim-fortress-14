using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Kitchen;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Kitchen;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Operators.Cooking;

/// <summary>
/// Finds the path to the target food recipe and saves it to blackboard.
/// </summary>
public sealed partial class GetCookingRecipePathOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;
    private NpcKitchenSystem _npcKitchen;

    /// <summary>
    /// A key containing the prototype of the target recipe.
    /// </summary>
    [DataField]
    public string TargetRecipeKey = "TargetRecipe";

    /// <summary>
    /// The key in which the path will be stored.
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
        if (blackboard.ContainsKey(ResultKey))
            return (true, null);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue(TargetRecipeKey, out ProtoId<FoodRecipePrototype>? protoId, _entity)
            || !_npcKitchen.TryGetRecipesPath(owner, protoId.Value, out var path))
            return (false, null);

        return (true, new() { {ResultKey, path} });
    }
}
