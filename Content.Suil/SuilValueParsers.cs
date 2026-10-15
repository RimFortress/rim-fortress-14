using System.Diagnostics.CodeAnalysis;

namespace Content.Suil;

/// <summary>
/// Parses values into typed objects. A parser takes what it needs from the front of the stream and leaves the rest,
/// so the parsers can be chained (a shorthand does it).
/// </summary>
public interface ISuilValues
{
    /// <summary>
    /// Parses a value of the type with the default parser of the type (an enum, a nullable type and <c>none</c> are
    /// handled here).
    /// </summary>
    bool TryParse<T>(ref SuilValueStream stream, out T? value);

    /// <inheritdoc cref="TryParse{T}"/>
    bool TryParse(ref SuilValueStream stream, Type type, out object? value);

    /// <summary>
    /// Parses with the custom parser that a property names, for the parsers that are not the default ones.
    /// </summary>
    bool TryParseWith(Type parserType, ref SuilValueStream stream, [NotNullWhen(true)] out object? value);
}

/// <summary>
/// A parser of the values of one type.
/// </summary>
public interface ISuilValueParser
{
    /// <summary>
    /// The type of the value this parser makes.
    /// </summary>
    Type ValueType { get; }

    /// <summary>
    /// A default parser is used for every property of the type. A parser that is not is used only where a property
    /// names it (<c>[SuilProperty(parser: typeof(...))]</c>).
    /// </summary>
    bool Default { get; }

    /// <summary>
    /// Gives the parser the service to parse the nested values with. Called when the parser is registered.
    /// </summary>
    void Attach(ISuilValues values);

    bool TryParse(ref SuilValueStream stream, [NotNullWhen(true)] out object? value);
}

/// <summary>
/// The base of a parser that makes <typeparamref name="T"/>.
/// </summary>
public abstract class SuilValueParser<T> : ISuilValueParser
{
    /// <summary>
    /// Parses the nested values: the lengths of a shadow, the colors of a mix.
    /// </summary>
    protected ISuilValues Values { get; private set; } = null!;

    public Type ValueType => typeof(T);

    public virtual bool Default => true;

    void ISuilValueParser.Attach(ISuilValues values) => Values = values;

    bool ISuilValueParser.TryParse(ref SuilValueStream stream, [NotNullWhen(true)] out object? value)
    {
        if (!TryParse(ref stream, out T? parsed))
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }

    /// <summary>
    /// Parses a value of the type.
    /// </summary>
    /// <returns>True if the value was parsed.</returns>
    public abstract bool TryParse(ref SuilValueStream stream, [NotNullWhen(true)] out T? value);
}

/// <summary>
/// The parsers of the values: the default one of every type, and the custom ones that a property asks for.
/// </summary>
public sealed class SuilValueParsers : ISuilValues
{
    private readonly Dictionary<Type, List<ISuilValueParser>> _default = new();
    private readonly Dictionary<Type, ISuilValueParser> _custom = new();

    /// <summary>
    /// Adds a parser.
    /// </summary>
    public void Register(ISuilValueParser parser)
    {
        parser.Attach(this);

        if (!parser.Default)
        {
            _custom[parser.GetType()] = parser;
            return;
        }

        if (!_default.TryGetValue(parser.ValueType, out var list))
            _default[parser.ValueType] = list = new List<ISuilValueParser>();

        list.Add(parser);
    }

    /// <summary>
    /// Whether the type has a default parser (an enum always has one).
    /// </summary>
    public bool HasDefault(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsEnum || _default.TryGetValue(type, out var list) && list.Count > 0;
    }

    /// <summary>
    /// Parses with the custom parser of a property.
    /// </summary>
    public bool TryParseWith(Type parserType, ref SuilValueStream stream, [NotNullWhen(true)] out object? value)
    {
        if (!_custom.TryGetValue(parserType, out var parser))
            throw new ArgumentException($"the SUIL value parser {parserType.FullName} is not registered");

        return parser.TryParse(ref stream, out value);
    }

    public bool TryParse<T>(ref SuilValueStream stream, out T? value)
    {
        if (!TryParse(ref stream, typeof(T), out var parsed))
        {
            value = default;
            return false;
        }

        value = (T?) parsed;
        return true;
    }

    public bool TryParse(ref SuilValueStream stream, Type type, out object? value)
    {
        var parsing = type;

        // a reference type can be null as well as a Nullable<T>
        var underlying = Nullable.GetUnderlyingType(type) ?? (type.IsValueType ? null : type);

        if (underlying != null)
        {
            parsing = underlying;

            // `none` is the null of a nullable value
            if (stream.Peek() is SuilIdent ident && ident.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                stream.ConsumeNext();
                value = null;
                return true;
            }
        }

        if (!_default.TryGetValue(parsing, out var parsers) || parsers.Count == 0)
        {
            // one parser for every enum
            if (parsing.IsEnum)
                return TryParseEnum(parsing, ref stream, out value);

#if EXCEPTION_TOLERANCE
            value = null;
            return false;
#else
            throw new ArgumentException($"default SUIL value parser for type {parsing.FullName} not found");
#endif
        }

        var mark = stream.Mark();

        foreach (var parser in parsers)
        {
            if (parser.TryParse(ref stream, out value))
                return true;

            stream.Reset(mark);
        }

        value = null;
        return false;
    }

    /// <summary>
    /// An enum is written as the name of its member in kebab-case: <c>flex-start</c> is <c>FlexStart</c>.
    /// </summary>
    private static bool TryParseEnum(Type type, ref SuilValueStream stream, out object? value)
    {
        value = null;

        if (stream.ConsumeNext() is not SuilIdent ident)
            return false;

        var wanted = Normalize(ident.Value);

        foreach (var name in Enum.GetNames(type))
        {
            if (Normalize(name) != wanted)
                continue;

            value = Enum.Parse(type, name);
            return true;
        }

        return false;

        static string Normalize(string text) => text.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }
}
