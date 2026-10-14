using Robust.Shared.Utility;

namespace Content.Client._RF.Suil;

/// <summary>
/// Where the files named in <c>use</c> come from. The paths it gets are already validated and cleaned:
/// they are rooted, have no '..', and a file path ends with <c>.suil</c>.
/// </summary>
public interface ISuilFileSource
{
    /// <summary>
    /// Reads and parses a file. It should throw if the file doesn't exist.
    /// </summary>
    SuilFile Load(ResPath path);

    /// <summary>
    /// The <c>.suil</c> files of a directory, for <c>use "/dir/*"</c>. The expander keeps only the files that are
    /// directly in the directory, so a source that returns nested ones as well does no harm.
    /// An absent directory is an empty list.
    /// </summary>
    IReadOnlyList<ResPath> List(ResPath directory);
}
