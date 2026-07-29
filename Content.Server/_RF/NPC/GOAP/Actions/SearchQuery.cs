using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions;

/// <summary>
/// Searches for an entity based on the search query and saves it to GoapState.
/// </summary>
public sealed partial class SearchQuery : BaseGoapAction<SearchQuery>
{
    /// <summary>
    /// Search query prototype;
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;

    /// <summary>
    /// The key in which the result will be stored.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = "Target";

    /// <summary>
    /// The key in which the result coordinates will be stored.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetCoordinatesKey = "TargetCoordinates";
}

public sealed class SearchQuerySystem : GoapActionSystem<SearchQuery>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedNpcSearcherSystem _npcSearcher = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, SearchQuery action)
    {
        if (!_proto.Resolve(action.Query, out var proto))
            return int.MaxValue;

        // Increase the cost of the action based on the number of checks that will be performed.
        return 1f + proto.Filters.Count * 0.15f + proto.Considerations.Count * 0.2f;
    }

    protected override bool ActionStartup(Entity<GoapComponent> ent, SearchQuery action)
    {
        var state = ent.Comp.State;

        if (!_npcSearcher.TryGetBestResult(ent.Owner, state, action.Query, out var result))
        {
            CreateDump(ent, action, $"search query '{action.Query}' was empty");
            return false;
        }

        state.SetValue(action.TargetKey, result.Value);
        state.SetValue(action.TargetCoordinatesKey, Transform(result.Value).Coordinates);
        return true;
    }
}
