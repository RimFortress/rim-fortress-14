using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS.Markup;

/// <summary>
/// A fork of the engine's <c>IMarkupTagHandler</c>. Classes that implement it are instantiated once by
/// <see cref="RfMarkupTagManager"/> and handle all tags of their name, so they must not keep state
/// that belongs to a specific tag or text.
/// </summary>
/// <remarks>
/// Differences from the engine interface:
/// <list type="bullet">
/// <item><description>Handlers push <see cref="RfInlineStyle"/> values onto <see cref="RfMarkupContext"/>; there is no
/// pop method, closing a tag restores the context automatically.</description></item>
/// <item><description><see cref="TryCreateControl"/> is only called for opening nodes.</description></item>
/// <item><description><see cref="ApplyContext"/> lets a handler configure the control it created using the style
/// that is active at the position of the tag (font, size, color...), every time the text is laid out.</description></item>
/// </list>
/// </remarks>
public interface IRfMarkupTagHandler
{
    /// <summary>
    /// The string used as the tag's name when writing rich text (case-insensitive).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Called when an opening node of this tag is encountered. Push the style that applies to the text
    /// inside the tag with <see cref="RfMarkupContext.Push"/>; push nothing to leave the style unchanged.
    /// </summary>
    /// <param name="node">The markup node containing the parameter and attributes.</param>
    /// <param name="context">The context to push the style on.</param>
    void Push(MarkupNode node, RfMarkupContext context) { }

    /// <summary>
    /// Called after <see cref="Push"/>. Supplies text rendered before the tag's children, in the tag's own style.
    /// </summary>
    /// <param name="node">The markup node containing the parameter and attributes.</param>
    /// <returns>The text that gets rendered.</returns>
    string TextBefore(MarkupNode node) => "";

    /// <summary>
    /// Called when a closing node of this tag is encountered, before the tag's style is dropped.
    /// Supplies text rendered after the tag's children. It is drawn in the style of the enclosing text,
    /// because the style is restored before the text is measured.
    /// </summary>
    /// <param name="node">The markup node containing the parameter and attributes.</param>
    /// <returns>The text that gets rendered.</returns>
    string TextAfter(MarkupNode node) => "";

    /// <summary>
    /// Called when the text is created to supply a control that gets rendered inline before the tag's children.
    /// The returned control must be a new instance on every call. Text continues to the right of the control
    /// and then below it. Only called for opening nodes.
    /// </summary>
    /// <param name="node">The markup node containing the parameter and attributes.</param>
    /// <param name="control">A UI control for placing in line with the tag's children.</param>
    /// <returns><c>true</c> if this tag supplies a control.</returns>
    bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        return false;
    }

    /// <summary>
    /// Called on every layout of the text, right before a control created by <see cref="TryCreateControl"/>
    /// is measured, with the context as it is at the position of the tag (after <see cref="Push"/>).
    /// Use it to make the control follow the surrounding text, e.g. copy the font or the color.
    /// </summary>
    /// <remarks>
    /// It runs inside the owner's measure pass and on every style change, so it must be cheap and idempotent:
    /// only assign a property if its value actually changes, otherwise the control is invalidated for nothing.
    /// </remarks>
    /// <param name="node">The markup node that created the control.</param>
    /// <param name="control">The control returned by <see cref="TryCreateControl"/>.</param>
    /// <param name="context">The context at the position of the tag.</param>
    void ApplyContext(MarkupNode node, Control control, RfMarkupContext context) { }
}
