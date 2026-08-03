using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.NPC.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Server._RF.NPC.GOAP.Actions.Combat;

// TODO: docs
public sealed partial class Juke : BaseGoapAction<Juke>
{
    [DataField]
    public JukeType JukeType = JukeType.AdjacentTile;
}

public sealed class JukeActionSystem : GoapActionSystem<Juke>
{
    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Juke action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Juke action)
    {
        EnsureComp<NPCJukeComponent>(ent).JukeType = action.JukeType;
        return true;
    }

    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, Juke action, GoapPlanFinishReason reason)
    {
        RemComp<NPCJukeComponent>(ent);
    }
}
