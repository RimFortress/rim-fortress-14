using Content.Server.Hands.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Makes the agent drop the item in hands.
/// </summary>
public sealed partial class Drop : BaseGoapAction<Drop>;

public sealed class DropSystem : GoapActionSystem<Drop>
{
    [Dependency] private readonly HandsSystem _hands = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Drop action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Drop action)
    {
        var state = ent.Comp.State;

        if (!Goap.TryGetValue(state, GoapState.ActiveHand, out _))
        {
            CreateDump(ent, action, "agent has no hands");
            return false;
        }

        var owner = state.GetValue(GoapState.Owner);

        if (_hands.TryDrop(owner))
            return true;

        return false;
    }
}
