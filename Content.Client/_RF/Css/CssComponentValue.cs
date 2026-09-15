namespace Content.Client._RF.Css;

/// <summary>
/// Anything that can live in a rule's prelude or a declaration's value:
/// a preserved token, a function, or a simple block (§5.2).
/// </summary>
public interface ICssComponentValue;

/// <summary>
/// A function invocation, e.g. rgb(255 0 0) — name is the function-token's
/// value (without the trailing '('), Value is everything up to the matching ')'.
/// </summary>
public sealed class CssFunction(string name, List<ICssComponentValue> value) : ICssComponentValue
{
    public string Name { get; } = name;
    public List<ICssComponentValue> Value { get; } = value;
}

/// <summary>
/// A {}/()/[] block. AssociatedToken is the opening token that started it —
/// used to know what "ending token" to look for and what kind of block this is.
/// </summary>
public sealed class CssSimpleBlock(ICssToken associatedToken, List<ICssComponentValue> value) : ICssComponentValue
{
    public ICssToken AssociatedToken { get; } = associatedToken;
    public List<ICssComponentValue> Value { get; } = value;
}

public sealed class CssDeclaration(string name)
{
    public string Name { get; } = name;
    public List<ICssComponentValue> Value { get; } = new();
    public bool Important { get; set; }
}

/// <summary>
/// The prelude is deliberately unstructured here — CSS Syntax doesn't know or
/// care that a style rule's prelude is a selector. That interpretation happens
/// one layer up (Selectors Level 4), not in this parser.
/// </summary>
public sealed class CssQualifiedRule
{
    public List<ICssComponentValue> Prelude { get; } = new();
    public CssSimpleBlock? Block { get; set; }
}

public sealed class CssAtRule(string name)
{
    public string Name { get; } = name;
    public List<ICssComponentValue> Prelude { get; } = new();
    public CssSimpleBlock? Block { get; set; }
}
