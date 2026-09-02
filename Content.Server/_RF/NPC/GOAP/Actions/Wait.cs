using Content.Server._RF.NPC.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Actions;

/// <summary>
/// Waits the specified amount of time.
/// </summary>
public sealed partial class Wait : BaseGoapAction<Wait>
{
    /// <summary>
    /// State key for the time we'll wait for.
    /// This gets removed after execution.
    /// </summary>
    [DataField(required: true)]
    public StateKey<TimeSpan> TimeKey;
}

public sealed partial class WaitActionSystem : GoapActionSystem<Wait>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private NpcTimingSystem _npcTiming = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Wait action) => 1.5f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Wait action)
    {
        if (!TryGet(ent, action.TimeKey, out var time))
            return false;

        CreateDump($"waiting {time}. CurTime: {_timing.CurTime}");
        return true;
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Wait action)
        => _npcTiming.Wait(ent, this, action.TimeKey);

    protected override void ActionShutdown(Entity<GoapComponent> ent, Wait action)
    {
        CreateDump($"waiting finished. CurTime: {_timing.CurTime}");
    }
}
