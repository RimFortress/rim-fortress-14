using Content.Client._RF.CSS.Text;
using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._RF.CSS;

/// <summary>
/// Length resolution: turns the <see cref="LengthUnit"/> values of CSS properties into virtual pixels.
/// </summary>
/// <remarks>
/// <para>
/// Properties keep the value as it was written (<c>1.5em</c>) and are converted when they are used.
/// The conversion needs a <see cref="LengthContext"/>: the computed font size of the element,
/// the root font size, the metrics of the font and the viewport size. It is cached per control and rebuilt
/// when something it depends on changes: the font properties of this control or of an ancestor, the
/// parent, the UI scale or the viewport (the resizing of the root control). When that happens everything that was resolved from it
/// (text style, background, border, margin) is rebuilt too.
/// </para>
/// <para>
/// <c>font-size</c> is resolved against the parent's context. Its default is <c>1em</c>, so an element that
/// doesn't declare a font size gets its parent's one, which is the CSS inheritance of <c>font-size</c>.
/// </para>
/// </remarks>
public partial class CssControl
{
    private LengthContext? _lengthContext;

    /// <summary>
    /// The root whose size is the viewport; its resizing rebuilds the context.
    /// </summary>
    private Control? _viewportRoot;

    /// <summary>
    /// The nearest <see cref="CssControl"/> ancestor (controls of other types don't take part in CSS).
    /// </summary>
    internal CssControl? CssParent
    {
        get
        {
            for (var parent = Parent; parent != null; parent = parent.Parent)
            {
                if (parent is CssControl css)
                    return css;
            }

            return null;
        }
    }

    /// <summary>
    /// The context used to resolve the lengths of this control. Cached; see the class remarks.
    /// </summary>
    protected LengthContext LengthContext => _lengthContext ??= BuildLengthContext();

    /// <summary>
    /// The computed <c>font-size</c> of this control, in virtual pixels.
    /// </summary>
    public float ComputedFontSize => LengthContext.FontSize;

    /// <summary>
    /// Resolves a length to virtual pixels for this control.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <param name="percentBase">
    /// What 100% is for the property the value belongs to. Properties without a defined reference
    /// resolve a percentage to zero.
    /// </param>
    protected float ToPx(LengthUnit length, float percentBase = 0f) => length.Resolve(LengthContext, percentBase);

    /// <summary>
    /// Resolves a pair of lengths to virtual pixels for this control.
    /// </summary>
    protected Vector2 ToPx(UnitVector2 length, Vector2 percentBase = default) => length.Resolve(LengthContext, percentBase);

    /// <summary>
    /// Resolves four lengths (left, top, right, bottom) to a thickness in virtual pixels for this control.
    /// </summary>
    protected Thickness ToThickness(LengthUnit left, LengthUnit top, LengthUnit right, LengthUnit bottom)
        => new(ToPx(left), ToPx(top), ToPx(right), ToPx(bottom));

    /// <summary>
    /// The padding of this control in virtual pixels. Padding can't be negative, so a <c>calc()</c> that
    /// comes out below zero is clamped to zero.
    /// </summary>
    protected Thickness ToPaddingThickness()
        => new(
            MathF.Max(0f, ToPx(PaddingLeft)),
            MathF.Max(0f, ToPx(PaddingTop)),
            MathF.Max(0f, ToPx(PaddingRight)),
            MathF.Max(0f, ToPx(PaddingBottom)));

    /// <summary>
    /// The size of the viewport that <c>vw</c>/<c>vh</c> refer to, in virtual pixels.
    /// </summary>
    private Vector2 GetViewportSize() => GetViewportRoot()?.Size ?? Vector2.Zero;

    private Control? GetViewportRoot()
    {
        Control? root = null;

        for (var control = Parent; control != null; control = control.Parent)
        {
            root = control;
        }

        return root;
    }

    /// <inheritdoc/>
    protected override void EnteredTree()
    {
        base.EnteredTree();

        _viewportRoot = GetViewportRoot();

        if (_viewportRoot != null)
            _viewportRoot.OnResized += RefreshLengthContext;

        RefreshLengthContext(); // the viewport may have changed while the control was outside of the tree

        // The only place a control gets its style when it is put into a tree: its parent is styled already, since the
        // controls enter the tree from the top down, and its descendants follow. This also covers a screen built
        // before the CSS was loaded or reloaded while it was detached.
        Css.UpdateStyleOnEnter(this);
    }

    /// <inheritdoc/>
    protected override void ExitedTree()
    {
        base.ExitedTree();

        if (_viewportRoot != null)
            _viewportRoot.OnResized -= RefreshLengthContext;

        _viewportRoot = null;
    }

    private LengthContext BuildLengthContext()
    {
        var viewport = GetViewportSize();
        var scale = UIScale;
        var parent = CssParent;

        LengthContext parentContext;

        if (parent != null)
        {
            parentContext = parent.LengthContext;
        }
        else
        {
            var initialFont = _fonts.Resolve(CssFontFamily.SansSerif, FontWeight.Normal, FontSlant.Normal, DefaultFontSize);
            var initial = MeasureFont(initialFont, DefaultFontSize, scale);
            var initialLineHeight = NaturalLineHeight(initialFont, DefaultFontSize, scale);

            parentContext = new LengthContext(
                DefaultFontSize,
                DefaultFontSize,
                initial.Ex,
                initial.Ch,
                initialLineHeight,
                viewport,
                initial.Cap,
                initial.Ic,
                initialLineHeight);
        }

        // font-size is relative to the parent; the root's own rem refers to the initial size.
        var fontSize = FontSize.Resolve(parentContext, parentContext.FontSize);
        var rootFontSize = parent == null ? fontSize : parentContext.RootFontSize;

        var font = _fonts.Resolve(FontFamily, FontWeight, FontStyle, fontSize);
        var metrics = MeasureFont(font, fontSize, scale);

        // line-height may itself be written in em/ex/ch, so it is resolved against the context without lh.
        // rlh in the root's own line-height refers to the initial value.
        var partial = new LengthContext(
            fontSize,
            rootFontSize,
            metrics.Ex,
            metrics.Ch,
            0f,
            viewport,
            metrics.Cap,
            metrics.Ic,
            parentContext.RootLineHeight);
        var lineHeight = LineHeight.Resolve(partial).Kind == CssLineHeightKind.Length
            ? LineHeight.Resolve(partial).ResolvePx(font, scale) / scale
            : LineHeight.Kind == CssLineHeightKind.Multiplier
                ? NaturalLineHeight(font, fontSize, scale) * LineHeight.Value
                : NaturalLineHeight(font, fontSize, scale);

        return partial with
        {
            LineHeight = lineHeight,
            RootLineHeight = parent == null ? lineHeight : parentContext.RootLineHeight,
        };
    }

    /// <summary>
    /// The natural line height of a font in virtual pixels. A font without metrics (headless) gets 1.2 of its size.
    /// </summary>
    private static float NaturalLineHeight(Font font, float fontSize, float scale)
    {
        var height = font.GetLineHeight(scale) / scale;
        return height > 0f ? height : fontSize * 1.2f;
    }

    private static (float Ex, float Ch, float Cap, float Ic) MeasureFont(Font font, float fontSize, float scale)
    {
        var ex = font.GetCharMetrics(new Rune('x'), scale) is { } x ? x.Height / scale : fontSize * 0.5f;
        var ch = font.GetCharMetrics(new Rune('0'), scale) is { } zero ? zero.Advance / scale : fontSize * 0.5f;
        var cap = font.GetCharMetrics(new Rune('H'), scale) is { } h ? h.Height / scale : fontSize * 0.7f;
        var ic = font.GetCharMetrics(new Rune('\u6C34'), scale) is { } water ? water.Advance / scale : fontSize;

        return (ex, ch, cap, ic);
    }

    /// <summary>
    /// Called by the setters of the properties that decide the font.
    /// </summary>
    private void OnFontChanged()
    {
        InvalidateTextStyle();
        RefreshLengthContext();
    }

    /// <summary>
    /// Rebuilds the cached <see cref="LengthContext"/>; if the result differs, rebuilds everything that
    /// was resolved from it and refreshes the descendants.
    /// </summary>
    private void RefreshLengthContext()
    {
        var old = _lengthContext;
        _lengthContext = null;

        // Nobody has asked for the context yet, so nothing was resolved from it, and no descendant depends on it.
        if (old == null)
            return;

        if (old == LengthContext)
            return;

        OnLengthContextChanged();

        foreach (var child in Children)
        {
            RefreshDescendantContexts(child);
        }
    }

    private static void RefreshDescendantContexts(Control control)
    {
        if (control is CssControl css)
        {
            css.RefreshLengthContext(); // propagates further by itself if its context changed
            return;
        }

        foreach (var child in control.Children)
        {
            RefreshDescendantContexts(child);
        }
    }

    /// <summary>
    /// Rebuilds everything that holds resolved lengths.
    /// </summary>
    private void OnLengthContextChanged()
    {
        InvalidateTextStyle();

        if (Background != null)
            Background = GetBackground();

        ApplyMargin();
        InvalidateMeasure();
    }

    /// <inheritdoc/>
    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        RefreshLengthContext();
    }
}
