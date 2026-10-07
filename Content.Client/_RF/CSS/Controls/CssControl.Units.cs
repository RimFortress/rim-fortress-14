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
            var (initialEx, initialCh) = MeasureFont(initialFont, DefaultFontSize, scale);

            parentContext = new LengthContext(
                DefaultFontSize,
                DefaultFontSize,
                initialEx,
                initialCh,
                initialFont.GetLineHeight(scale) / scale,
                viewport);
        }

        // font-size is relative to the parent; the root's own rem refers to the initial size.
        var fontSize = FontSize.Resolve(parentContext, parentContext.FontSize);
        var rootFontSize = parent == null ? fontSize : parentContext.RootFontSize;

        var font = _fonts.Resolve(FontFamily, FontWeight, FontStyle, fontSize);
        var (ex, ch) = MeasureFont(font, fontSize, scale);

        // line-height may itself be written in em/ex/ch, so it is resolved against the context without lh.
        var partial = new LengthContext(fontSize, rootFontSize, ex, ch, 0f, viewport);
        var lineHeight = LineHeight.Resolve(partial).ResolvePx(font, scale) / scale;

        return partial with { LineHeight = lineHeight };
    }

    private static (float Ex, float Ch) MeasureFont(Font font, float fontSize, float scale)
    {
        var ex = font.GetCharMetrics(new Rune('x'), scale) is { } x ? x.Height / scale : fontSize * 0.5f;
        var ch = font.GetCharMetrics(new Rune('0'), scale) is { } zero ? zero.Advance / scale : fontSize * 0.5f;

        return (ex, ch);
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
