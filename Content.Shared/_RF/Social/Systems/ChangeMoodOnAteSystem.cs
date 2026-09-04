using Content.Shared._RF.Social.Components;
using Content.Shared.Nutrition;
using Content.Shared.Whitelist;

namespace Content.Shared._RF.Social.Systems;

public sealed partial class ChangeMoodOnAteSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SocialSystem _social = default!;

    [Dependency] private EntityQuery<ChangeMoodOnAteComponent> _query = default!;

    [SubscribeLocalEvent]
    private void OnFullyEaten(Entity<MetaDataComponent> ent, ref FullyEatenEvent args)
    {
        if (!_query.TryComp(args.User, out var comp))
            return;

        foreach (var (protoId, whitelist) in comp.Effects)
        {
            if (!_whitelist.IsWhitelistPass(whitelist, ent))
                continue;

            _social.AddMoodEffect(args.User, protoId);

            if (comp.FirstSuitable)
                break;
        }
    }
}
