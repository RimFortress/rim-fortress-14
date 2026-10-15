using System.IO;
using System.Linq;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Suil;

/// <summary>
/// What the files of the language look like after a change on the disk, see <see cref="ResourceSuilFileSource.FindChanges"/>.
/// </summary>
/// <param name="Changed">The files that were read before and have other text now.</param>
/// <param name="Added">The files that are in the folder now and weren't when it was scanned.</param>
/// <param name="Removed">The files that were read before and are gone.</param>
public sealed record SuilFileChanges(IReadOnlyList<string> Changed, IReadOnlyList<string> Added, IReadOnlyList<string> Removed)
{
    public bool Any => Changed.Count > 0 || Added.Count > 0 || Removed.Count > 0;
}

/// <summary>
/// The globals of every file: the list of <c>/global-usings.suil</c> and the files that list brings in, which don't get
/// the globals themselves.
/// </summary>
public sealed record SuilGlobals(IReadOnlyList<string> Paths, HashSet<string> Files)
{
    public static readonly SuilGlobals None = new(Array.Empty<string>(), new HashSet<string>());
}

/// <summary>
/// The files of SUIL from the resources: <c>/dir/file</c> of a <c>use</c> is the file <c>dir/file.suil</c> in the
/// root folder (<c>Resources/UI</c>). It remembers what it has read, so a later <see cref="FindChanges"/> can tell
/// which files were edited: this is how the hot reload works without a file watcher, which the sandbox doesn't allow.
/// </summary>
public sealed class ResourceSuilFileSource(IResourceCache resources, ResPath root) : ISuilFileSource
{
    // path -> hash of the text that was read. The hash is only compared inside the process.
    private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _scanned = new(StringComparer.Ordinal);

    /// <summary>
    /// The path of the file in the language: rooted in the folder of the files, with the extension.
    /// </summary>
    public string ToSuilPath(ResPath resourcePath)
        => "/" + resourcePath.RelativeTo(root).ToString().TrimStart('/');

    public ResPath ToResourcePath(string suilPath) => root / new ResPath(suilPath).ToRelativePath();

    public bool Has(string suilPath) => resources.ContentFileExists(ToResourcePath(suilPath));

    public SuilFile Load(string path)
    {
        var text = ReadText(path);
        _seen[path] = text.GetHashCode();
        return SuilParser.ParseFile(text, path);
    }

    public IReadOnlyList<string> List(string directory)
    {
        var folder = root / new ResPath(directory).ToRelativePath();

        return resources.ContentFindFiles(folder)
            .Where(file => file.Extension == "suil")
            .Select(ToSuilPath)
            .ToList();
    }

    /// <summary>
    /// Forgets what was read: the next reads of the files are the ones that later changes are compared with.
    /// </summary>
    public void Forget()
    {
        _seen.Clear();
        _scanned.Clear();
    }

    /// <summary>
    /// Remembers which files are in the folder now, to see the new ones later.
    /// </summary>
    public void MarkScanned()
    {
        _scanned.Clear();

        foreach (var path in List("/"))
        {
            _scanned.Add(path);
        }
    }

    /// <summary>
    /// Reads the files again and compares them with what was read last time.
    /// </summary>
    public SuilFileChanges FindChanges()
    {
        var changed = new List<string>();
        var removed = new List<string>();

        foreach (var (path, hash) in _seen)
        {
            if (!Has(path))
                removed.Add(path);
            else if (ReadText(path).GetHashCode() != hash)
                changed.Add(path);
        }

        var added = List("/").Where(path => !_scanned.Contains(path) && !_seen.ContainsKey(path)).ToList();

        changed.Sort(StringComparer.Ordinal);
        removed.Sort(StringComparer.Ordinal);
        added.Sort(StringComparer.Ordinal);
        return new SuilFileChanges(changed, added, removed);
    }

    /// <summary>
    /// Reads <c>/global-usings.suil</c> and finds out which files it brings in.
    /// </summary>
    public SuilGlobals LoadGlobals()
    {
        if (!Has(SuilGlobalUsings.DefaultPath))
            return SuilGlobals.None;

        var paths = SuilGlobalUsings.FromFile(Load(SuilGlobalUsings.DefaultPath));

        // The files that the global ones bring don't get the globals themselves: found by expanding the list alone
        // and seeing what was read.
        var recorder = new SuilRecordingSource(this);
        var list = new SuilFile(paths.Select(path => (ISuilTopLevelItem) new SuilUse(path, default)).ToList(), null);
        SuilExpander.Expand(list, recorder);

        return new SuilGlobals(paths, recorder.Loaded);
    }

    private string ReadText(string path)
    {
        var resource = ToResourcePath(path);

        if (!resources.ContentFileExists(resource))
            throw new FileNotFoundException($"SUIL file '{path}' is not found in {root}", resource.ToString());

        using var reader = new StreamReader(resources.ContentFileRead(resource), EncodingHelpers.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// A source that remembers the files that were read through it: what a file depends on.
/// </summary>
public sealed class SuilRecordingSource(ISuilFileSource inner) : ISuilFileSource
{
    public HashSet<string> Loaded { get; } = new(StringComparer.Ordinal);

    public SuilFile Load(string path)
    {
        Loaded.Add(path);
        return inner.Load(path);
    }

    public IReadOnlyList<string> List(string directory) => inner.List(directory);
}
