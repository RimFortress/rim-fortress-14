using System.Linq;
using System.Numerics;
using Content.Client._RF.UserInterface.Scope;
using Content.Client.Cuffs;
using Content.Client.Examine;
using Content.Client.Hands.Systems;
using Content.Client.Inventory;
using Content.Client.Strip;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Client.Verbs.UI;
using Content.Shared.Cuffs;
using Content.Shared.Hands.Components;
using Content.Shared.Input;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;

namespace Content.Client._RF.Info.Controls;

public sealed partial class InventoryInfoTab : InfoTab
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly EntityUid _virtualHiddenEntity;

    private Vector2 _maxSlotPos;

    public const string HiddenPocketEntityId = "StrippingHiddenEntity";

    public int ButtonSeparation { get; set; } = 4;

    // The slots are made again when the stripping system says that they changed.
    public InventoryInfoTab() : base(0f)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _virtualHiddenEntity = _entity.SpawnEntity(HiddenPocketEntityId, MapCoordinates.Nullspace);

        Scope.ListenSystem<StrippableSystem>(
            (s, h) => s.OnUiNeedsUpdate += h,
            (s, h) => s.OnUiNeedsUpdate -= h,
            Refresh);
    }

    /*
     * All the code below is taken from StrippableBoundUserInterface,
     * with slight modifications as it only works with StrippingMenu,
     * which is a window, and I need to insert the inventory as part of the interface
     */
    protected override void Update(EntityUid uid)
    {
        ClearButtons();

        if (_entity.TryGetComponent<InventoryComponent>(uid, out var inv))
        {
            foreach (var slot in inv.Slots)
            {
                AddInventoryButton(uid, slot.Name, inv);
            }
        }

        if (_entity.TryGetComponent<HandsComponent>(uid, out var handsComp) && handsComp.CanBeStripped)
        {
            var ent = new Entity<HandsComponent>(uid, handsComp);

            // good ol hands shit code. there is a GuiHands comparer that does the same thing... but these are hands
            // and not gui hands... which are different...
            foreach (var (id, hand) in handsComp.Hands)
            {
                if (hand.Location != HandLocation.Right)
                    continue;

                AddHandButton(ent, id, hand);
            }

            foreach (var (id, hand) in handsComp.Hands)
            {
                if (hand.Location != HandLocation.Middle)
                    continue;

                AddHandButton(ent, id, hand);
            }

            foreach (var (id, hand) in handsComp.Hands)
            {
                if (hand.Location != HandLocation.Left)
                    continue;

                AddHandButton(ent, id, hand);
            }
        }
    }

    private void ClearButtons()
    {
        InventoryContainer.RemoveAllChildren();
        HandsContainer.RemoveAllChildren();
    }

    private void AddInventoryButton(EntityUid invUid, string slotId, InventoryComponent inv)
    {
        if (!System<ClientInventorySystem>().TryGetSlotContainer(invUid, slotId, out var container, out var slotDef, inv))
            return;

        var entity = container.ContainedEntity;

        // If this is a full pocket, obscure the real entity
        // this does not work for modified clients because they are still sent the real entity
        if (entity != null && System<StrippableSystem>().IsStripHidden(slotDef, _player.LocalEntity))
            entity = _virtualHiddenEntity;

        var button = new SlotButton(new ClientInventorySystem.SlotData(slotDef, container));
        button.Pressed += SlotPressed;

        InventoryContainer.AddChild(button);

        UpdateEntityIcon(button, entity);

        LayoutContainer.SetPosition(button, slotDef.StrippingWindowPos * (SlotControl.DefaultButtonSize + ButtonSeparation));

        if (slotDef.StrippingWindowPos.X > _maxSlotPos.X)
            _maxSlotPos.X = slotDef.StrippingWindowPos.X;
        if (slotDef.StrippingWindowPos.Y > _maxSlotPos.Y)
            _maxSlotPos.Y = slotDef.StrippingWindowPos.Y;

        UpdateLayoutSize();
    }

    private void AddHandButton(Entity<HandsComponent> ent, string handId, Hand hand)
    {
        var button = new HandButton(handId, hand.Location);

        button.Pressed += SlotPressed;

        var heldEntity = System<HandsSystem>().GetHeldItem(ent.AsNullable(), handId);
        if (_entity.TryGetComponent<VirtualItemComponent>(heldEntity, out var virt) && Uid != null)
        {
            button.Blocked = true;
            if (System<CuffableSystem>().GetAllCuffs(Uid.Value).Contains(virt.BlockingEntity))
                button.BlockedRect.MouseFilter = MouseFilterMode.Ignore;
        }

        UpdateEntityIcon(button, heldEntity);
        HandsContainer.AddChild(button);
    }

    private void SlotPressed(GUIBoundKeyEventArgs ev, SlotControl slot)
    {
        if (slot.Entity == null)
            return;

        if (ev.Function == ContentKeyFunctions.ExamineEntity)
        {
            System<ExamineSystem>().DoExamine(slot.Entity.Value);
            ev.Handle();
        }
        else if (ev.Function == EngineKeyFunctions.UseSecondary)
        {
            Controller<VerbMenuUIController>().OpenVerbMenu(slot.Entity.Value);
            ev.Handle();
        }
    }

    private void UpdateEntityIcon(SlotControl button, EntityUid? entity)
    {
        // Hovering, highlighting & storage are features of general hands & inv GUIs. This UI just re-uses these because I'm lazy.
        button.ClearHover();
        button.StorageButton.Visible = false;

        if (entity == null)
        {
            button.SetEntity(null);
            return;
        }

        EntityUid? viewEnt;
        if (_entity.TryGetComponent<VirtualItemComponent>(entity, out var virt))
            viewEnt = _entity.HasComponent<SpriteComponent>(virt.BlockingEntity) ? virt.BlockingEntity : null;
        else if (_entity.HasComponent<SpriteComponent>(entity))
            viewEnt = entity;
        else
            return;

        button.SetEntity(viewEnt);
    }

    private void UpdateLayoutSize()
    {
        InventoryContainer.SetSize = new(
            (_maxSlotPos.X + 1) * (SlotControl.DefaultButtonSize + ButtonSeparation),
            (_maxSlotPos.Y + 1) * (SlotControl.DefaultButtonSize + ButtonSeparation));
    }
}

