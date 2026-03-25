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
    [DataField]
    public string TargetRecipePathfindingKey = "TargetRecipePathfinding";

    /// <summary>
    /// The key in which the recipe will be saved.
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
        if (!blackboard.TryGetValue(TargetRecipePathfindingKey, out List<ProtoId<FoodRecipePrototype>>? path, _entity)
            || !blackboard.TryGetValue<ProtoId<FoodRecipePrototype>>(TargetRecipeKey, out var protoId, _entity)
            || !_npcKitchen.TryGetRecipesPath(blackboard.GetOwner(), protoId, out path))
            return (false, null);

        if (!blackboard.TryGetValue<ProtoId<FoodRecipePrototype>>(ResultKey, out var recipe, _entity))
        {
            return (true, new()
            {
                {TargetRecipePathfindingKey, path},
                {ResultKey, path[0]},
            });
        }

        var index = path.IndexOf(recipe);

        if (index == -1)
        {
            return (true, new()
            {
                {TargetRecipePathfindingKey, path},
                {ResultKey, path[0]},
            });
        }

        if (index + 1 < path.Count)
        {
            return (true, new()
            {
                {TargetRecipePathfindingKey, path},
                {ResultKey, path[index + 1]},
            });
        }

        return (false, null);
    }
}
