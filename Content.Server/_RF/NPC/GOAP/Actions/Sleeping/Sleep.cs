using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Bed.Sleep;

namespace Content.Server._RF.NPC.GOAP.Actions.Sleeping;

/// <summary>
/// Makes the agent fall asleep.
/// </summary>
public sealed partial class Sleep : BaseGoapAction<Sleep>;

public sealed class SleepActionSystem : GoapActionSystem<Sleep>
{
    [Dependency] private readonly SleepingSystem _sleeping = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Sleep action) => 2f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Sleep action)
    {
        if (HasComp<SleepingComponent>(ent))
        {
            CreateDump("agent already sleeps");
            return true;
        }

        return _sleeping.TrySleeping(ent.Owner);
    }
}
