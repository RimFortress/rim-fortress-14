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

public sealed class WaitSystem : GoapActionSystem<Wait>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Wait action) => 1.5f;

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Wait action)
    {
        var state = ent.Comp.State;

        if (!Goap.TryGetValue(state, action.TimeKey, out var time))
        {
            KeyNotFound(ent, action, action.TimeKey);
            return GoapActionResult.Failed;
        }

        time -= _timing.FrameTime;
        state.SetValue(action.TimeKey, time);

        return time <= TimeSpan.Zero ? GoapActionResult.Finished : GoapActionResult.Continuing;
    }
}
