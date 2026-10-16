using System.Linq;
using Content.Shared._RF.Stockpile.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.UserInterface.Controls.Stockpile;

public sealed partial class StockpileItemButton : RfButton
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public event Action<int>? OnSettingsChanged;

    private readonly bool _category;

    public int MaxValue
    {
        get;
        set
        {
            var val = field;
            field = value;

            Counter.Visible = !_category && field != -1;
            UnlimitedCheck.Pressed = field == -1;

            if (field != -1)
                Counter.Value = field;

            if (val == field)
                return;

            OnSettingsChanged?.Invoke(field);
        }
    }

    public StockpileItemButton()
    {
        InitializeComponent();
    }

    public StockpileItemButton(StockpileCategoryPrototype proto, int count)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _category = true;
        var sprite = _entManager.System<SpriteSystem>();

        NameLabel.Text = count == 0 ? proto.Name : $"{proto.Name} ({count})";

        if (_prototype.TryIndex(proto.IconEntity, out var entProto))
        {
            Texture.Textures = sprite
                .GetPrototypeTextures(entProto)
                .Select(x => (Texture) x.Default)
                .ToList();
        }
        else if (proto.Icon != null)
            Texture.Textures = [sprite.Frame0(proto.Icon)];

        Counter.Visible = false;

        EnsureSetup();
    }

    public StockpileItemButton(EntityPrototype proto, int count)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        Disabled = true;

        NameLabel.Text = string.IsNullOrEmpty(proto.EditorSuffix) ? proto.Name : $"{proto.Name} [{proto.EditorSuffix}]";
        NameLabel.Text = count == 0 ? NameLabel.Text : $"{NameLabel.Text} ({count})";

        Texture.Textures = _entManager
            .System<SpriteSystem>()
            .GetPrototypeTextures(proto)
            .Select(x => (Texture) x.Default)
            .ToList();

        Counter.OnValueChanged += value => MaxValue = value.New;

        EnsureSetup();
    }

    private void EnsureSetup()
    {
        ToolTip = NameLabel.Text;
    }

    private void OnUnlimitedPressed()
    {
        UnlimitedCheck.ToolTip = Loc.GetString(UnlimitedCheck.Pressed
            ? "stockpile-settings-menu-unlimited-checkbox-enabled-tooltip"
            : "stockpile-settings-menu-unlimited-checkbox-disabled-tooltip");

        MaxValue = UnlimitedCheck.Pressed ? -1 : 0;
    }
}

