using Content.Shared._RF.MathHelpers.Assignment;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.EntityConditions;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Folds a set of entities into a number, see <see cref="EntityScoringSystem"/>: the score of each entity is what the curves give
/// with the entity as the user, and the set is the sum, the mean, the greatest, the least of the scores, or how many entities there
/// are. The entities that do not meet the conditions are not counted. Unlike <see cref="ForEachEntity"/> it does not carry the result
/// from one entity to the next, so it is the one for the "strongest", "how many are alive", "the mean health". The entities are
/// what a key of the state of the user is, the same as for <see cref="ForEachEntity"/>. The user must have a
/// <see cref="GoapComponent"/>.
/// </summary>
public sealed partial class AggregateEntities : BaseMathCurve<AggregateEntities>
{
    /// <summary>
    /// The key of the state of the user with the entities.
    /// </summary>
    [DataField(required: true)]
    public StateKey<IReadOnlyCollection<EntityUid>> Key;

    /// <summary>
    /// How the scores are folded.
    /// </summary>
    [DataField]
    public EntityAggregate Kind = EntityAggregate.Sum;

    /// <summary>
    /// The score of an entity, calculated with the entity as the user. Not needed to count.
    /// </summary>
    [DataField]
    public List<MathCurve> Curves = new();

    /// <summary>
    /// What an entity has to be like to be counted, all of them if it is not set. The user is the source of the conditions.
    /// </summary>
    [DataField]
    public EntityCondition[]? Conditions;
}

public sealed partial class AggregateEntitiesCurveSystem : MathCurveSystem<AggregateEntities>
{
    [Dependency] private EntityScoringSystem _scoring = default!;
    [Dependency] private SharedGoapSystem _goap = default!;

    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    protected override float Curve(AggregateEntities curve, float input, MathCurveContext ctx)
    {
        if (ctx.User is not { } user
            || !_goapQuery.TryComp(user, out var goap)
            || !_goap.TryGetValue(goap.State, curve.Key, out var entities))
            return 0f;

        return _scoring.Aggregate(entities, curve.Curves, curve.Kind, curve.Conditions, ctx.Variables, user);
    }
}
