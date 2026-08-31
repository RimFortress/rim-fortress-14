using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Search.Components;

/// <summary>
/// A component that stores a cache of entity search results.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedNpcSearcherSystem))]
public sealed partial class NpcSearcherComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<SearchQueryPrototype>, LiveSearchResult> Queries = new();

    public sealed class LiveSearchResult
    {
        /// <summary>
        /// Candidates that cleared every Filter, kept in score order via a
        /// balanced tree instead of a flat list — point insert/remove is
        /// O(log n) with no array shifting and no full re-sort, unlike a
        /// List that needs RemoveAll + Sort on every single point change.
        /// Ties broken by EntityUid so equal scores don't collide as keys.
        /// </summary>
        private readonly SortedSet<(float Score, EntityUid Uid)> _byScore = new(ScoreComparer.Instance);

        /// <summary>
        /// O(1) reverse lookup: current score for a candidate, if any — used
        /// to find its entry in <see cref="_byScore"/> without scanning.
        /// </summary>
        private readonly Dictionary<EntityUid, float> _scores = new();

        /// <summary>
        /// Materialized read view for <see cref="SharedNpcSearcherSystem.GetResults"/>.
        /// Rebuilt lazily — null'd out on any write, so a read between two
        /// writes is just a null check and a reference return, no allocation.
        /// </summary>
        private List<EntityUid>? _cache;

        /// <summary>
        /// Every candidate currently somewhere in the pipeline for this query
        /// — passed Query, resting mid-Filters, or fully scored. Mirrors
        /// what's recorded on each candidate's SearchTrackedComponent; kept
        /// here too so cleanup on agent shutdown doesn't need to scan the
        /// world for SearchTrackedComponent.
        /// </summary>
        public readonly HashSet<EntityUid> Tracked = new();

        public IReadOnlyList<EntityUid> Results => _cache ??= Rebuild();

        public int Count => _byScore.Count;

        private List<EntityUid> Rebuild()
        {
            var list = new List<EntityUid>(_byScore.Count);

            foreach (var (_, uid) in _byScore)
            {
                list.Add(uid);
            }

            return list;
        }

        /// <summary>
        /// Inserts or updates a candidate's score. A score &lt;= 0 removes it,
        /// mirroring the "positive total required to be in the result"
        /// rule the pipeline already applies everywhere else.
        /// </summary>
        [Access(typeof(SharedNpcSearcherSystem))]
        public void Upsert(EntityUid uid, float score)
        {
            if (_scores.TryGetValue(uid, out var old))
            {
                if (MathHelper.CloseToPercent(old, score, 0.01f))
                    return; // no actual change - don't invalidate the cache for nothing

                _byScore.Remove((old, uid));
            }

            if (score <= 0f)
                _scores.Remove(uid);
            else
            {
                _scores[uid] = score;
                _byScore.Add((score, uid));
            }

            _cache = null;
        }

        /// <summary>
        /// Removes a candidate from the result, if present.
        /// </summary>
        [Access(typeof(SharedNpcSearcherSystem))]
        public void Remove(EntityUid uid)
        {
            if (!_scores.Remove(uid, out var score))
                return;

            _byScore.Remove((score, uid));
            _cache = null;
        }

        private sealed class ScoreComparer : IComparer<(float Score, EntityUid Uid)>
        {
            public static readonly ScoreComparer Instance = new();

            public int Compare((float Score, EntityUid Uid) x, (float Score, EntityUid Uid) y)
            {
                var cmp = y.Score.CompareTo(x.Score); // descending: higher score first
                return cmp != 0 ? cmp : x.Uid.CompareTo(y.Uid);
            }
        }
    }
}
