using Content.Shared._RF.Needs.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._RF.Needs.Systems;

/// <summary>
/// Manages <see cref="ModifySpeedOnNeedComponent"/>
/// </summary>
public sealed class ModifySpeedOnNeedSystem : EntitySystem
{
    [Dependency] private readonly NeedsSystem _needs = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ModifySpeedOnNeedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshModifiers);
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
}
