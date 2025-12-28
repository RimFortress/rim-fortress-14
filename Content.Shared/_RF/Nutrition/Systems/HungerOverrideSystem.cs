using System.Linq;
using Content.Shared._RF.Needs.Systems;
using Content.Shared._RF.Nutrition.Components;
using Content.Shared._RF.World;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Random;

namespace Content.Shared._RF.Nutrition.Systems;

public sealed class HungerOverrideSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRimFortressWorldSystem _world = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<HungerOverrideComponent, ComponentInit>(OnComponentInit, after: new[] { typeof(HungerSystem) });
    }

    private void OnComponentInit(EntityUid uid, HungerOverrideComponent component, ComponentInit args)
    {
        if (!TryComp(uid, out HungerComponent? hunger))
            return;

        hunger.BaseDecayRate = 1;
        hunger.HungerThresholdDecayModifiers = NeedsSystem.CalculateDecayRates(
            hunger.Thresholds,
            component.ThresholdDecayTime.ToDictionary(kv => kv.Key, kv => _world.FromWorldTime(kv.Value)),
            hunger.ThresholdUpdateRate);

        DirtyField(uid, hunger, nameof(hunger.BaseDecayRate));
        DirtyField(uid, hunger, nameof(hunger.HungerThresholdDecayModifiers));

        _hunger.SetHunger(uid, component.RandomizeValue?.Next(_random) ?? _hunger.GetHunger(hunger));
    }
}
