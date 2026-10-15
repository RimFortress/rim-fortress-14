namespace Content.Client._RF.Suil;

/// <summary>
/// A selector or a style that can't be built from the SUIL text, e.g. a tag of a control that doesn't exist.
/// </summary>
public sealed class SuilStyleError(string context, string message) : Exception
{
    public override string Message => $"error while {context}: {message}";
}
