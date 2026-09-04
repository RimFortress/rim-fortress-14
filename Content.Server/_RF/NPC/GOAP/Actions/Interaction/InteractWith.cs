using Content.Server.CombatMode;
using Content.Server.DoAfter;
using Content.Server.Interaction;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.CombatMode;
using Content.Shared.DoAfter;
using Content.Shared.Timing;
using JetBrains.Annotations;
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
    /// Exit with failure if doafter wasn't raised.
    /// </summary>
    [DataField]
    public bool ExpectDoAfter;

    /// <summary>
    /// The key where the ID of the current `doAfter` is stored.
    /// </summary>
    public StateKey<ushort> CurrentDoAfter = "CurrentInteractWithDoAfter";
}

/// <summary>
/// Manages the <see cref="InteractWith"/> operator and also provides
/// out-of-the-box logic for AI interaction with objects for other operators.
/// </summary>
public sealed partial class InteractWithSystem : GoapActionSystem<InteractWith>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DoAfterSystem _doAfterSystem = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private CombatModeSystem _combatMode = default!;
    [Dependency] private InteractionSystem _interaction = default!;

    [Dependency] private EntityQuery<DoAfterComponent> _doAfterQuery;

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
        => !TryGet(ent, action.TargetKey, out var target)
            ? GoapActionResult.Failed
            : DoInteraction(ent, action, target, action.CurrentDoAfter, action.ExpectDoAfter);

    /// <summary>
    /// Involves AI interaction with the target object,
    /// as if the player had clicked on it with the left mouse button,
    /// handling all the logic for `doAfter`, `UseDelay`, and `CombatMode`.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="target">Target entity.</param>
    /// <param name="currentDoAfter">The key where the ID of the current `doAfter` is stored.</param>
    /// <param name="expectDoAfter">Exit with failure if doafter wasn't raised.</param>
    /// <returns>
    /// <see cref="GoapActionResult.Finished"/> if the interaction completed successfully,
    /// <see cref="GoapActionResult.Continuing"/> if it is in progress,
    /// <see cref="GoapActionResult.Failed"/> if the interaction failed.
    /// </returns>
    [PublicAPI]
    public GoapActionResult DoInteraction(
        Entity<GoapComponent> ent,
        GoapAction action,
        EntityUid target,
        StateKey<ushort> currentDoAfter,
        bool expectDoAfter)
    {
        if (Deleted(target))
        {
            CreateDump($"{ToPrettyString(target)} deleted");
            return GoapActionResult.Failed;
        }

        var waitResult = Wait(ent, action, currentDoAfter, out var nextId);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        if (TryComp<CombatModeComponent>(ent, out var combatMode))
            _combatMode.SetInCombatMode(ent, false, combatMode);

#if TOOLS
        var handEnt = TryGet(ent, GoapState.ActiveHandEntity, out var hand)
            ? ToPrettyString(hand)
            : "hand";
        CreateDump($"interacted with {ToPrettyString(target)} using {handEnt}");
#endif
        _interaction.UserInteraction(ent, Transform(target).Coordinates, target);

        // Detect doAfter, save it, and don't exit from this operator
        if (_doAfterQuery.TryComp(ent, out var doAfter) && nextId != doAfter.NextId)
        {
            CreateDump($"started doAfter {nextId} at {_timing.CurTime}");
            ent.Comp.State.SetValue(currentDoAfter, nextId);
            return GoapActionResult.Continuing;
        }

        // We shouldn't arrive here if we start a doafter, so fail if we expected a doafter
        if (!expectDoAfter)
            return GoapActionResult.Finished;

        CreateDump("expected doAfter, but not started");
        return GoapActionResult.Failed;
    }

    [PublicAPI]
    public GoapActionResult Wait(
        Entity<GoapComponent> ent,
        GoapAction action,
        StateKey<ushort> currentDoAfter,
        out ushort nextId)
    {
        nextId = 0;

        // Handle ongoing doAfter, and store the doAfter.nextId so we can detect if we started one
        if (_doAfterQuery.TryComp(ent, out var doAfter))
        {
            // if currentDoAfter contains something, we have an active doAfter
            if (TryGet(ent, currentDoAfter, out var doAfterId))
            {
                var status = _doAfterSystem.GetStatus(ent, doAfterId);
                switch (status)
                {
                    case DoAfterStatus.Running:
                        return GoapActionResult.Continuing;
                    case DoAfterStatus.Finished:
                        CreateDump($"doAfter returned status '{status}' at {_timing.CurTime}");
                        ent.Comp.State.Remove(currentDoAfter);
                        return GoapActionResult.Finished;
                    default:
                        CreateDump($"doAfter returned status '{status}' at {_timing.CurTime}");
                        return GoapActionResult.Failed;
                }
            }

            nextId = doAfter.NextId;
        }

        if (TryComp<UseDelayComponent>(ent, out var useDelay)
            && _useDelay.IsDelayed(new(ent, useDelay)))
            return GoapActionResult.Continuing;

        ent.Comp.State.Remove(currentDoAfter);
        return GoapActionResult.Finished;
    }
}
