using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Is the specified key not in the specified range of us.
/// </summary>
public sealed partial class TargetNotInRangePrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private SharedTransformSystem _transformSystem = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    [DataField(required: true)]
    public string RangeKey = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _transformSystem = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var coordinates, _entManager))
            return true;

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_entManager.TryGetComponent<TransformComponent>(target, out var targetXform))
            return true;

        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
        return !_transformSystem.InRange(coordinates, targetXform.Coordinates, range);
    }
}
