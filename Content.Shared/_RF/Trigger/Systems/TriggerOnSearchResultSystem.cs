using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._RF.Trigger.Systems;

public sealed partial class TriggerOnSearchResultSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = null!;

    [SubscribeLocalEvent]
    private void OnSearchResultChanged(Entity<TriggerOnSearchResultComponent> ent, ref SearchResultChangedEvent args)
    {
        _trigger.Trigger(ent, args.Entity, $"{args.Query}.{(args.Added ? "added" : "removed")}", predicted: false);
    }
}
