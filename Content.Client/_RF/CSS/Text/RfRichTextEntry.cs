using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Content.Client._RF.CSS.Markup;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS.Text;

/// <summary>
/// One laid-out line of an <see cref="RfRichTextEntry"/>.
/// </summary>
/// <param name="StartIndex">Index of the first rune of the line (counting runes of the filtered text).</param>
/// <param name="Width">Line width in pixels, trailing spaces and hanging whitespace excluded, indent included.</param>
/// <param name="Height">Line box height in pixels (<c>line-height</c> already applied).</param>
/// <param name="SpaceCount">Number of stretchable spaces inside the line (for <c>text-align: justify</c>).</param>
/// <param name="EndsHard">Was the line ended by an explicit newline?</param>
public readonly record struct RfLine(int StartIndex, float Width, int Height, int SpaceCount, bool EndsHard);

/// <summary>
/// A copy of the engine's internal <c>RichTextEntry</c> (the layout and drawing core of <c>RichTextLabel</c>)
/// reworked for CSS text. Differences from the original:
/// <list type="bullet">
/// <item><description>It is a class, not a struct, so there is no copy-and-reassign dance.</description></item>
/// <item><description>The style is passed to <see cref="Update"/> instead of being fixed in the constructor.</description></item>
/// <item><description>Layout keeps a list of <see cref="RfLine"/> (width, height, space count) instead of bare break indices.</description></item>
/// <item><description>Drawing is done in passes: shadows, outline, fill (with decorations, ellipsis and controls).</description></item>
/// </list>
/// </summary>
/// <remarks>
/// If the entry is replaced or discarded without its owner being removed, <see cref="RemoveControls"/>
/// must be called, so the controls created by markup tags don't stay attached to the parent.
/// </remarks>
public sealed class RfRichTextEntry
{
    /// <summary>
    /// The markup tags allowed by default: safe formatting tags only (no fonts, no controls).
    /// </summary>
    public static readonly Type[] DefaultTags =
    [
        typeof(RfBoldItalicTag),
        typeof(RfBoldTag),
        typeof(RfBulletTag),
        typeof(RfColorTag),
        typeof(RfHeadingTag),
        typeof(RfItalicTag),
        typeof(RfUnderlineTag),
        typeof(RfStrikeTag),
    ];

    private enum Pass : byte
    {
        Shadow,
        Outline,
        Fill,
    }

    private static readonly Rune Space = new(' ');
    private static readonly Rune Newline = new('\n');
    private static readonly Rune Ellipsis = new('…');

    private readonly Type[]? _tagsAllowed;
    private readonly RfMarkupContext _context;
    private readonly List<RfLineBreak> _breaks = new();
    private readonly List<RfLine> _lines = new();
    private readonly List<int> _runeAdvances = new();
    private readonly List<int> _spaceIndices = new();
    private RfTextStyle? _style;
    private int _truncatedEndIndex = int.MaxValue;

    /// <summary>
    /// The displayed message.
    /// </summary>
    public readonly FormattedMessage Message;

    /// <summary>
    /// Controls created by markup tags, keyed by the index of the node that created them.
    /// </summary>
    public readonly Dictionary<int, Control>? Controls;

    /// <summary>
    /// Width of the widest visible line, in pixels. Valid after <see cref="Update"/>.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Total height of all visible lines, in pixels. Valid after <see cref="Update"/>.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// Did <c>line-clamp</c> cut some of the lines off?
    /// </summary>
    public bool Truncated { get; private set; }

    /// <summary>
    /// The visible lines. Valid after <see cref="Update"/>.
    /// </summary>
    public IReadOnlyList<RfLine> Lines => _lines;

    /// <summary>
    /// Creates an entry and the controls of its markup tags (they are added to <paramref name="parent"/>).
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="parent">The control that will own controls created by markup tags.</param>
    /// <param name="tagManager">Markup tag manager.</param>
    /// <param name="tagsAllowed">Allowed markup tags, <c>null</c> = all of them.</param>
    public RfRichTextEntry(
        FormattedMessage message,
        Control parent,
        RfMarkupTagManager tagManager,
        Type[]? tagsAllowed = null)
    {
        Message = message;
        _tagsAllowed = tagsAllowed;
        _context = tagManager.CreateContext();
        Controls = GetControls(parent, tagManager);
    }

    private Dictionary<int, Control>? GetControls(Control parent, RfMarkupTagManager tagManager)
    {
        Dictionary<int, Control>? tagControls = null;
        var nodeIndex = -1;

        foreach (var node in Message)
        {
            nodeIndex++;

            // Only opening tags may provide a control.
            if (node.Name == null || node.Closing)
                continue;

            if (!tagManager.TryGetHandler(node.Name, _tagsAllowed, out var handler)
                || !handler.TryCreateControl(node, out var control))
                continue;

            // Tag handler instances are shared between entries: they must not keep state,
            // and must return a new control on every call.
            DebugTools.Assert(handler.TryCreateControl(node, out var other) && other != control);

            parent.Children.Add(control);
            tagControls ??= new Dictionary<int, Control>();
            tagControls.Add(nodeIndex, control);
        }

        return tagControls;
    }

    /// <summary>
    /// Removes all controls owned by this entry from their parents.
    /// </summary>
    public void RemoveControls()
    {
        if (Controls == null)
            return;

        foreach (var control in Controls.Values)
        {
            control.Orphan();
        }
    }

    /// <summary>
    /// Hides all controls owned by this entry (used when the entry is out of frame).
    /// </summary>
    public void HideControls()
    {
        if (Controls == null)
            return;

        foreach (var control in Controls.Values)
        {
            control.Visible = false;
        }
    }

    /// <summary>
    /// Recalculates line breaks and dimensions.
    /// </summary>
    /// <param name="tagManager">Markup tag manager.</param>
    /// <param name="style">The computed style used for this layout and for the following <see cref="Draw"/> calls.</param>
    /// <param name="maxSizeX">Maximum width available for the text, in pixels.</param>
    /// <param name="uiScale">Current UI scale.</param>
    public void Update(RfMarkupTagManager tagManager, RfTextStyle style, float maxSizeX, float uiScale)
    {
        _style = style;
        _breaks.Clear();
        _lines.Clear();
        _runeAdvances.Clear();
        _spaceIndices.Clear();
        Truncated = false;
        _truncatedEndIndex = int.MaxValue;
        Width = 0;
        Height = 0;

        var defaultFont = style.Font;
        var spacing = (int) MathF.Round(style.LetterSpacing * uiScale);
        var wordSpacing = (int) MathF.Round(style.WordSpacing * uiScale);
        var indent = (int) MathF.Round(MathF.Max(0f, style.TextIndent) * uiScale);
        var limit = style.Wraps ? maxSizeX : float.PositiveInfinity;

        var wordWrap = new RfWordWrap(limit, style.WordBreak, style.OverflowWrap, _breaks, indent);
        var filter = new RfTextFilter(style);
        _context.Reset(RfInlineStyle.FromTextStyle(style), defaultFont);

        // A line gets its height from the font that was active when it was closed (same as the engine does).
        var lineHeights = new List<int>();

        var nodeIndex = -1;

        foreach (var node in Message)
        {
            nodeIndex++;
            var text = filter.Apply(tagManager.ProcessNode(node, _context, _tagsAllowed));
            var font = _context.Font;

            foreach (var rune in text.EnumerateRunes())
            {
                var skip = wordWrap.NextRune(rune);
                SyncHeights(font);

                if (rune == Space)
                    _spaceIndices.Add(wordWrap.BreakIndexCounter);

                // Every rune gets exactly one entry, so rune index == list index.
                if (skip || !font.TryGetCharMetrics(rune, uiScale, out var metrics))
                {
                    _runeAdvances.Add(0);
                    continue;
                }

                var advance = metrics.Advance + spacing + (rune == Space ? wordSpacing : 0);
                _runeAdvances.Add(advance);

                var advMetrics = new CharMetrics(
                    metrics.BearingX,
                    metrics.BearingY,
                    advance,
                    metrics.Width,
                    metrics.Height);

                wordWrap.NextMetrics(advMetrics, out var abort);
                SyncHeights(font);

                if (!abort)
                    continue;

                wordWrap.FinalizeText();
                SyncHeights(font);
                BuildLines(style, lineHeights);
                return;
            }

            if (Controls == null || !Controls.TryGetValue(nodeIndex, out var control))
                continue;

            // Let the tag configure its control with the style active at this position.
            tagManager.ApplyContext(node, control, _context, _tagsAllowed);

            // An inline control is treated as one large rune.
            control.Measure(new Vector2(limit, float.PositiveInfinity));

            var desired = control.DesiredPixelSize;
            wordWrap.NextMetrics(new CharMetrics(0, 0, desired.X, desired.X, desired.Y), out var controlAbort);
            SyncHeights(font);

            if (!controlAbort)
                continue;

            wordWrap.FinalizeText();
            SyncHeights(font);
            BuildLines(style, lineHeights);
            return;
        }

        wordWrap.FinalizeText();

        SyncHeights(_context.Font);
        BuildLines(style, lineHeights);
        return;

        void SyncHeights(Font font)
        {
            var height = style.LineHeight.ResolvePx(font, uiScale);

            while (lineHeights.Count < _breaks.Count)
            {
                lineHeights.Add(height);
            }
        }
    }

    // Turns the raw break list into RfLine entries and applies line-clamp.
    private void BuildLines(RfTextStyle style, List<int> lineHeights)
    {
        var spaceCursor = 0;
        var totalRunes = _runeAdvances.Count;

        for (var k = 0; k < _breaks.Count; k++)
        {
            var start = k == 0 ? 0 : _breaks[k - 1].Index;
            var end = Math.Min(_breaks[k].Index, totalRunes);

            var first = spaceCursor;
            while (spaceCursor < _spaceIndices.Count && _spaceIndices[spaceCursor] < end)
            {
                spaceCursor++;
            }

            var count = spaceCursor - first;

            // Spaces at the start of a wrapped line and at the end of any line hang: they are not stretched.
            var lead = start;
            while (k > 0 && count > 0 && _spaceIndices[first] == lead)
            {
                first++;
                count--;
                lead++;
            }

            var last = end - 1;
            while (count > 0 && _spaceIndices[first + count - 1] == last)
            {
                count--;
                last--;
            }

            _lines.Add(new RfLine(start, _breaks[k].Width, lineHeights[k], count, _breaks[k].IsHard));
        }

        if (style.LineClamp is { } clamp and > 0 && clamp < _lines.Count)
        {
            Truncated = true;
            _truncatedEndIndex = _lines[clamp].StartIndex;
            _lines.RemoveRange(clamp, _lines.Count - clamp);
        }

        var maxWidth = 0f;
        foreach (var line in _lines)
        {
            maxWidth = MathF.Max(maxWidth, line.Width);
            Height += line.Height;
        }

        Width = (int) MathF.Ceiling(maxWidth);
    }

    /// <summary>
    /// Draws the entry using the style of the last <see cref="Update"/> call.
    /// </summary>
    /// <param name="tagManager">Markup tag manager.</param>
    /// <param name="handle">Drawing handle.</param>
    /// <param name="drawBox">The box to draw the text in, in pixels. Its width is used for alignment and ellipsis.</param>
    /// <param name="uiScale">Current UI scale.</param>
    public void Draw(
        RfMarkupTagManager tagManager,
        DrawingHandleScreen handle,
        UIBox2 drawBox,
        float uiScale)
    {
        if (_style is not { } style || _lines.Count == 0)
            return;

        // CSS paints the first shadow layer on top, so the layers are drawn in reverse order.
        for (var i = style.Shadows.Count - 1; i >= 0; i--)
        {
            DrawPass(tagManager, handle, drawBox, uiScale, Pass.Shadow, style.Shadows[i]);
        }

        if (style.Outline != null)
            DrawPass(tagManager, handle, drawBox, uiScale, Pass.Outline, null);

        DrawPass(tagManager, handle, drawBox, uiScale, Pass.Fill, null);
    }

    private void DrawPass(
        RfMarkupTagManager tagManager,
        DrawingHandleScreen handle,
        UIBox2 drawBox,
        float uiScale,
        Pass pass,
        CssTextShadow? shadow)
    {
        var style = _style!;
        var defaultFont = style.Font;

        _context.Reset(RfInlineStyle.FromTextStyle(style), defaultFont);

        var filter = new RfTextFilter(style);
        var spacing = (int) MathF.Round(style.LetterSpacing * uiScale);
        var wordSpacing = (int) MathF.Round(style.WordSpacing * uiScale);
        var available = drawBox.Width;
        var ellipsisWidth = (defaultFont.GetCharMetrics(Ellipsis, uiScale)?.Advance ?? 0) + spacing;
        var passOffset = shadow is { } s ? s.Offset * uiScale : Vector2.Zero;

        int lineIndex;
        var runeIndex = 0;
        var lineTop = drawBox.Top;
        var baseline = Vector2.Zero;
        float extraPerSpace;
        int spacesLeft;
        var controlYAdvance = 0f;
        int? cutIndex;
        var ellipsisDone = false;

        BeginLine(0);

        var nodeIndex = -1;

        foreach (var node in Message)
        {
            nodeIndex++;
            var text = filter.Apply(tagManager.ProcessNode(node, _context, _tagsAllowed));
            var color = _context.Color;
            var font = _context.Font;

            foreach (var rune in text.EnumerateRunes())
            {
                while (lineIndex + 1 < _lines.Count && _lines[lineIndex + 1].StartIndex <= runeIndex)
                {
                    BeginLine(lineIndex + 1);
                }

                // Everything past the last visible line is clamped away.
                if (runeIndex >= _truncatedEndIndex)
                {
                    if (cutIndex != null && !ellipsisDone)
                        DrawEllipsis(font, color);

                    return;
                }

                if (cutIndex is { } cut && runeIndex >= cut)
                {
                    if (!ellipsisDone)
                        DrawEllipsis(font, color);

                    runeIndex++;
                    continue;
                }

                if (rune == Newline)
                {
                    runeIndex++;
                    continue;
                }

                // A space that starts a wrapped line hangs: it takes no room.
                var hanging = rune == Space && lineIndex > 0 && runeIndex == _lines[lineIndex].StartIndex;
                var startX = baseline.X;
                var advance = 0f;

                if (hanging)
                {
                    DrawGlyph(font, rune, color);
                }
                else
                {
                    advance = DrawGlyph(font, rune, color);

                    if (advance > 0f)
                    {
                        advance += spacing;

                        if (rune == Space)
                        {
                            advance += wordSpacing;

                            if (spacesLeft > 0)
                            {
                                advance += extraPerSpace;
                                spacesLeft--;
                            }
                        }
                    }
                }

                if (advance > 0f)
                    DrawDecoration(font, startX, advance, color);

                baseline.X += advance;
                runeIndex++;
            }

            if (Controls == null || !Controls.TryGetValue(nodeIndex, out var control))
                continue;

            var invertedScale = 1f / uiScale;

            if (pass == Pass.Fill)
            {
                // Controls may have been hidden via HideControls while the entry was out of frame.
                control.Visible = true;
                control.Measure(new Vector2(Width, Height));
                control.Arrange(UIBox2.FromDimensions(
                    baseline.X * invertedScale,
                    (baseline.Y - defaultFont.GetAscent(uiScale)) * invertedScale,
                    control.DesiredSize.X,
                    control.DesiredSize.Y));
            }
            else
            {
                // Other passes still need the control's advance to place the following glyphs.
                control.Measure(new Vector2(Width, Height));
            }

            controlYAdvance = MathF.Max(0f, control.DesiredPixelSize.Y - _lines[lineIndex].Height);
            baseline.X += control.DesiredPixelSize.X;
        }

        // Text ended exactly where the ellipsis has to go.
        if (cutIndex != null && !ellipsisDone && Truncated)
            DrawEllipsis(_context.Font, _context.Color);

        return;

        void BeginLine(int k)
        {
            lineIndex = k;
            var line = _lines[k];

            if (k > 0)
                lineTop += _lines[k - 1].Height + controlYAdvance;

            controlYAdvance = 0f;

            // CSS half-leading: the text is centered inside the line box.
            var halfLeading = (line.Height - defaultFont.GetLineHeight(uiScale)) / 2f;
            var indent = k == 0 ? MathF.Max(0f, style.TextIndent) * uiScale : 0f;
            var free = available - line.Width;
            var offset = 0f;

            extraPerSpace = 0f;
            spacesLeft = 0;

            switch (style.TextAlign)
            {
                case CssTextAlign.Center:
                    offset = free / 2f;
                    break;
                case CssTextAlign.Right:
                case CssTextAlign.End:
                    offset = free;
                    break;
                case CssTextAlign.Justify:
                    if (!line.EndsHard && k < _lines.Count - 1 && line.SpaceCount > 0 && free > 0f)
                    {
                        extraPerSpace = free / line.SpaceCount;
                        spacesLeft = line.SpaceCount;
                    }

                    break;
            }

            // When the line overflows, it stays start-aligned.
            baseline = new Vector2(
                drawBox.Left + MathF.Max(0f, offset) + indent,
                lineTop + halfLeading + defaultFont.GetAscent(uiScale));

            // Decide where the line has to be cut for an ellipsis.
            cutIndex = null;
            var lastVisibleOfTruncated = Truncated && k == _lines.Count - 1;
            var overflows = style.TextOverflow == CssTextOverflow.Ellipsis && line.Width > available + 0.5f;

            if (lastVisibleOfTruncated || overflows)
                cutIndex = ComputeCut(k, available - indent - ellipsisWidth);
        }

        float DrawGlyph(Font font, Rune rune, Color color)
        {
            var position = baseline + passOffset;

            return pass == Pass.Outline
                ? font.DrawCharOutline(handle, rune, position, uiScale, style.Outline!.Value)
                : font.DrawChar(handle, rune, position, uiScale, shadow?.Color ?? color);
        }

        void DrawDecoration(Font font, float x, float width, Color color)
        {
            if (pass == Pass.Outline)
                return;

            DrawTextDecoration(
                handle,
                style,
                style.DecorationLine | _context.Current.Decoration,
                font,
                new Vector2(x, baseline.Y) + passOffset,
                width,
                shadow?.Color ?? _context.Current.DecorationColor ?? style.DecorationColor ?? color,
                uiScale);
        }

        void DrawEllipsis(Font font, Color color)
        {
            ellipsisDone = true;
            var advance = DrawGlyph(font, Ellipsis, color);
            DrawDecoration(font, baseline.X, advance, color);
        }
    }

    // Finds the rune index at which line k must be cut so that an ellipsis still fits into the budget.
    private int ComputeCut(int k, float budget)
    {
        var start = _lines[k].StartIndex;
        var end = k + 1 < _lines.Count ? _lines[k + 1].StartIndex : _truncatedEndIndex;
        end = Math.Min(end, _runeAdvances.Count);

        var run = 0f;

        for (var i = start; i < end; i++)
        {
            run += _runeAdvances[i];

            if (run > budget)
                return i;
        }

        return end;
    }

    // Draws the decoration lines for one glyph advance. Adjacent segments share their edges, so there are no seams.
    private static void DrawTextDecoration(
        DrawingHandleScreen handle,
        RfTextStyle style,
        CssTextDecorationLine line,
        Font font,
        Vector2 baseline,
        float width,
        Color color,
        float uiScale)
    {
        if (line == CssTextDecorationLine.None || width <= 0f)
            return;

        // Heuristics: Font doesn't expose underline position/thickness metrics.
        var thickness = style.DecorationThickness is { } t
            ? MathF.Max(1f, MathF.Round(t * uiScale))
            : MathF.Max(1f, MathF.Round(font.GetHeight(uiScale) / 14f));
        var ascent = font.GetAscent(uiScale);
        var descent = font.GetDescent(uiScale);

        if ((line & CssTextDecorationLine.Underline) != 0)
            Line(baseline.Y + (style.UnderlineOffset is { } o ? o * uiScale : descent * 0.4f));

        if ((line & CssTextDecorationLine.Overline) != 0)
            Line(baseline.Y - ascent);

        if ((line & CssTextDecorationLine.LineThrough) != 0)
            Line(baseline.Y - ascent * 0.35f - thickness / 2f);

        return;

        void Line(float y)
        {
            y = MathF.Round(y);
            handle.DrawRect(new UIBox2(baseline.X, y, baseline.X + width, y + thickness), color);

            // Dotted/dashed/wavy fall back to solid for now.
            if (style.DecorationStyle == CssTextDecorationStyle.Double)
                handle.DrawRect(new UIBox2(baseline.X, y + thickness * 2f, baseline.X + width, y + thickness * 3f), color);
        }
    }
}
