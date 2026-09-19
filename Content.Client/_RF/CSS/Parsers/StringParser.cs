using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class StringParser : CssValueParser<string>
{
    public override bool TryParse(ref CssValueStream stream, [NotNullWhen(true)] out string? value)
    {
        switch (stream.ConsumeNext())
        {
            case StringToken str:
                value = str.Value;
                return true;
            case IdentToken ident:
                value = ident.Value;
                return true;
            default:
                value = null;
                return false;
        }
    }
}
