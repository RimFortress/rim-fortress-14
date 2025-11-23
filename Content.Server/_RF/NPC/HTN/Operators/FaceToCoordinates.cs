using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Makes the entity turn toward the specified coordinates
/// </summary>
public sealed partial class FaceToCoordinates : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private RotateToFaceSystem _rotate;
    private TransformSystem _xform;

    [DataField]
    public string TargetCoordinates = "TargetCoordinates";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _rotate = sysManager.GetEntitySystem<RotateToFaceSystem>();
        _xform = sysManager.GetEntitySystem<TransformSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue(TargetCoordinates, out EntityCoordinates? coords, _entity))
            return HTNOperatorStatus.Failed;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var mapCoords = _xform.ToMapCoordinates(coords.Value);

        return _rotate.TryFaceCoordinates(owner, mapCoords.Position)
            ? HTNOperatorStatus.Finished
            : HTNOperatorStatus.Failed;
    }
}
