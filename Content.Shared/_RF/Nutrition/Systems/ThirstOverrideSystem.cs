using System.Linq;
using Content.Shared._RF.Nutrition.Components;
using Content.Shared._RF.World;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Random;

namespace Content.Shared._RF.Nutrition.Systems;

public sealed class ThirstOverrideSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRimFortressWorldSystem _world = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ThirstOverrideComponent, ComponentInit>(OnComponentInit, after: new[] { typeof(HungerSystem) });
    }

    private void OnComponentInit(EntityUid uid, ThirstOverrideComponent component, ComponentInit args)
    {
        if (!TryComp(uid, out ThirstComponent? thirst))
            return;

        thirst.BaseDecayRate = thirst.ThirstThresholds.Max(x => x.Value)
                               / (float)(_world.FromWorldTime(component.FullDecayTime) / thirst.UpdateRate);
        _thirst.SetThirst(uid, thirst, component.RandomizeValue?.Next(_random) ?? thirst.CurrentThirst);
        DirtyField(uid, thirst, nameof(ThirstComponent.BaseDecayRate));
    }
}
