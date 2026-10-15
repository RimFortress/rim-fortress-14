using System.IO;
using System.Linq;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Suil;

/// <summary>
/// The files of SUIL from the resources: <c>/dir/file</c> of a <c>use</c> is the file <c>dir/file.suil</c> in the
/// root folder (<c>Resources/UI</c>).
/// </summary>
public sealed class ResourceSuilFileSource(IResourceCache resources, ResPath root) : ISuilFileSource
{
    /// <summary>
    /// The path of the file in the language: rooted in the folder of the files, with the extension.
    /// </summary>
    public string ToSuilPath(ResPath resourcePath)
        => "/" + resourcePath.RelativeTo(root).ToString().TrimStart('/');

    public ResPath ToResourcePath(string suilPath) => root / new ResPath(suilPath).ToRelativePath();

    public bool Has(string suilPath) => resources.ContentFileExists(ToResourcePath(suilPath));

    public SuilFile Load(string path)
    {
        var resource = ToResourcePath(path);

        if (!resources.ContentFileExists(resource))
            throw new FileNotFoundException($"SUIL file '{path}' is not found in {root}", resource.ToString());

        using var reader = new StreamReader(resources.ContentFileRead(resource), EncodingHelpers.UTF8);
        return SuilParser.ParseFile(reader.ReadToEnd(), path);
    }

    public IReadOnlyList<string> List(string directory)
    {
        var folder = root / new ResPath(directory).ToRelativePath();

        return resources.ContentFindFiles(folder)
            .Where(file => file.Extension == "suil")
            .Select(ToSuilPath)
            .ToList();
    }
}
