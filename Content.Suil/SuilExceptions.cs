using System.Linq;

namespace Content.Suil;

public sealed class SuilTokenizeError(string context, int errorCp, int position, string? expected, params int[] expectedCp)
    : Exception
{
    /// <summary>
    /// The index of the code point the tokenizer stopped at.
    /// </summary>
    public int Position { get; } = position;

    public int Line { get; internal set; }

    public string? File { get; internal set; }

    public int Column { get; internal set; }

    public override string Message
    {
        get
        {
            var errStr = errorCp != -1 ? char.ConvertFromUtf32(errorCp) : "EOF";
            var expectedStr = expected ?? string.Join(null, expectedCp.Select(char.ConvertFromUtf32));
            return $"error while {context} ({SuilErrors.Where(File, Line, Column)}): code point '{errStr}' was received, but expected '{expectedStr}'";
        }
    }
}

public sealed class SuilParserError(string context, string message, SuilPosition position) : Exception
{
    public SuilPosition Position { get; } = position;

    /// <summary>
    /// The mistake is certain, there is no other way to read the text: a parser that tried one way must not go on with
    /// another and hide it.
    /// </summary>
    public bool Definite { get; init; }

    /// <summary>
    /// The file with the mistake, if its name was given to the parser.
    /// </summary>
    public string? File => Position.File;

    public override string Message => $"error while {context} ({SuilErrors.Where(Position)}): {message}";
}

public sealed class SuilExpansionError(string context, string message, SuilPosition position) : Exception
{
    public SuilPosition Position { get; } = position;

    /// <summary>
    /// The file with the mistake: for a mistake in an imported file, it is the imported one.
    /// </summary>
    public string? File => Position.File;

    public override string Message => $"error while {context} ({SuilErrors.Where(Position)}): {message}";
}

internal static class SuilErrors
{
    /// <summary>
    /// <c>/dir/file.suil:3:14</c>, or <c>3:14</c> if the file is not known.
    /// </summary>
    public static string Where(SuilPosition position) => Where(position.File, position.Line, position.Column);

    public static string Where(string? file, int line, int column)
        => file == null ? $"{line}:{column}" : $"{file}:{line}:{column}";
}
