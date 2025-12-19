using Content.Server._RF.NPC.Systems;
using Content.Server._RF.Trigger.Components.Effects;
using Content.Shared.Trigger;

namespace Content.Server._RF.Trigger.Systems;

public sealed class SetNpcTaskOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly NpcControlSystem _control = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<SetNpcTaskOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<SetNpcTaskOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.KeysIn.Contains(args.Key))
            return;

        _control.TrySetPassiveTask(ent.Owner, ent.Comp.Task);
        args.Handled = true;
    }
}
