using Content.Shared._RF.Needs.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._RF.Needs.Systems;

/// <summary>
/// Manages <see cref="ModifySpeedOnNeedComponent"/>
/// </summary>
public sealed partial class ModifySpeedOnNeedSystem : EntitySystem
{
    [Dependency] private NeedsSystem _needs = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    [SubscribeLocalEvent]
    private void OnRefreshModifiers(Entity<ModifySpeedOnNeedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        foreach (var (category, modifiers) in ent.Comp.Modifiers)
        {
            if (_needs.TryGetThreshold(ent.Owner, category, out var threshold, out _)
                && modifiers.TryGetValue(threshold.Value, out var modifier))
                args.ModifySpeed(modifier);
        }
    }

    [SubscribeLocalEvent]
    private void OnInit(Entity<ModifySpeedOnNeedComponent> ent, ref ComponentInit args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<ModifySpeedOnNeedComponent> ent, ref ComponentRemove args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnThresholdChanged(Entity<ModifySpeedOnNeedComponent> ent, ref NeedThresholdChangedEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }
}
