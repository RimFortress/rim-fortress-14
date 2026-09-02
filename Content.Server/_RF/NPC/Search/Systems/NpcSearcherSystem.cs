using Content.Server._RF.NPC.Systems;
using Content.Server.Administration.Managers;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Administration;

namespace Content.Server._RF.NPC.Search.Systems;

public sealed partial class NpcSearcherSystem : SharedNpcSearcherSystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private NpcHelperSystem _npcHelper = default!;

    public override void Initialize()
    {
        base.Initialize();

#if TOOLS
        SubscribeNetworkEvent<NpcSearchDebugInfoRequest>(OnNpcSearchDebugInfoRequest);
#endif
    }

    private void OnNpcSearchDebugInfoRequest(NpcSearchDebugInfoRequest msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug)
            || !TryGetEntity(msg.Target, out var target)
            || !TryComp(target, out NpcSearcherComponent? searcher))
            return;

        var ent = new Entity<NpcSearcherComponent>(target.Value, searcher);
        var info = new List<NpcSearchDebugInfo>();
        var id = 0;

        foreach (var (protoId, live) in searcher.Queries)
        {
            if (!Proto.Resolve(protoId, out var proto))
                continue;

            info.Add(GetDebugInfo(ent, id, proto, live));
            id++;
        }

        var graph = new NpcSearchGraph(info, new(), new(), new());
        RaiseNetworkEvent(new NpcSearchDebugInfoMessage(GetNetEntity(target.Value), graph), args.SenderSession);
    }

    /// <summary>
    /// Builds a debug snapshot for one query prototype straight from the
    /// live pipeline state — <see cref="NpcSearcherComponent.Queries"/> for
    /// which candidates are currently tracked, and each candidate's own
    /// <see cref="SearchTrackedComponent"/> entry for where exactly it sits
    /// (which Filter last rejected it, or its cached per-Consideration
    /// scores). No Query/Filter/Consideration is ever re-run here — this is
    /// a read of whatever the reactive pipeline already computed.
    /// </summary>
    private NpcSearchDebugInfo GetDebugInfo(
        Entity<NpcSearcherComponent> ent,
        int id,
        SearchQueryPrototype proto,
        NpcSearcherComponent.LiveSearchResult live)
    {
        var query = new HashSet<string>();
        var filters = new List<(ObjectDebugReflection Reflection, HashSet<string> Filtered)>(proto.Filters.Count);

        foreach (var filter in proto.Filters)
        {
            filters.Add((_npcHelper.GetReflection(filter), new()));
        }

        var considerations =
            new List<(ObjectDebugReflection Reflection, Dictionary<string, float> Result)>(proto.Considerations.Count);

        foreach (var con in proto.Considerations)
        {
            considerations.Add((_npcHelper.GetReflection(con), new()));
        }

        var results = new Dictionary<string, (List<float> Parts, float Result)>();

        foreach (var uid in live.Tracked)
        {
            var str = ToPrettyString(uid).ToString();
            query.Add(str);

            if (!TryComp(uid, out SearchTrackedComponent? tracked)
                || !TryGetTracker(tracked, (ent.Owner, proto), out var entry))
                continue; // Tracked and SearchTrackedComponent are kept in sync - shouldn't happen

            if (entry.ConsiderationScores == null)
            {
                // Still resting somewhere in the Filters chain - the
                // filter right after its last cleared one is the one
                // currently rejecting it.
                filters[entry.FilterStage + 1].Filtered.Add(str);
                continue;
            }

            var parts = new List<float>(entry.ConsiderationScores.Length);
            var score = 1f;

            for (var i = 0; i < entry.ConsiderationScores.Length; i++)
            {
                var s = entry.ConsiderationScores[i];
                considerations[i].Result[str] = s;
                parts.Add(s);
                score *= s;
            }

            if (score > 0f)
                results[str] = (parts, score);
        }

        return new NpcSearchDebugInfo(
            Id: id,
            ProtoId: proto,
            Query: (_npcHelper.GetReflection(proto.Query), query),
            Filters: filters,
            Considerations: considerations,
            Results: results);
    }
}
