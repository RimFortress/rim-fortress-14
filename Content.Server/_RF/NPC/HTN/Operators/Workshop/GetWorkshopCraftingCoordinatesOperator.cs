using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Workshops.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators.Workshop;

public sealed partial class GetWorkshopCraftingCoordinatesOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;
    private WorkshopSystem _workshop;

    [DataField]
    public string TargetKey = "Target";

    [DataField(required: true)]
    public string ResultKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _workshop = sysManager.GetEntitySystem<WorkshopSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, _entity))
            return (false, null);

        var coords = _workshop.GetCraftingPlace(uid);

        if (!coords.IsValid(_entity))
            return (false, null);

        return (true, new() { {ResultKey, coords} });
    }
}
