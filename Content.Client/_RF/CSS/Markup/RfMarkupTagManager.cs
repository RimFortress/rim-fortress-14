using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._RF.CSS.Text;
using Robust.Client.UserInterface;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS.Markup;

/// <summary>
/// A fork of the engine's <c>MarkupTagManager</c> for <see cref="IRfMarkupTagHandler"/> tags.
/// Unlike the engine one it has no hard-coded engine tags (the sandbox restriction doesn't apply to content),
/// finds every handler by reflection, matches names case-insensitively, and owns the tag open/close bookkeeping
/// of <see cref="RfMarkupContext"/>.
/// </summary>
public sealed partial class RfMarkupTagManager : IRfMarkupTagManager, IPostInjectInit
{
    [Dependency] private IReflectionManager _reflection = null!;
    [Dependency] private IDynamicTypeFactory _factory = null!;
    [Dependency] private ICssFontManager _fonts = null!;

    private readonly Dictionary<string, IRfMarkupTagHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void PostInject()
    {
        _handlers.Clear();

        foreach (var type in _reflection.GetAllChildren<IRfMarkupTagHandler>().Where(t => !t.IsAbstract && !t.IsInterface))
        {
            var handler = _factory.CreateInstance<IRfMarkupTagHandler>(type);

            if (!_handlers.TryAdd(handler.Name, handler))
                throw new InvalidOperationException($"duplicated markup tag '{handler.Name}': {type.FullName}");
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IRfMarkupTagHandler> Handlers => _handlers.Values;

    /// <inheritdoc/>
    public IRfMarkupTagHandler? GetHandler(string name) => _handlers.GetValueOrDefault(name);

    /// <inheritdoc/>
    public bool TryGetHandler(string name, Type[]? tagsAllowed, [NotNullWhen(true)] out IRfMarkupTagHandler? handler)
    {
        // Using a whitelist prevents new tags from sneaking in.
        if (_handlers.TryGetValue(name, out var found)
            && (tagsAllowed == null || Array.IndexOf(tagsAllowed, found.GetType()) != -1))
        {
            handler = found;
            return true;
        }

        handler = null;
        return false;
    }

    /// <inheritdoc/>
    public RfMarkupContext CreateContext() => new(_fonts);

    /// <inheritdoc/>
    public string ProcessNode(MarkupNode node, RfMarkupContext context, Type[]? tagsAllowed)
    {
        // A node without a name is a text node.
        if (node.Name == null)
            return node.Value.StringValue ?? "";

        if (!TryGetHandler(node.Name, tagsAllowed, out var handler))
            return "";

        if (!node.Closing)
        {
            context.BeginTag(node.Name);
            handler.Push(node, context);
            return handler.TextBefore(node);
        }

        var text = handler.TextAfter(node);
        context.EndTag(node.Name);
        return text;
    }

    /// <inheritdoc/>
    public void ApplyContext(MarkupNode node, Control control, RfMarkupContext context, Type[]? tagsAllowed)
    {
        if (node.Name != null && TryGetHandler(node.Name, tagsAllowed, out var handler))
            handler.ApplyContext(node, control, context);
    }
}
