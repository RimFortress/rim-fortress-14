namespace Content.Suil;

/// <summary>
/// The bindings that work their values out again every frame or every so often (<c>text: bind ... every 1s</c>). A view
/// adds its bindings when it enters the tree of the controls and removes them when it leaves it; the client calls
/// <see cref="Update"/> every frame.
/// </summary>
public static class SuilBindings
{
    private sealed class Entry(Action action, double period)
    {
        public readonly Action Action = action;
        public readonly double Period = period;
        public double Elapsed;
        public bool Removed;
    }

    private static readonly object Lock = new();
    private static List<Entry> _entries = new();

    /// <summary>
    /// Told about an exception of a binding, in a build with <c>EXCEPTION_TOLERANCE</c>. The binding that failed is not
    /// called any more.
    /// </summary>
    public static Action<Exception>? Failed { get; set; }

    /// <summary>
    /// Adds a binding. <paramref name="period"/> is the seconds between the calls, 0 for every frame. Returns what
    /// <see cref="Remove"/> takes.
    /// </summary>
    public static object Add(Action action, double period)
    {
        var entry = new Entry(action, period);

        lock (Lock)
        {
            // a new list, so Update can go through the old one while a binding adds or removes
            _entries = new List<Entry>(_entries) { entry };
        }

        return entry;
    }

    public static void Remove(object? token)
    {
        if (token is not Entry entry)
            return;

        lock (Lock)
        {
            entry.Removed = true;
            var kept = new List<Entry>(_entries.Count);

            foreach (var other in _entries)
            {
                if (!ReferenceEquals(other, entry))
                    kept.Add(other);
            }

            _entries = kept;
        }
    }

    /// <summary>
    /// Calls the bindings that are due.
    /// </summary>
    public static void Update(double frameTime)
    {
        List<Entry> entries;

        lock (Lock)
        {
            entries = _entries;
        }

        foreach (var entry in entries)
        {
            if (entry.Removed)
                continue;

            if (entry.Period > 0)
            {
                entry.Elapsed += frameTime;

                if (entry.Elapsed < entry.Period)
                    continue;

                entry.Elapsed = 0;
            }

#if EXCEPTION_TOLERANCE
            try
            {
                entry.Action();
            }
            catch (Exception e)
            {
                Remove(entry);
                Failed?.Invoke(e);
            }
#else
            entry.Action();
#endif
        }
    }
}
