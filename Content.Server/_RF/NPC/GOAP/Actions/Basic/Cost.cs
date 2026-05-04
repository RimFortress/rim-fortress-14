using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Server._RF.NPC.GOAP.Actions.Basic;

/// <summary>
/// An action that simply has a specified cost, that's it.
/// </summary>
/// <remarks>
/// Generally speaking, if you have to resort to using this just to get the plan to work properly,
/// you’re probably doing something wrong. Solutions like this are very unreliable.
/// </remarks>
public sealed partial class Cost : BaseGoapAction<Cost>
{
    [DataField(required: true)]
    public float Value;
}

public sealed class CostSystem : GoapActionSystem<Cost>
{
    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Cost action)
        => action.Value;
}
