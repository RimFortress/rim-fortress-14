using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Goes over a list of entities: for each of them the curves are calculated, with the entity as the user, and the input of
/// them is what the curves have given for the one before. It starts with <see cref="Initial"/> (0), and what the last entity
/// gives is the result, so it is a sum, a product, a maximum, whatever the curves make of the previous and the entity. The input
/// of the curve itself is not used. The variables of the curve are the same for every entity.
/// The entities are what a key of the state of the user is, so they can be anything that a key can be: the results of a search
/// query (<c>Query/Name/All</c>), the participants of the situations that the user owns (<c>OwnedEngagement/Raid/Role/Raider/All</c>),
/// a set that was put into the state. The user must have a <see cref="GoapComponent"/>.
/// </summary>
public sealed partial class ForEachEntity : BaseMathCurve<ForEachEntity>
{
    /// <summary>
    /// The key of the state of the user with the entities.
    /// </summary>
    [DataField(required: true)]
    public StateKey<IReadOnlyCollection<EntityUid>> Key;

    /// <summary>
    /// What it starts with, the curves are calculated from 0.
    /// </summary>
    [DataField]
    public List<MathCurve> Initial = new();

    /// <summary>
    /// What is calculated for an entity. The input is the result for the previous one.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Curves = new();
}

public sealed partial class ForEachEntityCurveSystem : MathCurveSystem<ForEachEntity>
{
    [Dependency] private MathCurvesSystem _curves = default!;
    [Dependency] private SharedGoapSystem _goap = default!;

    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    protected override float Curve(ForEachEntity curve, float input, MathCurveContext ctx)
    {
        var result = _curves.Get(curve.Initial, 0f, ctx);

        if (ctx.User is not { } user
            || !_goapQuery.TryComp(user, out var goap)
            || !_goap.TryGetValue(goap.State, curve.Key, out var entities))
            return result;

        // The curves can change what the key is.
        foreach (var uid in entities.ToArray())
        {
            result = _curves.Get(curve.Curves, result, ctx with { User = uid });
        }

        return result;
    }
}
