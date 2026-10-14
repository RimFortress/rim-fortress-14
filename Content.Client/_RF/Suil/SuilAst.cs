using System.Collections.Immutable;

namespace Content.Client._RF.Suil;

/// <summary>
/// The position of the first token of a node (both 1-based).
/// </summary>
public readonly record struct SuilPosition(int Line, int Column);

/// <summary>
/// A basic interface for any SUIL parsing result.
/// </summary>
public interface ISuilNode;

/// <summary>
/// What may be at the top level of a file before the root element: <see cref="SuilUse"/>,
/// <see cref="SuilLet"/> and <see cref="SuilRule"/>.
/// </summary>
public interface ISuilTopLevelItem : ISuilNode;

/// <summary>
/// What may be in a block: <see cref="SuilElement"/> and <see cref="SuilContentSlot"/>.
/// </summary>
public interface ISuilElementNode : ISuilNode;

/// <summary>
/// The right-hand side of <c>let</c>: a <see cref="SuilTemplate"/> or a <see cref="SuilValueList"/>.
/// </summary>
public interface ISuilLetValue : ISuilNode;

public interface ISuilValue : ISuilNode;

/// <summary>
/// A parsed file. <paramref name="Root"/> is null for a library file that is only imported with <c>use</c>.
/// </summary>
public sealed record SuilFile(List<ISuilTopLevelItem> Items, SuilElement? Root) : ISuilNode;

/// <summary>
/// <c>use "path";</c>
/// </summary>
public sealed record SuilUse(string Path, SuilPosition Position) : ISuilTopLevelItem;

/// <summary>
/// <c>let name = value;</c> or <c>let name = (params) { ... }</c>
/// </summary>
public sealed record SuilLet(string Name, ISuilLetValue Value, SuilPosition Position) : ISuilTopLevelItem;

public sealed record SuilTemplate(List<string> Parameters, List<ISuilElementNode> Body) : ISuilLetValue;

public sealed record SuilValueList(List<ISuilValue> Values) : ISuilLetValue;

// ---- Styles ----

/// <summary>
/// <c>selectors { declarations }</c>
/// </summary>
public sealed record SuilRule(
    List<SuilSelector> Selectors,
    List<SuilDeclaration> Declarations,
    SuilPosition Position) : ISuilTopLevelItem;

public sealed record SuilDeclaration(string Name, List<ISuilValue> Value, SuilPosition Position);

/// <summary>
/// Compound selectors joined by combinators, left to right. The combinator of the first step is
/// <see cref="SuilCombinator.None"/>.
/// </summary>
public sealed record SuilSelector(List<SuilSelectorStep> Steps);

public enum SuilCombinator
{
    None,
    Descendant,
    Child,
}

public sealed record SuilSelectorStep(SuilCombinator Combinator, SuilCompoundSelector Compound);

public sealed record SuilCompoundSelector(
    string? Type,
    string? Id,
    List<string> Classes,
    List<string> PseudoClasses);

// ---- Elements ----

public enum SuilAccess
{
    Private,
    Public,
    Protected,
}

/// <summary>
/// An element of the tree or a usage of a template (the parser doesn't distinguish them).
/// <paramref name="Tag"/> is null for <c>_</c>, an element without a type.
/// </summary>
public sealed record SuilElement(
    SuilAccess Access,
    string? Tag,
    string? Id,
    List<string> Classes,
    List<SuilProperty> Properties,
    List<ISuilElementNode> Children,
    SuilPosition Position) : ISuilElementNode;

/// <summary>
/// <c>%content%</c>
/// </summary>
public sealed record SuilContentSlot(string Name, SuilPosition Position) : ISuilElementNode;

/// <summary>
/// A property in the parentheses of an element. <paramref name="Value"/> is null for a flag (<c>disabled</c>).
/// <paramref name="IsHard"/> is the <c>!</c> after the value: styles can't override it.
/// </summary>
public sealed record SuilProperty(string Name, List<ISuilValue>? Value, bool IsHard, SuilPosition Position);

// ---- Values ----

public sealed record SuilNumber(double Value, bool IsInteger) : ISuilValue;

public sealed record SuilPercentage(double Value) : ISuilValue;

public sealed record SuilDimension(double Value, bool IsInteger, string Unit) : ISuilValue;

/// <summary>
/// <paramref name="Hex"/> is validated and kept without <c>#</c>: 3, 4, 6 or 8 hex digits.
/// </summary>
public sealed record SuilColor(string Hex) : ISuilValue;

public sealed record SuilIdent(string Value) : ISuilValue;

public sealed record SuilString(ImmutableArray<StringPart> Parts) : ISuilValue
{
    public bool Equals(SuilString? other) => other != null && StringPart.SequenceEqual(Parts, other.Parts);

    public override int GetHashCode() => StringPart.Hash(Parts);
}

public sealed record SuilVariable(string Name) : ISuilValue;

public sealed record SuilLocalization(ImmutableArray<StringPart> Key, List<SuilNamedArgument> Arguments) : ISuilValue;

public sealed record SuilNamedArgument(string Name, List<ISuilValue> Value);

public sealed record SuilFunction(string Name, List<List<ISuilValue>> Arguments) : ISuilValue;
