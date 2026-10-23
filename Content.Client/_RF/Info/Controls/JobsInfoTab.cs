using System.Linq;
using Content.Client._RF.NPC.UtilityAi.Systems;
using Content.Client._RF.UserInterface.Controllers;
using Content.Client._RF.UserInterface.Scope;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Systems;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Info.Controls;

public sealed partial class JobsInfoTab : InfoTab
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    // The jobs and the task are refreshed by the events of the systems.
    public JobsInfoTab() : base(0f)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        JobsSettingsButton.OnPressed += _ => Controller<NpcJobsUiController>().ToggleSettingsWindow();

        Scope.ListenSystem<UtilityAiSystem, EntityUid>(
            (s, h) => s.OnUtilityAiUpdated += h,
            (s, h) => s.OnUtilityAiUpdated -= h,
            uid =>
            {
                if (VisibleInTree && Uid == uid)
                    UpdateCurrentTask();
            });
        Scope.ListenSystem<NpcJobsSystem, int>(
            (s, h) => s.OnJobChanged += h,
            (s, h) => s.OnJobChanged -= h,
            _ =>
            {
                if (VisibleInTree)
                    Refresh();
            });
    }

    protected override void Update(EntityUid uid)
    {
        Container.RemoveAllChildren();

        UpdateCurrentTask();

        if (!_entity.TryGetComponent(_player.LocalEntity, out NpcJobsComponent? comp))
            return;

        foreach (var job in comp.Jobs.OrderBy(x => x.Name))
        {
            Container.AddChild(new JobInfo(uid, job));
        }
    }

    private void UpdateCurrentTask()
    {
        if (Uid is not { } uid)
            return;

        var taskName = System<UtilityAiSystem>().TryGetCurrentGoal(uid, out var id) && _proto.TryIndex(id, out var task)
            ? $"[color={task.Color.ToHex()}][bold]{Loc.GetString(task.Name)}[/bold][/color]"
            : Loc.GetString("job-info-tab-task-none");

        CurrentTask.Text = $"{Loc.GetString("job-info-tab-current-task")}: {taskName}";
    }
}
