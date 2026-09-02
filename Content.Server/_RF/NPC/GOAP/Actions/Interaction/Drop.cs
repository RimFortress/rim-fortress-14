using Content.Server.Hands.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Makes the agent drop the item in hands.
/// </summary>
public sealed partial class Drop : BaseGoapAction<Drop>;

public sealed partial class DropSystem : GoapActionSystem<Drop>
{
    [Dependency] private HandsSystem _hands = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Drop action)
        => TryGet(ent, GoapState.ActiveHand, out _) && _hands.TryDrop(ent.Owner);
}
