using System.Numerics;
using Content.Client._RF.Stylesheets;
using Robust.Client.Graphics;

namespace Content.Client._RF.UserInterface.Controls;

public sealed class FancyBack : ShaderPanelContainer
{
    public const string StylePropertyEdgeColor = "edge-color";
    public const string StylePropertyPadSize = "pad-size";
    public const string StylePropertyEdgeSize = "edge-size";

    private float _padSize = 4;
    private float _edgeSize = 2;
    private Color _edgeColor = StyleFortress.GoldFortress;

    private bool _hasTopEdge = true;
    private bool _hasBottomEdge = true;
    private bool _hasMargins = true;

    public bool HasTopEdge
    {
        get => _hasTopEdge;
        set
        {
            _hasTopEdge = value;
            InvalidateMeasure();
        }
    }

    public bool HasBottomEdge
    {
        get => _hasBottomEdge;
        set
        {
            _hasBottomEdge = value;
            InvalidateMeasure();
        }
    }

    public bool HasMargins
    {
        get => _hasMargins;
        set
        {
            _hasMargins = value;
            InvalidateMeasure();
        }
    }

    protected override void StylePropertiesChanged()
    {
        _edgeColor = TryGetStyleProperty(StylePropertyEdgeColor, out Color color) ? color: _edgeColor;
        _edgeSize = TryGetStyleProperty(StylePropertyPadSize, out float edge) ? edge: _edgeSize;
        _padSize  = TryGetStyleProperty(StylePropertyEdgeSize, out float pad) ? pad: _padSize;

        base.StylePropertiesChanged();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var padSize = HasMargins ? _padSize : 0;
        var padSizeTotal = 0f;

        if (HasBottomEdge)
            padSizeTotal += padSize + _edgeSize;
        if (HasTopEdge)
            padSizeTotal += padSize + _edgeSize;

        var size = Vector2.Zero;

        availableSize.Y -= padSizeTotal;

        foreach (var child in Children)
        {
            child.Measure(availableSize);
            size = Vector2.Max(size, child.DesiredSize);
        }

        return size + new Vector2(0, padSizeTotal);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var box = new UIBox2(Vector2.Zero, finalSize);

        var padSize = HasMargins ? _padSize : 0;

        if (HasTopEdge)
        {
            box += (0, padSize + _edgeSize, 0, 0);
        }

        if (HasBottomEdge)
        {
            box += (0, 0, 0, -(padSize + _edgeSize));
        }

        foreach (var child in Children)
        {
            child.Arrange(box);
        }

        return finalSize;
    }


    protected override void Draw(DrawingHandleScreen handle)
    {
        UIBox2 centerBox = PixelSizeBox;

        var padSize = HasMargins ? _padSize : 0;

        if (HasTopEdge)
        {
            centerBox += (0, (padSize + _edgeSize) * UIScale, 0, 0);
            handle.DrawRect(
                new UIBox2(0, padSize * UIScale, PixelWidth, centerBox.Top),
                _edgeColor);
        }

        if (HasBottomEdge)
        {
            centerBox += (0, 0, 0, -((padSize + _edgeSize) * UIScale));
            handle.DrawRect(
                new UIBox2(0, centerBox.Bottom, PixelWidth, PixelHeight - padSize * UIScale),
                _edgeColor);
        }

        DrawTexture(handle, centerBox);
    }
}
