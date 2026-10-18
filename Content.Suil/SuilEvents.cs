namespace Content.Suil;

/// <summary>
/// How an event of a control is subscribed to without reflection, written by the generator for every public event whose
/// delegate returns nothing and takes nothing or one argument. The hot reload subscribes the handlers of a file through it.
/// </summary>
/// <param name="Name">The name in SUIL: the member in kebab-case, <c>on-pressed</c>.</param>
/// <param name="MemberName">The member in the code, <c>OnPressed</c>.</param>
/// <param name="ArgumentType">The type of the argument of the event, <c>null</c> if it has none.</param>
/// <param name="Subscribe">
/// Subscribes a handler to the event of the control and returns the delegate that was added, to unsubscribe it later. The
/// handler gets the argument of the event, <c>null</c> if there is none.
/// </param>
/// <param name="Unsubscribe">Takes the delegate that <paramref name="Subscribe"/> returned off the event.</param>
public sealed record SuilEventAccessor(
    string Name,
    string MemberName,
    Type? ArgumentType,
    Func<object, Action<object?>, Delegate> Subscribe,
    Action<object, Delegate> Unsubscribe);

/// <summary>
/// The events of the control types and the bool members of the arguments of the events, which <c>set .open args.pressed</c>
/// reads. A type has the events it declares itself, the ones of its base types are looked for up the chain.
/// </summary>
public sealed class SuilEventTable
{
    private readonly Dictionary<Type, IReadOnlyDictionary<string, SuilEventAccessor>> _events = new();
    private readonly Dictionary<Type, IReadOnlyDictionary<string, Func<object, bool>>> _flags = new();
    private readonly Dictionary<Type, IReadOnlyDictionary<string, Func<object, object>>> _members = new();

    public void Add(Type type, IReadOnlyDictionary<string, SuilEventAccessor> events) => _events[type] = events;

    /// <summary>
    /// Registers the readers of the bool members of an arguments type, by their names in SUIL.
    /// </summary>
    public void AddArguments(Type argumentType, IReadOnlyDictionary<string, Func<object, bool>> flags) => _flags[argumentType] = flags;

    /// <summary>
    /// Registers the readers of the members of an arguments type, by their names in SUIL: <c>args.new</c> of a call.
    /// </summary>
    public void AddMembers(Type argumentType, IReadOnlyDictionary<string, Func<object, object>> members) => _members[argumentType] = members;

    public void Clear()
    {
        _events.Clear();
        _flags.Clear();
        _members.Clear();
    }

    /// <summary>
    /// Finds an event of a control type; the nearest base type that declares it wins.
    /// </summary>
    public bool TryGetEvent(Type? type, string name, out SuilEventAccessor accessor)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (_events.TryGetValue(current, out var declared) && declared.TryGetValue(name, out accessor!))
                return true;
        }

        accessor = null!;
        return false;
    }

    /// <summary>
    /// Finds the reader of a member of the arguments of an event, <c>new</c> for <c>New</c>.
    /// </summary>
    public bool TryGetMember(Type? argumentType, string name, out Func<object, object> reader)
    {
        for (var current = argumentType; current != null; current = current.BaseType)
        {
            if (_members.TryGetValue(current, out var declared) && declared.TryGetValue(name, out reader!))
                return true;
        }

        reader = null!;
        return false;
    }

    /// <summary>
    /// Finds the reader of a bool member of the arguments of an event, <c>pressed</c> for <c>Pressed</c>.
    /// </summary>
    public bool TryGetFlag(Type? argumentType, string name, out Func<object, bool> reader)
    {
        for (var current = argumentType; current != null; current = current.BaseType)
        {
            if (_flags.TryGetValue(current, out var declared) && declared.TryGetValue(name, out reader!))
                return true;
        }

        reader = null!;
        return false;
    }
}
