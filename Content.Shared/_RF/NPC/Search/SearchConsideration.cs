using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Shared._RF.NPC.Search;

/// <summary>
/// Evaluates the entities in a search query based on a specific criteria to find the most relevant result.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class SearchConsideration
{
    /// <summary>
    /// Curves that modify the result of consideration.
    /// </summary>
    [DataField]
    public List<MathCurve> Curves = new();

    /// <summary>
    /// Returns an entity score from the search query on a scale from 0 to 1.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="target">Entity for evaluation.</param>
    /// <param name="searcher"></param>
    public abstract float Score(GoapState state, EntityUid target, IQuerySearcher searcher);
}

public abstract partial class BaseSearchConsideration<T> : SearchConsideration where T : BaseSearchConsideration<T>
{
    public override float Score(GoapState state, EntityUid target, IQuerySearcher searcher)
    {
        return searcher.Score(state, target, (T)this);
    }
}
