using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the number of situations in which the user is involved.
/// </summary>
public sealed partial class EngagementMembership : BaseMathCurve<EngagementMembership>
{
    /// <summary>
    /// A dataset containing only the situations that will be included in the calculation.
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype>? Dataset;
}

public sealed partial class EngagementMembershipCurveSystem : MathCurveSystem<EngagementMembership>
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EntityQuery<EngagementParticipantComponent> _partQuery = default!;
    [Dependency] private EntityQuery<EngagementComponent> _engageQuery = default!;

    protected override float Curve(EngagementMembership curve, float input, MathCurveContext ctx)
    {
        if (!_partQuery.TryComp(ctx.User, out var comp))
            return 0f;

        if (!_proto.TryIndex(curve.Dataset, out var set))
            return comp.Membership.Count;

        var prototypes = set.Values.ToList();
        var count = 0f;

        foreach (var (uid, _) in comp.Membership)
        {
            if (_engageQuery.TryComp(uid, out var engageComp)
                && prototypes.Contains(engageComp.Kind.Id))
                count++;
        }

        return count;
    }
}
