using System.Linq;

namespace Content.Client._RF.Css;

public sealed class CssTokenizeError(string context, int errorCp, string? expected, params int[] expectedCp) : Exception
{
    public override string Message
    {
        get
        {
            var errStr = errorCp != -1 ? char.ConvertFromUtf32(errorCp) : "EOF";
            var expectedStr = expected ?? string.Join(null, expectedCp.Select(char.ConvertFromUtf32));
            return $"error while {context}: code point '{errStr}' was received, but expected '{expectedStr}'";
        }
    }
}

public sealed class CssParserError(string context, string message) : Exception
{
    public override string Message => $"error while {context}: {message}";
}
