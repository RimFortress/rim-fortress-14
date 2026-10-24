using System.Linq;
using JetBrains.Annotations;

namespace Content.Shared._RF.Utilities;

public static class RfCaseConversion
{
    /// <summary>
    /// Returns the input string with the first letter capitalized.
    /// </summary>
    [PublicAPI, Pure]
    public static string CapitalizeFirst(string input) =>
        string.IsNullOrEmpty(input) ? input : $"{char.ToUpper(input[0])}{input.Substring(1)}";

    /// <summary>
    /// Returns the input string with the first letter lowercase.
    /// </summary>
    [PublicAPI, Pure]
    public static string LowerFirst(string input) =>
        string.IsNullOrEmpty(input) ? input : $"{char.ToLower(input[0])}{input.Substring(1)}";

    /// <summary>
    /// Returns a string converted from kebab-case to PascalCase.
    /// </summary>
    [PublicAPI, Pure]
    public static string KebabToPascal(string input) => string.Concat(input.Split('-').Select(CapitalizeFirst));
}
