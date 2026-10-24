namespace Content.Suil;

/// <summary>
/// The values of a declaration (or of the arguments of a function) as a stream that the value parsers read, the SUIL
/// counterpart of <c>SuilValueStream</c>. A comma is the value <see cref="SuilComma"/>; the arguments of a function
/// are separated by it when a function is read as a stream (<see cref="OfArguments"/>).
/// </summary>
public struct SuilValueStream(IReadOnlyList<ISuilValue> items)
{
    private int _position;

    public IReadOnlyList<ISuilValue> Items { get; } = items;

    /// <summary>
    /// The stream of the values inside a function: its arguments with <see cref="SuilComma"/> between them.
    /// </summary>
    public static SuilValueStream OfArguments(SuilFunction function) => new(Flatten(function.Arguments));

    /// <summary>
    /// The stream of the values inside a function: its arguments with <see cref="SuilComma"/> between them.
    /// </summary>
    public static IReadOnlyList<ISuilValue> Flatten(IReadOnlyList<List<ISuilValue>> arguments)
    {
        var result = new List<ISuilValue>();

        for (var i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
                result.Add(new SuilComma());

            result.AddRange(arguments[i]);
        }

        return result;
    }

    public readonly bool AtEnd => _position >= Items.Count;

    /// <summary>
    /// The next value, if there is one; nothing is consumed.
    /// </summary>
    public readonly ISuilValue? Peek() => _position < Items.Count ? Items[_position] : null;

    public ISuilValue? ConsumeNext() => _position < Items.Count ? Items[_position++] : null;

    public bool TryConsumeComma()
    {
        if (Peek() is not SuilComma)
            return false;

        _position++;
        return true;
    }

    /// <summary>
    /// A snapshot of the position, for backtracking.
    /// </summary>
    public readonly int Mark() => _position;

    /// <summary>
    /// Restores a position taken with <see cref="Mark"/>.
    /// </summary>
    public void Reset(int mark) => _position = mark;
}
