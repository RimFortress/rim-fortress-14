using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class FloatParser : CssValueParser<float>
{
    public override bool TryParse(ref CssValueStream stream, out float value)
    {
        value = 0;

        switch (stream.Peek())
        {
            case DimensionToken dimension:
                DebugTools.Assert(dimension.Unit == "px", "CSS units not supported by now");
                stream.ConsumeNext();
                value = (float)dimension.Value;
                return true;

            case NumberToken { Value: 0 }: // unitless zero is a valid length
                stream.ConsumeNext();
                return true;
            default:
                return false;
        }
    }
}
