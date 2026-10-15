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
public sealed record SuilFile(List<ISuilTopLevelItem> Items, SuilElement? Root) : ISuilNode
{
    /// <summary>
    /// The <c>@Class =&gt; element</c> declarations of the file, in the order they are written.
    /// </summary>
    public IEnumerable<SuilViewDeclaration> Views => Items.OfType<SuilViewDeclaration>();
}

/// <summary>
/// <c>@Game.Ui.Picker =&gt; button { ... }</c>: the tree of a C# class. <paramref name="ClassName"/> is as it is written,
/// a full name or its tail (<c>Ui.Picker</c>, <c>Picker</c>) that names one class.
/// </summary>
public sealed record SuilViewDeclaration(string ClassName, SuilElement Root, SuilPosition Position) : ISuilTopLevelItem;

/// <summary>
/// <c>use "path";</c>
/// </summary>
public sealed record SuilUse(string Path, SuilPosition Position) : ISuilTopLevelItem;

/// <summary>
/// <c>let name = value;</c> or <c>let name = (params) { ... }</c>
/// </summary>
public sealed record SuilLet(string Name, ISuilLetValue Value, SuilPosition Position) : ISuilTopLevelItem;

/// <summary>
/// <c>let name = (a, b: 5) { ... }</c>. A parameter with a default value (<c>b: 5</c>) may be left out of the usage.
/// </summary>
public sealed record SuilTemplate(List<string> Parameters, List<ISuilElementNode> Body) : ISuilLetValue
{
    /// <summary>
    /// The default values of the parameters that have one, as written (they may have variables).
    /// </summary>
    public Dictionary<string, List<ISuilValue>> Defaults { get; init; } = new();
}

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
/// <c>@font-face { font-family: ...; src: url("..."); }</c>
/// </summary>
public sealed record SuilFontFace(List<SuilDeclaration> Descriptors, SuilPosition Position) : ISuilTopLevelItem;

/// <summary>
/// <c>name: value;</c>. <paramref name="Important"/> is <c>!important</c>: the declaration wins over the ones without it,
/// whatever their specificity.
/// </summary>
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
/// The name of a class or an id of an element: text with &lt;variable&gt; references, like <c>icon-&lt;name&gt;</c>.
/// After the expansion every name is literal.
/// </summary>
public sealed record SuilName(ImmutableArray<StringPart> Parts)
{
    public static SuilName Literal(string text) => new(ImmutableArray.Create(StringPart.Literal(text)));

    public static implicit operator SuilName(string text) => Literal(text);

    public bool IsLiteral => Parts.All(p => !p.IsVariable);

    /// <summary>
    /// The source form: variables are in angle brackets.
    /// </summary>
    public override string ToString() => string.Concat(Parts.Select(p => p.IsVariable ? $"<{p.Text}>" : p.Text));

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
    SuilPosition Position) : ISuilElementNode
{
    /// <summary>
    /// The handlers of the events of the element, <c>on-pressed =&gt; @OnSubmit</c> in the parentheses.
    /// </summary>
    public List<SuilEventHandler> Events { get; init; } = new();

    /// <summary>
    /// The positional arguments of a usage of a template, <c>btn ("Go", 12)</c>: the values before the named ones, in
    /// the order of the parameters of the template. Only a template takes them.
    /// </summary>
    public List<SuilArgument> Arguments { get; init; } = new();
}

/// <summary>
/// A positional argument: the values of one parameter.
/// </summary>
public sealed record SuilArgument(List<ISuilValue> Values, SuilPosition Position);

// ---- Event handlers ----

/// <summary>
/// <c>on-pressed =&gt; @OnSubmit</c> or <c>on-pressed =&gt; { ... }</c>: what the element does when its event is raised.
/// <paramref name="Event"/> is the name of the event as written, the name of the member in kebab-case.
/// </summary>
public sealed record SuilEventHandler(string Event, List<ISuilStatement> Statements, SuilPosition Position);

/// <summary>
/// A statement of a handler: a call of a method of the view, or a change of an element.
/// </summary>
public interface ISuilStatement
{
    SuilPosition Position { get; }
}

/// <summary>
/// <c>@OnSubmit</c>: calls the method of the class of the view. The method takes either nothing or the arguments of
/// the event.
/// </summary>
public sealed record SuilCallStatement(string Method, SuilPosition Position) : ISuilStatement;

/// <summary>
/// <c>#details (visible: true, add .open, remove :loading)</c> or <c>&amp;(set .active args.pressed)</c>: changes an
/// element. The parentheses hold the properties to assign (constants) and the changes of the classes.
/// </summary>
public sealed record SuilTargetStatement(SuilTarget Target, List<ISuilTargetItem> Items, SuilPosition Position)
    : ISuilStatement;

/// <summary>
/// An item in the parentheses of a <see cref="SuilTargetStatement"/>.
/// </summary>
public interface ISuilTargetItem
{
    SuilPosition Position { get; }
}

/// <summary>
/// <c>min-width +: 10</c>: changes a numeric property by a number, the way <c>+=</c> does. The operators are
/// <c>+: -: *: /:</c>.
/// </summary>
public sealed record SuilOperatorItem(string Property, char Operator, SuilNumNode Operand, SuilPosition Position)
    : ISuilTargetItem;

/// <summary>
/// <c>visible: true</c>: assigns a property of the element.
/// </summary>
public sealed record SuilPropertyItem(SuilProperty Property) : ISuilTargetItem
{
    public SuilPosition Position => Property.Position;
}

public enum SuilStyleOp
{
    /// <summary><c>add</c>: puts the classes and the pseudo-classes on the element.</summary>
    Add,

    /// <summary><c>remove</c>: takes them off.</summary>
    Remove,

    /// <summary><c>set</c>: puts them on or takes them off by a state: <c>set .open true</c>.</summary>
    Set,
}

/// <summary>
/// A class (<c>.open</c>) or a pseudo-class (<c>:loading</c>) in a change of the styles.
/// </summary>
public sealed record SuilStyleRef(bool IsPseudo, SuilName Name);

/// <summary>
/// The state of <c>set</c>: a bool expression, like <c>true</c>, <c>args.pressed</c> or <c>!#menu.visible</c>.
/// </summary>
public sealed record SuilStateValue(SuilBoolNode Expression);

// ---- Bool expressions ----

/// <summary>
/// A node of a bool expression: <c>!</c>, <c>&amp;&amp;</c> and <c>||</c> of <c>true</c>, <c>false</c>, the variables
/// (<c>&lt;flag&gt;</c>) and, in the handlers of the events only, the bool properties of the elements.
/// </summary>
public abstract record SuilBoolNode;

public sealed record SuilBoolConst(bool Value) : SuilBoolNode;

/// <summary>
/// <c>&lt;flag&gt;</c>: a variable that holds <c>true</c> or <c>false</c>. It is replaced when the file is expanded.
/// </summary>
public sealed record SuilBoolVariable(string Name, SuilPosition Position) : SuilBoolNode;

public sealed record SuilBoolNot(SuilBoolNode Operand) : SuilBoolNode;

public sealed record SuilBoolAnd(SuilBoolNode Left, SuilBoolNode Right) : SuilBoolNode;

public sealed record SuilBoolOr(SuilBoolNode Left, SuilBoolNode Right) : SuilBoolNode;

/// <summary>
/// <c>#menu/header.visible</c> or <c>&amp;.disabled</c>: a bool property of an element, read when the handler runs.
/// </summary>
/// <param name="Target">The element, the same way a statement names it.</param>
/// <param name="Property">The property as written (kebab-case).</param>
public sealed record SuilBoolReference(SuilTarget Target, string Property) : SuilBoolNode;

/// <summary>
/// <c>args.pressed</c>: a bool member of the arguments of the event.
/// </summary>
public sealed record SuilBoolArgs(string Member, SuilPosition Position) : SuilBoolNode;

// ---- Number expressions ----

/// <summary>
/// A node of a number expression, the <c>calc()</c> of a numeric field: <c>+ - * /</c>, parentheses, <c>min()</c>,
/// <c>max()</c>, <c>clamp()</c> and <c>abs()</c> of numbers, <c>px</c> lengths, the variables and, in the handlers of the
/// events only, the numeric properties of the elements.
/// </summary>
public abstract record SuilNumNode;

public sealed record SuilNumConst(double Value) : SuilNumNode;

/// <summary>
/// <c>&lt;size&gt;</c>: a variable that holds a number. It is replaced when the file is expanded.
/// </summary>
public sealed record SuilNumVariable(string Name, SuilPosition Position) : SuilNumNode;

public sealed record SuilNumNegate(SuilNumNode Operand) : SuilNumNode;

/// <param name="Operator">One of <c>+ - * /</c>.</param>
public sealed record SuilNumBinary(char Operator, SuilNumNode Left, SuilNumNode Right, SuilPosition Position) : SuilNumNode;

/// <param name="Name"><c>min</c>, <c>max</c>, <c>clamp</c> or <c>abs</c>.</param>
public sealed record SuilNumFunction(string Name, List<SuilNumNode> Arguments, SuilPosition Position) : SuilNumNode;

/// <summary>
/// <c>#panel.min-width</c> or <c>&amp;.min-height</c>: a numeric property of an element, read when the handler runs.
/// </summary>
public sealed record SuilNumReference(SuilTarget Target, string Property) : SuilNumNode;

/// <summary>
/// A number expression as the value of a property. When the file is expanded the variables are replaced and the constants
/// are folded to a length in <c>px</c>: what stays an expression depends on the elements, and is for the handlers.
/// </summary>
public sealed record SuilNumValue(SuilNumNode Root, SuilPosition Position) : ISuilValue;

/// <summary>
/// A bool expression as the value of a property. When the file is expanded the variables are replaced and the constants
/// are folded: what stays an expression is only what depends on the elements, which the handlers of the events can use.
/// </summary>
public sealed record SuilBoolValue(SuilBoolNode Root, SuilPosition Position) : ISuilValue;

/// <summary>
/// <c>add .open :loading</c>, <c>remove .closed</c>, <c>set .open true</c>.
/// </summary>
public sealed record SuilStyleItem(
    SuilStyleOp Op,
    List<SuilStyleRef> Refs,
    SuilStateValue? State,
    SuilPosition Position) : ISuilTargetItem;

/// <summary>
/// The element a statement works on: <c>&amp;</c> is the element the event was raised on, <c>#id</c> is the element
/// of the view with that id, and <c>#id/field/field</c> goes on through the public fields of a view inside it.
/// </summary>
/// <param name="IsSender"><c>&amp;</c>.</param>
/// <param name="Id">The id, for a target that isn't <c>&amp;</c>.</param>
/// <param name="Path">The fields after the id, as written (kebab-case).</param>
public sealed record SuilTarget(bool IsSender, SuilName? Id, List<string> Path, SuilPosition Position);

/// <summary>
/// A <c>let</c> inside a block of elements or a template: a constant or a template that the elements after it in the
/// same block can use. It is gone after the expansion.
/// </summary>
public sealed record SuilLocalLet(SuilLet Let) : ISuilElementNode;

/// <summary>
/// <c>%content%</c>
/// </summary>
public sealed record SuilContentSlot(string Name, SuilPosition Position) : ISuilElementNode;

/// <summary>
/// A property in the parentheses of an element. <paramref name="Value"/> is null for a flag (<c>disabled</c>).
/// A property is hard by default: the styles can't change what the file assigned. <paramref name="IsSoft"/> is the
/// <c>?</c> before the value (<c>(text: ?"x")</c>), which lets the styles override it.
/// </summary>
public sealed record SuilProperty(string Name, List<ISuilValue>? Value, bool IsSoft, SuilPosition Position);

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
