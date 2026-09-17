using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class BoolParser : CssValueParser<bool>
{
    public override bool TryParse(ref CssValueStream stream, out bool value)
    {
        value = false;

        if (stream.ConsumeNext() is not IdentToken ident
            || !bool.TryParse(ident.Value, out var @bool))
            return false;

        value = @bool;
        return true;
    }
}
