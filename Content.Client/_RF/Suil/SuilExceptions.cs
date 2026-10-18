using Content.Suil;

namespace Content.Client._RF.Suil;

/// <summary>
/// A selector or a style that can't be built from the SUIL text, e.g. a tag of a control that doesn't exist.
/// </summary>
/// <remarks>
/// <see cref="Position"/> is where the rule is written, the file included, when it is known.
/// </remarks>
public sealed class SuilStyleError(string context, string message, SuilPosition position = default, Exception? inner = null)
    : Exception(message, inner)
{
    public SuilPosition Position { get; } = position;

    /// <summary>
    /// The file with the rule.
    /// </summary>
    public string? File => Position.File;

    public string Problem => base.Message;

    public override string Message => Position.Line > 0
        ? $"error while {context} ({(Position.File == null ? $"{Position.Line}:{Position.Column}" : $"{Position.File}:{Position.Line}:{Position.Column}")}): {base.Message}"
        : $"error while {context}: {base.Message}";
}
