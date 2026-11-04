using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Fills a role that nobody has in the situations of a kind that the target owns, see <see cref="OwnershipSystem"/>, with one of
/// those who have another role in them, the one who is best by the rank. It is how a squad gets a new leader when it has lost
/// the old one.
/// </summary>
public sealed partial class PromoteEngagementActor : EntityEffectBase<PromoteEngagementActor>
{
    /// <summary>
    /// The kind of the situations.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Engagement;

    /// <summary>
    /// The role that is filled.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EngagementRolePrototype> Role;

    /// <summary>
    /// How good a candidate is, calculated with the candidate as the user. The first one if it is not set.
    /// </summary>
    [DataField]
    public List<MathCurve> Rank = new();
}

public sealed partial class PromoteEngagementActorEntityEffectsSystem
    : EntityEffectSystem<MetaDataComponent, PromoteEngagementActor>
{
    [Dependency] private EngagementSystem _engagement = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private MathCurvesSystem _curves = null!;

    [Dependency] private EntityQuery<EngagementComponent> _engagementQuery;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<PromoteEngagementActor> args)
    {
        var rank = args.Effect.Rank;

        foreach (var owned in _ownership.GetOwned(ent).ToArray())
        {
            if (!_engagementQuery.TryComp(owned, out var comp) || Prototype(owned)?.ID != args.Effect.Engagement.Id)
                continue;

            if (_engagement.TryGetActors((owned, comp), args.Effect.Role, out _))
                continue;

            var candidates = comp.Actors.Values.SelectMany(x => x).ToList();

            if (candidates.Count == 0)
                continue;

            var actor = candidates.MaxBy(x => _curves.Get(rank, 0f, x));

            if (_engagement.LeaveEngagement((owned, comp), actor, EngagementEndReason.Finished))
                _engagement.TryJoinEngagement((owned, comp), args.Effect.Role, actor);
        }
    }
}
