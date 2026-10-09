using Content.Client.Changelog;
using JetBrains.Annotations;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// The <see cref="ChangelogButton"/> built on <see cref="CssButton"/>: its text shows whether there are new
/// changelog entries, and it gets the <c>positive</c> style class while there are.
/// </summary>
[UsedImplicitly]
public sealed partial class CssChangelogButton : CssButton
{
    [Dependency] private ChangelogManager _changelogManager = default!;

    public CssChangelogButton()
    {
        IoCManager.InjectDependencies(this);

        // So that measuring before opening returns a correct height.
        Text = " ";
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _changelogManager.NewChangelogEntriesChanged += UpdateText;
        UpdateText();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _changelogManager.NewChangelogEntriesChanged -= UpdateText;
    }

    private void UpdateText()
    {
        if (_changelogManager.NewChangelogEntries)
        {
            Text = Loc.GetString("changelog-button-new-entries");
            AddCssStyleClass(CssClass.Positive);
        }
        else
        {
            Text = Loc.GetString("changelog-button");
            RemoveCssStyleClass(CssClass.Positive);
        }
    }
}
