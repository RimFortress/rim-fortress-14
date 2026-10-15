using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil.Parsers;

[UsedImplicitly]
public sealed class BoolParser : SuilValueParser<bool>
{
    public override bool TryParse(ref SuilValueStream stream, out bool value)
    {
        value = false;

        if (stream.ConsumeNext() is not SuilIdent ident
            || !bool.TryParse(ident.Value, out var @bool))
            return false;

        value = @bool;
        return true;
    }
}
