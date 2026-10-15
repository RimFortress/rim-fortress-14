using System.Diagnostics.CodeAnalysis;
using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil.Parsers;

[UsedImplicitly]
public sealed class StringParser : SuilValueParser<string>
{
    public override bool TryParse(ref SuilValueStream stream, [NotNullWhen(true)] out string? value)
    {
        switch (stream.ConsumeNext())
        {
            case SuilString str:
                value = str.Text;
                return true;
            case SuilIdent ident:
                value = ident.Value;
                return true;
            default:
                value = null;
                return false;
        }
    }
}
