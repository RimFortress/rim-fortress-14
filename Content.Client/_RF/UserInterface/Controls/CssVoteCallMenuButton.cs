using Content.Client.Voting;
using Content.Client.Voting.UI;
using JetBrains.Annotations;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A button that opens the vote call menu, the <see cref="CssButton"/> version of <see cref="VoteCallMenuButton"/>.
/// Disables itself if the client cannot call votes.
/// </summary>
[UsedImplicitly]
public sealed partial class CssVoteCallMenuButton : CssButton
{
    [Dependency] private IVoteManager _voteManager = null!;

    private VoteCallMenu? _voteCallMenu;

    public CssVoteCallMenuButton()
    {
        Text = Loc.GetString("ui-vote-menu-button");
        ToggleMode = true;
        OnPressed += OnButtonPressed;
    }

    private void OnButtonPressed(CssButtonEventArgs args)
    {
        if (_voteCallMenu is { IsOpen: true })
        {
            _voteCallMenu.Close();
            return;
        }

        _voteCallMenu = new VoteCallMenu();
        _voteCallMenu.OnClose += () => Pressed = false;
        _voteCallMenu.OpenCentered();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        UpdateCanCall(_voteManager.CanCallVote);
        _voteManager.CanCallVoteChanged += UpdateCanCall;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        if (_voteCallMenu is { IsOpen: true })
            _voteCallMenu.Close();

        _voteManager.CanCallVoteChanged -= UpdateCanCall;
    }

    private void UpdateCanCall(bool canCall)
    {
        Disabled = !canCall;
    }
}
