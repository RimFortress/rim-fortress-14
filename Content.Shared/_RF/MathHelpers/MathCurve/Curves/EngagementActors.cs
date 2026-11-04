using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Groups.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns how many participants the situations of a kind that the user owns have in a role, and satisfy the conditions.
/// A situation belongs to the user when the user is among its owners, see <see cref="OwnershipSystem"/>.
/// </summary>
public sealed partial class EngagementActors : BaseMathCurve<EngagementActors>
{
    /// <summary>
    /// The kind of the situations, the prototype.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Engagement;

    /// <summary>
    /// The role that is counted, all roles if it is not set.
    /// </summary>
    [DataField]
    public ProtoId<EngagementRolePrototype>? Role;

    /// <summary>
    /// What the participants have to be like to be counted: the conditions are checked with their state, so the conditions that
    /// look at the owner of the state, such as that of the mob state, are the ones that fit. A participant that is not an
    /// agent satisfies none.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<GoapCondition> Conditions = new();
}

public sealed partial class EngagementActorsCurveSystem : MathCurveSystem<EngagementActors>
{
    [Dependency] private GroupsSystem _groups = default!;
    [Dependency] private SharedGoapSystem _goap = default!;

    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    protected override float Curve(EngagementActors curve, float input, MathCurveContext ctx)
    {
        if (ctx.User is not { } user)
            return 0f;

        var count = 0;

        foreach (var actor in _groups.GetMembers(user, curve.Engagement, curve.Role))
        {
            if (curve.Conditions.Count == 0
                || _goapQuery.TryComp(actor, out var goap) && _goap.CheckCondition(actor, goap.State, curve.Conditions))
                count++;
        }

        return count;
    }
}
