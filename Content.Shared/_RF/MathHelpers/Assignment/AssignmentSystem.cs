using System.Linq;
using System.Numerics;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.EntityConditions;
using JetBrains.Annotations;

namespace Content.Shared._RF.MathHelpers.Assignment;

/// <summary>
/// The distribution of entities between buckets (<see cref="Assignment"/>) where the weight of an entity and the cost of giving it to
/// a bucket are what the curves say. It is for anything that is a set of entities shared out by the strength, the distance, the load:
/// the targets between the squads of a raid, jobs between colonists, kits between raiders, seats between guests.
/// What the buckets are, and what to do with the result, is up to the one who uses it.
/// </summary>
public sealed class AssignmentSystem : EntitySystem
{
    [Dependency] private EntityScoringSystem _scoring = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private SharedTransformSystem _transform = null!;

    /// <summary>
    /// Gives the entities to the buckets.
    /// </summary>
    /// <param name="items">The entities.</param>
    /// <param name="buckets">The buckets, their loads are changed. The keys are the entities that the bucket is for.</param>
    /// <param name="weight">How much an entity weighs, calculated with the entity as the user. The heaviest are given first.</param>
    /// <param name="cost">What it costs a bucket to take an entity, calculated with the entity as the user. The least wins. The
    /// curves see the variables that are passed, those of the bucket (<see cref="AssignmentBucket{TKey}.Variables"/>) and:
    /// <c>weight</c> of the entity, <c>load</c> of the bucket, <c>capacity</c> of the bucket, <c>count</c> of the items it has
    /// got, <c>distance</c> from the bucket to the entity if the bucket has a position.</param>
    /// <param name="current">The bucket that an entity has now, see <see cref="Assignment.Assign"/>.</param>
    /// <param name="variables">The variables that the curves see, such as those of the owner of the process.</param>
    /// <param name="conditions">What an entity has to be like to be given, all if it is not set.</param>
    /// <param name="source">The entity that the conditions look from.</param>
    /// <param name="skipWeightless">Do not give the entities that weigh nothing (not more than 0).</param>
    [PublicAPI]
    public AssignmentResult<EntityUid, EntityUid> AssignEntities(
        IEnumerable<EntityUid> items,
        IReadOnlyList<AssignmentBucket<EntityUid>> buckets,
        IEnumerable<MathCurve.MathCurve> weight,
        IEnumerable<MathCurve.MathCurve> cost,
        Func<EntityUid, EntityUid?>? current = null,
        Dictionary<string, float>? variables = null,
        EntityCondition[]? conditions = null,
        EntityUid? source = null,
        bool skipWeightless = false)
    {
        var weighted = _scoring.Score(items, weight, conditions, variables, positiveOnly: skipWeightless, source: source)
            .Select(x => (x.Uid, x.Score));
        var costCurves = cost as IReadOnlyCollection<MathCurve.MathCurve> ?? cost.ToList();

        return Assignment.Assign(weighted, buckets, (item, itemWeight, bucket) =>
        {
            var costVariables = new Dictionary<string, float>(variables ?? new())
            {
                ["weight"] = itemWeight,
                ["load"] = bucket.Load,
                ["capacity"] = bucket.Capacity,
                ["count"] = bucket.Count,
            };

            foreach (var (key, value) in bucket.Variables)
            {
                costVariables[key] = value;
            }

            if (bucket.Position is { } position)
                costVariables["distance"] = Vector2.Distance(position, _transform.GetWorldPosition(item));

            return _curves.Get(costCurves, 0f, item, costVariables);
        }, current == null ? null : (EntityUid item, out EntityUid key) =>
        {
            var found = current(item);
            key = found ?? default;
            return found != null;
        });
    }
}
