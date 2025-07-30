using System.Numerics;
using Content.Server.Movement.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Movement.Pulling.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics.Joints;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Moves an entity pulled by an NPC to the specified coordinates
/// </summary>
public sealed partial class MovePulledOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private TransformSystem _xform = default!;

    private EntityQuery<PullableComponent> _pullableQuery;
    private EntityQuery<PullerComponent> _pullerQuery;

    /// <summary>
    /// Key containing the coordinates by which the entity will be moved
    /// </summary>
    [DataField]
    public string CoordinatesKey = "TargetCoordinates";

    /// <summary>
    /// A key containing the minimum distance to move the entity to the target coordinates
    /// </summary>
    [DataField]
    public string RangeKey = NPCBlackboard.PullingMoveCloseRange;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);

        _pullableQuery = _entityManager.GetEntityQuery<PullableComponent>();
        _pullerQuery = _entityManager.GetEntityQuery<PullerComponent>();

        _xform = sysManager.GetEntitySystem<TransformSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValueOrDefault<EntityUid>(NPCBlackboard.Owner, _entityManager);

        if (!blackboard.TryGetValue(CoordinatesKey, out EntityCoordinates coords, _entityManager)
            || !_pullerQuery.TryComp(owner, out var puller)
            || !_pullableQuery.TryComp(puller.Pulling, out var pullable))
            return HTNOperatorStatus.Failed;

        var maxRange = blackboard.GetValueOrDefault<float>(NPCBlackboard.PullerThrowDistance, _entityManager);
        var fromUserCoords = _xform.WithEntityId(coords, owner);
        var userCoords = new EntityCoordinates(owner, Vector2.Zero);

        if (!_xform.InRange(coords, userCoords, maxRange))
        {
            var direction = fromUserCoords.Position - userCoords.Position;

            if (pullable.PullJointId != null &&
                _entityManager.TryGetComponent(owner, out JointComponent? joint) &&
                joint.GetJoints.TryGetValue(pullable.PullJointId, out var pullJoint) &&
                pullJoint is DistanceJoint dist)
                maxRange = MathF.Max(0.01f, dist.MaxLength - 0.01f);

            fromUserCoords = new EntityCoordinates(owner, direction.Normalized() * (maxRange - 0.01f));
            coords = _xform.WithEntityId(fromUserCoords, coords.EntityId);
        }

        var moving = _entityManager.EnsureComponent<PullMovingComponent>(puller.Pulling!.Value);
        moving.MovingTo = coords;

        if (!_entityManager.TryGetComponent<TransformComponent>(puller.Pulling, out var xform)
            || !coords.TryDistance(_entityManager, xform.Coordinates, out var distance)
            || !blackboard.TryGetValue(RangeKey, out float range, _entityManager))
            return HTNOperatorStatus.Failed;

        // Well, we can only hope that the object will not encounter an impossible obstacle
        // while moving to the given coordinates and the operator will not continue working endlessly.
        // TODO: fix this
        return distance <= range ? HTNOperatorStatus.Finished : HTNOperatorStatus.Continuing;
    }
}
