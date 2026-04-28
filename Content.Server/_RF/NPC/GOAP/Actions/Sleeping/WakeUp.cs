using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Bed.Sleep;

namespace Content.Server._RF.NPC.GOAP.Actions.Sleeping;

/// <summary>
/// Makes the agent wake up.
/// </summary>
public sealed partial class WakeUp : BaseGoapAction<WakeUp>;

public sealed class WakeUpActionSystem : GoapActionSystem<WakeUp>
{
    [Dependency] private readonly SleepingSystem _sleeping = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, WakeUp action) => 0.5f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, WakeUp action)
    {
        if (!HasComp<SleepingComponent>(ent))
        {
            CreateDump(ent, action, "agent isn't sleeping");
            return true;
        }

        if (_sleeping.TryWaking(ent.Owner))
            return true;

        return false;
    }
}
