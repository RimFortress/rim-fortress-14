using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Content.Shared._RF.Trigger.Components.Effects;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.Systems;

public sealed partial class InterruptGoalOnTriggerSystem : EntitySystem
{
    [Dependency] private SharedGoapSystem _goap = default!;
    [Dependency] private SharedUtilityAiSystem _utilityAi = default!;

    [SubscribeLocalEvent]
    private void OnTrigger(Entity<InterruptGoalOnTriggerComponent> ent, ref TriggerEvent ev)
    {
        if (ev.Key == null
            || !ent.Comp.Goals.TryGetValue(ev.Key, out var goals)
            || !_utilityAi.TryGetCurrentGoal(ent.Owner, out var current)
            || !goals.Contains(current.Value)
            || !TryComp(ent, out GoapComponent? goap))
            return;

        _goap.PlanShutdown(new(ent, goap), GoapPlanFinishReason.Interrupted);
    }
}
