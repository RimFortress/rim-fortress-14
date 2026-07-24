using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.CombatMode;
using Content.Server.DoAfter;
using Content.Server.Interaction;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
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
public sealed class InteractWithSystem : GoapActionSystem<InteractWith>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly CombatModeSystem _combatMode = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly EntityQuery<DoAfterComponent> _doAfterQuery = default!;

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
        => !TryGetValue(ent, action, action.TargetKey, out var target)
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
        // Handle ongoing doAfter, and store the doAfter.nextId so we can detect if we started one
        ushort nextId = 0;
        if (_doAfterQuery.TryComp(ent, out var doAfter))
        {
            // if currentDoAfter contains something, we have an active doAfter
            if (TryGetValue(ent, action, currentDoAfter, out var doAfterId))
            {
                var status = _doAfterSystem.GetStatus(ent, doAfterId);
                switch (status)
                {
                    case DoAfterStatus.Running:
                        return GoapActionResult.Continuing;
                    case DoAfterStatus.Finished:
                        CreateDump(ent, action, $"doAfter returned status '{status}' at {_timing.CurTime}");
                        return GoapActionResult.Finished;
                    default:
                        CreateDump(ent, action, $"doAfter returned status '{status}' at {_timing.CurTime}");
                        return GoapActionResult.Failed;
                }
            }

            nextId = doAfter.NextId;
        }

        if (TryComp<UseDelayComponent>(ent, out var useDelay)
            && _useDelay.IsDelayed(new(ent, useDelay)))
            return GoapActionResult.Continuing;

        if (TryComp<CombatModeComponent>(ent, out var combatMode))
            _combatMode.SetInCombatMode(ent, false, combatMode);

        _interaction.UserInteraction(ent, Transform(target).Coordinates, target);

        // Detect doAfter, save it, and don't exit from this operator
        if (doAfter != null && nextId != doAfter.NextId)
        {
            CreateDump(ent, action, $"started doAfter {nextId} at {_timing.CurTime}");
            ent.Comp.State.SetValue(currentDoAfter, nextId);
            return GoapActionResult.Continuing;
        }

        // We shouldn't arrive here if we start a doafter, so fail if we expected a doafter
        if (!expectDoAfter)
            return GoapActionResult.Finished;

        CreateDump(ent, action, "expected doAfter, but not started");
        return GoapActionResult.Failed;
    }
}
