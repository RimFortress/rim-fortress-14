namespace Content.Client._RF.CSS;

public record struct CssValueStream(List<ICssComponentValue> Items)
{
    private int _position;

    public bool AtEnd => Peek() is null;

    public ICssComponentValue? Peek()
    {
        var i = _position;
        while (i < Items.Count && Items[i] is WhitespaceToken)
        {
            i++;
        }

        return i < Items.Count ? Items[i] : null;
    }

    public ICssComponentValue? ConsumeNext()
    {
        while (_position < Items.Count && Items[_position] is WhitespaceToken)
        {
            _position++;
        }

        return _position < Items.Count ? Items[_position++] : null;
    }

    public bool TryConsumeComma()
    {
        if (Peek() is not CommaToken)
            return false;

        ConsumeNext();
        return true;
    }

    /// <summary>
    /// Snapshot the current position for backtracking.
    /// </summary>
    public int Mark() => _position;

    /// <summary>
    /// Restore a previously taken snapshot.
    /// </summary>
    public void Reset(int mark) => _position = mark;
}
