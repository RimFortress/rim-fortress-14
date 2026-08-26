using Content.Server.Hands.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// An agent switches active hands to the free one.
/// </summary>
public sealed partial class SwapToFreeHand : BaseGoapAction<SwapToFreeHand>;

public sealed class SwapToFreeHandSystem : GoapActionSystem<SwapToFreeHand>
{
    [Dependency] private readonly HandsSystem _hands = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, SwapToFreeHand action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, SwapToFreeHand action)
    {
        if (!_hands.IsHolding(ent.Owner, null))
        {
            CreateDump("active entity hand was empty");
            return true;
        }

        return _hands.TrySelectEmptyHand(Get(ent, GoapState.Owner));
    }
}
