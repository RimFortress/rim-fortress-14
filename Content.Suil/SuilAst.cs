using System.Collections.Immutable;
using System.Linq;

namespace Content.Suil;

/// <summary>
/// The position of the first token of a node (both 1-based) and the file it is in, if the file has a name.
/// </summary>
public readonly record struct SuilPosition(int Line, int Column, string? File = null);

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

/// <summary>
/// <c>name: value;</c>. <paramref name="Important"/> is <c>!important</c>: the declaration wins over the ones without it,
/// whatever their specificity.
/// </summary>
/// <summary>
/// <c>@font-face { font-family: ...; src: url("..."); }</c>
/// </summary>
public sealed record SuilFontFace(List<SuilDeclaration> Descriptors, SuilPosition Position) : ISuilTopLevelItem;

public sealed record SuilDeclaration(string Name, List<ISuilValue> Value, SuilPosition Position, bool Important = false);

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
    List<string> PseudoClasses)
{
    /// <summary>
    /// <c>*</c>: any element.
    /// </summary>
    public bool Universal { get; init; }

    /// <summary>
    /// <c>[property=value]</c>
    /// </summary>
    public List<SuilAttributeSelector> Attributes { get; init; } = new();

    /// <summary>
    /// <c>:nth-child(2n+1)</c>, <c>:not(.a, .b)</c> and the like.
    /// </summary>
    public List<SuilPseudoFunction> Functions { get; init; } = new();
}

public enum SuilAttributeOperator
{
    /// <summary><c>[name]</c></summary>
    Exists,

    /// <summary><c>=</c></summary>
    Equals,

    /// <summary><c>*=</c></summary>
    Contains,

    /// <summary><c>^=</c></summary>
    StartsWith,

    /// <summary><c>$=</c></summary>
    EndsWith,

    /// <summary><c>~=</c></summary>
    WhitespaceList,

    /// <summary><c>|=</c></summary>
    HyphenList,
}

/// <summary>
/// <c>[name op value i]</c>, matched against the property of a control. <paramref name="Value"/> is empty for
/// <see cref="SuilAttributeOperator.Exists"/>.
/// </summary>
public sealed record SuilAttributeSelector(
    string Name,
    SuilAttributeOperator Operator,
    List<ISuilValue> Value,
    bool IgnoreCase);

/// <summary>
/// A pseudo-class with arguments: <paramref name="Anb"/> for <c>nth-child</c>, <c>nth-last-child</c>, <c>nth-of-type</c>
/// and <c>nth-last-of-type</c>, <paramref name="Selectors"/> for <c>not</c> and <c>is</c>.
/// </summary>
public sealed record SuilPseudoFunction(string Name, SuilAnb? Anb, List<SuilSelector>? Selectors);

// ---- Elements ----

/// <summary>
/// The name of a class or an id of an element: text with `variable` references, like <c>icon-`name`</c>.
/// After the expansion every name is literal.
/// </summary>
public sealed record SuilName(ImmutableArray<StringPart> Parts)
{
    public static SuilName Literal(string text) => new(ImmutableArray.Create(StringPart.Literal(text)));

    public static implicit operator SuilName(string text) => Literal(text);

    public bool IsLiteral => Parts.All(p => !p.IsVariable);

    /// <summary>
    /// The source form: variables are in backticks.
    /// </summary>
    public override string ToString() => string.Concat(Parts.Select(p => p.IsVariable ? $"`{p.Text}`" : p.Text));

    public bool Equals(SuilName? other) => other != null && StringPart.SequenceEqual(Parts, other.Parts);

    public override int GetHashCode() => StringPart.Hash(Parts);
}

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
    SuilName? Id,
    List<SuilName> Classes,
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
    /// <summary>
    /// The text of the string. A string with variables is expanded before anything reads it.
    /// </summary>
    public string Text => string.Concat(Parts.Select(p => p.Text));

    public bool Equals(SuilString? other) => other != null && StringPart.SequenceEqual(Parts, other.Parts);

    public override int GetHashCode() => StringPart.Hash(Parts);
}

public sealed record SuilVariable(string Name) : ISuilValue;

public sealed record SuilLocalization(ImmutableArray<StringPart> Key, List<SuilNamedArgument> Arguments) : ISuilValue;

public sealed record SuilNamedArgument(string Name, List<ISuilValue> Value);

/// <summary>
/// <c>name(arguments)</c>. A parenthesized group <c>(1px + 2px)</c> is a function with an empty name.
/// </summary>
public sealed record SuilFunction(string Name, List<List<ISuilValue>> Arguments) : ISuilValue;

/// <summary>
/// A comma between values of a list (<c>font-family: "A", B</c>). It only appears in the declarations of the rules and
/// in the values read as a stream (<see cref="SuilValueStream"/>), where the arguments of a function are separated by it.
/// </summary>
public sealed record SuilComma : ISuilValue;

/// <summary>
/// A lone operator in a value: <c>/</c> (<c>background: a / b</c>, <c>12px/1.5</c>) or one of <c>+ - * /</c> in
/// <c>calc()</c>.
/// </summary>
public sealed record SuilDelim(int CodePoint) : ISuilValue;
