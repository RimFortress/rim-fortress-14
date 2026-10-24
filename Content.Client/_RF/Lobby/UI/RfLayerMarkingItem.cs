using Content.Suil;
using System.Linq;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Content.Client.Guidebook.Controls;
using Content.Client.Humanoid;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.Lobby.UI;

/// <summary>
/// A marking in the list of a layer: its preview, the button that selects it and the button that opens the colors of
/// its sprites. The <see cref="RfBoxContainer"/> version of <see cref="LayerMarkingItem"/>.
/// </summary>
public sealed partial class RfLayerMarkingItem : RfBoxContainer, ISearchableControl
{
    [Dependency] private IEntityManager _entity = default!;

    private readonly SpriteSystem _sprite;

    private readonly MarkingsViewModel _markingsModel;
    private readonly MarkingPrototype _markingPrototype;
    private readonly ProtoId<OrganCategoryPrototype> _organ;
    private readonly HumanoidVisualLayers _layer;
    private readonly bool _interactive;

    private List<RfColorSelectorSliders>? _colorSliders;

    public event Action<GUIBoundKeyEventArgs, RfLayerMarkingItem>? Pressed;
    public event Action<GUIBoundKeyEventArgs, RfLayerMarkingItem>? Unpressed;
    public ProtoId<MarkingPrototype> MarkingId => _markingPrototype.ID;

    public RfLayerMarkingItem(
        MarkingsViewModel model,
        ProtoId<OrganCategoryPrototype> organ,
        HumanoidVisualLayers layer,
        MarkingPrototype prototype,
        bool interactive)
    {
        InitializeComponent();
        IoCManager.InjectDependencies(this);

        SelectButton.OnPressed += _ => SelectButtonPressed();
        ColorsButton.OnPressed += _ => ColorsButtonPressed();

        _sprite = _entity.System<SpriteSystem>();

        _markingsModel = model;
        _markingPrototype = prototype;
        _organ = organ;
        _layer = layer;
        _interactive = interactive;

        UpdateData();
        UpdateSelection();

        OnKeyBindDown += OnPressed;
        OnKeyBindUp += OnUnpressed;

        if (!interactive)
            SelectButton.MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _markingsModel.MarkingsReset += UpdateSelection;
        _markingsModel.MarkingsChanged += MarkingsChanged;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _markingsModel.MarkingsReset -= UpdateSelection;
        _markingsModel.MarkingsChanged -= MarkingsChanged;
    }

    private void MarkingsChanged(ProtoId<OrganCategoryPrototype> organ, HumanoidVisualLayers layer)
    {
        if (_organ != organ || _layer != layer)
            return;

        UpdateSelection();
    }

    private void UpdateData()
    {
        MarkingTexture.Textures = _markingPrototype.Sprites.Select(layer => _sprite.Frame0(layer)).ToList();
        UpdatePreviewColors();
        SelectButton.SetPlainText(Loc.GetString($"marking-{_markingPrototype.ID}"));
    }

    private void UpdateSelection()
    {
        var selected = _markingsModel.IsMarkingSelected(_organ, _layer, _markingPrototype.ID);
        SelectButton.Pressed = selected && _interactive;
        ColorsButton.Visible = selected && _interactive
                               && _markingsModel.IsMarkingColorCustomizable(_organ, _layer, _markingPrototype.ID);

        if (!selected || !_interactive)
        {
            ColorsButton.Pressed = false;
            ColorsContainer.Visible = false;
        }

        UpdatePreviewColors();

        if (_markingsModel.GetMarking(_organ, _layer, _markingPrototype.ID) is { } marking &&
            _colorSliders is { } sliders)
        {
            for (var i = 0; i < _markingPrototype.Sprites.Count; i++)
            {
                sliders[i].Color = marking.MarkingColors[i];
            }
        }
    }

    // A selected marking is shown in its colors, the others in the colors of their sprites.
    private void UpdatePreviewColors()
    {
        MarkingTexture.Colors = _markingsModel.GetMarking(_organ, _layer, _markingPrototype.ID) is { } marking
            ? marking.MarkingColors
            : [];
    }

    private void SelectButtonPressed()
    {
        if (!_interactive)
        {
            SelectButton.Pressed = false;
            return;
        }

        if (_markingsModel.IsMarkingSelected(_organ, _layer, _markingPrototype.ID))
        {
            if (!_markingsModel.TryDeselectMarking(_organ, _layer, _markingPrototype.ID))
                SelectButton.Pressed = true;
        }
        else
        {
            if (!_markingsModel.TrySelectMarking(_organ, _layer, _markingPrototype.ID))
                SelectButton.Pressed = false;
        }
    }

    private void ColorsButtonPressed()
    {
        ColorsContainer.Visible = ColorsButton.Pressed;

        if (_colorSliders is not null)
            return;

        if (_markingsModel.GetMarking(_organ, _layer, _markingPrototype.ID) is not { } marking)
            return;

        _colorSliders = new();

        for (var i = 0; i < _markingPrototype.Sprites.Count; i++)
        {
            var container = new RfBoxContainer
            {
                FlexDirection = SuilFlexDirection.Column,
                Gap = new LengthUnit(2f, LengthUnitType.Px),
            };
            ColorsContainer.AddChild(container);

            var selector = new RfColorSelectorSliders
            {
                SelectorType = RfColorSelectorSliders.ColorSelectorType.Hsv,
            };

            var label = _markingPrototype.Sprites[i] switch
            {
                SpriteSpecifier.Rsi rsi => Loc.GetString($"marking-{_markingPrototype.ID}-{rsi.RsiState}"),
                SpriteSpecifier.Texture texture => Loc.GetString($"marking-{_markingPrototype.ID}-{texture.TexturePath.Filename}"),
                _ => throw new InvalidOperationException("SpriteSpecifier not of known type"),
            };

            var labelControl = new UiElement();
            labelControl.SetPlainText(label);
            container.AddChild(labelControl);
            container.AddChild(selector);

            selector.Color = marking.MarkingColors[i];
            _colorSliders.Add(selector);

            var colorIndex = i;
            selector.OnColorChanged += _ =>
            {
                _markingsModel.TrySetMarkingColor(_organ, _layer, _markingPrototype.ID, colorIndex, selector.Color);
            };
        }
    }

    public bool CheckMatchesSearch(string query)
    {
        return Loc.GetString($"marking-{_markingPrototype.ID}").Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public void SetHiddenState(bool state, string query)
    {
        Visible = CheckMatchesSearch(query) ? state : !state;
    }

    private void OnPressed(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Pressed?.Invoke(args, this);
    }

    private void OnUnpressed(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Unpressed?.Invoke(args, this);
    }
}
