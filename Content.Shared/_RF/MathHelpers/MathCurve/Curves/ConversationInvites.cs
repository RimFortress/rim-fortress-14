using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Robust.Shared.Timing;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the number of invitations to a conversation that the user has received from other entities.
/// </summary>
public sealed partial class ConversationInvites : BaseMathCurve<ConversationInvites>
{
    /// <summary>
    /// Will only accepted invitations be included in the count?
    /// </summary>
    [DataField]
    public bool AcceptedOnly;
}

public sealed class ConversationInvitesCurveSystem : MathCurveSystem<ConversationInvites>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityQuery<GoapComponent> _goapQuery = default!;

    protected override float Curve(ConversationInvites curve, float input, EntityUid? user)
    {
        if (!_goapQuery.TryComp(user, out var goap)
            || !goap.State.TryGetValue(GoapState.ConversationInvitesKey, out var invites))
            return 0f;

        return invites.Count(x
            => x.Value.ValidUntil >= _timing.CurTime && (!curve.AcceptedOnly || x.Value.Accespted));
    }
}
