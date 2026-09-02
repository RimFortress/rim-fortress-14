using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the number of invitations to a situations that the user has received from other entities.
/// </summary>
public sealed partial class EngagementInvites : BaseMathCurve<EngagementInvites>
{
    /// <summary>
    /// A dataset containing only the situations that will be included in the calculation.
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype>? Dataset;
}

public sealed partial class EngagementInvitesCurveSystem : MathCurveSystem<EngagementInvites>
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private readonly EntityQuery<EngagementParticipantComponent> _partQuery = default!;
    [Dependency] private readonly EntityQuery<EngagementComponent> _engageQuery = default!;

    protected override float Curve(EngagementInvites curve, float input, MathCurveContext ctx)
    {
        if (!_partQuery.TryComp(ctx.User, out var comp))
            return 0f;

        if (!_proto.TryIndex(curve.Dataset, out var set))
            return comp.Invites.Count;

        var prototypes = set.Values.ToList();
        var count = 0f;

        foreach (var invite in comp.Invites)
        {
            if (_engageQuery.TryComp(invite.EngageUid, out var engageComp)
                && prototypes.Contains(engageComp.Kind.Id))
                count++;
        }

        return count;
    }
}
