using Content.Client._RF.UserInterface.Controllers;
using Content.Shared._RF.World.Components;
using Robust.Client.Player;

namespace Content.Client._RF.UserInterface.Controls.TopInfoPanel;

public sealed partial class TopPanelWidget : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly InfoPanelUiController _controller;

    public TopPanelWidget()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _controller = UserInterfaceManager.GetUIController<InfoPanelUiController>();
    }

    private string PopulationText()
        => _entity.TryGetComponent(_player.LocalEntity, out RimFortressPlayerComponent? player)
            ? player.Pops.Count.ToString()
            : string.Empty;

    private string DateText()
        => $"{Loc.GetString("top-info-panel-datetime-day")} {Math.Floor(_controller.GetWorldTime().TotalDays) + 1}";

    private string TimeText()
    {
        var time = _controller.GetWorldTime();
        return $"{time.Hours:00}:{time.Minutes:00}";
    }
}
