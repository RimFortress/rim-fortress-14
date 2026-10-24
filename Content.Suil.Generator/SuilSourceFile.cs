namespace Content.Suil.Generator;

/// <summary>
/// An error of a .suil file that was found while it was read. It is reported once, with the files.
/// </summary>
internal sealed record SuilSourceError(string Message, SuilPosition Position);

/// <summary>
/// Thrown by <see cref="AdditionalFilesSource"/> for a file that has an error already reported.
/// </summary>
internal sealed class SuilSourceException(string path) : Exception($"The file '{path}' has errors");

/// <summary>
/// A .suil file of the project and its parsed tree. Two files are equal when their paths and texts are:
/// this lets the generator cache the parsing while the file is not edited.
/// </summary>
internal sealed class SuilSourceFile : IEquatable<SuilSourceFile>
{
    public string Path { get; }

    public string Text { get; }

    public SuilFile? Tree { get; }

    public SuilSourceError? Error { get; }

    private SuilSourceFile(string path, string text, SuilFile? tree, SuilSourceError? error)
    {
        Path = path;
        Text = text;
        Tree = tree;
        Error = error;
    }

    public static SuilSourceFile Parse(string path, string text)
    {
        try
        {
            return new SuilSourceFile(path, text, SuilParser.ParseFile(text, path), null);
        }
        catch (SuilParserError e)
        {
            return new SuilSourceFile(path, text, null, new SuilSourceError(e.Message, e.Position));
        }
        catch (SuilTokenizeError e)
        {
            return new SuilSourceFile(path, text, null,
                new SuilSourceError(e.Message, new SuilPosition(e.Line, e.Column, path)));
        }
    }

    public bool Equals(SuilSourceFile? other) => other != null && Path == other.Path && Text == other.Text;

    public override bool Equals(object? obj) => obj is SuilSourceFile other && Equals(other);

    public override int GetHashCode() => unchecked(Path.GetHashCode() * 397 ^ Text.GetHashCode());
}

/// <summary>
/// The files of <c>use</c> are the additional files of the project that are inside the resource root.
/// </summary>
internal sealed class AdditionalFilesSource(Dictionary<string, SuilSourceFile> files) : ISuilFileSource
{
    public bool Has(string path) => files.ContainsKey(path);

    public SuilFile Load(string path)
    {
        if (!files.TryGetValue(path, out var file))
            throw new FileNotFoundException($"'{path}' is not among the .suil files of the project", path);

        return file.Tree ?? throw new SuilSourceException(path);
    }

    public IReadOnlyList<string> List(string directory)
    {
        var prefix = directory.TrimEnd('/') + "/";
        return files.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
    }
}
