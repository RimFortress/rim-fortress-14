using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Basic;

/// <summary>
/// Copies the value of the target key and stores it in another key.
/// </summary>
public sealed partial class CopyTo : BaseGoapAction<CopyTo>
{
    /// <summary>
    /// The key whose value will be copied.
    /// </summary>
    [DataField(required: true)]
    public StateKey<object> From;

    /// <summary>
    /// The key in which the copy will be saved.
    /// </summary>
    [DataField(required: true)]
    public StateKey<object> To;
}

public sealed partial class CopyToSystem : GoapActionSystem<CopyTo>
{
    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, CopyTo action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, CopyTo action)
    {
        if (!TryGet(ent, action.From, out var from))
            return false;

        Set(ent, action.To, from);
        return true;
    }
}
