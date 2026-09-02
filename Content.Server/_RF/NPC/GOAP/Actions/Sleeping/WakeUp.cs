using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Bed.Sleep;

namespace Content.Server._RF.NPC.GOAP.Actions.Sleeping;

/// <summary>
/// Makes the agent wake up.
/// </summary>
public sealed partial class WakeUp : BaseGoapAction<WakeUp>;

public sealed partial class WakeUpActionSystem : GoapActionSystem<WakeUp>
{
    [Dependency] private SleepingSystem _sleeping = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, WakeUp action) => 0.5f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, WakeUp action)
    {
        if (!HasComp<SleepingComponent>(ent))
        {
            CreateDump("agent isn't sleeping");
            return true;
        }

        return _sleeping.TryWaking(ent.Owner);
    }
}
