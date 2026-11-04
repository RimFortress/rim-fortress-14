using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.EntityConditions;
using JetBrains.Annotations;

namespace Content.Shared._RF.MathHelpers.Assignment;

/// <summary>
/// How a set of entities is folded into a number.
/// </summary>
public enum EntityAggregate
{
    /// <summary>The sum of the scores.</summary>
    Sum,

    /// <summary>The mean of the scores, 0 if there are none.</summary>
    Average,

    /// <summary>The greatest of the scores, 0 if there are none.</summary>
    Max,

    /// <summary>The least of the scores, 0 if there are none.</summary>
    Min,

    /// <summary>How many entities there are (the curves are not looked at).</summary>
    Count,
}

/// <summary>
/// The evaluator of a set of entities by curves: the score of each entity is what the curves give with the entity as their user, so
/// "how strong a fighter is", "how good a candidate is", "how bad a patient is" are the same curves, and the set is folded into
/// a number (the sum, the mean, the greatest), sorted, or the best of it is found. The entities of the set that do not meet the
/// conditions are not counted. It is the part of the "for each entity" that the effects of the groups, the distribution
/// (<see cref="AssignmentSystem"/>) and the curves have in common.
/// </summary>
public sealed class EntityScoringSystem : EntitySystem
{
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private SharedEntityConditionsSystem _conditions = null!;

    /// <summary>
    /// The scores of the entities of the set that are not gone and meet the conditions.
    /// </summary>
    /// <param name="set">The entities.</param>
    /// <param name="curves">What the score is, the input is 0.</param>
    /// <param name="conditions">What an entity has to be like to be in the set, all if it is not set.</param>
    /// <param name="variables">The variables that the curves see.</param>
    /// <param name="positiveOnly">Leave the entities that score 0 or less, such as those that cannot fight.</param>
    /// <param name="source">The entity that the conditions look from, such as the owner of the set.</param>
    [PublicAPI]
    public List<(EntityUid Uid, float Score)> Score(
        IEnumerable<EntityUid> set,
        IEnumerable<MathCurve.MathCurve> curves,
        EntityCondition[]? conditions = null,
        Dictionary<string, float>? variables = null,
        bool positiveOnly = false,
        EntityUid? source = null)
    {
        var scores = new List<(EntityUid, float)>();
        var list = curves as IReadOnlyCollection<MathCurve.MathCurve> ?? curves.ToList();

        foreach (var uid in set)
        {
            if (TerminatingOrDeleted(uid) || !_conditions.TryConditions(uid, conditions, source))
                continue;

            var score = _curves.Get(list, 0f, uid, variables);

            if (!positiveOnly || score > 0f)
                scores.Add((uid, score));
        }

        return scores;
    }

    /// <summary>
    /// Folds the scores of the set into a number.
    /// </summary>
    [PublicAPI]
    public float Aggregate(
        IEnumerable<EntityUid> set,
        IEnumerable<MathCurve.MathCurve> curves,
        EntityAggregate kind,
        EntityCondition[]? conditions = null,
        Dictionary<string, float>? variables = null,
        EntityUid? source = null)
    {
        // The curves of a count are not needed, and are not calculated.
        if (kind == EntityAggregate.Count)
            return set.Count(x => !TerminatingOrDeleted(x) && _conditions.TryConditions(x, conditions, source));

        var scores = Score(set, curves, conditions, variables, source: source);

        if (scores.Count == 0)
            return 0f;

        return kind switch
        {
            EntityAggregate.Sum => scores.Sum(x => x.Score),
            EntityAggregate.Average => scores.Average(x => x.Score),
            EntityAggregate.Max => scores.Max(x => x.Score),
            EntityAggregate.Min => scores.Min(x => x.Score),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>
    /// The entity of the set with the best (greatest) score, the first one if the curves are not set.
    /// </summary>
    [PublicAPI]
    public EntityUid? Best(
        IEnumerable<EntityUid> set,
        IEnumerable<MathCurve.MathCurve> curves,
        EntityCondition[]? conditions = null,
        Dictionary<string, float>? variables = null,
        EntityUid? source = null)
    {
        var scores = Score(set, curves, conditions, variables, source: source);

        if (scores.Count == 0)
            return null;

        var best = scores[0];

        foreach (var score in scores)
        {
            if (score.Score > best.Score)
                best = score;
        }

        return best.Uid;
    }

    /// <summary>
    /// The entities of the set from the best (greatest) score to the worst.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> Sorted(
        IEnumerable<EntityUid> set,
        IEnumerable<MathCurve.MathCurve> curves,
        EntityCondition[]? conditions = null,
        Dictionary<string, float>? variables = null,
        EntityUid? source = null)
        => Score(set, curves, conditions, variables, source: source)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Uid)
            .ToList();
}
