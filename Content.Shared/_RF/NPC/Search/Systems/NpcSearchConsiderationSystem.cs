using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search.Components;

namespace Content.Shared._RF.NPC.Search.Systems;

/// <summary>
/// A system that handles search query considerations.
/// </summary>
/// <typeparam name="T">Search considerations type.</typeparam>
public abstract class NpcSearchConsiderationSystem<T> : EntitySystem where T : BaseSearchConsideration<T>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, GetSearchScore<T>>(OnGetSearchScore);
    }

    private void OnGetSearchScore(Entity<NpcSearcherComponent> ent, ref GetSearchScore<T> ev)
    {
        ev.Result = GetScore(ev.State, ev.Target, ev.Con);
    }

    /// <summary>
    /// Returns result of the consideration of the target entity in the search query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="target">Target entity.</param>
    /// <param name="con">Search consideration.</param>
    protected abstract float GetScore(GoapState state, EntityUid target, T con);
}
