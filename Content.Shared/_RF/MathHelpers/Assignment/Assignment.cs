using System.Linq;
using System.Numerics;

namespace Content.Shared._RF.MathHelpers.Assignment;

/// <summary>
/// Something that items are given to: a squad that gets targets, a worker that gets jobs, a soldier that gets a kit.
/// </summary>
/// <typeparam name="TKey">What identifies the bucket for the one who uses it.</typeparam>
public sealed class AssignmentBucket<TKey>(TKey key, float capacity = 1f)
{
    public readonly TKey Key = key;

    /// <summary>
    /// How much the bucket is meant to take, such as the strength of a squad. It is what the cost looks at (with <see cref="Load"/>),
    /// the bucket takes more than that if it costs the least, see <see cref="MaxLoad"/> for the limit.
    /// </summary>
    public float Capacity = capacity;

    /// <summary>
    /// The weight of what the bucket has already got. It may start from something, such as the items that stay with it.
    /// </summary>
    public float Load;

    /// <summary>
    /// How many items the bucket has already got.
    /// </summary>
    public int Count;

    /// <summary>
    /// The most weight the bucket can take, nothing that does not fit is given to it. No limit if it is not set.
    /// </summary>
    public float? MaxLoad;

    /// <summary>
    /// The most items the bucket can take. No limit if it is not set.
    /// </summary>
    public int? MaxItems;

    /// <summary>
    /// Where the bucket is, for the cost to look at the distance.
    /// </summary>
    public Vector2? Position;

    /// <summary>
    /// Anything else the cost has to know about the bucket, such as the variables of a squad.
    /// </summary>
    public Dictionary<string, float> Variables = new();

    /// <summary>
    /// Does an item of the weight still fit in the bucket.
    /// </summary>
    public bool Fits(float weight)
        => (MaxItems == null || Count < MaxItems) && (MaxLoad == null || Load + weight <= MaxLoad);
}

/// <summary>
/// The result of <see cref="Assignment.Assign"/>.
/// </summary>
public sealed class AssignmentResult<TItem, TKey> where TKey : notnull
{
    /// <summary>
    /// The items that every bucket has got, by the keys of the buckets. All the buckets are here, even those that have got nothing.
    /// </summary>
    public readonly Dictionary<TKey, List<TItem>> Taken = new();

    /// <summary>
    /// The items that no bucket could take: they did not fit, or the cost said that they could not be given to any.
    /// </summary>
    public readonly List<TItem> Unassigned = new();
}

/// <summary>
/// Tells which bucket (the key) an item has now, if it has any.
/// </summary>
public delegate bool CurrentBucket<in TItem, TKey>(TItem item, out TKey key);

/// <summary>
/// The distribution of items between buckets, greedy: the heaviest items are given first, each to the bucket where it costs the
/// least. It is not the best distribution there can be, but it is stable, cheap, and good for the cases like squads, jobs and kits,
/// where the cost is made of what a bucket has got so far. The algorithm knows nothing about the world, the entities, the curves:
/// <see cref="AssignmentSystem"/> is the one that calculates costs from them.
/// </summary>
public static class Assignment
{
    /// <summary>
    /// Gives the items to the buckets.
    /// </summary>
    /// <param name="items">The items and their weights.</param>
    /// <param name="buckets">The buckets. Their loads are changed.</param>
    /// <param name="cost">What it costs the bucket to take the item. The least wins. Not a number or infinity means
    /// the item cannot be given to the bucket.</param>
    /// <param name="current">The bucket that the item has now (the key), if it has any. Such an item stays where it is, unless
    /// the bucket is gone or the item does not fit it, so the distribution does not change without a reason when it is made
    /// again. They are the first to be given, so the load of the others counts them.</param>
    public static AssignmentResult<TItem, TKey> Assign<TItem, TKey>(
        IEnumerable<(TItem Item, float Weight)> items,
        IReadOnlyList<AssignmentBucket<TKey>> buckets,
        Func<TItem, float, AssignmentBucket<TKey>, float> cost,
        CurrentBucket<TItem, TKey>? current = null) where TKey : notnull
    {
        var result = new AssignmentResult<TItem, TKey>();

        foreach (var bucket in buckets)
        {
            result.Taken[bucket.Key] = new List<TItem>();
        }

        var free = new List<(TItem Item, float Weight)>();

        foreach (var (item, weight) in items.OrderByDescending(x => x.Weight))
        {
            AssignmentBucket<TKey>? owner = null;

            if (current != null && current(item, out var keep))
                owner = buckets.FirstOrDefault(x => EqualityComparer<TKey>.Default.Equals(x.Key, keep));

            if (owner != null && owner.Fits(weight))
                Take(owner, item, weight);
            else
                free.Add((item, weight));
        }

        foreach (var (item, weight) in free)
        {
            AssignmentBucket<TKey>? best = null;
            var bestCost = float.MaxValue;

            foreach (var bucket in buckets)
            {
                if (!bucket.Fits(weight))
                    continue;

                var value = cost(item, weight, bucket);

                if (!float.IsFinite(value) || value >= bestCost)
                    continue;

                best = bucket;
                bestCost = value;
            }

            if (best == null)
                result.Unassigned.Add(item);
            else
                Take(best, item, weight);
        }

        return result;

        void Take(AssignmentBucket<TKey> bucket, TItem item, float weight)
        {
            bucket.Load += weight;
            bucket.Count++;
            result.Taken[bucket.Key].Add(item);
        }
    }

    /// <summary>
    /// Divides the items into the given number of lists so that the weights of the lists are about the same: the strongest of the
    /// items are the first of the lists, every other goes to the list that is the weakest at the time.
    /// </summary>
    public static List<List<T>> Partition<T>(IEnumerable<(T Item, float Weight)> items, int count)
    {
        var all = items.ToList();
        var buckets = Enumerable.Range(0, Math.Max(0, Math.Min(count, all.Count)))
            .Select(i => new AssignmentBucket<int>(i))
            .ToList();

        var result = Assign(all, buckets, (_, _, bucket) => bucket.Load);

        return buckets.Select(x => result.Taken[x.Key]).ToList();
    }
}
