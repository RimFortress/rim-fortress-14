using System.Collections;
using System.Linq;

namespace Content.Client._RF.CSS;

/// <summary>
/// A set of CSS style classes (or pseudo-classes) of a <see cref="CssControl"/>; changing it restyles the control.
/// </summary>
/// <remarks>
/// Like the engine's <c>Control.StyleClassCollection</c>, it has <see cref="Parse"/>, so XAML can set it from a
/// string with the classes separated by spaces: <c>CssStyleClasses="open-left main-menu-button"</c>.
/// </remarks>
public sealed class CssStyleClassCollection : ICollection<string>, IReadOnlyCollection<string>
{
    internal CssControl? Owner;

    /// <summary>
    /// The prefix of a name in <see cref="CssStylesheet.GetAncestorReach"/>: "." for classes, ":" for pseudo-classes.
    /// </summary>
    internal string Prefix = ".";

    private readonly HashSet<string> _values;

    internal CssStyleClassCollection(CssControl owner, string prefix = ".")
    {
        Owner = owner;
        Prefix = prefix;
        _values = new();
    }

    public CssStyleClassCollection()
    {
        _values = new();
    }

    public CssStyleClassCollection(IEnumerable<string> items)
    {
        _values = new(items);
    }

    public CssStyleClassCollection(params string[] items)
    {
        _values = new(items);
    }

    /// <summary>
    /// Used by XAML: the classes separated by spaces.
    /// </summary>
    public static CssStyleClassCollection Parse(string s) =>
        new(s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public int Count => _values.Count;

    public bool IsReadOnly => false;

    public IEnumerator<string> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(string item)
    {
        if (_values.Add(item))
            Owner?.RestyleCss(Prefix + item);
    }

    public void Clear()
    {
        if (_values.Count == 0)
            return;

        _values.Clear();
        Owner?.RestyleCss(null);
    }

    public bool Contains(string item) => _values.Contains(item);

    public void CopyTo(string[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);

    public bool Remove(string item)
    {
        if (!_values.Remove(item))
            return false;

        if (Owner != null)
            Owner.RestyleCss(Prefix + item);

        return true;
    }

    public override string ToString() => string.Join(' ', _values.OrderBy(x => x, StringComparer.Ordinal));
}
