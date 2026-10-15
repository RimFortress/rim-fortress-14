using Content.Suil;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// Helpers for the tests that write the tree of a view as a bare element: the tests name their class in a declaration.
/// </summary>
internal static class SuilTestText
{
    /// <summary>
    /// Puts <c>@name =&gt;</c> before the root element of the text, which is the last item of a file.
    /// </summary>
    public static string AsView(string text, string name = "Test")
    {
        SuilElement? root;
        try
        {
            root = SuilParser.ParseFile(text).Root;
        }
        catch (Exception e) when (e is SuilParserError or SuilTokenizeError)
        {
            // a broken text is the element of the view, so the reload still meets the break
            return "@" + name + " => " + text;
        }

        if (root == null)
            return text;

        var offset = 0;
        for (var line = 1; line < root.Position.Line; line++)
        {
            offset = text.IndexOf('\n', offset) + 1;
        }

        offset += root.Position.Column - 1;
        return text.Substring(0, offset) + "@" + name + " => " + text.Substring(offset);
    }
}
