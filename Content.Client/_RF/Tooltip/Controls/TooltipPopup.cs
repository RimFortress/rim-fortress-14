using System.Numerics;
using Content.Client._RF.Suil;
using Content.Client._RF.Tooltip.Prototypes;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.CCVar;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RF.Tooltip.Controls;

/// <summary>
/// A more advanced version of the tooltip that bypasses the current engine-based implementation, since it's crap.
/// It is shown by the base tooltip supplier of <see cref="UiElement"/>, and its layout is in
/// <c>elements/tooltip/tooltip.suil</c>.<br/>
/// A tooltip can follow the cursor (<see cref="Tracking"/>) and hide when it moves outside the owner control, and,
/// if it is <see cref="Pinnable"/>, it is pinned after it has been open for the time of
/// <see cref="RfVars.PopupPinDelay"/>: it stops following the cursor and allows the cursor to hover over its content,
/// including opening nested tooltips multiple times.
/// </summary>
public sealed partial class TooltipPopup : Popup
{
    private const string AngleRect = "angle-rect";
    private const string AngleRectGolden = "angle-rect-golden";

    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private TimeSpan PinDelay => TimeSpan.FromSeconds(_cfg.GetCVar(RfVars.PopupPinDelay));

    /// <summary>
    /// Offset of the tooltip from the mouse cursor.
    /// </summary>
    public Vector2 MouseOffset { get; set; } = new(10, 10);

    /// <summary>
    /// Control that opened this popup.
    /// </summary>
    public Control? Owner;

    /// <summary>
    /// Is this popup pinned?
    /// </summary>
    public bool Pinned { get; private set; }

    /// <summary>
    /// Does the popup follow the cursor until it is pinned?
    /// </summary>
    public bool Tracking { get; set; } = true;

    /// <summary>
    /// Is the popup pinned after <see cref="RfVars.PopupPinDelay"/>?
    /// </summary>
    public bool Pinnable { get; set; } = true;

    private TimeSpan? _pinAt;

    public TooltipPopup()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        CloseOnClick = false;
    }

    public TooltipPopup(ITooltipDefinition def, bool tracking = true, bool pinnable = true) : this()
    {
        Tracking = tracking;
        Pinnable = pinnable;
        SetDefinition(def);
    }

    /// <summary>
    /// Updates the tooltip content.
    /// </summary>
    [PublicAPI]
    public void SetDefinition(ITooltipDefinition def)
    {
        var title = ToFirstUpper(def.Title);
        var hasTitle = !string.IsNullOrEmpty(title);
        var hasIcon = false;

        Title.SetPlainText(title);
        IconPanel.Visible = false;
        EntView.Visible = false;
        SpriteView.Visible = false;
        Texture.Visible = false;
        Body.Visible = false;
        ControlAfter.RemoveAllChildren();

        switch (def.IconType)
        {
            case TooltipIconType.Base:
                Texture.Stretch = TextureRect.StretchMode.KeepAspectCentered;
                break;
            case TooltipIconType.Tile:
                Texture.Stretch = TextureRect.StretchMode.KeepAspectCovered;
                Texture.CanShrink = true;
                break;
        }

        if (_proto.TryIndex(def.EntIcon, out var proto)
            && proto.HasComp<SpriteComponent>(_entity.ComponentFactory))
        {
            hasIcon = true;
            EntView.Visible = true;
            IconPanel.TooltipSupplier = def.CanIconFocus ? _ => Preview(EntityPreview(proto)) : null;
            EntView.SetPrototype(def.EntIcon);
        }

        if (def.UidIcon != null
            && _entity.TryGetEntity(def.UidIcon, out var uid)
            && _entity.HasComponent<SpriteComponent>(uid))
        {
            hasIcon = true;
            SpriteView.Visible = true;
            IconPanel.TooltipSupplier = def.CanIconFocus ? _ => Preview(SpritePreview(def.UidIcon.Value)) : null;
            SpriteView.SetEntity(def.UidIcon.Value);
        }

        if (def.TexturePath != null)
        {
            var tex = _cache.GetResource<TextureResource>(def.TexturePath.Value).Texture;
            hasIcon = true;
            Texture.Visible = true;
            IconPanel.TooltipSupplier = def.CanIconFocus ? _ => Preview(TexturePreview(tex)) : null;
            Texture.Texture = tex;
        }

        IconPanel.Visible = hasIcon;
        IconDivider.Visible = hasIcon;
        Header.Visible = hasIcon || hasTitle;
        HeaderDivider.Visible = hasIcon || hasTitle;

        if (def.Body != null)
        {
            Body.Visible = true;
            Body.Text = def.Body;
        }

        if (def.ControlAfter != null)
            ControlAfter.AddChild(def.ControlAfter);

        return;

        string ToFirstUpper(string str)
            => string.IsNullOrEmpty(str) ? str : $"{char.ToUpper(str[0])}{str.Substring(1)}";

        Control EntityPreview(EntityPrototype entProto)
        {
            var view = new EntityPrototypeView
            {
                SetSize = new Vector2(128, 128),
                Scale = new Vector2(4f),
            };
            view.SetPrototype(entProto.ID);
            return view;
        }

        Control SpritePreview(NetEntity netUid)
        {
            var view = new SpriteView
            {
                SetSize = new Vector2(128, 128),
                Scale = new Vector2(4f),
            };
            view.SetEntity(netUid);
            return view;
        }

        Control TexturePreview(Texture tex)
        {
            return new TextureRect
            {
                SetSize = new Vector2(128, 128),
                Stretch = Texture.Stretch,
                CanShrink = Texture.CanShrink,
                Texture = tex,
            };
        }
    }

    // The enlarged icon, in the panel of the icon: shown by the engine when the cursor stays over the icon.
    private static Control Preview(Control view)
    {
        var panel = new RfBoxContainer();
        panel.AddSuilStyleClass("panel-dark-bordered");
        panel.AddChild(view);
        return panel;
    }

    /// <summary>
    /// The tooltip is put at the cursor, begins to follow it if <see cref="Tracking"/>, and starts a timer until it is
    /// pinned if <see cref="Pinnable"/>.
    /// </summary>
    [PublicAPI]
    public void StartTracking()
    {
        if (_pinAt != null)
            return;

        Pinned = false;
        Panel.RemoveSuilStyleClass(AngleRectGolden);
        Panel.AddSuilStyleClass(AngleRect);
        Progress.Visible = Pinnable;
        PlaceAtMouse();

        if (!Pinnable)
            return;

        Progress.MinValue = (float) _timing.CurTime.TotalSeconds;
        Progress.MaxValue = (float) (_timing.CurTime + PinDelay).TotalSeconds;
        _pinAt = _timing.CurTime + PinDelay;
    }

    private void PlaceAtMouse()
        => PopupContainer.SetPopupOrigin(this, UserInterfaceManager.MousePositionScaled.Position - MouseOffset);

    private void Pin()
    {
        Pinned = true;
        Progress.Visible = false;
        MouseFilter = MouseFilterMode.Stop;
        Panel.RemoveSuilStyleClass(AngleRect);
        Panel.AddSuilStyleClass(AngleRectGolden);
        _pinAt = null;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Pinned || !Tracking && _pinAt == null)
            return;

        if (_pinAt is { } pinAt)
        {
            if (pinAt < _timing.CurTime)
            {
                Pin();
                return;
            }

            Progress.Value = (float) _timing.CurTime.TotalSeconds;
        }

        if (Tracking)
            PlaceAtMouse();
    }
}
