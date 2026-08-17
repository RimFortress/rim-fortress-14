using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Search.Components;

/// <summary>
/// Marks an entity as currently sitting somewhere in one or more agents'
/// search pipelines. Lets a Query/Filter/Consideration system, when
/// something relevant to it changes on this entity, find out exactly which
/// (agent, query prototype) pairs care — instead of scanning every agent.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedNpcSearcherSystem))]
public sealed partial class SearchTrackedComponent : Component
{
    /// <summary>
    /// An entities that has marked this entity as temporarily captured.
    /// The captured result can be filtered using the Captured filter
    /// to avoid AI conflicts over a single entity.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> Captured = new();

    [ViewVariables]
    public readonly Dictionary<(EntityUid Agent, ProtoId<SearchQueryPrototype> ProtoId), SearchTrackEntry> Tracking = new();
}

/// <summary>
/// Where a tracked candidate currently sits in the pipeline for one
/// (agent, query prototype) pair.
/// </summary>
/// <param name="FilterStage">
/// Index of the last <see cref="SearchQueryPrototype.Filters"/> entry this
/// candidate has cleared. -1 means it has only passed the Query stage.
/// </param>
/// <param name="ConsiderationScores">
/// Per-consideration cached scores, in <see cref="SearchQueryPrototype.Considerations"/>
/// order. Null until the candidate has cleared every Filter — only then does
/// it become eligible for Consideration scoring/caching.
/// </param>
public readonly record struct SearchTrackEntry(int FilterStage, float[]? ConsiderationScores);
