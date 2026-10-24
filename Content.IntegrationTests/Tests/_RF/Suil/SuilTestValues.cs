using System.Collections.Generic;
using Content.Suil;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The values of a declaration written as text, for the tests of the parsers.
/// </summary>
public static class SuilTestValues
{
    public static List<ISuilValue> Values(string text)
    {
        var file = SuilParser.ParseFile($"x {{ p: {text}; }}");
        return ((SuilRule) file.Items[0]).Declarations[0].Value;
    }

    public static SuilValueStream Stream(string text) => new(Values(text));
}
