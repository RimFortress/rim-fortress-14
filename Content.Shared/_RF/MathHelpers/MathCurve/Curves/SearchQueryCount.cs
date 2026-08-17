using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the number of entities that match the search query.
/// The user must not be null and must have GoapComponent.
/// </summary>
public sealed partial class SearchQueryCount : BaseMathCurve<SearchQueryCount>
{
    /// <summary>
    /// Search query prototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;
}

public sealed class SearchQueryCountCurveSystem : MathCurveSystem<SearchQueryCount>
{
    [Dependency] private readonly SharedNpcSearcherSystem _npcSearcher = default!;

    protected override float Curve(SearchQueryCount curve, float input, EntityUid? user)
        => TryComp(user, out GoapComponent? goap)
            ? _npcSearcher.GetResultsCount(user.Value, goap.State, curve.Query)
            : 0f;
}
