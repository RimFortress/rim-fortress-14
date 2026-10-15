using System.Collections.Immutable;

namespace Content.Suil;

/// <summary>
/// A marker interface for any SUIL (Styled UI Layout) token.
/// </summary>
public interface ISuilToken;

/// <summary>
/// A token together with the position of its first code point (both 1-based, in code points).
/// </summary>
public readonly record struct SuilToken(ISuilToken Value, int Line, int Column);

/// <summary>
/// A piece of a string or a localization key: either literal text or a `variable` reference.
/// </summary>
public readonly record struct StringPart(string Text, bool IsVariable)
{
    public static StringPart Literal(string text) => new(text, false);

    public static StringPart Variable(string name) => new(name, true);

    internal static bool SequenceEqual(ImmutableArray<StringPart> a, ImmutableArray<StringPart> b)
    {
        if (a.IsDefault || b.IsDefault)
            return a.IsDefault && b.IsDefault;

        if (a.Length != b.Length)
            return false;

        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    internal static int Hash(ImmutableArray<StringPart> parts)
    {
        var hash = 17;

        if (!parts.IsDefault)
        {
            foreach (var part in parts)
            {
                hash = unchecked(hash * 31 + part.GetHashCode());
            }
        }

        return hash;
    }
}

/// <summary>
/// Whitespace and comments (<c>//</c> and <c>/* */</c>) collapse into a single token.
/// It is significant: it separates the descendant combinator in selectors and tells
/// a glued <c>(</c> (<see cref="FunctionToken"/>) from the props list of an element.
/// </summary>
public readonly record struct WhitespaceToken : ISuilToken;

/// <summary>
/// Only used inside the tokenizer, it is never returned by <see cref="SuilTokenizer.Tokenize"/>.
/// </summary>
public readonly record struct EofToken : ISuilToken;

/// <summary>
/// An identifier in kebab-case: <c>button</c>, <c>icon-sort-up</c>, <c>let</c>, <c>use</c>.
/// The keywords <c>let</c> and <c>use</c> are plain idents, the parser treats them contextually.
/// </summary>
public readonly record struct IdentToken(string Value) : ISuilToken;

/// <summary>
/// The single <c>_</c>: an element without a type and a style.
/// </summary>
public readonly record struct UnderscoreToken : ISuilToken;

/// <summary>
/// An ident glued to a following <c>(</c>, like <c>texture(</c>. The parenthesis is consumed.
/// </summary>
public readonly record struct FunctionToken(string Value) : ISuilToken;

/// <summary>
/// <c>#name</c>. <paramref name="IsId"/> is true when the value starts like an identifier:
/// <c>#bad</c> is both a valid id and a valid color, <c>#123456</c> is only a color.
/// The parser decides by the position (an element head or a value).
/// </summary>
public readonly record struct HashToken(string Value, bool IsId) : ISuilToken;

/// <summary>
/// <c>.icon-`name`</c> or <c>#`prefix`-button</c>: a class or an id whose name has `variable` references.
/// A plain <c>.name</c> and <c>#name</c> are not turned into it. <paramref name="Sigil"/> is '.' or '#'.
/// </summary>
public readonly record struct InterpolatedNameToken(int Sigil, ImmutableArray<StringPart> Parts) : ISuilToken
{
    public bool Equals(InterpolatedNameToken other) => Sigil == other.Sigil && StringPart.SequenceEqual(Parts, other.Parts);

    public override int GetHashCode() => unchecked(Sigil * 397 ^ StringPart.Hash(Parts));
}

public readonly record struct NumberToken(double Value, bool IsInteger) : ISuilToken;

public readonly record struct PercentageToken(double Value) : ISuilToken;

public readonly record struct DimensionToken(double Value, bool IsInteger, string Unit) : ISuilToken;

/// <summary>
/// <c>"text with `variable` inside"</c>. Escapes are already resolved.
/// </summary>
public readonly record struct StringToken(ImmutableArray<StringPart> Parts) : ISuilToken
{
    public bool Equals(StringToken other) => StringPart.SequenceEqual(Parts, other.Parts);

    public override int GetHashCode() => StringPart.Hash(Parts);
}

/// <summary>
/// A reference to a variable outside of a string: <c>`name`</c>.
/// </summary>
public readonly record struct VariableToken(string Name) : ISuilToken;

/// <summary>
/// <c>$key-name</c> or <c>$prefix-`p`-suffix</c>. <paramref name="HasArguments"/> is true when the key is glued
/// to a following <c>(</c>, that parenthesis is consumed and the parser expects the arguments and <c>)</c>.
/// </summary>
public readonly record struct LocToken(ImmutableArray<StringPart> Key, bool HasArguments) : ISuilToken
{
    public bool Equals(LocToken other) => HasArguments == other.HasArguments && StringPart.SequenceEqual(Key, other.Key);

    public override int GetHashCode() => unchecked(StringPart.Hash(Key) * 397 ^ (HasArguments ? 1 : 0));
}

/// <summary>
/// <c>%content%</c>, the place of the template where the block of the usage is inserted.
/// </summary>
public readonly record struct ContentSlotToken(string Name) : ISuilToken;

/// <summary>
/// A single code point that doesn't start any other token, e.g. '+', '=', '.', '>', '!', '|'.
/// </summary>
public readonly record struct DelimToken(int CodePoint) : ISuilToken;

/// <summary>
/// U+0028 LEFT PARENTHESIS (()
/// </summary>
public readonly record struct LeftParenthesisToken : ISuilToken;

/// <summary>
/// U+0029 RIGHT PARENTHESIS ())
/// </summary>
public readonly record struct RightParenthesisToken : ISuilToken;

/// <summary>
/// U+002C COMMA (,)
/// </summary>
public readonly record struct CommaToken : ISuilToken;

/// <summary>
/// U+003A COLON (:)
/// </summary>
public readonly record struct ColonToken : ISuilToken;

/// <summary>
/// U+003B SEMICOLON (;)
/// </summary>
public readonly record struct SemicolonToken : ISuilToken;

/// <summary>
/// U+007B LEFT CURLY BRACKET ({)
/// </summary>
public readonly record struct LeftCurlyBracket : ISuilToken;

/// <summary>
/// U+007D RIGHT CURLY BRACKET (})
/// </summary>
public readonly record struct RightCurlyBracket : ISuilToken;
