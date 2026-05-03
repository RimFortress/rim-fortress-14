using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.CombatMode;
using Content.Server.DoAfter;
using Content.Server.Interaction;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.CombatMode;
using Content.Shared.DoAfter;
using Content.Shared.Timing;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Causes the agent to interact with the target entity.
/// </summary>
public sealed partial class InteractWith : BaseGoapAction<InteractWith>
{
    /// <summary>
    /// Key that contains the target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// Exit with failure if doafter wasn't raised
    /// </summary>
    [DataField]
    public bool ExpectDoAfter;

    public StateKey<ushort> CurrentDoAfter = "CurrentInteractWithDoAfter";
}

public sealed class InteractWithSystem : GoapActionSystem<InteractWith>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly CombatModeSystem _combatMode = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly EntityQuery<DoAfterComponent> _doAfterQuery;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, InteractWith action) => 1f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, InteractWith action)
    {
        ent.Comp.State.Remove(action.CurrentDoAfter);
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, InteractWith action)
    {
        ent.Comp.State.Remove(action.CurrentDoAfter);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, InteractWith action)
    {
        var state = ent.Comp.State;
        var owner = state.GetValue(GoapState.Owner);

        // Handle ongoing doAfter, and store the doAfter.nextId so we can detect if we started one
        ushort nextId = 0;
        if (_doAfterQuery.TryComp(owner, out var doAfter))
        {
            // if CurrentDoAfter contains something, we have an active doAfter
            if (Goap.TryGetValue(state, action.CurrentDoAfter, out var doAfterId))
            {
                var status = _doAfterSystem.GetStatus(owner, doAfterId, null);
                switch (status)
                {
                    case DoAfterStatus.Running:
                        return GoapActionResult.Continuing;
                    case DoAfterStatus.Finished:
                        CreateDump(ent, action, $"doAster returned status '{status}' at {_timing.CurTime}");
                        return GoapActionResult.Finished;
                    default:
                        CreateDump(ent, action, $"doAster returned status '{status}' at {_timing.CurTime}");
                        return GoapActionResult.Failed;
                }
            }

            nextId = doAfter.NextId;
        }

        if (!Goap.TryGetValue(state, action.TargetKey, out var target))
        {
            KeyNotFound(ent, action, action.TargetKey);
            return GoapActionResult.Failed;
        }

        if (TryComp<UseDelayComponent>(owner, out var useDelay)
            && _useDelay.IsDelayed(new(owner, useDelay)))
            return GoapActionResult.Continuing;

        if (TryComp<CombatModeComponent>(owner, out var combatMode))
            _combatMode.SetInCombatMode(owner, false, combatMode);

        _interaction.UserInteraction(owner, Transform(target).Coordinates, target);

        // Detect doAfter, save it, and don't exit from this operator
        if (doAfter != null && nextId != doAfter.NextId)
        {
            CreateDump(ent, action, $"started doAfter {nextId} at {_timing.CurTime}");
            state.SetValue(action.CurrentDoAfter, nextId);
            return GoapActionResult.Continuing;
        }

        // We shouldn't arrive here if we start a doafter, so fail if we expected a doafter
        if (action.ExpectDoAfter)
        {
            CreateDump(ent, action, "expected doAfter, but not started");
            return GoapActionResult.Failed;
        }

        return GoapActionResult.Finished;
    }
}
