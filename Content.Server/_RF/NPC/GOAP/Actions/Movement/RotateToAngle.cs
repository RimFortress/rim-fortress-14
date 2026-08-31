using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Actions.Movement;

/// <summary>
/// Rotates the agent to the specified angle.
/// </summary>
public sealed partial class RotateToAngle : BaseGoapAction<RotateToAngle>
{
    /// <summary>
    /// A key that stores the angle for rotation.
    /// </summary>
    [DataField]
    public StateKey<Angle> TargetKey = "RotateAngle";

    /// <summary>
    /// A key that stores the rotation speed.
    /// </summary>
    [DataField]
    public StateKey<float> RotationSpeedKey = GoapState.RotateSpeed;

    // Didn't use a key because it's likely the same between all NPCs
    [DataField]
    public Angle Tolerance = Angle.FromDegrees(1);
}

public sealed class RotateToAngleSystem : GoapActionSystem<RotateToAngle>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, RotateToAngle action) => 0.5f;

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, RotateToAngle action)
    {
        if (!TryGet(ent, action.TargetKey, out var angle)
            || !TryGet(ent, action.RotationSpeedKey, out var speed))
            return GoapActionResult.Failed;

        if (_rotate.TryRotateTo(ent, angle, _timing.FrameTime.Seconds, action.Tolerance, speed))
            return GoapActionResult.Finished;

        return speed == float.MaxValue ? GoapActionResult.Failed : GoapActionResult.Continuing;
    }
}
