using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Workshops.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators.Workshop;

/// <summary>
/// Looks for a next ingredient needed for a workshop recipe.
/// </summary>
public sealed partial class GetWorkshopIngredientOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;
    private WorkshopSystem _workshop;

    /// <summary>
    /// The key stores the workshop entity.
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    /// <summary>
    /// The key in which the found ingredient will be stored.
    /// </summary>
    [DataField(required: true)]
    public string ResultKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _workshop = sysManager.GetEntitySystem<WorkshopSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entity))
            return (false, null);

        if (_workshop.TryGetNextCraftingItem(blackboard.GetOwner(), target, out var uid, out var reason))
            return (true, new() { {ResultKey, uid.Value} });

        return (false, null);
    }
}
