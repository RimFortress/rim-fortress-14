using Content.Client._RF.UserInterface.Controllers;
using Content.Client._RF.UserInterface.Scope;
using Content.Shared._RF.World.Components;
using Robust.Client.Player;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.TopInfoPanel.UI;

public sealed partial class TopPanelWidget : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly InfoPanelUiController _controller;

    public TopPanelWidget()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _controller = Controller<InfoPanelUiController>();

        Scope.Every(1f, UpdateClock, runOnEnter: true);
        Scope.Entered(UpdateTemperature);
        Scope.ListenController<InfoPanelUiController>(
            (c, h) => c.OnWorldTempChanged += h,
            (c, h) => c.OnWorldTempChanged -= h,
            UpdateTemperature);
    }

    private void UpdateClock()
    {
        Population.Text = PopulationText();
        Date.Text = DateText();
        Time.Text = TimeText();

        var hours = _controller.GetWorldTime().Hours;
        Time.TexturePath = hours is >= 6 and < 19
            ? "/Textures/_RF/Interface/sun-solid.svg.192dpi.png"
            : "/Textures/_RF/Interface/moon-solid.svg.192dpi.png";
    }

    private void UpdateTemperature()
    {
        Temperature.Text = _controller.TempeText;
        Temperature.TexturePath = _controller.WorldTempCelsius switch
        {
            < 10 => "/Textures/_RF/Interface/temperature-low-solid.svg.192dpi.png",
            < 20 => "/Textures/_RF/Interface/temperature-half-solid.svg.192dpi.png",
            < 30 => "/Textures/_RF/Interface/temperature-three-quarters-solid.svg.192dpi.png",
            _ => "/Textures/_RF/Interface/temperature-high-solid.svg.192dpi.png",
        };
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
