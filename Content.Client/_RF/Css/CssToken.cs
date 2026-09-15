namespace Content.Client._RF.Css;

/// <summary>
/// A marker interface for any CSS token. Different token types carry
/// different data (string, number, single code point, or nothing at all) —
/// see the CSS Syntax Module Level 3 definitions for each token type.
/// </summary>
public interface ICssToken;

public readonly record struct WhitespaceToken : ICssToken;

public readonly record struct EofToken : ICssToken;

public readonly record struct StringToken(string Value) : ICssToken;

/// <summary>
/// A single code point, e.g. produced for characters like '+', '.', '>'
/// that don't start any other token.
/// </summary>
public readonly record struct DelimToken(int CodePoint) : ICssToken;

/// <summary>
/// <paramref name="IsId"/> reflects the token's type flag: "id" if the value
/// would be a valid ident sequence, "unrestricted" otherwise. Matters for
/// whether the hash can be used as a valid ID selector (e.g. #1abc can't).
/// </summary>
public readonly record struct HashToken(string Value, bool IsId) : ICssToken;

/// <summary>
/// U+0028 LEFT PARENTHESIS (()
/// </summary>
public readonly record struct LeftParenthesisToken : ICssToken;

/// <summary>
/// U+0029 RIGHT PARENTHESIS ())
/// </summary>
public readonly record struct RightParenthesisToken : ICssToken;

/// <summary>
/// U+002C COMMA (,)
/// </summary>
public readonly record struct CommaToken : ICssToken;

/// <summary>
/// U+003A COLON (:)
/// </summary>
public readonly record struct ColonToken : ICssToken;

/// <summary>
/// U+003B SEMICOLON (;)
/// </summary>
public readonly record struct SemicolonToken : ICssToken;

/// <summary>
/// U+005B LEFT SQUARE BRACKET ([)
/// </summary>
public readonly record struct LeftSquareBracket : ICssToken;

/// <summary>
/// U+005D RIGHT SQUARE BRACKET (])
/// </summary>
public readonly record struct RightSquareBracket : ICssToken;

/// <summary>
/// U+007B LEFT CURLY BRACKET ({)
/// </summary>
public readonly record struct LeftCurlyBracket : ICssToken;

/// <summary>
/// U+007D RIGHT CURLY BRACKET (})
/// </summary>
public readonly record struct RightCurlyBracket : ICssToken;

/// <summary>
/// U+003C LESS-THAN SIGN !--
/// </summary>
public readonly record struct CdoToken : ICssToken;

/// <summary>
/// -->
/// </summary>
public readonly record struct CdcToken : ICssToken;

public readonly record struct IdentToken(string Value) : ICssToken;

public readonly record struct FunctionToken(string Value) : ICssToken;

public readonly record struct UrlToken(string Value) : ICssToken;

public readonly record struct NumberToken(double Value, bool IsInteger) : ICssToken;

public readonly record struct PercentageToken(double Value) : ICssToken;

public readonly record struct DimensionToken(double Value, bool IsInteger, string Unit) : ICssToken;

public readonly record struct AtKeywordToken(string Value) : ICssToken;
