using System.Linq;

namespace Content.Client._RF.Suil;

public sealed class SuilTokenizeError(string context, int errorCp, int position, string? expected, params int[] expectedCp)
    : Exception
{
    /// <summary>
    /// The index of the code point the tokenizer stopped at.
    /// </summary>
    public int Position { get; } = position;

    public int Line { get; internal set; }

    public int Column { get; internal set; }

    public override string Message
    {
        get
        {
            var errStr = errorCp != -1 ? char.ConvertFromUtf32(errorCp) : "EOF";
            var expectedStr = expected ?? string.Join(null, expectedCp.Select(char.ConvertFromUtf32));
            return $"error while {context} ({Line}:{Column}): code point '{errStr}' was received, but expected '{expectedStr}'";
        }
    }
}

public sealed class SuilParserError(string context, string message, SuilPosition position) : Exception
{
    public SuilPosition Position { get; } = position;

    public override string Message => $"error while {context} ({Position.Line}:{Position.Column}): {message}";
}
