using Content.Suil;
using System.Numerics;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.Lobby.UI;

public sealed partial class RfLobbyCharacterPreviewPanel : RfBoxContainer
{
    [Dependency] private IEntityManager _entManager = default!;

    public RfButton CharacterSetupButton => CharacterSetup;
    public RfButton EquipmentSetupButton => EquipmentSetup;

    private readonly List<EntityUid> _previewDummies = new();

    public RfLobbyCharacterPreviewPanel()
    {
        InitializeComponent();
        IoCManager.InjectDependencies(this);
    }

    public void SetLoaded(bool value)
    {
        Loaded.Visible = value;
        Unloaded.Visible = !value;
    }

    public void SetProfiles(Dictionary<EntityUid, string> entities)
    {
        foreach (var dummy in _previewDummies)
        {
            _entManager.DeleteEntity(dummy);
        }

        _previewDummies.Clear();
        ViewBox.RemoveAllChildren();

        foreach (var (uid, name) in entities)
        {
            _previewDummies.Add(uid);
            var box = new RfBoxContainer
            {
                FlexDirection = SuilFlexDirection.Column,
                Margin = new Thickness(3f, 0),
            };

            // The name of the character is not markup.
            var label = new UiElement
            {
                MaxWidth = 112,
            };
            label.SetPlainText(name);

            var spriteView = new SpriteView
            {
                OverrideDirection = Direction.South,
                Scale = new Vector2(4f, 4f),
                MaxSize = new Vector2(112, 112),
                Stretch = SpriteView.StretchMode.Fill,
                VerticalAlignment = VAlignment.Top,
            };

            spriteView.SetEntity(uid);

            box.AddChild(spriteView);
            box.AddChild(label);
            ViewBox.AddChild(box);
        }
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        foreach (var dummy in _previewDummies)
        {
            _entManager.DeleteEntity(dummy);
        }

        _previewDummies.Clear();
    }
}
