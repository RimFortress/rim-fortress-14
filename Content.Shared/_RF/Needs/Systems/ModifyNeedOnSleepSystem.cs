using Content.Shared._RF.Needs.Components;
using Content.Shared.Bed.Sleep;
using Robust.Shared.Timing;

namespace Content.Shared._RF.Needs.Systems;

/// <summary>
/// Manages <see cref="ModifyNeedOnSleepComponent"/>
/// </summary>
public sealed class ModifyNeedOnSleepSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NeedsSystem _needs = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SleepingComponent, ModifyNeedOnSleepComponent>();
        while (query.MoveNext(out var uid, out _, out var comp))
        {
            if (comp.NextUpdate > _timing.CurTime)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.UpdateRate;

            foreach (var (need, modifiers) in comp.Modifiers)
            {
                if (_needs.TryGetThreshold(uid, need, out var threshold)
                    && modifiers.TryGetValue(threshold, out var modifier))
                    _needs.AddValue(uid, need, modifier);
            }
        }
    }
}
