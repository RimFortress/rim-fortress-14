using System;
using System.Collections.Generic;

namespace Content.Suil;

/// <summary>
/// The paths of SUIL files are plain strings in the form of resource paths: rooted, separated by '/',
/// like <c>/UI/theme/buttons.suil</c>. <c>/UI/theme/*</c> is the folder form of <c>use</c>.
/// The engine side turns them into its own path type.
/// </summary>
public static class SuilPaths
{
    public const string Extension = ".suil";

    /// <summary>
    /// Removes empty and '.' segments and resolves '..' (a '..' above the root is dropped).
    /// </summary>
    public static string Clean(string path)
    {
        var rooted = path.StartsWith("/", StringComparison.Ordinal);
        var segments = new List<string>();

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
                continue;

            if (segment == "..")
            {
                if (segments.Count > 0 && segments[segments.Count - 1] != "..")
                    segments.RemoveAt(segments.Count - 1);
                else if (!rooted)
                    segments.Add(segment);

                continue;
            }

            segments.Add(segment);
        }

        return (rooted ? "/" : string.Empty) + string.Join("/", segments);
    }

    /// <summary>
    /// The directory a path is in: <c>/a/b.suil</c> is <c>/a</c>, <c>/b.suil</c> is <c>/</c>.
    /// </summary>
    public static string ParentOf(string path)
    {
        var index = path.LastIndexOf('/');
        return index <= 0 ? "/" : path.Substring(0, index);
    }

    /// <summary>
    /// Whether a path from <c>use</c> is a folder (<c>/dir/*</c>) and not a file.
    /// </summary>
    public static bool IsFolder(string path) => path.EndsWith("/*", StringComparison.Ordinal);

    /// <summary>
    /// The directory of a folder path: <c>/dir/*</c> is <c>/dir</c>, <c>/*</c> is <c>/</c>.
    /// </summary>
    public static string FolderDirectory(string folderPath)
        => folderPath == "/*" ? "/" : folderPath.Substring(0, folderPath.Length - 2);
}
