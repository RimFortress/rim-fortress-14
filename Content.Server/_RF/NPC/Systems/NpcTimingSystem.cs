using System.Linq;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using JetBrains.Annotations;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Systems;

/// <summary>
/// A system that provides a convenient API for performing time-delayed actions for GOAP operations.
/// </summary>
public sealed class NpcTimingSystem : GoapDebugDumpSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Waits for the time specified in the agent's state.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="handler"></param>
    /// <param name="timeKey">A key containing the remaining wait time.</param>
    /// <returns>
    /// <see cref="GoapActionResult.Continuing"/> if the time hasn't passed yet,
    /// <see cref="GoapActionResult.Finished"/> if it has passed, or
    /// <see cref="GoapActionResult.Failed"/> if the specified key is not present in the state.
    /// </returns>
    [PublicAPI]
    public GoapActionResult Wait(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        StateKey<TimeSpan> timeKey)
    {
        if (!handler.TryGet(ent, timeKey, out var time))
            return GoapActionResult.Failed;

        time -= _timing.FrameTime;
        handler.Set(ent, timeKey, time);

        return time > TimeSpan.Zero ? GoapActionResult.Continuing : GoapActionResult.Finished;
    }

    /// <summary>
    /// Adds an action to the queue that will be executed after all previous actions plus the specified time.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="handler"></param>
    /// <param name="time">The time after which the action will be invoked.</param>
    /// <param name="queueKey">
    /// A key that will store the current state of the queue.
    /// By default <see cref="GoapState.WaitActionsQueue"/>.
    /// </param>
    /// <param name="onFinish">The action that will be triggered once the wait is complete.</param>
    /// <remarks>To process the queue, call <see cref="WaitQueue"/></remarks>
    /// <returns><see cref="GoapActionResult.Continuing"/></returns>
    [PublicAPI]
    public static GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        TimeSpan time,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Func<bool>? onFinish = null)
    {
        queueKey ??= GoapState.WaitActionsQueue;

        if (handler.TryGet(ent, queueKey.Value, out var queue))
            queue.Add((time, onFinish));
        else
            handler.Set(ent, queueKey.Value, new() { (time, onFinish) });

        handler.CreateDump($"action delayed by {queue?.Sum(x => x.Time.TotalSeconds) ?? time.TotalSeconds}s");
        return GoapActionResult.Continuing;
    }

    [PublicAPI]
    public static GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        TimeSpan time,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Action? onFinish = null)
        => EnqueueWait(ent,
            handler,
            time,
            queueKey,
            () =>
            {
                onFinish?.Invoke();
                return true;
            });

    [PublicAPI]
    public static GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        float timeSeconds,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Action? onFinish = null)
        => EnqueueWait(ent, handler, TimeSpan.FromSeconds(timeSeconds), queueKey, onFinish);

    [PublicAPI]
    public  GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        (float Min, float Max) minMaxSeconds,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Action? onFinish = null)
        => EnqueueWait(ent,
            handler,
            TimeSpan.FromSeconds(_random.NextFloat(minMaxSeconds.Min, minMaxSeconds.Max)),
            queueKey,
            onFinish);

    [PublicAPI]
    public static GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        float timeSeconds,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Func<bool>? onFinish = null)
        => EnqueueWait(ent, handler, TimeSpan.FromSeconds(timeSeconds), queueKey, onFinish);

    [PublicAPI]
    public GoapActionResult EnqueueWait(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        (float Min, float Max) minMaxSeconds,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        Func<bool>? onFinish = null)
        => EnqueueWait(ent,
            handler,
            TimeSpan.FromSeconds(_random.NextFloat(minMaxSeconds.Min, minMaxSeconds.Max)),
            queueKey,
            onFinish);

    /// <summary>
    /// Handles the logic of the action queue.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="handler"></param>
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
        GoapDebugDumpSystem handler,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null,
        bool removeKeyOnFinish = true)
    {
        queueKey ??= GoapState.WaitActionsQueue;

        if (!handler.TryGet(ent, queueKey.Value, out var queue))
            return GoapActionResult.Finished;

        if (queue.Count == 0)
        {
            if (removeKeyOnFinish)
                handler.Remove(ent, queueKey.Value);
            return GoapActionResult.Finished;
        }

        var entry = queue[0];
        var time = entry.Time - _timing.FrameTime;

        if (time <= TimeSpan.Zero)
        {
            var result = entry.Act?.Invoke();

            if (removeKeyOnFinish && queue.Count == 1)
            {
                handler.Remove(ent, queueKey.Value);
                return result != false ? GoapActionResult.Finished : GoapActionResult.Failed;
            }

            if (result == false)
                return GoapActionResult.Failed;

            queue.RemoveAt(0);
            handler.Set(ent, queueKey.Value, queue);
            return queue.Count > 0 ? GoapActionResult.Continuing : GoapActionResult.Finished;
        }

        queue[0] = (time, entry.Act);
        handler.Set(ent, queueKey.Value, queue);
        return GoapActionResult.Continuing;
    }

    /// <summary>
    /// Clears action queue.
    /// </summary>
    /// <param name="ent">GOAP agent.</param>
    /// <param name="queueKey">
    /// A key that stores the queue of actions.
    /// By default <see cref="GoapState.WaitActionsQueue"/>.
    /// </param>
    [PublicAPI]
    public static void ClearQueue(
        Entity<GoapComponent> ent,
        StateKey<List<(TimeSpan Time, Func<bool>? Act)>>? queueKey = null)
        => ent.Comp.State.SetValue(queueKey ?? GoapState.WaitActionsQueue, new());
}
