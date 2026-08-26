using System.Numerics;
using Content.Server.Interaction;
using Content.Server.Movement.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Movement.Pulling.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics.Joints;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Moves an entity pulled by an NPC to the specified coordinates.
/// </summary>
public sealed partial class MovePulling : BaseGoapAction<MovePulling>
{
    /// <summary>
    /// Key containing the coordinates by which the entity will be moved.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetCoordinatesKey = "TargetCoordinates";
}

public sealed class MovePullingGoapActionSystem : GoapActionSystem<MovePulling>
{
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly EntityQuery<PullableComponent> _pullableQuery = default!;
    [Dependency] private readonly EntityQuery<PullerComponent> _pullerQuery = default!;

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, MovePulling action)
        => TryGet(ent, action.TargetCoordinatesKey, out var targetCoords)
            ? UpdatePulling(ent, this, targetCoords)
            : GoapActionResult.Failed;

    /// <summary>
    /// Updates the movement of the pulled object until it reaches the target coordinates.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="handler"></param>
    /// <param name="targetCoords">Target coordinates.</param>
    /// <param name="maxRangeKey">
    /// A key that stores the maximum possible radius
    /// at which a pulled object can move away from the agent.
    /// </param>
    /// <param name="closeRangeKey">
    /// A key that stores the maximum radius of the pulled object
    /// from the target coordinates required to complete the action.
    /// </param>
    /// <returns>Action update result.</returns>
    [PublicAPI]
    public GoapActionResult UpdatePulling(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        EntityCoordinates targetCoords,
        StateKey<float>? maxRangeKey = null,
        StateKey<float>? closeRangeKey = null)
    {
        if (!_pullerQuery.TryComp(ent, out var puller))
        {
            ComponentNotFound<PullerComponent>();
            return GoapActionResult.Failed;
        }

        if (!_pullableQuery.TryComp(puller.Pulling, out var pullable))
        {
            ComponentNotFound<PullableComponent>(puller.Pulling);
            return GoapActionResult.Failed;
        }

        if (!handler.TryGet(ent, maxRangeKey ?? GoapState.PullerThrowDistance, out var maxRange))
            return GoapActionResult.Failed;

        var fromUserCoords = _xform.WithEntityId(targetCoords, ent);
        var userCoords = new EntityCoordinates(ent, Vector2.Zero);

        if (!_xform.InRange(targetCoords, userCoords, maxRange))
        {
            var direction = fromUserCoords.Position - userCoords.Position;

            if (pullable.PullJointId != null &&
                TryComp(ent, out JointComponent? joint) &&
                joint.GetJoints.TryGetValue(pullable.PullJointId, out var pullJoint) &&
                pullJoint is DistanceJoint dist)
                maxRange = MathF.Max(0.01f, dist.MaxLength - 0.01f);

            fromUserCoords = new EntityCoordinates(ent, direction.Normalized() * (maxRange - 0.01f));
            targetCoords = _xform.WithEntityId(fromUserCoords, targetCoords.EntityId);
        }

        if (!_interaction.InRangeUnobstructed(ent, targetCoords, maxRange))
        {
            handler.CreateDump($"something is blocking the ({ToPrettyString(puller.Pulling)}) movement");
            return  GoapActionResult.Failed;
        }

        var moving = EnsureComp<PullMovingComponent>(puller.Pulling!.Value);
        moving.MovingTo = targetCoords;

        if (!targetCoords.TryDistance(EntityManager, Transform(puller.Pulling.Value).Coordinates, out var distance)
            || !handler.TryGet(ent, closeRangeKey ?? GoapState.PullingMoveCloseRange, out var closeRange))
            return GoapActionResult.Failed;

        return distance <= closeRange ? GoapActionResult.Finished : GoapActionResult.Continuing;
    }
}
