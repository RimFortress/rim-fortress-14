using Content.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A line of text that remembers what was entered and gives it back with the keys of the history, the
/// <see cref="RfLineEdit"/> version of <see cref="Robust.Client.UserInterface.Controls.HistoryLineEdit"/>.
/// </summary>
[Virtual, UsedImplicitly]
[SuilTag("history-line-edit")]
public class RfHistoryLineEdit : RfLineEdit
{
    private const int MaxHistorySize = 100;

    private string? _historyTemp;

    public List<string> History { get; } = new();

    public int HistoryIndex { get; set; }

    public event Action? OnHistoryChanged;

    /// <summary>
    /// If true, <see cref="RfLineEdit.OnTextChanged"/> is invoked when the history selection changes.
    /// </summary>
    public bool InvokeOnHistorySelect = true;

    public RfHistoryLineEdit()
    {
        OnTextEntered += OnSubmit;
    }

    public void ClearHistory()
    {
        History.Clear();
        HistoryIndex = 0;
    }

    private void OnSubmit(LineEditEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Text))
            return;

        if (History.Count == 0 || History[^1] != args.Text)
        {
            History.Add(args.Text);

            if (History.Count > MaxHistorySize)
                History.RemoveAt(0);
        }

        HistoryIndex = History.Count;
        OnHistoryChanged?.Invoke();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!HasKeyboardFocus())
            return;

        if (args.Function == EngineKeyFunctions.TextHistoryPrev)
        {
            if (HistoryIndex <= 0)
                return;

            if (HistoryIndex == History.Count)
                _historyTemp = Text;

            HistoryIndex--;
            SetText(History[HistoryIndex], InvokeOnHistorySelect);
            CursorPosition = Text.Length;

            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextHistoryNext)
        {
            if (HistoryIndex >= History.Count)
                return;

            HistoryIndex++;

            SetText(HistoryIndex == History.Count ? _historyTemp ?? string.Empty : History[HistoryIndex],
                InvokeOnHistorySelect);
            CursorPosition = Text.Length;

            args.Handle();
        }
    }
}
