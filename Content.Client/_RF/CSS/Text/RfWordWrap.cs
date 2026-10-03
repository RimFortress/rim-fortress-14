using System.Text;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS.Text;

/// <summary>
/// A finished line, reported by <see cref="RfWordWrap"/>.
/// </summary>
/// <param name="Index">Rune index at which the NEXT line starts (<see cref="int.MaxValue"/> for the last line).</param>
/// <param name="Width">Width of the finished line in pixels, trailing spaces excluded.</param>
/// <param name="IsHard">Was the line ended by an explicit newline (such lines are never justified)?</param>
public readonly record struct RfLineBreak(int Index, float Width, bool IsHard);

/// <summary>
/// A copy of the engine's internal <c>WordWrap</c>, extended for CSS text:
/// per-line widths (needed for <c>text-align</c>), <c>text-indent</c>, <c>word-break</c>, <c>overflow-wrap</c>
/// and non-wrapping modes (pass <see cref="float.PositiveInfinity"/> as the limit).
/// </summary>
/// <remarks>
/// Usage per rune: <see cref="NextRune"/> first, then (unless it returned <c>true</c>) <see cref="NextMetrics"/>
/// with the metrics of the same rune, letter/word spacing already included in the advance.
/// Finished lines are appended to the list passed to the constructor; call <see cref="FinalizeText"/> at the end.
/// </remarks>
public struct RfWordWrap
{
    private static readonly Rune Space = new(' ');
    private static readonly Rune Newline = new('\n');

    private readonly float _maxSizeX;
    private readonly bool _breakAll;
    private readonly bool _emergencyBreak;
    private readonly List<RfLineBreak> _breaks;

    /// <summary>
    /// Width of the widest finished line.
    /// </summary>
    public float MaxUsedWidth;

    /// <summary>
    /// Index of the rune that is currently being processed.
    /// </summary>
    public int BreakIndexCounter;

    /// <summary>
    /// Index the next rune will get.
    /// </summary>
    public int NextBreakIndexCounter;

    /// <summary>
    /// If the current word turns out to be too long, this is the rune index to break the line at
    /// and the line size at that point. <c>null</c> if the word starts a line.
    /// </summary>
    public (int Index, float LineSize)? WordStartBreakIndex;

    /// <summary>
    /// Size of the current word in pixels.
    /// </summary>
    public int WordSizePixels;

    /// <summary>
    /// Horizontal position of the text cursor in pixels.
    /// </summary>
    public int PosX;

    /// <summary>
    /// The previously processed rune.
    /// </summary>
    public Rune LastRune;

    // Width of the space run at the end of the current line (it hangs and doesn't count for alignment).
    private int _trailingPx;

    // The same, at the moment the current word started.
    private int _wordStartTrailingPx;

    /// <param name="maxSizeX">Line width limit in pixels, or <see cref="float.PositiveInfinity"/> to never wrap.</param>
    /// <param name="wordBreak">CSS <c>word-break</c>.</param>
    /// <param name="overflowWrap">CSS <c>overflow-wrap</c>.</param>
    /// <param name="breaks">The list finished lines are appended to.</param>
    /// <param name="firstLineIndent">CSS <c>text-indent</c> in pixels.</param>
    public RfWordWrap(
        float maxSizeX,
        CssWordBreak wordBreak,
        CssOverflowWrap overflowWrap,
        List<RfLineBreak> breaks,
        int firstLineIndent = 0)
    {
        this = default;
        _maxSizeX = maxSizeX;
        _breaks = breaks;
        _breakAll = wordBreak == CssWordBreak.BreakAll;
        _emergencyBreak = overflowWrap != CssOverflowWrap.Normal
                          || wordBreak is CssWordBreak.BreakAll or CssWordBreak.BreakWord;
        LastRune = new Rune('A');
        PosX = Math.Max(0, firstLineIndent);
    }

    /// <summary>
    /// Adds <paramref name="rune"/> as the next glyph and updates the line break state.
    /// </summary>
    /// <returns><c>true</c> if the rune occupies no space (an explicit newline) and must not be measured.</returns>
    public bool NextRune(Rune rune)
    {
        BreakIndexCounter = NextBreakIndexCounter;
        NextBreakIndexCounter += 1;

        var skip = false;

        if (IsWordBoundary(LastRune, rune) || rune == Newline)
        {
            // A word boundary means we know where the previous word ends.
            if (PosX > _maxSizeX && LastRune != Space && CanWrapBefore(WordStartBreakIndex, out var start))
            {
                // The word is too big for the line: break BEFORE it.
                Emit(start.Index, start.LineSize - _wordStartTrailingPx, false);
                PosX = WordSizePixels;
                _trailingPx = 0;
            }

            // Start a new word, since we hit a boundary.
            WordSizePixels = 0;
            WordStartBreakIndex = (BreakIndexCounter, PosX);
            _wordStartTrailingPx = _trailingPx;

            if (rune == Newline)
            {
                Emit(BreakIndexCounter, PosX - _trailingPx, true);
                PosX = 0;
                _trailingPx = 0;
                WordStartBreakIndex = null;
                skip = true;
            }
        }

        LastRune = rune;
        return skip;
    }

    /// <summary>
    /// Adds the metrics of the rune passed to the last <see cref="NextRune"/> call.
    /// </summary>
    /// <param name="metrics">Glyph metrics; the advance must already include letter/word spacing.</param>
    /// <param name="abort">Set if there is not enough room even for a single glyph and layout must stop.</param>
    public void NextMetrics(in CharMetrics metrics, out bool abort)
    {
        abort = false;

        var oldWordSizePixels = WordSizePixels;
        var posBefore = PosX;
        var trailingBefore = _trailingPx;

        WordSizePixels += metrics.Advance;
        PosX += metrics.Advance;
        _trailingPx = LastRune == Space ? trailingBefore + metrics.Advance : 0;

        if (PosX <= _maxSizeX)
            return;

        // The word doesn't fit on a line even alone: break inside it, right before the current glyph.
        if (_emergencyBreak && WordSizePixels > _maxSizeX)
        {
            if (oldWordSizePixels == 0)
            {
                // Not enough space for a single glyph.
                abort = true;
                return;
            }

            Emit(BreakIndexCounter, posBefore - trailingBefore, false);
            WordSizePixels = metrics.Advance;
            WordStartBreakIndex = null;
            PosX = WordSizePixels;
            _trailingPx = 0;
            return;
        }

        // Otherwise move the whole word to the next line. After that the word is the first on its line,
        // so there is nowhere left to break before it (this also covers words that overflow when
        // emergency breaks are disabled).
        if (oldWordSizePixels != 0 && CanWrapBefore(WordStartBreakIndex, out var start))
        {
            Emit(start.Index, start.LineSize - _wordStartTrailingPx, false);
            PosX = WordSizePixels;
            _trailingPx = 0;
            WordStartBreakIndex = null;
        }
    }

    /// <summary>
    /// Finishes the text and emits the last line(s).
    /// </summary>
    /// <returns>Width of the widest line in pixels.</returns>
    public int FinalizeText()
    {
        // Wrapping isn't checked for the last word while processing runes.
        if (PosX > _maxSizeX && CanWrapBefore(WordStartBreakIndex, out var start))
        {
            Emit(start.Index, start.LineSize - _wordStartTrailingPx, false);
            Emit(int.MaxValue, WordSizePixels, false);
        }
        else
        {
            Emit(int.MaxValue, PosX - _trailingPx, false);
        }

        return (int) MathF.Ceiling(MaxUsedWidth);
    }

    // A break before a word that starts the line would only produce an empty line.
    private static bool CanWrapBefore((int Index, float LineSize)? wordStart, out (int Index, float LineSize) start)
    {
        start = wordStart.GetValueOrDefault();
        return wordStart is { LineSize: > 0 };
    }

    private void Emit(int index, float width, bool hard)
    {
        width = MathF.Max(0f, width);
        MaxUsedWidth = MathF.Max(MaxUsedWidth, width);
        _breaks.Add(new RfLineBreak(index, width, hard));
    }

    private readonly bool IsWordBoundary(Rune a, Rune b)
        => _breakAll || a == Space || b == Space || a == new Rune('-') || b == new Rune('-');
}
