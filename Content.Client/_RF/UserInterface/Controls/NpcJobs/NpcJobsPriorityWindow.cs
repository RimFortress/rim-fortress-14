using System.Linq;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controllers;
using Content.Client.UserInterface.Controls;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.World.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls.NpcJobs;

public sealed partial class NpcJobsPriorityWindow : RfWindow
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;

    private NpcJobsSystem _npcJobs = default!;

    public NpcJobsPriorityWindow()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }

    private void OnJobSettingsPressed()
    {
        UserInterfaceManager
            .GetUIController<NpcJobsUiController>()
            .ToggleSettingsWindow();
    }

    public void EnsureSetup()
    {
        _npcJobs = _entity.System<NpcJobsSystem>();
        _npcJobs.OnJobChanged += _ =>
        {
            if (IsOpen)
                BuildTable();
        };

        BuildTable();
    }

    public override void Close()
    {
        Table.ClearAll();
        UserInterfaceManager
            .GetUIController<NpcJobsUiController>()
            .SettingsWindow?.Close();
        base.Close();
    }

    public void BuildTable()
    {
        Table.ClearAll();

        if (!_entity.TryGetComponent(_player.LocalEntity, out RimFortressPlayerComponent? player)
            || !_entity.TryGetComponent(_player.LocalEntity, out NpcJobsComponent? jobs))
            return;

        var headers = new List<Control> { new() };
        var orderedData = jobs.Jobs
            .OrderBy(x => x.Name)
            .ToList();

        foreach (var job in orderedData)
        {
            var label = new UiElement();
            label.SetPlainText(job.Name);

            headers.Add(Centered(label, 30f));
        }

        Table.CreateRow(headers);

        foreach (var uid in player.Pops)
        {
            var cells = new List<Control> { new NpcJobsPopCell(uid) };

            foreach (var job in orderedData)
            {
                var button = new NpcJobPriorityButton
                {
                    MaxValue = jobs.MaxPriority,
                    Priority = _npcJobs.GetPriority(uid, job.Id),
                };

                button.OnPriorityChanged += value =>
                    _npcJobs.SetJobPriority(uid, job.Id, value);

                cells.Add(Centered(button));
            }

            Table.CreateRow(cells);
        }

        Table.InvalidateMeasure();
    }

    /// <summary>
    /// A cell that holds the control in its middle and takes the space of the column.
    /// </summary>
    private static RfBoxContainer Centered(Control control, float minWidth = 0f)
    {
        var box = new RfBoxContainer
        {
            FlexGrow = 1,
            MinWidth = minWidth,
            JustifyContent = SuilJustifyContent.Center,
            AlignItems = SuilAlignItems.Center,
        };

        box.AddChild(control);
        return box;
    }
}
