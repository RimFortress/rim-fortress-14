using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns all entities that share the same owner with the agent.
/// </summary>
public sealed partial class Owned : BaseSearchQuery<Owned>;

public sealed class OwnedQuerySystem : NpcSearchQuerySystem<Owned>
{
    [Dependency] private OwnershipSystem _ownership = default!;

    protected override void GetQuery(GoapState state, Owned query)
    {
        var owner = state.GetValue(GoapState.Owner);
        var enumerator = _ownership.GetEntitiesEnumerator(owner);

        while (Query.Count < query.Limit && enumerator.MoveNext(out var uid))
        {
            Query.Add(uid);
        }
    }
}
