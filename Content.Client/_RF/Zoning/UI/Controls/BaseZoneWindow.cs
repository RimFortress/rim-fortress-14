using System.Linq;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.Zoning.Components;

namespace Content.Client._RF.Zoning.UI.Controls;

public sealed partial class BaseZoneWindow : RfWindow
{
    [Dependency] private IEntityManager _entity = default!;

    private readonly ZoningUiController _controller;
    private Entity<ZoneComponent>? _zone;

    public BaseZoneWindow()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _controller = UserInterfaceManager.GetUIController<ZoningUiController>();

        Tab.Tabs[0].Title = Loc.GetString("base-zone-window-visuals-tab-general-title");
        Tab.Tabs[1].Title = Loc.GetString("base-zone-window-visuals-tab-visuals-title");
    }

    public void Open(Entity<ZoneComponent> ent)
    {
        Open();
        _zone = ent;
        _controller.SelectZone(ent);

        if (_entity.TryGetComponent(ent, out MetaDataComponent? meta)
            && meta.EntityPrototype != null)
            Title = meta.EntityPrototype.Name;

        RfTabContainer.SetTabVisible(Tab.Tabs[1].Page,
            _entity.TryGetComponent(ent, out ZoneVisualsComponent? comp) && comp.Editable);

        BottomPanel.SetZone(ent);
        NameChange.SetZone(ent);
        VisualsControl.SetZone(ent);
        UpdateContent();
    }

    private void OnTabChanged()
    {
        if (Tab.CurrentTab == 1)
            VisualsControl.SetZone(_zone);
    }

    private void UpdateContent()
    {
        ContentBox.Content.RemoveAllChildren();

        if (!(_zone?.Comp.Entities.Count > 0))
            return;

        ContentBox.Visible = true;

        var contents = _zone.Value.Comp.Entities
            .Where(uid => _entity.HasComponent<MetaDataComponent>(uid))
            .Select(uid => new Entity<MetaDataComponent>(uid, _entity.GetComponent<MetaDataComponent>(uid)))
            .GroupBy(x => x.Comp.EntityName)
            .Select(x => x.ToHashSet())
            .ToList();

        foreach (var group in contents)
        {
            ContentBox.Content.AddChild(new ZoneContentControl(group.First(), group.Count));
        }
    }

    public override void Close()
    {
        base.Close();
        _controller.DeselectZone(_zone?.Owner);
        _zone = null;
        VisualsControl.ResetVisuals();
        ContentBox.Visible = false;
        ContentBox.CollapseAll();
    }

    private void OnZoneUpdate(Entity<ZoneComponent> ent)
    {
        if (ent == _zone)
            UpdateContent();
    }

    private void OnZoneDelete(Entity<ZoneComponent> ent)
    {
        if (ent == _zone)
            Close();
    }
}
