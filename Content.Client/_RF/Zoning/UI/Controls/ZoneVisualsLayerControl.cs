using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.Zoning.Components;

namespace Content.Client._RF.Zoning.UI.Controls;

public sealed partial class ZoneVisualsLayerControl : RfBoxContainer
{
    public ZoneVisualsStyle Style;

    public event Action<ZoneVisualsStyle>? OnStyleChanged;
    public event Action<ZoneVisualsStyle>? OnStyleAdded;
    public event Action<ZoneVisualsStyle>? OnStyleRemoved;

    public ZoneVisualsLayerControl()
    {
        InitializeComponent();

        LayerEnableButton.OnPressed += _ =>
        {
            SettingsBox.Visible = LayerEnableButton.Pressed;
            OnLayerToggled(LayerEnableButton.Pressed);
        };

        BorderColorButton.OnPressed += _ =>
        {
            BorderColor.Visible = BorderColorButton.Pressed;
            OnBorderColorToggled(BorderColorButton.Pressed);
        };
        BorderColor.OnColorChanged += OnBorderColorChanged;

        ZoneColorButton.OnPressed += _ =>
        {
            ZoneColor.Visible = ZoneColorButton.Pressed;
            OnZoneColorToggled(ZoneColorButton.Pressed);
        };
        ZoneColor.OnColorChanged += OnZoneColorChanged;

        ShowTileBordersButton.OnPressed += _ => OnShowTileBordersToggled(ShowTileBordersButton.Pressed);
        ShowConditionsButton.OnPressed += _ => OnShowConditionsToggled(ShowConditionsButton.Pressed);
    }

    public ZoneVisualsLayerControl(ZoneVisualsState state, ZoneVisualsStyle? style) : this()
    {
        Style = style != null ? new(style.Value) : new();

        LayerExpand.Title = Loc.GetString("zone-visuals-state", ("state", state.ToString().ToLowerInvariant()));
        LayerEnableButton.Pressed = SettingsBox.Visible = style != null;

        BorderColorButton.Pressed = BorderColor.Visible = Style.BorderColor != null;
        BorderColor.Color = Style.BorderColor ?? Color.Transparent;

        ZoneColorButton.Pressed = ZoneColor.Visible = Style.ZoneColor != null;
        ZoneColor.Color = Style.ZoneColor ?? Color.Transparent;

        ShowTileBordersButton.Pressed = Style.ShowTileBorders == true;
        ShowConditionsButton.Pressed = Style.ShowConditions == true;
    }

    private void OnLayerToggled(bool enabled)
    {
        if (enabled)
            OnStyleAdded?.Invoke(Style);
        else
            OnStyleRemoved?.Invoke(Style);
    }

    private void OnBorderColorToggled(bool enabled)
    {
        BorderColor.Color = Style.BorderColor ?? Color.Transparent;
        Style.BorderColor = enabled ? BorderColor.Color : null;
        OnStyleChanged?.Invoke(Style);
    }

    private void OnBorderColorChanged(Color color)
    {
        Style.BorderColor = color;
        OnStyleChanged?.Invoke(Style);
    }

    private void OnZoneColorToggled(bool enabled)
    {
        ZoneColor.Color = Style.ZoneColor ?? Color.Transparent;
        Style.ZoneColor = enabled ? ZoneColor.Color : null;
        OnStyleChanged?.Invoke(Style);
    }

    private void OnZoneColorChanged(Color color)
    {
        Style.ZoneColor = color;
        OnStyleChanged?.Invoke(Style);
    }

    private void OnShowTileBordersToggled(bool enabled)
    {
        Style.ShowTileBorders = enabled ? true : null;
        OnStyleChanged?.Invoke(Style);
    }

    private void OnShowConditionsToggled(bool enabled)
    {
        Style.ShowConditions = enabled ? true : null;
        OnStyleChanged?.Invoke(Style);
    }
}
