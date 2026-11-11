using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.GOAP.Domains;

/// <summary>
/// The one who knows what the keys of a family of domains are (<c>Engagement/...</c>, <c>Query/...</c>): a key of a domain is not kept
/// in the state of an agent, it is found out from the world when it is asked for.
/// </summary>
public interface IGoapDomainProvider
{
    /// <summary>
    /// Finds the value of the key of one of the families that the provider is registered for.
    /// </summary>
    /// <param name="state">The state of the agent that asks.</param>
    /// <param name="owner">The agent.</param>
    /// <param name="domains">The key, divided into domains.</param>
    /// <param name="value">The value, of the type that is asked for.</param>
    bool TryResolve<T>(
        GoapState state,
        EntityUid owner,
        StateKey<T>[] domains,
        [NotNullWhen(true)] out object? value) where T : notnull;
}

/// <summary>
/// Raised on an agent (and broadcast) when the value of a key of a domain, which somebody watches, has changed for it, see
/// <see cref="GoapDomainSystem.Watch{T}"/>. The new value is what <c>SharedGoapSystem.TryGetValue</c> gives now.
/// </summary>
/// <param name="Agent">The agent.</param>
/// <param name="Key">The key (its id).</param>
[PublicAPI, ByRefEvent]
public readonly record struct GoapDomainKeyChanged(EntityUid Agent, string Key);

/// <summary>
/// Raised on an agent (and broadcast) when something in its state has been set or removed, a key of any type. It is for those who
/// have to know that a key of a domain which depends on the state of the agent, such as that of the owner of a situation, may
/// have changed, and do not know the type of it.
/// </summary>
/// <param name="Agent">The agent.</param>
/// <param name="Key">The key (its id).</param>
[PublicAPI, ByRefEvent]
public readonly record struct GoapStateKeyChanged(EntityUid Agent, string Key);

/// <summary>
/// The keys of the domains of the state of an agent (see <see cref="GoapState"/>): who finds their values, and how those who
/// depend on them (the search queries and filters of the agents, first of all) get to know that a value has changed.
/// <para>
/// A <b>provider</b> (<see cref="IGoapDomainProvider"/>) is registered for the first segment of a key, a <b>family</b>: it finds the
/// values, and it says when they may have changed for some agent (<see cref="Invalidate"/>): the participants of a situation when
/// someone joins it, the owner when it gets a new situation. A new family of keys is a new provider; the GOAP knows nothing about it.
/// </para>
/// <para>
/// Those who depend on a key <b>watch</b> it (<see cref="Watch{T}"/>). An invalidation does not do anything at once: at the end of the
/// tick the watched keys of the family are found out again for each agent that was told about, and
/// <see cref="GoapDomainKeyChanged"/> is raised for those whose values are not the ones they were. So many changes of one thing
/// make one check, and a value that is the same does not trouble anyone.
/// </para>
/// </summary>
public sealed partial class GoapDomainSystem : EntitySystem
{
    [Dependency] private SharedGoapSystem _goap = null!;
    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    /// <summary>
    /// How many times <see cref="GoapDomainKeyChanged"/> has been raised, for the diagnostics and the tests.
    /// </summary>
    [ViewVariables]
    public int Notified { get; private set; }

    private readonly Dictionary<string, IGoapDomainProvider> _providers = new();
    private readonly Dictionary<string, Dictionary<string, Func<GoapState, object?>>> _watched = new();
    private readonly Dictionary<EntityUid, Dictionary<string, object?>> _last = new();
    private readonly HashSet<(EntityUid Agent, string Family)> _pending = new();

    [SubscribeLocalEvent]
    private void OnTerminating(Entity<GoapComponent> ent, ref EntityTerminatingEvent args)
    {
        _last.Remove(ent);
    }

    /// <summary>
    /// The family of a key: its first domain.
    /// </summary>
    [PublicAPI, Pure]
    public static string Family(string key)
    {
        var end = key.IndexOf(GoapState.KeyDomainSeparator, StringComparison.Ordinal);
        return end < 0 ? key : key[..end];
    }

    /// <summary>
    /// Is the key one of a domain: it is not kept in the state.
    /// </summary>
    [PublicAPI, Pure]
    public static bool IsDomainKey(string key)
        => key.Contains(GoapState.KeyDomainSeparator, StringComparison.Ordinal);

    /// <summary>
    /// Makes the provider the one who finds the values of the keys of the families.
    /// </summary>
    [PublicAPI]
    public void Register(IGoapDomainProvider provider, params string[] families)
    {
        foreach (var family in families)
        {
            if (!_providers.TryAdd(family, provider))
                Log.Error($"The family of keys {family} has a provider already.");
        }
    }

    /// <summary>
    /// Finds the value of a key of a domain, by the provider of the family.
    /// </summary>
    [PublicAPI]
    public bool TryResolve<T>(
        GoapState state,
        StateKey<T>[] domains,
        [NotNullWhen(true)] out object? value) where T : notnull
    {
        value = null;

        return domains.Length >= 2
               && _providers.TryGetValue(domains[0].Id, out var provider)
               && provider.TryResolve(state, SharedGoapSystem.Owner(state), domains, out value);
    }

    /// <summary>
    /// Tells that the key is depended on: when its value changes for an agent, <see cref="GoapDomainKeyChanged"/> is raised for it.
    /// It is for the keys of the domains, and a key of another kind is ignored. It is cheap to watch again, it is how the search
    /// systems tell what the queries and the filters are about whenever the prototypes are loaded.
    /// </summary>
    [PublicAPI]
    public void Watch<T>(StateKey<T> key) where T : notnull
    {
        if (!IsDomainKey(key.Id))
            return;

        var family = Family(key.Id);

        if (!_watched.TryGetValue(family, out var keys))
            _watched[family] = keys = new();

        keys.TryAdd(key.Id, state => _goap.TryGetValue(state, key, out var value) ? value : null);
    }

    /// <summary>
    /// Does anybody watch the keys of the family: if nobody does, there is no need to tell about the changes.
    /// </summary>
    [PublicAPI, Pure]
    public bool IsWatched(string family) => _watched.ContainsKey(family);

    /// <summary>
    /// Tells that the keys of the family may have changed for the agent. The check is made at the end of the tick, see
    /// <see cref="Flush"/>. It is nothing if nobody watches the family.
    /// </summary>
    [PublicAPI]
    public void Invalidate(EntityUid agent, string family)
    {
        if (_watched.ContainsKey(family) && _goapQuery.HasComp(agent))
            _pending.Add((agent, family));
    }

    /// <summary>
    /// Checks the watched keys of the agents that were told about at once, and not at the end of the tick.
    /// </summary>
    [PublicAPI]
    public void Flush()
    {
        // The checks can tell about more.
        for (var round = 0; round < 8 && _pending.Count > 0; round++)
        {
            var pending = _pending.ToArray();
            _pending.Clear();

            foreach (var (agent, family) in pending)
            {
                Check(agent, family);
            }
        }

        _pending.Clear();
    }

    public override void Update(float frameTime)
    {
        if (_pending.Count > 0)
            Flush();
    }

    private void Check(EntityUid agent, string family)
    {
        if (TerminatingOrDeleted(agent)
            || !_goapQuery.TryComp(agent, out var goap)
            || !_watched.TryGetValue(family, out var keys))
            return;

        if (!_last.TryGetValue(agent, out var last))
            _last[agent] = last = new();

        foreach (var (key, resolve) in keys.ToArray())
        {
            var value = Copy(resolve(goap.State));
            last.TryGetValue(key, out var old);

            if (ValueEquals(old, value))
                continue;

            last[key] = value;
            Notified++;
            var ev = new GoapDomainKeyChanged(agent, key);
            RaiseLocalEvent(agent, ref ev, true);
        }
    }

    /// <summary>
    /// What is kept to be compared with: a set is copied, it is not to change with the world.
    /// </summary>
    private static object? Copy(object? value)
        => value is IEnumerable<EntityUid> uids ? new HashSet<EntityUid>(uids) : value;

    private static bool ValueEquals(object? a, object? b)
    {
        if (a is HashSet<EntityUid> setA && b is HashSet<EntityUid> setB)
            return setA.SetEquals(setB);

        return Equals(a, b);
    }
}
