using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.Zoning.Components;
using JetBrains.Annotations;

namespace Content.Client._RF.Zoning.UI.Controls;

public sealed partial class ZoneVisualsControl : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;

    private readonly ZoningUiController _controller;
    private EntityUid? _zone;
    private Dictionary<ZoneVisualsState, ZoneVisualsStyle> _originalVisuals = new();

    public ZoneVisualsControl()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _controller = UserInterfaceManager.GetUIController<ZoningUiController>();
    }

    [PublicAPI]
    public void SetZone(EntityUid? zone)
    {
        _zone = zone;
        UpdateVisuals();
    }

    [PublicAPI]
    public void ResetVisuals()
    {
        VisualChangesBox.Visible = false;

        if (_entity.HasComponent<ZoneVisualsComponent>(_zone))
            _controller.SetVisuals(_zone.Value, _originalVisuals);
    }

    private void SaveVisuals()
    {
        if (!_entity.TryGetComponent(_zone, out ZoneVisualsComponent? visuals))
            return;

        _controller.SetVisuals(_zone.Value, visuals.States);
        _originalVisuals = new(visuals.States);
    }

    private void UpdateVisuals()
    {
        VisualsBox.RemoveAllChildren();
        _originalVisuals.Clear();

        if (!_entity.TryGetComponent(_zone, out ZoneVisualsComponent? visuals) || !visuals.Editable)
            return;

        _originalVisuals = new(visuals.States);

        for (var i = ZoneVisualsComponent.PriorityOrder.Length - 1; i >= 0; i--)
        {
            var state = ZoneVisualsComponent.PriorityOrder[i];
            var control = new ZoneVisualsLayerControl(state,
                visuals.States.TryGetValue(state, out var s) ? s : null);

            control.OnStyleAdded += style =>
            {
                visuals.States[state] = style;
                VisualChangesBox.Visible = true;
            };

            control.OnStyleRemoved += _ =>
            {
                visuals.States.Remove(state);
                VisualChangesBox.Visible = true;
            };

            control.OnStyleChanged += style =>
            {
                visuals.States[state] = style;
                VisualChangesBox.Visible = true;
            };

            VisualsBox.AddChild(control);
        }
    }

    private void OnZoneUpdate(Entity<ZoneComponent> ent)
    {
        if (ent == _zone)
            UpdateVisuals();
    }
}
