using Content.Server._RF.NPC.Executable.Systems;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.Executable.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns a set of all passive targets for the specified goals.
/// </summary>
public sealed partial class PassiveTargets : BaseSearchQuery<PassiveTargets>
{
    /// <summary>
    /// Goals list.
    /// </summary>
    [DataField(required: true)]
    public HashSet<ProtoId<ExecutableGoalPrototype>> Goals = new();
}

public sealed partial class PassiveTargetQuerySystem : NpcSearchQuerySystem<PassiveTargets>
{
    [Dependency] private ExecutableGoalSystem _executable = default!;
    [Dependency] private readonly EntityQuery<NpcControllerComponent> _controllerQuery = default!;

    [SubscribeLocalEvent]
    private void OnPassiveGoapSet(NpcPassiveGoalSet ev)
    {
        if (!_controllerQuery.TryComp(ev.User, out var controller))
            return;

        foreach (var uid in controller.CanControl)
        {
            if (!SearcherQuery.TryComp(uid, out var comp))
                continue;

            foreach (var (proto, _) in comp.Queries)
            {
                if (!TryGetQuery(proto, out var query) || !query.Goals.Contains(ev.Goal))
                    continue;

                Searcher.ReportDirty(uid, proto, added: new() { ev.Target });
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnPassiveGoalRemove(Entity<SearchTrackedComponent> ent, ref NpcPassiveGoalRemoved ev)
    {
        foreach (var ((agent, proto), _) in ent.Comp.Tracking)
        {
            if (!TryGetQuery(proto, out var query) || !query.Goals.Contains(ev.Goal))
                continue;

            Searcher.ReportDirty(agent, proto, removed: new() { ent });
        }
    }

    protected override void GetQuery(GoapState state, PassiveTargets query)
    {
        var owner = SharedGoapSystem.Owner(state);
        var enumerator = EntityQueryEnumerator<PassiveGoalTargetComponent>();

        while (Query.Count < query.Limit && enumerator.MoveNext(out var uid, out var comp))
        {
            if (query.Goals.Contains(comp.Goal) && _executable.CanControl(comp.User, owner))
                Query.Add(uid);
        }
    }
}
