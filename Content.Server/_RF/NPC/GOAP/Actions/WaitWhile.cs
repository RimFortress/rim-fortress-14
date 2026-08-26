using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions;

/// <summary>
/// The agent will wait until the specified conditions are met.
/// </summary>
public sealed partial class WaitWhile : BaseGoapAction<WaitWhile>
{
    /// <summary>
    /// Wait conditions.
    /// </summary>
    [DataField(required: true)]
    public List<GoapCondition> Conditions = new();
}

public sealed class WaitWhileSystem : GoapActionSystem<WaitWhile>
{
    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, WaitWhile action)
        => 1f + action.Conditions.Count * 0.1f;

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, WaitWhile action)
    {
        if (action.Conditions.Count == 0)
        {
            CreateDump("warn: conditions list are empty");
            return GoapActionResult.Finished;
        }

        foreach (var con in action.Conditions)
        {
            var check = Goap.CheckCondition(ent, ent.Comp.State, con, out var dump);

            CreateDump(dump?.Dump != null
                ? $"{con.GetType().Name}: {check} ({dump?.Dump})"
                : $"{con.GetType().Name}: {check}");

            if (!check)
                return GoapActionResult.Finished;
        }

        return GoapActionResult.Continuing;
    }
}
