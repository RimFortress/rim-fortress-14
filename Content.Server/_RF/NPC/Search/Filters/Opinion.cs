using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities by the opinion of the agent about them: what effects it has on it (all of them), and how good or bad it is.
/// </summary>
public sealed partial class Opinion : BaseSearchFilter<Opinion>
{
    /// <summary>
    /// The opinion effects of the agent about the entity that it must have, all of them.
    /// </summary>
    [DataField]
    public List<ProtoId<SocialEffectPrototype>> HasEffects = new();

    /// <summary>
    /// The opinion must be more than this.
    /// </summary>
    [DataField]
    public int? MoreThan;

    /// <summary>
    /// The opinion must be less than this.
    /// </summary>
    [DataField]
    public int? LessThan;
}

public sealed partial class OpinionSearchFilterSystem : NpcSearchFilterSystem<Opinion>
{
    [Dependency] private SocialSystem _social = null!;

    [SubscribeLocalEvent]
    private void OnOpinionChanged(Entity<SocialComponent> ent, ref OpinionChangedEvent args) => DirtyFilter(args.Other);

    protected override bool Filter(GoapState state, EntityUid target, Opinion filter)
    {
        var owner = Goap.GetValue(state, GoapState.Owner);

        foreach (var effect in filter.HasEffects)
        {
            if (!_social.HasOpinionEffect(owner, target, effect))
                return false;
        }

        if (filter.MoreThan == null && filter.LessThan == null)
            return true;

        var opinion = _social.GetOpinion(owner, target);

        return (filter.MoreThan == null || opinion > filter.MoreThan)
               && (filter.LessThan == null || opinion < filter.LessThan);
    }
}
