using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.GOAP.Actions.Movement;

/// <summary>
/// Sets a random angle to the state.
/// </summary>
public sealed partial class PickRandomAngle : BaseGoapAction<PickRandomAngle>
{
    [DataField]
    public StateKey<Angle> TargetKey = "RotateTarget";
}

public sealed partial class PickRandomAngleSystem : GoapActionSystem<PickRandomAngle>
{
    [Dependency] private IRobustRandom _random = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, PickRandomAngle action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, PickRandomAngle action)
    {
        Set(ent, action.TargetKey, _random.NextAngle());
        return true;
    }
}
