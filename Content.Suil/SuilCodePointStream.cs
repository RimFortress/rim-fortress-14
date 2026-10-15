namespace Content.Suil;

/// <summary>
/// The stream of Unicode code points the tokenizer reads, with the SUIL tokenizer's interface:
/// consume, peek, reconsume. -1 is the end of the input.
/// </summary>
public struct SuilCodePointStream(int[] codePoints)
{
    private int _position = -1; // index of the last code point to have been consumed

    /// <summary>
    /// The index of the last code point to have been consumed (-1 before the first one).
    /// </summary>
    public int Position => _position;

    /// <summary>
    /// The last code point to have been consumed.
    /// </summary>
    public int Current => _position >= 0 && _position < codePoints.Length
        ? codePoints[_position]
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
    /// Consumes code points as long as the condition <paramref name="while"/> is true.
    /// </summary>
    /// <returns>All code points consumed.</returns>
    public int[] ConsumeWhile(Func<int, bool> @while)
    {
        var start = _position + 1;
        var end = start;

        while (@while(PeekAbsolute(end)))
        {
            end++;
        }

        var result = new int[end - start];
        Array.Copy(codePoints, start, result, 0, result.Length);
        _position = end - 1;
        return result;
    }

    /// <summary>
    /// Returns the code point <paramref name="offset"/> positions away
    /// from the current one, without moving the cursor.
    /// </summary>
    public int PeekNext(int offset = 1) => PeekAbsolute(_position + offset);

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
    /// of <paramref name="expected"/> without moving the cursor.
    /// </summary>
    public bool NextIs(params int[] expected)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            if (PeekNext(i + 1) != expected[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Reconsume the current input code point.
    /// </summary>
    public void Reconsume() => _position--;

    private int PeekAbsolute(int index) => index >= 0 && index < codePoints.Length ? codePoints[index] : -1;
}
