using System.Linq;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Systems;

/// <summary>
/// A system that provides a convenient API for performing time-delayed actions for GOAP operations.
/// </summary>
public sealed class NpcTimingSystem : GoapDebugDumpSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Waits for the time specified in the agent's state.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="timeKey">A key containing the remaining wait time.</param>
    /// <returns>
    /// <see cref="GoapActionResult.Continuing"/> if the time hasn't passed yet,
    /// <see cref="GoapActionResult.Finished"/> if it has passed, or
    /// <see cref="GoapActionResult.Failed"/> if the specified key is not present in the state.
    /// </returns>
    [PublicAPI]
    public GoapActionResult Wait(
        Entity<GoapComponent> ent,
        GoapAction action,
        StateKey<TimeSpan> timeKey)
    {
        if (!TryGetValue(ent, action, timeKey, out var time))
            return GoapActionResult.Failed;

        time -= _timing.FrameTime;
        ent.Comp.State.SetValue(timeKey, time);

        return time > TimeSpan.Zero ? GoapActionResult.Continuing : GoapActionResult.Finished;
    }

    /// <summary>
    /// Adds an action to the queue that will be executed after all previous actions plus the specified time.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="time">The time after which the action will be invoked.</param>
    /// <param name="queueKey">
    /// A key that will store the current state of the queue.
    /// By default <see cref="GoapState.WaitActionsQueue"/>.
    /// </param>
    /// <param name="onFinish">The action that will be triggered once the wait is complete.</param>
    /// <remarks>To process the queue, call <see cref="WaitQueue"/></remarks>
    /// <returns><see cref="GoapActionResult.Continuing"/></returns>
    [PublicAPI]
    public GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapAction action,
        TimeSpan time,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Func<bool>? onFinish = null)
    {
        var state = ent.Comp.State;
        queueKey ??= GoapState.WaitActionsQueue;

        if (TryGetValue(ent, action, queueKey.Value, out var queue))
            queue.Add((time, onFinish));
        else
            state.SetValue(queueKey.Value, new() { (time, onFinish) });

        CreateDump(ent, action, $"action delayed by {queue?.Sum(x => x.Time.TotalSeconds) ?? time.TotalSeconds}s");
        return GoapActionResult.Continuing;
    }

    [PublicAPI]
    public GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapAction action,
        TimeSpan time,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Action? onFinish = null)
        => EnqueueWait(ent,
            action,
            time,
            queueKey,
            () =>
            {
                onFinish?.Invoke();
                return true;
            });

    [PublicAPI]
    public GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapAction action,
        float timeSeconds,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Action? onFinish = null)
        => EnqueueWait(ent, action, TimeSpan.FromSeconds(timeSeconds), queueKey, onFinish);

    [PublicAPI]
    public GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapAction action,
        float timeSeconds,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Func<bool>? onFinish = null)
        => EnqueueWait(ent, action, TimeSpan.FromSeconds(timeSeconds), queueKey, onFinish);

    /// <summary>
    /// Handles the logic of the action queue.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="queueKey">
    /// A key that stores the queue of actions.
    /// By default <see cref="GoapState.WaitActionsQueue"/>.
    /// </param>
    /// <param name="removeKeyOnFinish">Will the key with the queue be deleted after all actions have been processed?</param>
    /// <returns>
    /// <see cref="GoapActionResult.Finished"/> if there are no remaining actions in the queue;
    /// <see cref="GoapActionResult.Failed"/> if the action in the queue returned false;
    /// else, <see cref="GoapActionResult.Continuing"/>.
    /// </returns>
    [PublicAPI]
    public GoapActionResult WaitQueue(
        Entity<GoapComponent> ent,
        GoapAction action,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        bool removeKeyOnFinish = true)
    {
        var state = ent.Comp.State;
        queueKey ??= GoapState.WaitActionsQueue;

        if (!TryGetValue(ent, action, queueKey.Value, out var queue))
            return GoapActionResult.Finished;

        if (queue.Count == 0)
        {
            if (removeKeyOnFinish)
                state.Remove(queueKey.Value);
            return GoapActionResult.Finished;
        }

        var entry = queue[0];
        var time = entry.Time - _timing.FrameTime;

        if (time <= TimeSpan.Zero)
        {
            var result = entry.Act?.Invoke();

            if (removeKeyOnFinish && queue.Count == 1)
            {
                state.Remove(queueKey.Value);
                return result != false ? GoapActionResult.Finished : GoapActionResult.Failed;
            }

            if (result == false)
                return GoapActionResult.Failed;

            queue.RemoveAt(0);
            state.SetValue(queueKey.Value, queue);
            return queue.Count > 0 ? GoapActionResult.Continuing : GoapActionResult.Finished;
        }

        queue[0] = (time, entry.Act);
        state.SetValue(queueKey.Value, queue);
        return GoapActionResult.Continuing;
    }
}
