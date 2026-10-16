using Content.Suil;
using System.Numerics;
using Content.Client._RF.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ColorNaming;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// Sliders that choose a color (RGB or HSV, and the alpha), the <see cref="UiElement"/> version of
/// <see cref="ColorSelectorSliders"/>.
/// </summary>
/// <remarks>
/// <para>
/// The layout is in <c>elements/common/color-selector-sliders.suil</c>: a header (the type of the sliders and the name of the
/// color) and the rows of <see cref="RfColorSlider"/> with <see cref="IntCounter"/> for the three channels, the alpha
/// and the hex code. The rows are <c>.color-selector-row</c>, the texts at the start of them are
/// <c>.color-selector-label</c>, the header is <c>.color-selector-header</c>; the sliders and the boxes are styled as
/// any <c>.slider</c> and <c>int-counter</c>.
/// </para>
/// <para>
/// It is used like the engine control: <see cref="Color"/>, <see cref="SelectorType"/>, <see cref="IsAlphaVisible"/>
/// and <see cref="OnColorChanged"/>, which is fired when the player changes the color, not when the code does.
/// </para>
/// </remarks>
[UsedImplicitly]
[SuilTag("color-selector-sliders")]
public sealed partial class RfColorSelectorSliders : RfBoxContainer
{
    [Dependency] private ILocalizationManager _localization = default!;

    private static readonly SliderStrategy HsvStrategy = new HsvSliderStrategy();
    private static readonly SliderStrategy RgbStrategy = new RgbSliderStrategy();

    private const float AlphaDivisor = 100f;

    private Color _currentColor = Color.White;
    private Vector4 _colorData;
    private ColorSelectorType _currentType = ColorSelectorType.Rgb;
    private bool _updating;

    // OnColorChanged is not fired for every step of a slider that is dragged, see ChangeInterval.
    private bool _changePending;
    private float _sinceChange = float.MaxValue;

    /// <summary>
    /// The chosen color, setting it does not fire <see cref="OnColorChanged"/>.
    /// </summary>
    [PublicAPI]
    public Color Color
    {
        get => _currentColor;
        set
        {
            // The color the code knows is the one of the last report, it would move the slider back.
            if (IsDragged)
                return;

            _currentColor = value;
            _colorData = Strategy.ToColorData(value);
            _changePending = false;

            UpdateAllSliders();
        }
    }

    private bool IsDragged => TopSlider.Grabbed || MiddleSlider.Grabbed || BottomSlider.Grabbed || AlphaSlider.Grabbed;

    [PublicAPI]
    public ColorSelectorType SelectorType
    {
        get => _currentType;
        set
        {
            _currentType = value;
            TypeSelector.TrySelectValue(value);
            _colorData = Strategy.ToColorData(_currentColor);

            UpdateType();
            UpdateAllSliders();
        }
    }

    [PublicAPI]
    public bool IsAlphaVisible
    {
        get => AlphaRow.Visible;
        set => AlphaRow.Visible = value;
    }

    /// <summary>
    /// The least time in seconds between two <see cref="OnColorChanged"/> while a slider is dragged: whatever uses the
    /// color (restyles a character, sends a message) is not called at every frame. The first step is reported at once,
    /// the last one is when the slider is released. The other ways to change the color are reported at once.
    /// </summary>
    [PublicAPI]
    public float ChangeInterval { get; set; } = 0.1f;

    /// <summary>
    /// Fired when the player changes the color, at most once in <see cref="ChangeInterval"/> while a slider is dragged.
    /// </summary>
    [PublicAPI]
    public event Action<Color>? OnColorChanged;

    private SliderStrategy Strategy => _currentType == ColorSelectorType.Hsv ? HsvStrategy : RgbStrategy;

    public RfColorSelectorSliders()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        foreach (var type in Enum.GetValues<ColorSelectorType>())
        {
            TypeSelector.AddItem(Loc.GetString($"color-selector-sliders-{type.ToString().ToLower()}"), type);
        }

        TypeSelector.OnItemSelected += args => SelectorType = (ColorSelectorType) args.Value!;

        foreach (var order in new[] { Order.Top, Order.Middle, Order.Bottom, Order.Alpha })
        {
            var (slider, input) = Get(order);
            var captured = order;

            slider.OnValueChanged += _ => OnSliderValueChanged(captured);
            slider.OnReleased += _ => ReportPendingChange();
            input.MinValue = 0;
            input.MaxValue = (int) GetDivisor(captured);
            input.OnValueChanged += args => slider.Value = args.New / GetDivisor(captured);
        }

        HexEdit.OnTextEntered += OnHexEntered;
        // Enter is easy to miss while editing several colors in a row.
        HexEdit.OnFocusExit += OnHexEntered;

        UpdateType();
        Color = _currentColor;
    }

    private (RfSlider Slider, IntCounter Input) Get(Order order) => order switch
    {
        Order.Top => (TopSlider, TopInput),
        Order.Middle => (MiddleSlider, MiddleInput),
        Order.Bottom => (BottomSlider, BottomInput),
        _ => (AlphaSlider, AlphaInput),
    };

    private float GetDivisor(Order order) => order == Order.Alpha ? AlphaDivisor : Strategy.GetDivisor(order);

    private void UpdateType()
    {
        var strategy = Strategy;
        var (top, middle, bottom) = strategy.GetLabels();
        TopLabel.SetPlainText(top);
        MiddleLabel.SetPlainText(middle);
        BottomLabel.SetPlainText(bottom);

        // The boxes follow the range of the type; the clamped value is not a change the player made.
        _updating = true;
        TopInput.MaxValue = (int) strategy.GetDivisor(Order.Top);
        MiddleInput.MaxValue = (int) strategy.GetDivisor(Order.Middle);
        BottomInput.MaxValue = (int) strategy.GetDivisor(Order.Bottom);
        _updating = false;

        TopSlider.Gradient.ConfigureSlider(strategy.TopPreset);
        MiddleSlider.Gradient.ConfigureSlider(strategy.MiddlePreset);
        BottomSlider.Gradient.ConfigureSlider(strategy.BottomPreset);
    }

    private void UpdateSlider(Order order)
    {
        var (slider, input) = Get(order);

        var value = order switch
        {
            Order.Top => _colorData.X,
            Order.Middle => _colorData.Y,
            Order.Bottom => _colorData.Z,
            _ => _colorData.W,
        };

        slider.SetValueWithoutEvent(value);
        input.SetValue((int) (value * GetDivisor(order)));
    }

    private void UpdateVisuals()
    {
        TopSlider.Gradient.SetBaseColor(_colorData);
        MiddleSlider.Gradient.SetBaseColor(_colorData);
        BottomSlider.Gradient.SetBaseColor(_colorData);
        HexEdit.SetText(_currentColor.ToHex());
    }

    private void UpdateAllSliders()
    {
        DescriptionLabel.SetPlainText(ColorNaming.Describe(_currentColor, _localization));
        UpdateVisuals();
        UpdateSlider(Order.Top);
        UpdateSlider(Order.Middle);
        UpdateSlider(Order.Bottom);
        UpdateSlider(Order.Alpha);
    }

    private void OnSliderValueChanged(Order order)
    {
        if (_updating)
            return;

        _colorData = new Vector4(TopSlider.Value, MiddleSlider.Value, BottomSlider.Value, AlphaSlider.Value);
        _currentColor = Strategy.FromColorData(_colorData);

        UpdateVisuals();
        UpdateSlider(order);

        _changePending = true;
        if (!IsDragged || _sinceChange >= ChangeInterval)
            ReportPendingChange();
    }

    private void ReportPendingChange()
    {
        if (!_changePending)
            return;

        _changePending = false;
        _sinceChange = 0f;
        DescriptionLabel.SetPlainText(ColorNaming.Describe(_currentColor, _localization));
        OnColorChanged?.Invoke(_currentColor);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _sinceChange = MathF.Min(_sinceChange + args.DeltaSeconds, 60f);

        if (_changePending && _sinceChange >= ChangeInterval)
            ReportPendingChange();
    }

    private void OnHexEntered(RfLineEdit.LineEditEventArgs args)
    {
        var alpha = _currentColor.A;
        _currentColor = Color.FromHex(args.Text, _currentColor);

        // The alpha that can't be edited can't be changed with the code either.
        if (!IsAlphaVisible)
            _currentColor.A = alpha;

        _colorData = Strategy.ToColorData(_currentColor);
        OnColorChanged?.Invoke(_currentColor);

        UpdateAllSliders();
    }

    private enum Order
    {
        Top,
        Middle,
        Bottom,
        Alpha,
    }

    public enum ColorSelectorType
    {
        Rgb,
        Hsv,
    }

    private abstract class SliderStrategy
    {
        public abstract ColorSelectorStyleBox.ColorSliderPreset TopPreset { get; }
        public abstract ColorSelectorStyleBox.ColorSliderPreset MiddlePreset { get; }
        public abstract ColorSelectorStyleBox.ColorSliderPreset BottomPreset { get; }

        /// <summary>
        /// The color as the values of the sliders, each between 0 and 1.
        /// </summary>
        public abstract Vector4 ToColorData(Color color);

        public abstract Color FromColorData(Vector4 colorData);

        /// <summary>
        /// What a slider value is multiplied by to get the number shown in the box (not for the alpha).
        /// </summary>
        public abstract float GetDivisor(Order order);

        public abstract (string Top, string Middle, string Bottom) GetLabels();
    }

    private sealed class RgbSliderStrategy : SliderStrategy
    {
        public override ColorSelectorStyleBox.ColorSliderPreset TopPreset => ColorSelectorStyleBox.ColorSliderPreset.Red;
        public override ColorSelectorStyleBox.ColorSliderPreset MiddlePreset => ColorSelectorStyleBox.ColorSliderPreset.Green;
        public override ColorSelectorStyleBox.ColorSliderPreset BottomPreset => ColorSelectorStyleBox.ColorSliderPreset.Blue;

        public override Vector4 ToColorData(Color color) => new(color.R, color.G, color.B, color.A);

        public override Color FromColorData(Vector4 colorData) => new(colorData.X, colorData.Y, colorData.Z, colorData.W);

        public override float GetDivisor(Order order) => byte.MaxValue;

        public override (string Top, string Middle, string Bottom) GetLabels() => (
            Loc.GetString("color-selector-sliders-red"),
            Loc.GetString("color-selector-sliders-green"),
            Loc.GetString("color-selector-sliders-blue"));
    }

    private sealed class HsvSliderStrategy : SliderStrategy
    {
        public override ColorSelectorStyleBox.ColorSliderPreset TopPreset => ColorSelectorStyleBox.ColorSliderPreset.Hue;
        public override ColorSelectorStyleBox.ColorSliderPreset MiddlePreset => ColorSelectorStyleBox.ColorSliderPreset.Saturation;
        public override ColorSelectorStyleBox.ColorSliderPreset BottomPreset => ColorSelectorStyleBox.ColorSliderPreset.Value;

        public override Vector4 ToColorData(Color color) => Color.ToHsv(color);

        public override Color FromColorData(Vector4 colorData) => Color.FromHsv(colorData);

        public override float GetDivisor(Order order) => order == Order.Top ? 360f : 100f;

        public override (string Top, string Middle, string Bottom) GetLabels() => (
            Loc.GetString("color-selector-sliders-hue"),
            Loc.GetString("color-selector-sliders-saturation"),
            Loc.GetString("color-selector-sliders-value"));
    }
}
