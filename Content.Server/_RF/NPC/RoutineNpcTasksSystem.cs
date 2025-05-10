using System.Linq;
using Content.Server._RF.NPC.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC;

/// <summary>
/// Manages <see cref="RoutineNpcTasksComponent"/>
/// </summary>
public sealed class RoutineNpcTasksSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly NpcControlSystem _controlSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoutineNpcTasksComponent, NpcTaskFinished>(OnTaskFinished);
    }

    private void OnTaskFinished(EntityUid uid, RoutineNpcTasksComponent comp, NpcTaskFinished args)
    {
        if (comp.Tasks.Find(x => x.Id == args.Task) is not { } task
            || !_prototype.TryIndex(args.Task, out var proto))
            return;

        if (args.Failed && task.CooldownOnFail != null)
            task.AvailableOn = _timing.CurTime + task.CooldownOnFail;

        if (task.FinishOnFailed && args.Failed)
            return;

        _controlSystem.TrySetPassiveTask(uid, proto);
    }

    public void SetRoutinePriority(Entity<RoutineNpcTasksComponent?> entity, ProtoId<NpcTaskPrototype> taskId, int priority)
    {
        if (!Resolve(entity, ref entity.Comp)
            || entity.Comp.Tasks.Find(x => x.Id == taskId) is not { } task)
            return;

        task.Priority = Math.Clamp(priority, 0, entity.Comp.MaxPriority);
    }

    public bool TrySetRoutineTask(Entity<RoutineNpcTasksComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        foreach (var taskData in entity.Comp.Tasks.OrderBy(x => x.Priority))
        {
            if (!_prototype.TryIndex(taskData.Id, out var task)
                || taskData.AvailableOn != null && _timing.CurTime < taskData.AvailableOn
                || !_controlSystem.TrySetPassiveTask(entity.Owner, task))
                continue;

            taskData.AvailableOn = null;
            return true;
        }

        return false;
    }
}
