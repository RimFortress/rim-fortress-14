namespace Content.Client._RF.Css;

public record struct CodePointStream(int[] CodePoints)
{
    private int _position = -1; // index of the last code point to have been consumed

    /// <summary>
    /// The last code point to have been consumed.
    /// </summary>
    public int Current => _position >= 0 && _position < CodePoints.Length
        ? CodePoints[_position]
        : -1; // EOF sentinel

    /// <summary>
    /// Consume the next input code point.
    /// </summary>
    public int ConsumeNext(int amount = 1)
    {
        _position += amount;
        return Current;
    }

    /// <summary>
    /// Consumes code points until they meet the condition <paramref name="until"/>
    /// </summary>
    /// <returns>All code points consumed.</returns>
    public int[] ConsumeUntil(Func<int, bool> until)
    {
        var output = Array.Empty<int>();

        while (true)
        {
            if (until(ConsumeNext()))
                return output;

            Array.Resize(ref output, output.Length + 1);
            output[^1] = Current;
        }
    }

    /// <summary>
    /// Consumes code points as long as the condition <paramref name="while"/> is true.
    /// </summary>
    /// <param name="while"></param>
    /// <returns>All code points consumed.</returns>
    public int[] ConsumeWhile(Func<int, bool> @while)
    {
        var output = Array.Empty<int>();

        while (true)
        {
            if (!@while(ConsumeNext()))
                return output;

            Array.Resize(ref output, output.Length + 1);
            output[^1] = Current;
        }
    }

    /// <summary>
    /// Returns the code point <paramref name="offset"/> positions away
    /// from the current one, without moving the cursor.
    /// </summary>
    public int PeekNext(int offset = 1)
    {
        var idx = _position + offset;
        return idx < CodePoints.Length ? CodePoints[idx] : -1;
    }

    /// <summary>
    /// Returns an array of the next <paramref name="amount"/> elements to the stream.
    /// </summary>
    public int[] PeekNextAmount(int amount)
    {
        var array = new int[amount];

        for (var i = 0; i < amount; i++)
        {
            array[i] = PeekNext(i + 1);
        }

        return array;
    }

    /// <summary>
    /// Checks the next N code points for a match with the sequence
    /// of <paramref name="codePoints"/> without moving the cursor.
    /// </summary>
    public bool NextIs(params int[] codePoints)
    {
        for (var i = 0; i < codePoints.Length; i++)
        {
            if (PeekNext(i + 1) != codePoints[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Reconsume the current input code point.
    /// </summary>
    public void Reconsume() => _position--;
}
