#nullable enable
using Content.Server._RF.NPC.Steering.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._RF.NPC.Steering;

/// <summary>
/// Moves to <see cref="TestNodeMoveSystem.Target"/> with the steering.
/// </summary>
public sealed partial class TestNodeMove : BaseGoapAction<TestNodeMove>
{
}

public sealed partial class TestNodeMoveSystem : GoapActionSystem<TestNodeMove>
{
    public static readonly StateKey<EntityCoordinates> Target = "TestNodeMoveTarget";

    // The system is registered on the server only, and this one is on both sides.
    private RfSteeringSystem _steering => EntityManager.System<RfSteeringSystem>();

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, TestNodeMove action)
        => TryGet(ent, Target, out var target) ? _steering.Move(ent, target, 0.4f) : GoapActionResult.Failed;

    protected override void ActionShutdown(Entity<GoapComponent> ent, TestNodeMove action)
    {
        _steering.Cancel(ent);
    }
}

/// <summary>
/// Breaks the wall at the coordinates that the path gave for the goal of the node.
/// </summary>
public sealed partial class TestNodeBreak : BaseGoapAction<TestNodeBreak>
{
}

public sealed partial class TestNodeBreakSystem : GoapActionSystem<TestNodeBreak>
{
    public static int Broken;

    private static readonly StateKey<EntityCoordinates> BreakTarget = "BreakTarget";

    [Dependency] private EntityLookupSystem _lookup = default!;

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, TestNodeBreak action)
    {
        if (!TryGet(ent, BreakTarget, out var target))
            return GoapActionResult.Failed;

        foreach (var wall in _lookup.GetEntitiesInRange<PhysicsComponent>(target, 0.4f))
        {
            if (wall.Owner != ent.Owner)
                QueueDel(wall);
        }

        Broken++;
        return GoapActionResult.Finished;
    }
}
