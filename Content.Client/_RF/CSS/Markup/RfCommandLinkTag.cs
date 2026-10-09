using Content.Client._RF.UserInterface;
using System.Diagnostics.CodeAnalysis;
using Content.Client._RF.CSS.Text;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Console;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS.Markup;

/// <summary>
/// <c>[cmdlink="Text" command="cmd"]</c>: an inline link that executes a console command when clicked.
/// It is the reference example of a tag that creates a control: the control copies the font of the
/// surrounding text through <see cref="IRfMarkupTagHandler.ApplyContext"/>.
/// </summary>
/// <remarks>
/// The command is executed with the client's permissions, so this tag must not be allowed for untrusted text
/// (it is not part of <see cref="RfRichTextEntry.DefaultTags"/>).
/// </remarks>
[UsedImplicitly]
public sealed class RfCommandLinkTag : IRfMarkupTagHandler
{
    /// <inheritdoc/>
    public string Name => "cmdlink";

    /// <inheritdoc/>
    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (node.Value.StringValue is not { } text
            || !node.Attributes.TryGetValue("command", out var command)
            || command.StringValue is not { } commandText)
            return false;

        var link = new RfCommandLinkControl { Command = commandText };
        link.SetPlainText(text);
        control = link;
        return true;
    }

    /// <inheritdoc/>
    public void ApplyContext(MarkupNode node, Control control, RfMarkupContext context)
    {
        if (control is RfCommandLinkControl link)
            link.ApplyContext(context.Current);
    }
}

/// <summary>
/// The control created by <see cref="RfCommandLinkTag"/>. Underlined and blue by default; both can be
/// overridden from CSS through the <c>command-link</c> class.
/// </summary>
public sealed class RfCommandLinkControl : CssControl
{
    private RfInlineStyle? _appliedContext;

    /// <summary>
    /// The console command executed on click.
    /// </summary>
    public string Command { get; set; } = "";

    /// <summary>
    /// Creates a link with the default look.
    /// </summary>
    public RfCommandLinkControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;
        TextDecorationLine = CssTextDecorationLine.Underline;
        TextColor = new Color(0.4f, 0.6f, 1f);
        AddCssStyleClass(CssClass.CommandLink);
    }

    /// <summary>
    /// Copies the font of the surrounding text. The color and decoration are left to the control's own
    /// style and CSS, so a stylesheet can restyle every link.
    /// </summary>
    /// <param name="style">The inline style at the position of the tag.</param>
    public void ApplyContext(RfInlineStyle style)
    {
        if (_appliedContext == style)
            return;

        _appliedContext = style;
        FontFamily = style.Family;
        FontSize = style.Size;
        FontWeight = style.Weight;
        FontStyle = style.Slant;
    }

    /// <inheritdoc/>
    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || Command.Length == 0)
            return;

        IoCManager.Resolve<IConsoleHost>().ExecuteCommand(Command);
        args.Handle();
    }
}
