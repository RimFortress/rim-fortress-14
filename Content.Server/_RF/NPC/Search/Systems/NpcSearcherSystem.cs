using System.Linq;
using Content.Server._RF.NPC.Systems;
using Content.Server.Administration.Managers;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Administration;

namespace Content.Server._RF.NPC.Search.Systems;

public sealed class NpcSearcherSystem : SharedNpcSearcherSystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly NpcHelperSystem _npcHelper = default!;

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
            || !TryComp(target, out GoapComponent? goap))
            return;

        var ent = new Entity<GoapComponent>(target.Value, goap);
        var info = new List<NpcSearchDebugInfo>();
        var id = 0;

        foreach (var proto in Proto.EnumeratePrototypes<SearchQueryPrototype>())
        {
            info.Add(GetDebugInfo(ent, id, proto));
            id++;
        }

        var graph = new NpcSearchGraph(info, new(), new(), new());
        RaiseNetworkEvent(new NpcSearchDebugInfoMessage(GetNetEntity(target.Value), graph), args.SenderSession);
    }

    private NpcSearchDebugInfo GetDebugInfo(Entity<GoapComponent> ent, int id, SearchQueryPrototype proto)
    {
        var query = Query(ent.Comp.State, proto.Query);
        var filters = new List<(ObjectDebugReflection Reflection, HashSet<string> Filtered)>();
        var filtered = query.ToHashSet();

        foreach (var uid in query)
        {
            for (var i = 0; i < proto.Filters.Count; i++)
            {
                var filter = proto.Filters[i];

                if (!Filter(ent.Comp.State, uid, filter))
                {
                    if (i > filters.Count - 1)
                        filters.Add((_npcHelper.GetReflection(filter), new()));

                    continue;
                }

                if (i > filters.Count - 1)
                    filters.Add((_npcHelper.GetReflection(filter), new() { ToPrettyString(uid) }));
                else
                    filters[i].Filtered.Add(ToPrettyString(uid));

                filtered.Remove(uid);
                break;
            }
        }

        var considerations = new List<(ObjectDebugReflection Reflection, Dictionary<string, float> Result)>();

        foreach (var consideration in proto.Considerations)
        {
            considerations.Add((_npcHelper.GetReflection(consideration),
                filtered.Select(x => (ToPrettyString(x).ToString(), Score(ent.Comp.State, x, consideration)))
                    .ToDictionary()));
        }

        var results = new Dictionary<string, (List<float> Parts, float Result)>();
        foreach (var uid in filtered)
        {
            var str = ToPrettyString(uid).ToString();
            var parts = new List<float>();
            var score = 1f;

            foreach (var (_, result) in considerations)
            {
                var s = result[str];
                score *= s;
                parts.Add(s);
            }

            if (score == 0)
                continue;

            results.Add(str, (parts, score));
        }

        return new NpcSearchDebugInfo(
            Id: id,
            ProtoId: proto,
            Query: (_npcHelper.GetReflection(proto.Query), query.Select(x => ToPrettyString(x).ToString()).ToHashSet()),
            Filters: filters,
            Considerations: considerations,
            Results: results);
    }
}
