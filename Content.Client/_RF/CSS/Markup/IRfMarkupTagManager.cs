using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS.Markup;

/// <summary>
/// Finds and runs <see cref="IRfMarkupTagHandler"/> tags.
/// </summary>
public interface IRfMarkupTagManager
{
    /// <summary>
    /// All registered handlers.
    /// </summary>
    IReadOnlyCollection<IRfMarkupTagHandler> Handlers { get; }

    /// <summary>
    /// Returns the handler for the tag name, ignoring the whitelist.
    /// </summary>
    /// <param name="name">The name of the tag (case-insensitive).</param>
    IRfMarkupTagHandler? GetHandler(string name);

    /// <summary>
    /// Attempts to get the handler with the corresponding name.
    /// </summary>
    /// <param name="name">The name of the tag (case-insensitive).</param>
    /// <param name="tagsAllowed">List of allowed handler types. If <c>null</c>, all types are allowed.</param>
    /// <param name="handler">The instance responsible for tags of this name.</param>
    bool TryGetHandler(string name, Type[]? tagsAllowed, [NotNullWhen(true)] out IRfMarkupTagHandler? handler);

    /// <summary>
    /// Creates an empty context that resolves fonts through <c>ICssFontManager</c>.
    /// </summary>
    RfMarkupContext CreateContext();

    /// <summary>
    /// Runs one markup node: opens or closes its tag in <paramref name="context"/> and returns the text it produces.
    /// Unknown and disallowed tags produce nothing and don't touch the context.
    /// </summary>
    /// <param name="node">The node to process.</param>
    /// <param name="context">The context to update.</param>
    /// <param name="tagsAllowed">List of allowed handler types. If <c>null</c>, all types are allowed.</param>
    /// <returns>The text to render at this position.</returns>
    string ProcessNode(MarkupNode node, RfMarkupContext context, Type[]? tagsAllowed);

    /// <summary>
    /// Lets the handler of the node configure the control it created. See <see cref="IRfMarkupTagHandler.ApplyContext"/>.
    /// </summary>
    /// <param name="node">The node that created the control.</param>
    /// <param name="control">The control.</param>
    /// <param name="context">The context at the position of the node.</param>
    /// <param name="tagsAllowed">List of allowed handler types. If <c>null</c>, all types are allowed.</param>
    void ApplyContext(MarkupNode node, Control control, RfMarkupContext context, Type[]? tagsAllowed);
}
