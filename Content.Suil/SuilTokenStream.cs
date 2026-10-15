namespace Content.Suil;

/// <summary>
/// The SUIL equivalent of <see cref="Content.Client._RF.CSS.Streams.CssTokenStream"/>. The tokens keep
/// their positions, so errors can point at the place of the file.
/// </summary>
public record struct SuilTokenStream(SuilToken[] Tokens, string? File = null)
{
    private int _position = -1; // index of the last token to have been consumed

    /// <summary>
    /// Convenience constructor for feeding raw tokenizer output directly in.
    /// </summary>
    public SuilTokenStream(List<SuilToken> tokens, string? file = null) : this(tokens.ToArray(), file)
    {
    }

    /// <summary>
    /// The last token to have been consumed.
    /// </summary>
    public ISuilToken Current => _position >= 0 && _position < Tokens.Length
        ? Tokens[_position].Value
        : new EofToken();

    /// <summary>
    /// Consume the next input token.
    /// </summary>
    public ISuilToken ConsumeNext(int amount = 1)
    {
        _position += amount;
        return Current;
    }

    /// <summary>
    /// Returns the token <paramref name="offset"/> positions away
    /// from the current one, without moving the cursor.
    /// </summary>
    public ISuilToken PeekNext(int offset = 1)
    {
        var idx = _position + offset;
        return idx >= 0 && idx < Tokens.Length ? Tokens[idx].Value : new EofToken();
    }

    /// <summary>
    /// Consumes whitespace tokens if there are any next.
    /// </summary>
    public void SkipWhitespace()
    {
        while (PeekNext() is WhitespaceToken)
        {
            ConsumeNext();
        }
    }

    /// <summary>
    /// The position of the last token to have been consumed.
    /// </summary>
    public SuilPosition CurrentPosition => PositionAt(_position);

    /// <summary>
    /// The position of the token <paramref name="offset"/> positions away from the current one.
    /// </summary>
    public SuilPosition PeekPosition(int offset = 1) => PositionAt(_position + offset);

    private SuilPosition PositionAt(int index)
    {
        if (Tokens.Length == 0)
            return new SuilPosition(1, 1, File);

        if (index < 0)
            return new SuilPosition(Tokens[0].Line, Tokens[0].Column, File);

        if (index < Tokens.Length)
            return new SuilPosition(Tokens[index].Line, Tokens[index].Column, File);

        // After the last token: EOF
        var last = Tokens[Tokens.Length - 1];
        return new SuilPosition(last.Line, last.Column + 1, File);
    }

    /// <summary>
    /// Reconsume the current input token.
    /// </summary>
    public void Reconsume() => _position--;
}
