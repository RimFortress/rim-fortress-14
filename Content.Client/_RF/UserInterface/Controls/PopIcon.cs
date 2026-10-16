using System.Numerics;
using Content.Client._RF.Info;
using Content.Client._RF.Selection;
using Content.Client._RF.Stylesheets;
using Content.Client._RF.Suil;
using Content.Shared._RF.Needs.Components;
using Content.Shared._RF.Needs.Systems;
using Content.Shared._RF.Social.Systems;
using Content.Shared.Follower;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls;

public sealed partial class PopIcon : RfButton
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEntityNetworkManager _net = default!;

    private SocialSystem _social = default!;
    private NeedsSystem _needs = default!;
    private SpriteSystem _sprite = default!;

    private TimeSpan _lastClicked;
    private bool _setup;

    [PublicAPI]
    public Vector2 StatusIconSize { get; set; } = new(16, 16);

    public bool CombatMode
    {
        set
        {
            if (value)
            {
                AddSuilStyleClass(SuilClass.Negative);
            }
            else
            {
                RemoveSuilStyleClass(SuilClass.Negative);
            }
        }
    }

    public PopIcon(EntityUid uid, string? name)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        AddSuilStyleClass(SuilClass.Transparent);

        Icon.OverrideDirection = Direction.South;
        Icon.SetEntity(uid);
        ToolTip = name;
    }

    private void EnsureSetup()
    {
        if (_setup)
            return;

        _setup = true;

        _social = _entityManager.System<SocialSystem>();
        _needs = _entityManager.System<NeedsSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
    }

    private void OnButtonToggled()
    {
        if (Icon.Entity is not { } uid)
            return;

        var time = _timing.CurTime;

        if (_lastClicked > time - TimeSpan.FromSeconds(0.333))
        {
            Pressed = false;

            // THIS IS HORRIBLE,
            // but we can't work properly with the FollowerSystem on the client
            // and have to send the message to the server
            _net.SendSystemNetworkMessage(new FollowEntityRequest(_entityManager.GetNetEntity(uid)));
        }

        _lastClicked = time;

        var selection = _entityManager.System<SelectionSystem>();

        if (Pressed)
            selection.Select(uid);
        else
            selection.DeSelect(uid);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (Icon.Entity?.Owner is not { } uid)
            return;

        if (args.Function == EngineKeyFunctions.UIRightClick)
            UserInterfaceManager.GetUIController<PopInfoUIController>().OpenWindow(uid, GlobalPosition);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Icon.Entity is not { } uid)
            return;

        EnsureSetup();
        UpdateMood(_social.GetMood(uid.Owner));

        StatusContainer.RemoveAllChildren();

        if (!_entityManager.TryGetComponent(uid, out NeedsComponent? needs))
            return;

        foreach (var data in needs.Needs)
        {
            if (!_needs.TryGetNeedIcon(new(uid, needs), data.Id, out var needIcon))
                continue;

            _needs.TryGetThresholdLocalization(data.CurrentThreshold, out var locale);
            StatusContainer.AddChild(StatusIcon(needIcon, locale));
        }
    }

    private TextureRect StatusIcon(SpriteSpecifier sprite, string? desc = null)
    {
        return new TextureRect
        {
            Texture = _sprite.GetFrame(sprite, _timing.RealTime),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = StatusIconSize,
            MouseFilter = desc == null ? MouseFilterMode.Ignore : MouseFilterMode.Pass,
            ToolTip = desc,
            TooltipDelay = 0,
            TrackingTooltip = true,
        };
    }

    private void UpdateMood(int value)
    {
        var color = value switch
        {
            < 0 => StyleFortress.LightBad,
            > 0 => StyleFortress.Good,
            _ => StyleFortress.GraySilver,
        };

        var sign = value switch
        {
            > 0 => "+",
            < 0 => "–",
            _ => "",
        };

        MoodLabel.Text = $"[bold][color={color.ToHex()}]{sign}{Math.Abs(value)}[/color][/bold]";
    }
}

