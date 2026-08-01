using Content.Server._RF.NPC.Systems;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Prototypes;
using Content.Shared._RF.NPC.Search;
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

public sealed class PassiveTargetQuerySystem : NpcSearchQuerySystem<PassiveTargets>
{
    [Dependency] private readonly ExecutableGoalSystem _executable = default!;

    protected override void GetQuery(GoapState state, PassiveTargets query)
    {
        var owner = state.GetValue(GoapState.Owner);
        var enumerator = EntityQueryEnumerator<PassiveGoalTargetComponent>();

        while (Query.Count < query.Limit && enumerator.MoveNext(out var uid, out var comp))
        {
            if (query.Goals.Contains(comp.Goal) && _executable.CanControl(comp.User, owner))
                Query.Add(uid);
        }
    }
}
