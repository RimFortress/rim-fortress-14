using Content.Suil;

namespace Content.Suil.Tests;

/// <summary>
/// A mistake in a file says which file it is in, not only the line and the column.
/// </summary>
[TestFixture]
public sealed class SuilErrorLocationTests
{
    private sealed class Source(Dictionary<string, string> files) : ISuilFileSource
    {
        public SuilFile Load(string path)
            => SuilParser.ParseFile(files.TryGetValue(path, out var text) ? text : throw new FileNotFoundException(path), path);

        public IReadOnlyList<string> List(string directory) => Array.Empty<string>();
    }

    [Test]
    public void A_Tokenizer_Error_Names_The_File()
    {
        var error = Assert.Throws<SuilTokenizeError>(() => SuilTokenizer.Tokenize("box\n$.x", "/dir/a.suil"))!;

        Assert.That(error.File, Is.EqualTo("/dir/a.suil"));
        Assert.That(error.Message, Does.Contain("/dir/a.suil:2:"));
    }

    [Test]
    public void A_Parser_Error_Names_The_File()
    {
        var error = Assert.Throws<SuilParserError>(() => SuilParser.ParseFile("box\n@A as => box", "/dir/p.suil"))!;

        Assert.That(error.File, Is.EqualTo("/dir/p.suil"));
        Assert.That(error.Message, Does.Contain("(/dir/p.suil:2:"));
    }

    [Test]
    public void Without_A_File_The_Message_Has_The_Position_Only()
    {
        var error = Assert.Throws<SuilParserError>(() => SuilParser.ParseFile("@A as => box"))!;

        Assert.That(error.File, Is.Null);
        Assert.That(error.Message, Does.Contain("(1:"));
    }

    [Test]
    public void An_Expansion_Error_Names_The_File()
    {
        var error = Assert.Throws<SuilExpansionError>(() => SuilExpander.Expand("box (min-width: <x>)", fileName: "/dir/m.suil"))!;

        Assert.That(error.File, Is.EqualTo("/dir/m.suil"));
        Assert.That(error.Message, Does.Contain("/dir/m.suil:1:"));
    }

    [Test]
    public void An_Error_In_An_Imported_File_Names_The_Imported_File()
    {
        var source = new Source(new Dictionary<string, string>
        {
            ["/lib.suil"] = "let a = <missing>;",
        });

        var error = Assert.Throws<SuilExpansionError>(() => SuilExpander.Expand("use \"/lib\";\nbox", source, fileName: "/main.suil"))!;

        Assert.That(error.File, Is.EqualTo("/lib.suil"));
    }

    [Test]
    public void A_Missing_Import_Is_An_Expansion_Error_At_The_Use()
    {
        var source = new Source(new Dictionary<string, string>());

        var error = Assert.Throws<SuilExpansionError>(() => SuilExpander.Expand("use \"/nothing\";\nbox", source, fileName: "/main.suil"))!;

        Assert.That(error.File, Is.EqualTo("/main.suil"));
        Assert.That(error.Message, Does.Contain("/nothing.suil").And.Contain("not found"));
    }
}
