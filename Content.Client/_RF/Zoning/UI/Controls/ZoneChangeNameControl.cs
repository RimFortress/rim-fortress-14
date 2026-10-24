using Content.Client._RF.UserInterface.Scope;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.Zoning.Components;
using Content.Shared._RF.Zoning.Systems;
using JetBrains.Annotations;

namespace Content.Client._RF.Zoning.UI.Controls;

public sealed partial class ZoneChangeNameControl : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;

    private readonly ZoningUiController _controller;
    private EntityUid? _zone;

    public ZoneChangeNameControl()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _controller = Controller<ZoningUiController>();

        EditButton.OnPressed += _ => OnEditPressed();
        NameEdit.OnTextEntered += _ => SetName();
        NameEdit.OnFocusExit += _ => Cancel();
        ApplyNameButton.OnPressed += _ => SetName();
        CancelNameButton.OnPressed += _ => Cancel();

        Scope.ListenSystem<ZoningSystem, Entity<ZoneComponent>>(
            (s, h) => s.OnZoneUpdated += h,
            (s, h) => s.OnZoneUpdated -= h,
            OnZoneUpdate);
    }

    private void OnEditPressed()
    {
        if (!_entity.TryGetComponent(_zone, out MetaDataComponent? meta))
            return;

        NameBox.Visible = false;
        NameEditBox.Visible = true;
        NameEdit.Text = meta.EntityName;
        UserInterfaceManager.GrabKeyboardFocus(NameEdit);
    }

    private void Cancel()
    {
        NameBox.Visible = true;
        NameEditBox.Visible = false;
    }

    private void SetName()
    {
        NameBox.Visible = true;
        NameEditBox.Visible = false;

        if (_zone == null)
            return;

        NameLabel.Text = NameEdit.Text;
        _controller.SetName(_zone.Value, NameEdit.Text);
    }

    [PublicAPI]
    public void SetZone(EntityUid? zone)
    {
        _zone = zone;

        if (_entity.TryGetComponent(zone, out MetaDataComponent? meta)
            && meta.EntityPrototype != null)
            NameLabel.Text = meta.EntityName;
    }

    private void OnZoneUpdate(Entity<ZoneComponent> ent)
    {
        if (ent != _zone)
            return;

        SetZone(_zone);
    }
}
