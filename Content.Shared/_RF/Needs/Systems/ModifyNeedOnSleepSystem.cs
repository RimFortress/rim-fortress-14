using Content.Shared._RF.Needs.Components;
using Content.Shared.Bed.Sleep;

namespace Content.Shared._RF.Needs.Systems;

/// <summary>
/// Manages <see cref="ModifyNeedOnSleepComponent"/>
/// </summary>
public sealed class ModifyNeedOnSleepSystem : EntitySystem
{
    [Dependency] private readonly NeedsSystem _needs = default!;

    private EntityQuery<SleepingComponent> _query;

    /// <inheritdoc/>
    public override void Initialize()
    {
        _query = GetEntityQuery<SleepingComponent>();

        SubscribeLocalEvent<ModifyNeedOnSleepComponent, GetNeedDecayRateEvent>(OnGetNeedDecayRate);
    }

    private void OnGetNeedDecayRate(Entity<ModifyNeedOnSleepComponent> ent, ref GetNeedDecayRateEvent args)
    {
        if (_query.TryComp(ent, out _)
            && ent.Comp.DecayRateModifiers.TryGetValue(args.Need, out var modifiers)
            && _needs.TryGetThreshold(ent.Owner, args.Need, out var threshold)
            && modifiers.TryGetValue(threshold, out var modifier))
            args.Modifier *= modifier;
    }
}
