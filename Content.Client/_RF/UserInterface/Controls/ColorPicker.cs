using Content.Suil;
using System.Numerics;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A swatch of the chosen color with a button that opens the window of the sliders. The tree is in
/// <c>elements/common/color-picker.suil</c>.
/// </summary>
public sealed partial class ColorPicker : RfBoxContainer
{
    public event Action<Color>? OnColorChanged;

    private readonly ColorPickerWindow _window;

    [SuilProperty("chosen-color")]
    public Color Color
    {
        get => _window.Selector.Color;
        set
        {
            ColorPanel.BackgroundColor = value;
            _window.Selector.Color = value;
        }
    }

    public ColorPicker()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _window = UserInterfaceManager.CreateWindow<ColorPickerWindow>();

        PickColor.OnPressed += _ => OnPickPressed();

        _window.Selector.OnColorChanged += color =>
        {
            ColorPanel.BackgroundColor = color;
            OnColorChanged?.Invoke(color);
        };

        OnResized += () =>
        {
            var size = new Vector2(Height);
            ColorPanel.SetSize = size - new Vector2(ColorPanel.Margin.Left, ColorPanel.Margin.Top);
            PickColor.SetSize = size;
        };

        OnVisibilityChanged += _ =>
        {
            if (!VisibleInTree)
                _window.Close();
        };
    }

    private void OnPickPressed()
    {
        if (_window.IsOpen)
            _window.Close();
        else
            _window.Open(PickColor.GlobalPosition + new Vector2(0, PickColor.Height));
    }

    public void CloseWindow()
    {
        _window.Close();
    }
}
