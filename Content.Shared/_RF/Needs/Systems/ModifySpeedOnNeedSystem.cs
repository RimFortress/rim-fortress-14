using Content.Shared._RF.Needs.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._RF.Needs.Systems;

/// <summary>
/// Manages <see cref="ModifySpeedOnNeedComponent"/>
/// </summary>
public sealed class ModifySpeedOnNeedSystem : EntitySystem
{
    [Dependency] private readonly NeedsSystem _needs = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ModifySpeedOnNeedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshModifiers);
        SubscribeLocalEvent<ModifySpeedOnNeedComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ModifySpeedOnNeedComponent, ComponentRemove>(OnRemoved);
        SubscribeLocalEvent<ModifySpeedOnNeedComponent, NeedThresholdChangedEvent>(OnThresholdChanged);
    }

    private void OnRefreshModifiers(Entity<ModifySpeedOnNeedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        foreach (var (proto, modifiers) in ent.Comp.Modifiers)
        {
            if (_needs.TryGetThreshold(ent.Owner, proto, out var threshold)
                && modifiers.TryGetValue(threshold, out var modifier))
                args.ModifySpeed(modifier);
        }
    }

    private void OnInit(Entity<ModifySpeedOnNeedComponent> ent, ref ComponentInit args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRemoved(Entity<ModifySpeedOnNeedComponent> ent, ref ComponentRemove args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnThresholdChanged(Entity<ModifySpeedOnNeedComponent> ent, ref NeedThresholdChangedEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }
}
