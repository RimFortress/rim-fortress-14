using System.Linq;

namespace Content.Client._RF.Css;

/// <summary>
/// The token/component-value-level equivalent of CodePointStream. Per §5.2, the
/// parser algorithms don't care whether they're fed raw tokens or a list that
/// already contains folded functions/blocks — both are just ICssComponentValue.
/// </summary>
public record struct CssTokenStream(ICssComponentValue[] Tokens)
{
    private int _position = -1; // index of the last token to have been consumed

    /// <summary>
    /// Convenience constructor for feeding raw tokenizer output directly in.
    /// </summary>
    public CssTokenStream(List<ICssToken> tokens) : this(tokens.Cast<ICssComponentValue>().ToArray())
    {

    }

    /// <summary>
    /// The last token to have been consumed.
    /// </summary>
    public ICssComponentValue Current => _position >= 0 && _position < Tokens.Length
        ? Tokens[_position]
        : new EofToken();

    /// <summary>
    /// Consume the next input token.
    /// </summary>
    public ICssComponentValue ConsumeNext(int amount = 1)
    {
        _position += amount;
        return Current;
    }

    /// <summary>
    /// Returns the token <paramref name="offset"/> positions away
    /// from the current one, without moving the cursor.
    /// </summary>
    public ICssComponentValue PeekNext(int offset = 1)
    {
        var idx = _position + offset;
        return idx < Tokens.Length ? Tokens[idx] : new EofToken();
    }

    /// <summary>
    /// Returns an array of the next <paramref name="amount"/> elements to the stream.
    /// </summary>
    public ICssComponentValue[] PeekNextAmount(int amount)
    {
        var array = new ICssComponentValue[amount];

        for (var i = 0; i < amount; i++)
        {
            array[i] = PeekNext(i + 1);
        }

        return array;
    }

    /// <summary>
    /// Checks the next N tokens for a match with the sequence
    /// of <paramref name="tokens"/> without moving the cursor.
    /// </summary>
    public bool NextIs(params ICssComponentValue[] tokens)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (PeekNext(i + 1) != tokens[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Reconsume the current input token.
    /// </summary>
    public void Reconsume() => _position--;
}
