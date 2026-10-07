using System.Text;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// Logic shared by <see cref="CssLineEdit"/> and <see cref="CssTextEdit"/>, a copy of the engine's internal
/// <c>TextEditShared</c>.
/// </summary>
internal static class CssTextEditShared
{
    // Approach for EndWordPosition and PrevWordPosition taken from Avalonia.

    /// <summary>
    /// Runes of the string starting at <paramref name="start"/>, forward.
    /// </summary>
    public static IEnumerable<Rune> RunesFrom(string str, int start)
    {
        for (var i = start; i < str.Length;)
        {
            Rune.DecodeFromUtf16(str.AsSpan(i), out var rune, out var consumed);
            yield return rune;
            i += consumed;
        }
    }

    /// <summary>
    /// Runes of the string before <paramref name="end"/>, backward.
    /// </summary>
    public static IEnumerable<Rune> RunesBefore(string str, int end)
    {
        for (var i = end; i > 0;)
        {
            Rune.DecodeLastFromUtf16(str.AsSpan(0, i), out var rune, out var consumed);
            yield return rune;
            i -= consumed;
        }
    }

    public static int EndWordPosition(string str, int cursor)
    {
        using var runes = RunesFrom(str, cursor).GetEnumerator();
        return cursor + EndWordPosition(runes);
    }

    public static int PrevWordPosition(string str, int cursor)
    {
        using var runes = RunesBefore(str, cursor).GetEnumerator();
        return cursor + PrevWordPosition(runes);
    }

    /// <summary>
    /// The offset (in chars, positive) from the start of the runes to the end of the word.
    /// </summary>
    public static int EndWordPosition<T>(T runes) where T : IEnumerator<Rune>
    {
        if (!runes.MoveNext())
            return 0;

        var i = 0;
        if (!IterForward(CharClass.Whitespace))
            return i;

        var charClass = GetCharClass(runes.Current);
        IterForward(charClass);

        return i;

        bool IterForward(CharClass cClass)
        {
            var hasNext = true;

            do
            {
                var rune = runes.Current;

                if (GetCharClass(rune) != cClass)
                    break;

                i += rune.Utf16SequenceLength;

                hasNext = runes.MoveNext();
            } while (hasNext);

            return hasNext;
        }
    }

    /// <summary>
    /// The offset (in chars, negative) from the start of the (reversed) runes to the start of the word.
    /// </summary>
    public static int PrevWordPosition<T>(T runes) where T : IEnumerator<Rune>
    {
        if (!runes.MoveNext())
            return 0;

        var startRune = runes.Current;
        var charClass = GetCharClass(startRune);

        var i = 0;
        var keepGoing = IterBackward();

        if (keepGoing && charClass == CharClass.Whitespace)
        {
            charClass = GetCharClass(runes.Current);

            IterBackward();
        }

        return i;

        bool IterBackward()
        {
            do
            {
                var rune = runes.Current;

                if (GetCharClass(rune) != charClass)
                    return true;

                i -= rune.Utf16SequenceLength;
            } while (runes.MoveNext());

            return false;
        }
    }

    private static CharClass GetCharClass(Rune rune)
    {
        if (Rune.IsWhiteSpace(rune))
            return CharClass.Whitespace;

        if (Rune.IsLetterOrDigit(rune))
            return CharClass.AlphaNumeric;

        return CharClass.Other;
    }

    private enum CharClass : byte
    {
        Other,
        AlphaNumeric,
        Whitespace,
    }

    /// <summary>
    /// Helper type for the cursor blink animation.
    /// </summary>
    internal struct CursorBlink
    {
        /// <summary>
        /// The total length of the animation.
        /// </summary>
        private const float BlinkTime = 1.3f;

        // Because of the animation curves used, there is a plateau on either end of the animation.
        // When we reset the blink (i.e. when the user presses a button), we want this plateau to stay for a bit
        // longer, so we offset it by this start time in that case.
        private const float BlinkStartTime = BlinkTime * -0.2f;
        private const float HalfBlinkTime = BlinkTime / 2;

        public float Opacity;
        public float Timer;

        public void Reset()
        {
            Timer = BlinkTime + BlinkStartTime;
            UpdateOpacity();
        }

        public void FrameUpdate(FrameEventArgs args)
        {
            Timer += args.DeltaSeconds;
            UpdateOpacity();
        }

        private void UpdateOpacity()
        {
            if (Timer >= BlinkTime)
                Timer %= BlinkTime;

            Opacity = Timer < HalfBlinkTime
                ? 1 - Easings.InOutQuint(Timer * (1 / HalfBlinkTime))
                : Easings.InOutQuint((Timer - HalfBlinkTime) * (1 / HalfBlinkTime));
        }
    }
}
