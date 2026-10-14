using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Client._RF.Suil;

/// <summary>
/// The SUIL counterpart of C# <c>global using</c>: a file that lists the files imported into every other file.
/// It may have nothing but <c>use</c>:
/// <code>
/// use "/UI/theme/variables.suil";
/// use "/UI/theme/buttons.suil";
/// </code>
/// </summary>
public static class SuilGlobalUsings
{
    /// <summary>
    /// Where the list is looked for.
    /// </summary>
    public static readonly ResPath DefaultPath = new("/UI/global_usings.suil");

    /// <summary>
    /// Takes the paths out of a parsed global usings file, in the order they are written.
    /// </summary>
    /// <exception cref="SuilExpansionError">If the file has anything but <c>use</c>, or a path is invalid.</exception>
    [PublicAPI, Pure]
    public static List<ResPath> FromFile(SuilFile file)
    {
        const string context = "reading global usings";

        if (file.Root is { } root)
            throw new SuilExpansionError(context, "a global usings file can't have a root element", root.Position);

        var paths = new List<ResPath>();

        foreach (var item in file.Items)
        {
            if (item is not SuilUse use)
            {
                var position = item switch
                {
                    SuilLet let => let.Position,
                    SuilRule rule => rule.Position,
                    _ => default,
                };

                throw new SuilExpansionError(context, "a global usings file can only have 'use'", position);
            }

            var path = SuilExpander.ParseUsePath(use.Path, context, use.Position);

            if (!paths.Contains(path))
                paths.Add(path);
        }

        return paths;
    }
}
