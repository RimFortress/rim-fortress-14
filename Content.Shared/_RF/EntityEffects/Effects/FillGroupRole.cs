using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Groups.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Fills a role that nobody has in the groups of a kind that the target owns (<see cref="GroupsSystem.FillRole"/>) with one of those
/// who have another role in them, the one who is best by the rank. It is how a squad gets a new leader when it has lost the old one.
/// </summary>
public sealed partial class FillGroupRole : EntityEffectBase<FillGroupRole>
{
    /// <summary>
    /// The kind of the groups.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Group;

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

public sealed partial class FillGroupRoleEntityEffectsSystem : EntityEffectSystem<MetaDataComponent, FillGroupRole>
{
    [Dependency] private GroupsSystem _groups = null!;
    [Dependency] private MathCurvesSystem _curves = null!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<FillGroupRole> args)
    {
        var rank = args.Effect.Rank;

        foreach (var group in _groups.GetGroups(ent, args.Effect.Group))
        {
            _groups.FillRole(group, args.Effect.Role, rank.Count == 0 ? null : x => _curves.Get(rank, 0f, x));
        }
    }
}
