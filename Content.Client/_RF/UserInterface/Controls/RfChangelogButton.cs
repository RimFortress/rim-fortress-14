using Content.Suil;
using Content.Client.Changelog;
using JetBrains.Annotations;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// The <see cref="ChangelogButton"/> built on <see cref="RfButton"/>: its text shows whether there are new
/// changelog entries, and it gets the <c>positive</c> style class while there are.
/// </summary>
[UsedImplicitly]
[SuilTag("changelog-button")]
public sealed partial class RfChangelogButton : RfButton
{
    [Dependency] private ChangelogManager _changelogManager = default!;

    public RfChangelogButton()
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
            AddSuilStyleClass(SuilClass.Positive);
        }
        else
        {
            Text = Loc.GetString("changelog-button");
            RemoveSuilStyleClass(SuilClass.Positive);
        }
    }
}
