using Content.Shared._RF.Trigger.Components.Triggers;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._RF.Trigger.Systems;

public sealed class TriggerOnFullyAteSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<EdibleComponent, FullyEatenEvent>(OnFullyEaten);
    }

    private void OnFullyEaten(Entity<EdibleComponent> ent, ref FullyEatenEvent args)
    {
        if (TryComp<TriggerOnFullyAteComponent>(args.User, out var comp))
            _trigger.Trigger(args.User, ent, comp.KeyOut);
    }
}
