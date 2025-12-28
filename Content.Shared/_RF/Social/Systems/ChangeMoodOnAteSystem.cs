using Content.Shared._RF.Social.Components;
using Content.Shared.Nutrition;
using Content.Shared.Whitelist;

namespace Content.Shared._RF.Social.Systems;

public sealed class ChangeMoodOnAteSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SocialSystem _social = default!;

    private EntityQuery<ChangeMoodOnAteComponent> _query;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MetaDataComponent, FullyEatenEvent>(OnFullyEaten);

        _query = GetEntityQuery<ChangeMoodOnAteComponent>();
    }

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
