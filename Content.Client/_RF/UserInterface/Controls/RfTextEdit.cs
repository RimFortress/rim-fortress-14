using Content.Suil;
using System.Collections.Frozen;
using System.Numerics;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.Text;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A multi-line text editing control, the <see cref="UiElement"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.TextEdit"/>.
/// </summary>
/// <remarks>
/// <para>
/// This control uses a <see cref="Rope"/> to performantly manipulate editable text.
/// </para>
/// <para>
/// Text selection and cursor positions are tracked with two values: a "selection start" and a "cursor position".
/// When you do not have a selection (i.e. just a lone cursor) these values are at the same position.
/// If you have an active selection, the cursor position is the part of the selection you can move around
/// (via mouse or shift+left/right), while the start is the part that stays in place always.
/// </para>
/// <para>
/// Positions inside this control are all in chars.
/// Cursor positions and such should never be inside a surrogate pair, however.
/// </para>
/// <para>
/// The text is drawn inside the padding and borders with the font, <c>color</c>, <c>word-break</c> and
/// <c>overflow-wrap</c> of the control. The cursor is <c>caret-color</c> (the text color by default), the selection
/// is <c>-rf-selection-color</c>. The control matches <c>:focus</c>, <c>:disabled</c>, <c>:read-only</c> and
/// <c>:placeholder-shown</c>, the latter is the way to style the placeholder (e.g. <c>color</c>).
/// </para>
/// <para>
/// The scrollbar is <see cref="VScrollBar"/> of the type <see cref="RfVScrollBar"/> and is drawn over the text,
/// it follows <c>overflow-y</c> (<c>scroll</c> always shows it, <c>hidden</c> and <c>clip</c> never; otherwise it is
/// shown when the text doesn't fit) and <c>scrollbar-width: none</c>.
/// </para>
/// <para>
/// <see cref="Text"/> hides <see cref="UiElement.Text"/>: it is the plain text the user edits, not markup.
/// Letter and word spacing and the line height don't apply to it.
/// </para>
/// </remarks>
[UsedImplicitly]
[SuilTag("text-edit")]
public sealed partial class RfTextEdit : UiElement
{
    [Dependency] private IClipboardManager _clipboard = null!;

    private readonly RenderBox _renderBox;

    private CursorPos _cursorPosition;
    private CursorPos _selectionStart;

    // Cached last horizontal cursor position for vertical cursor movement.
    // Conceptually, moving the cursor up/down "keeps" the horizontal position as much as possible.
    // This allows it to remain in position even when the cursor is moving past "valleys" of short lines.
    private float? _horizontalCursorPos;

    // The positions of all line breaks inside the edited text: indices into the text rope where a line break
    // should occur, "before" the indexed character. So if I have the string "AB", with a line break at index 1,
    // that means the line break is BETWEEN A and B.
    //
    // Line breaks come from either explicit newlines (\n) or from word-wrapping behavior.
    // In the case of newlines, the newline character is considered "on the next line".
    // This also has implications for cursor bias, see below.
    private ValueList<int> _lineBreaks;

    private Rope.Node _textRope = Rope.Leaf.Empty;
    private Rope.Node? _placeholder;
    private bool _lineUpdateQueued;
    private bool _placeholderShown;

    private bool _editable = true;

    // The size of the area the text is drawn in, in virtual pixels.
    private Vector2 _contentSize;

    // Uncommitted IME positions are stored directly in the text rope.
    // This field tracks the start position thereof, and how long it is.
    // The intent is that the text is cut from the rope again if the composition gets cancelled or edited.
    private (CursorPos start, int length)? _imeData;

    private RfTextEditShared.CursorBlink _blink;

    // State for click-hold text selection. The position is relative to the area of the text.
    private bool _mouseSelectingText;
    private Vector2 _lastMouseSelectPos;

    // Reused by the word wrapping.
    private readonly List<RfLineBreak> _wrapBreaks = [];

    /// <summary>
    /// The vertical scrollbar.
    /// </summary>
    [PublicAPI, ViewVariables]
    public RfVScrollBar VScrollBar { get; }

    public event Action<TextEditEventArgs>? OnTextChanged;

    public RfTextEdit()
    {
        IoCManager.InjectDependencies(this);

        AddChild(_renderBox = new RenderBox(this));
        AddChild(VScrollBar = new RfVScrollBar { Visible = false });

        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;
        MouseFilter = MouseFilterMode.Stop;
        UpdateCursorShape();
    }

    /// <summary>
    /// Limits the number of lines the user is allowed to create. If the value is 0 we won't set a limit.
    /// </summary>
    public int MaxLines { get; set; }

    /// <summary>
    /// The current position of the cursor in the text rope.
    /// </summary>
    /// <remarks>
    /// See the class remarks for how text selection works in this control.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the given position would put the cursor inside a surrogate pair.
    /// </exception>
    public CursorPos CursorPosition
    {
        get => _cursorPosition;
        set
        {
            var clamped = MathHelper.Clamp(value.Index, 0, TextLength);
            if (TextLength != 0 && TextLength != clamped && !Rope.TryGetRuneAt(TextRope, clamped, out _))
                throw new ArgumentException("Cannot set cursor inside surrogate pair.");

            _cursorPosition = value with { Index = clamped };
            _selectionStart = _cursorPosition;
        }
    }

    /// <summary>
    /// The start position of the selection in the text rope.
    /// </summary>
    /// <remarks>
    /// See the class remarks for how text selection works in this control.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the given position would put the cursor inside a surrogate pair.
    /// </exception>
    public CursorPos SelectionStart
    {
        get => _selectionStart;
        set
        {
            var clamped = MathHelper.Clamp(value.Index, 0, TextLength);
            if (TextLength != 0 && TextLength != clamped && !Rope.TryGetRuneAt(TextRope, clamped, out _))
                throw new ArgumentException("Cannot set cursor inside surrogate pair.");

            _selectionStart = value with { Index = clamped };
        }
    }

    /// <summary>
    /// The text rope that can be viewed and edited.
    /// </summary>
    public Rope.Node TextRope
    {
        get => _textRope;
        set
        {
            _textRope = value;
            QueueLineBreakUpdate();
            UpdatePseudoClass();
        }
    }

    /// <summary>
    /// The text as a string. Setting it moves the cursor to the end and doesn't invoke <see cref="OnTextChanged"/>.
    /// </summary>
    public new string Text
    {
        get => Rope.Collapse(_textRope);
        set
        {
            TextRope = Rope.Insert(Rope.Leaf.Empty, 0, value);
            _selectionStart = _cursorPosition = new CursorPos(value.Length, LineBreakBias.Top);
            InvalidateHorizontalCursorPos();
        }
    }

    /// <summary>
    /// True to allow editing by the user. False to make it read-only.
    /// </summary>
    public bool Editable
    {
        get => _editable;
        set
        {
            _editable = value;
            UpdateCursorShape();
            SetSuilPseudoStyleClass(SuilPseudoClass.ReadOnly, !value);
        }
    }

    /// <summary>
    /// The lower (in string index terms) end of the active text selection.
    /// </summary>
    /// <remarks>
    /// Confusingly, because text is read top-to-bottom, "lower" is actually higher up on your monitor.
    /// </remarks>
    public CursorPos SelectionLower => CursorPos.Min(_selectionStart, _cursorPosition);

    /// <summary>
    /// The upper (in string index terms) end of the active text selection.
    /// </summary>
    public CursorPos SelectionUpper => CursorPos.Max(_selectionStart, _cursorPosition);

    /// <summary>
    /// The amount of chars selected by the active selection.
    /// </summary>
    public int SelectionLength => Math.Abs(_selectionStart.Index - _cursorPosition.Index);

    // TODO: cache
    public int TextLength => (int) Rope.CalcTotalLength(TextRope);

    public System.Range SelectionRange => (SelectionLower.Index)..(SelectionUpper.Index);

    /// <summary>
    /// A placeholder text to be displayed when no actual text is entered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This isn't editable by the user, but to make the internals simpler, it is still exposed as a rope.
    /// </para>
    /// <para>
    /// When set to null, the <c>:placeholder-shown</c> pseudo-class will not be applied when the actual text content
    /// is empty. If set to an empty text rope this does happen, but obviously no text is displayed either way.
    /// </para>
    /// </remarks>
    [ViewVariables(VVAccess.ReadWrite)]
    public Rope.Node? Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value;
            UpdatePseudoClass();
            QueueLineBreakUpdate();
        }
    }

    private bool IsPlaceholderVisible => Rope.IsNullOrEmpty(_textRope) && _placeholder != null;

    // Table used by cursor movement system, see below.
    private static readonly FrozenDictionary<BoundKeyFunction, MoveType> MoveTypeMap = new Dictionary<BoundKeyFunction, MoveType>
    {
        // @formatter:off
        { EngineKeyFunctions.TextCursorLeft,            MoveType.Left        },
        { EngineKeyFunctions.TextCursorRight,           MoveType.Right       },
        { EngineKeyFunctions.TextCursorUp,              MoveType.Up          },
        { EngineKeyFunctions.TextCursorDown,            MoveType.Down        },
        { EngineKeyFunctions.TextCursorWordLeft,        MoveType.LeftWord    },
        { EngineKeyFunctions.TextCursorWordRight,       MoveType.RightWord   },
        { EngineKeyFunctions.TextCursorBegin,           MoveType.BeginOfLine },
        { EngineKeyFunctions.TextCursorEnd,             MoveType.EndOfLine   },

        { EngineKeyFunctions.TextCursorSelectLeft,      MoveType.Left        | MoveType.SelectFlag },
        { EngineKeyFunctions.TextCursorSelectRight,     MoveType.Right       | MoveType.SelectFlag },
        { EngineKeyFunctions.TextCursorSelectUp,        MoveType.Up          | MoveType.SelectFlag },
        { EngineKeyFunctions.TextCursorSelectDown,      MoveType.Down        | MoveType.SelectFlag },
        { EngineKeyFunctions.TextCursorSelectWordLeft,  MoveType.LeftWord    | MoveType.SelectFlag },
        { EngineKeyFunctions.TextCursorSelectWordRight, MoveType.RightWord   | MoveType.SelectFlag },
        { EngineKeyFunctions.TextCursorSelectBegin,     MoveType.BeginOfLine | MoveType.SelectFlag },
        { EngineKeyFunctions.TextCursorSelectEnd,       MoveType.EndOfLine   | MoveType.SelectFlag },
        // @formatter:on
    }.ToFrozenDictionary();

    private void UpdateCursorShape()
        => DefaultCursorShape = Disabled || !_editable ? CursorShape.NotAllowed : CursorShape.IBeam;

    private void UpdatePseudoClass()
    {
        var shown = IsPlaceholderVisible;
        if (shown == _placeholderShown)
            return;

        _placeholderShown = shown;
        SetSuilPseudoStyleClass(SuilPseudoClass.PlaceholderShown, shown);
    }

    protected override void OnDisabledChanged()
    {
        base.OnDisabledChanged();

        CanKeyboardFocus = !Disabled;
        UpdateCursorShape();

        if (Disabled && HasKeyboardFocus())
            ReleaseKeyboardFocus();
    }

    protected override void OnTextStyleChanged()
    {
        base.OnTextStyleChanged();

        QueueLineBreakUpdate();
        InvalidateMeasure();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Handled || Disabled)
            return;

        var doCursorVisible = true;

        if (MoveTypeMap.TryGetValue(args.Function, out var moveType))
        {
            // Most movement operations like normal vs word-bound work the same with or without an active text
            // selection. To allow sharing this code, we map key functions to a flag that defines the actual operation.

            var selectFlag = (moveType & MoveType.SelectFlag) != 0;
            var newPos = CalcCursorMove(moveType & MoveType.ActionMask, selectFlag, out var keepH);

            _cursorPosition = newPos;

            // If not selecting text, keep selection start at cursor (possibly breaking an active selection).
            if (!selectFlag)
                _selectionStart = _cursorPosition;

            if (!keepH)
                InvalidateHorizontalCursorPos();

            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextBackspace)
        {
            if (Editable)
            {
                var oldText = _textRope;
                var changed = false;

                if (_selectionStart != _cursorPosition)
                {
                    TextRope = Rope.Delete(oldText, SelectionLower.Index, SelectionLength);
                    _cursorPosition = SelectionLower;
                    changed = true;
                }
                else if (_cursorPosition.Index != 0)
                {
                    var remPos = _cursorPosition.Index - 1;
                    var remAmt = 1;

                    // If this is a low surrogate remove two chars to remove the whole pair.
                    if (char.IsLowSurrogate(Rope.Index(oldText, remPos)))
                    {
                        remPos -= 1;
                        remAmt = 2;
                    }

                    TextRope = Rope.Delete(oldText, remPos, remAmt);
                    _cursorPosition.Index -= remAmt;
                    changed = true;
                }

                if (changed)
                {
                    _selectionStart = _cursorPosition;
                    OnTextChanged?.Invoke(new TextEditEventArgs(this, _textRope));
                }

                InvalidateHorizontalCursorPos();

                args.Handle();
            }
        }
        else if (args.Function == EngineKeyFunctions.TextDelete)
        {
            if (Editable)
            {
                var changed = false;

                if (_selectionStart != _cursorPosition)
                {
                    TextRope = Rope.Delete(TextRope, SelectionLower.Index, SelectionLength);
                    _cursorPosition = SelectionLower;
                    changed = true;
                }
                else if (_cursorPosition.Index < TextLength)
                {
                    var remAmt = 1;
                    if (char.IsHighSurrogate(Rope.Index(TextRope, _cursorPosition.Index)))
                        remAmt = 2;

                    TextRope = Rope.Delete(TextRope, _cursorPosition.Index, remAmt);
                    changed = true;
                }

                if (changed)
                {
                    _selectionStart = _cursorPosition;
                    OnTextChanged?.Invoke(new TextEditEventArgs(this, _textRope));
                }

                InvalidateHorizontalCursorPos();

                args.Handle();
            }
        }
        else if (args.Function == EngineKeyFunctions.TextWordBackspace)
        {
            if (Editable)
            {
                var changed = false;

                // If there is a selection, we just delete the selection. Otherwise we delete the previous word.
                if (_selectionStart != _cursorPosition)
                {
                    TextRope = Rope.Delete(TextRope, SelectionLower.Index, SelectionLength);
                    _cursorPosition = SelectionLower;
                    changed = true;
                }
                else if (_cursorPosition.Index != 0)
                {
                    using var runes = Rope.EnumerateRunesReverse(TextRope, _cursorPosition.Index).GetEnumerator();
                    var remAmt = -RfTextEditShared.PrevWordPosition(runes);

                    TextRope = Rope.Delete(TextRope, _cursorPosition.Index - remAmt, remAmt);
                    _cursorPosition.Index -= remAmt;
                    changed = true;
                }

                if (changed)
                {
                    _selectionStart = _cursorPosition;
                    OnTextChanged?.Invoke(new TextEditEventArgs(this, _textRope));
                }

                InvalidateHorizontalCursorPos();
                args.Handle();
            }
        }
        else if (args.Function == EngineKeyFunctions.TextWordDelete)
        {
            if (Editable)
            {
                var changed = false;

                // If there is a selection, we just delete the selection. Otherwise we delete the next word.
                if (_selectionStart != _cursorPosition)
                {
                    TextRope = Rope.Delete(TextRope, SelectionLower.Index, SelectionLength);
                    _cursorPosition = SelectionLower;
                    changed = true;
                }
                else if (_cursorPosition.Index < TextLength)
                {
                    using var runes = Rope.EnumerateRunes(TextRope, _cursorPosition.Index).GetEnumerator();
                    var endWord = _cursorPosition.Index + RfTextEditShared.EndWordPosition(runes);

                    TextRope = Rope.Delete(TextRope, _cursorPosition.Index, endWord - _cursorPosition.Index);
                    changed = true;
                }

                if (changed)
                {
                    _selectionStart = _cursorPosition;
                    OnTextChanged?.Invoke(new TextEditEventArgs(this, _textRope));
                }

                InvalidateHorizontalCursorPos();
                args.Handle();
            }
        }
        else if (args.Function == EngineKeyFunctions.TextNewline)
        {
            if (Editable && (MaxLines == 0 || GetLineCount() < MaxLines))
            {
                InsertAtCursor("\n");
                InvalidateHorizontalCursorPos();
            }

            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextSelectAll)
        {
            _cursorPosition = new CursorPos(TextLength, LineBreakBias.Bottom);
            _selectionStart = new CursorPos(0, LineBreakBias.Top);

            InvalidateHorizontalCursorPos();

            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.TextCursorSelect)
        {
            _mouseSelectingText = true;
            _lastMouseSelectPos = ContentPosition(args.RelativePosition);

            // Find closest cursor position under mouse.
            _cursorPosition = GetIndexAtPos(_lastMouseSelectPos);

            if (args.Function != EngineKeyFunctions.TextCursorSelect)
                _selectionStart = _cursorPosition;

            InvalidateHorizontalCursorPos();

            args.Handle();

            doCursorVisible = false;
        }
        else if (args.Function == EngineKeyFunctions.TextCopy)
        {
            var text = Rope.CollapseSubstring(TextRope, SelectionRange);
            if (text.Length != 0)
                _clipboard.SetText(text);

            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextCut)
        {
            if (Editable || SelectionLower != SelectionUpper)
            {
                var text = Rope.CollapseSubstring(TextRope, SelectionRange);
                if (text.Length != 0)
                    _clipboard.SetText(text);

                InsertAtCursor("");
            }

            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextPaste)
        {
            if (Editable)
                Paste();

            args.Handle();
            doCursorVisible = false;
        }
        else if (args.Function == EngineKeyFunctions.TextReleaseFocus)
        {
            ReleaseKeyboardFocus();
            args.Handle();
            return;
        }

        if (!args.Handled)
            return;

        // Reset this so the cursor is always visible immediately after a keybind is pressed.
        _blink.Reset();

        if (doCursorVisible)
            EnsureCursorVisible();
    }

    // Happens asynchronously, be aware.
    private async void Paste()
    {
        var text = await _clipboard.GetText();
        if (text == null)
            return;

        InsertAtCursor(text);
        EnsureCursorVisible();
    }

    private void CacheHorizontalCursorPos(CursorPos pos)
    {
        EnsureLineBreaksUpdated();

        if (_horizontalCursorPos != null)
            return;

        _horizontalCursorPos = GetHorizontalPositionAtIndex(pos);
    }

    /// <summary>
    /// Calculate the position the cursor should move to with a certain move.
    /// </summary>
    /// <remarks>
    /// This method is not pure: while it does not modify the actual cursor position yet,
    /// only calculating the next position, it still calls <c>CacheHorizontalCursorPos</c> manually.
    /// </remarks>
    /// <param name="type">The type of move the cursor is doing.</param>
    /// <param name="select">Whether a selection is being expanded.</param>
    /// <param name="keepHorizontalCursorPos">
    /// Whether the cached horizontal cursor position must be kept instead of being invalidated.
    /// </param>
    /// <returns>The new position of the cursor in the text.</returns>
    private CursorPos CalcCursorMove(MoveType type, bool select, out bool keepHorizontalCursorPos)
    {
        DebugTools.Assert(BitOperations.PopCount((uint) type) == 1, "Only a single movement bit may be set in the type");

        keepHorizontalCursorPos = false;

        var breakingSelection = _selectionStart != _cursorPosition && !select;
        switch (type)
        {
            case MoveType.Left:
            {
                // If we're breaking an active selection, move to the lower side of it.
                if (breakingSelection)
                    return SelectionLower;

                var (_, lineStart, _) = GetLineForCursorPos(_cursorPosition);

                if (_cursorPosition.Bias == LineBreakBias.Bottom && _cursorPosition.Index == lineStart)
                {
                    // Swap cursor bias around to make the cursor appear at the end of the previous line.
                    // This maintains the index, it's the same position in the text.
                    return _cursorPosition with { Bias = LineBreakBias.Top };
                }

                var newPos = CursorShiftedLeft();
                // Explicit newlines work kinda funny with bias, so keep it at top there.
                var bias = _cursorPosition.Index == TextLength || Rope.Index(TextRope, newPos) == '\n'
                    ? LineBreakBias.Top
                    : LineBreakBias.Bottom;

                return new CursorPos(newPos, bias);
            }
            case MoveType.Right:
            {
                if (breakingSelection)
                    return SelectionUpper;

                var (_, _, lineEnd) = GetLineForCursorPos(_cursorPosition);
                // Explicit newlines work kinda funny with bias, so keep it at top there.
                if (_cursorPosition.Bias == LineBreakBias.Top
                    && _cursorPosition.Index == lineEnd
                    && _cursorPosition.Index != TextLength
                    && Rope.Index(TextRope, _cursorPosition.Index) != '\n')
                {
                    // Swap cursor bias around to make the cursor appear at the start of the next line.
                    // This maintains the index, it's the same position in the text.
                    return _cursorPosition with { Bias = LineBreakBias.Bottom };
                }

                return new CursorPos(CursorShiftedRight(), LineBreakBias.Top);
            }
            case MoveType.LeftWord:
            {
                using var runes = Rope.EnumerateRunesReverse(TextRope, _cursorPosition.Index).GetEnumerator();
                var pos = _cursorPosition.Index + RfTextEditShared.PrevWordPosition(runes);

                return new CursorPos(pos, LineBreakBias.Bottom);
            }
            case MoveType.RightWord:
            {
                using var runes = Rope.EnumerateRunes(TextRope, _cursorPosition.Index).GetEnumerator();
                var pos = _cursorPosition.Index + RfTextEditShared.EndWordPosition(runes);

                return new CursorPos(pos, LineBreakBias.Bottom);
            }
            case MoveType.Up:
            {
                var cursor = _cursorPosition;
                if (breakingSelection)
                {
                    // If we're in a selection, we move from the selection LOWER upwards, not the cursor position.
                    InvalidateHorizontalCursorPos();

                    cursor = SelectionLower;
                }

                CacheHorizontalCursorPos(cursor);
                DebugTools.Assert(_horizontalCursorPos.HasValue, "Horizontal cursor pos must be cached!");

                var (line, _, _) = GetLineForCursorPos(cursor);

                // We're on the top line already, move to the start of it instead.
                if (line == 0)
                    return new CursorPos(0, LineBreakBias.Top);

                keepHorizontalCursorPos = true;

                return GetIndexAtHorizontalPos(line - 1, _horizontalCursorPos!.Value);
            }
            case MoveType.Down:
            {
                var cursor = _cursorPosition;
                if (breakingSelection)
                {
                    // If we're in a selection, we move from the selection UPPER downwards, not the cursor position.
                    InvalidateHorizontalCursorPos();

                    cursor = SelectionUpper;
                }

                CacheHorizontalCursorPos(cursor);
                DebugTools.Assert(_horizontalCursorPos.HasValue, "Horizontal cursor pos must be cached!");

                var (line, _, _) = GetLineForCursorPos(cursor);

                // On the last line already, move to the end of it.
                if (line == _lineBreaks.Count)
                    return new CursorPos(TextLength, LineBreakBias.Top);

                keepHorizontalCursorPos = true;

                return GetIndexAtHorizontalPos(line + 1, _horizontalCursorPos!.Value);
            }
            case MoveType.BeginOfLine:
            {
                var (_, lineStart, _) = GetLineForCursorPos(_cursorPosition);
                if (Rope.Index(TextRope, lineStart) == '\n')
                    lineStart += 1;

                return new CursorPos(lineStart, LineBreakBias.Bottom);
            }
            case MoveType.EndOfLine:
            {
                var (_, _, lineEnd) = GetLineForCursorPos(_cursorPosition);
                return new CursorPos(lineEnd, LineBreakBias.Top);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    [Pure]
    private int CursorShiftedLeft()
    {
        if (_cursorPosition.Index == 0)
            return _cursorPosition.Index;

        return (int) Rope.RuneShiftLeft(_cursorPosition.Index, _textRope);
    }

    [Pure]
    private int CursorShiftedRight()
    {
        if (_cursorPosition.Index == TextLength)
            return _cursorPosition.Index;

        return (int) Rope.RuneShiftRight(_cursorPosition.Index, _textRope);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.TextCursorSelect)
            _mouseSelectingText = false;
    }

    private void InvalidateHorizontalCursorPos()
    {
        _horizontalCursorPos = null;
    }

    protected override void TextEntered(GUITextEnteredEventArgs args)
    {
        base.TextEntered(args);

        if (!Editable || Disabled)
            return;

        InsertAtCursor(args.Text);
        _blink.Reset();
        EnsureCursorVisible();
    }

    protected override void TextEditing(GUITextEditingEventArgs args)
    {
        base.TextEditing(args);

        if (!Editable || Disabled)
            return;

        var ev = args.Event;
        var startChars = ev.GetStartChars();

        // Just break an active composition and build it anew to handle in-progress ones.
        AbortIme();

        if (ev.Text != "")
        {
            // Delete the active text selection.
            if (_selectionStart != _cursorPosition)
                InsertAtCursor("");

            var startPos = _cursorPosition;
            TextRope = Rope.Insert(TextRope, startPos.Index, ev.Text);
            OnTextChanged?.Invoke(new TextEditEventArgs(this, _textRope));

            _selectionStart = _cursorPosition = new CursorPos(startPos.Index + startChars, LineBreakBias.Top);
            _imeData = (startPos, ev.Text.Length);
        }

        EnsureCursorVisible();
    }

    private void AbortIme(bool delete = true)
    {
        if (!_imeData.HasValue)
            return;

        if (delete)
        {
            var (imeStart, imeLength) = _imeData.Value;

            TextRope = Rope.Delete(TextRope, imeStart.Index, imeLength);

            _selectionStart = _cursorPosition = imeStart;
        }

        _imeData = null;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inset = ContentInset;
        var scale = UIScale;

        VScrollBar.Measure(availableSize);

        return new Vector2(inset.Left + inset.Right, TextStyle.Font.GetLineHeight(scale) / scale + inset.Top + inset.Bottom);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var inset = ContentInset;
        var area = new UIBox2(
            inset.Left,
            inset.Top,
            MathF.Max(inset.Left, finalSize.X - inset.Right),
            MathF.Max(inset.Top, finalSize.Y - inset.Bottom));

        _contentSize = area.Size;
        _renderBox.Arrange(area);

        // The scrollbar is drawn over the text, so it never changes the width the text is wrapped to.
        var barWidth = MathF.Min(VScrollBar.DesiredSize.X, area.Width);
        VScrollBar.Arrange(new UIBox2(area.Right - barWidth, area.Top, area.Right, area.Bottom));

        var page = _contentSize.Y * UIScale;
        if (!MathHelper.CloseTo(VScrollBar.Page, page))
            VScrollBar.Page = page;

        UpdateLineBreaks((int) (_contentSize.X * UIScale));

        return finalSize;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        EnsureLineBreaksUpdated();
        UpdateScrollBarVisibility();

        _blink.FrameUpdate(args);

        if (!_mouseSelectingText)
            return;

        _cursorPosition = GetIndexAtPos(_lastMouseSelectPos);

        // Only move scrollbar when the cursor is dragging above/below the text control.
        if (_lastMouseSelectPos.Y < 0 || _lastMouseSelectPos.Y > _contentSize.Y)
            EnsureCursorVisible();
    }

    private void UpdateScrollBarVisibility()
    {
        var visible = ScrollbarWidth != SuilScrollbarWidth.None && OverflowY switch
        {
            SuilOverflow.Scroll => true,
            SuilOverflow.Hidden or SuilOverflow.Clip => false,
            _ => VScrollBar.CanScroll,
        };

        if (VScrollBar.Visible != visible)
            VScrollBar.Visible = visible;
    }

    /// <summary>
    /// The position relative to the area of the text, from the position relative to the control.
    /// </summary>
    private Vector2 ContentPosition(Vector2 relative)
    {
        var inset = ContentInset;
        return relative - new Vector2(inset.Left, inset.Top);
    }

    internal void QueueLineBreakUpdate()
    {
        _lineUpdateQueued = true;
    }

    private void EnsureLineBreaksUpdated()
    {
        if (_lineUpdateQueued)
            UpdateLineBreaks((int) (_contentSize.X * UIScale));
    }

    public void InsertAtCursor(string text)
    {
        var lower = SelectionLower.Index;
        var upper = SelectionUpper.Index;

        TextRope = Rope.ReplaceSubstring(TextRope, lower, upper - lower, text);
        OnTextChanged?.Invoke(new TextEditEventArgs(this, _textRope));

        _selectionStart = _cursorPosition = new CursorPos(lower + text.Length, LineBreakBias.Top);
    }

    private void UpdateLineBreaks(int pixelWidth)
    {
        _lineBreaks.Clear();
        _wrapBreaks.Clear();
        InvalidateHorizontalCursorPos();

        var style = TextStyle;
        var font = style.Font;
        var scale = UIScale;

        var wordWrap = new RfWordWrap(pixelWidth, style.WordBreak, style.OverflowWrap, _wrapBreaks);
        var aborted = false;

        foreach (var rune in Rope.EnumerateRunes(GetDisplayRope()))
        {
            var skip = wordWrap.NextRune(rune);

            // The word wrap counts runes, we count chars.
            wordWrap.NextBreakIndexCounter += rune.Utf16SequenceLength - 1;

            if (skip)
                continue;

            // Uh just skip unknown characters I guess.
            if (!font.TryGetCharMetrics(rune, scale, out var metrics))
                continue;

            wordWrap.NextMetrics(metrics, out var abort);
            if (!abort)
                continue;

            aborted = true;
            break;
        }

        if (!aborted)
            wordWrap.FinalizeText();

        foreach (var line in _wrapBreaks)
        {
            // The end of the last line.
            if (line.Index != int.MaxValue)
                _lineBreaks.Add(line.Index);
        }

        if (aborted)
            return;

        // Update scroll bar max size.
        var max = Math.Max(VScrollBar.Page, GetLineCount() * font.GetLineHeight(scale));
        if (!MathHelper.CloseTo(VScrollBar.MaxValue, max))
            VScrollBar.MaxValue = max;

        _lineUpdateQueued = false;
    }

    /// <summary>
    /// Get the rope of text actually being displayed. This may be the placeholder.
    /// </summary>
    private Rope.Node GetDisplayRope()
    {
        if (!Rope.IsNullOrEmpty(_textRope))
            return _textRope;

        return _placeholder ?? Rope.Leaf.Empty;
    }

    /// <summary>
    /// Returns the number of lines of text currently in the textbox.
    /// </summary>
    [Pure]
    public int GetLineCount()
    {
        return _lineBreaks.Count + 1;
    }

    /// <param name="position">Position relative to the area of the text.</param>
    private CursorPos GetIndexAtPos(Vector2 position)
    {
        EnsureLineBreaksUpdated();

        var clickPos = position * UIScale;
        clickPos.Y += VScrollBar.Value;

        var lineHeight = TextStyle.Font.GetLineHeight(UIScale);
        var lineIndex = (int) (clickPos.Y / lineHeight);

        return GetIndexAtHorizontalPos(lineIndex, position.X);
    }

    private CursorPos GetIndexAtHorizontalPos(int line, float horizontalPos)
    {
        // If the placeholder is visible, this function does not return correct results because it looks at TextRope,
        // but _lineBreaks is configured for the display rope.
        // Bail out early in this case, the function is not currently used in any situation in any location
        // where something else is desired if the placeholder is visible.
        if (IsPlaceholderVisible)
            return default;

        var right = _contentSize.X * UIScale;
        var font = TextStyle.Font;
        var uiScale = UIScale;
        horizontalPos *= uiScale;

        (int, int) FindVerticalLine()
        {
            // Step one: find the vertical line containing the mouse position.

            // Below the last line, return the far end of the last line then.
            if (line > _lineBreaks.Count)
                return (TextLength, TextLength);

            // Above the first line, clamp.
            if (line < 0)
                return (0, 0);

            return (
                line == 0 ? 0 : _lineBreaks[line - 1],
                _lineBreaks.Count == line ? TextLength : _lineBreaks[line]
            );
        }

        // textIdx = start index on the vertical line we're on.
        // breakIdx = where the next line starts.
        var (textIdx, breakIdx) = FindVerticalLine();

        var chrPosX = 0f;
        var lastChrPosX = 0f;
        var index = textIdx;
        foreach (var rune in Rope.EnumerateRunes(TextRope, textIdx))
        {
            if (index >= breakIdx)
                break;

            if (!font.TryGetCharMetrics(rune, uiScale, out var metrics))
            {
                index += rune.Utf16SequenceLength;
                continue;
            }

            if (chrPosX > horizontalPos)
                break;

            lastChrPosX = chrPosX;
            chrPosX += metrics.Advance;
            index += rune.Utf16SequenceLength;

            if (chrPosX > right)
                break;
        }

        // Distance between the right side of the glyph overlapping the mouse and the mouse.
        var distanceRight = chrPosX - horizontalPos;
        // Same but left side.
        var distanceLeft = horizontalPos - lastChrPosX;
        // If the mouse is closer to the left of the glyph we lower the index one, so we select before that glyph.
        if (index > 0 && distanceRight > distanceLeft)
            index = (int) Rope.RuneShiftLeft(index, TextRope);

        return new CursorPos(index, index == textIdx ? LineBreakBias.Bottom : LineBreakBias.Top);
    }

    private float GetHorizontalPositionAtIndex(CursorPos pos)
    {
        EnsureLineBreaksUpdated();

        var hPos = 0;
        var font = TextStyle.Font;
        var uiScale = UIScale;

        var (_, lineStart, _) = GetLineForCursorPos(pos);
        using var runeEnumerator = Rope.EnumerateRunes(TextRope, lineStart).GetEnumerator();

        var i = lineStart;
        while (i < pos.Index && runeEnumerator.MoveNext())
        {
            var rune = runeEnumerator.Current;
            if (font.TryGetCharMetrics(rune, uiScale, out var metrics))
                hPos += metrics.Advance;

            i += rune.Utf16SequenceLength;
        }

        return hPos / uiScale;
    }

    private (int lineIdx, int lineStart, int lineEnd) GetLineForCursorPos(CursorPos pos)
    {
        DebugTools.Assert(pos.Index >= 0);

        EnsureLineBreaksUpdated();

        if (_lineBreaks.Count == 0)
            return (0, 0, TextLength);

        for (var i = 0; i < _lineBreaks.Count; i++)
        {
            var lineIdx = _lineBreaks[i];
            if (pos.Bias == LineBreakBias.Bottom ? lineIdx > pos.Index : lineIdx >= pos.Index)
            {
                // First line.
                if (i == 0)
                    return (0, 0, lineIdx);

                return (i, _lineBreaks[i - 1], lineIdx);
            }
        }

        // Position is on last line.
        return (_lineBreaks.Count, _lineBreaks[^1], TextLength);
    }

    private int GetStartOfLine(int lineIndex)
    {
        // First line: start of text.
        if (lineIndex <= 0)
            return 0;

        // Past the last line: just put it at text end so nothing happens I guess.
        if (lineIndex > _lineBreaks.Count)
            return TextLength;

        return _lineBreaks[lineIndex - 1];
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _lastMouseSelectPos = ContentPosition(args.RelativePosition);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (MathHelper.CloseToPercent(0, args.Delta.Y) || OverflowY is SuilOverflow.Hidden or SuilOverflow.Clip)
            return;

        VScrollBar.ValueTarget -= GetScrollSpeed() * args.Delta.Y;
    }

    [Pure]
    private float GetScrollSpeed()
    {
        return TextStyle.Font.GetLineHeight(UIScale) * 2;
    }

    private void EnsureCursorVisible()
    {
        EnsureLineBreaksUpdated();

        var font = TextStyle.Font;

        var scrollOffset = VScrollBar.Value;
        var (cursorLine, _, _) = GetLineForCursorPos(_cursorPosition);

        var cursorMargin = font.GetLineHeight(UIScale) * 1.5f;
        var (lineT, lineB) = GetBoundsOfLine(cursorLine);

        // Give the cursor some margin so it's not *right* up at the visible edge.
        lineT -= cursorMargin;
        lineB += cursorMargin;

        // Vertical boundaries of the vertical section of text.
        var height = _contentSize.Y * UIScale;
        var visibleT = scrollOffset;
        var visibleB = scrollOffset + height;

        // Make the scroll bar move to a position where the cursor is visible within margin.
        if (lineT < visibleT)
        {
            // Part of the line is ABOVE the visible region, move scrollbar UP.
            VScrollBar.ValueTarget = lineT;
        }
        else if (lineB > visibleB)
        {
            // Part of the line is BELOW the visible region, move scrollbar DOWN.
            VScrollBar.ValueTarget = lineB - height;
        }
    }

    private (float start, float end) GetBoundsOfLine(int line)
    {
        var lineHeight = TextStyle.Font.GetLineHeight(UIScale);
        return (lineHeight * line, lineHeight * (line + 1));
    }

    protected override void KeyboardFocusEntered()
    {
        base.KeyboardFocusEntered();

        _blink.Reset();

        if (Editable)
            Root?.Window?.TextInputStart();
    }

    protected override void KeyboardFocusExited()
    {
        base.KeyboardFocusExited();

        Root?.Window?.TextInputStop();
        AbortIme(delete: false);
    }

    /// <summary>
    /// Sub-control responsible for doing the actual rendering work.
    /// </summary>
    /// <remarks>
    /// This is a sub-control to use <see cref="Control.RectClipContent"/>.
    /// </remarks>
    private sealed class RenderBox : Control
    {
        private readonly RfTextEdit _master;

        public RenderBox(RfTextEdit master)
        {
            _master = master;

            RectClipContent = true;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var drawBox = PixelSizeBox;
            var style = _master.TextStyle;
            var font = style.Font;
            var renderedTextColor = style.Color;

            var scrollOffset = -_master.VScrollBar.Value;

            var scale = UIScale;
            var baseLine = new Vector2(0, scrollOffset + font.GetAscent(scale));
            var height = font.GetLineHeight(scale);
            var descent = font.GetDescent(scale);

            var viewT = -scrollOffset;

            var startLineIndex = (int) (viewT / height);
            var startIdx = _master.GetStartOfLine(startLineIndex);

            var lineBreakIndex = startLineIndex;
            var count = startIdx;

            var selectionLower = _master.SelectionLower;
            var selectionUpper = _master.SelectionUpper;

            baseLine.Y += startLineIndex * height;

            int? selectStartPos = null;
            int? selectEndPos = null;
            var selecting = false;

            var imeStartIndex = -1;
            var imeEndIndex = -1;

            int? imeStartPos = null;
            int? imeEndPos = null;
            var imeing = false;

            if (_master._imeData.HasValue)
            {
                var (start, length) = _master._imeData.Value;
                imeStartIndex = start.Index;
                imeEndIndex = imeStartIndex + length;

                if (imeStartIndex < startIdx && imeEndIndex > startIdx)
                {
                    imeing = true;
                    imeStartPos = 0;
                }
            }

            if (selectionLower.Index < startIdx && selectionUpper.Index > startIdx)
            {
                selecting = true;
                selectStartPos = 0;
            }

            foreach (var rune in Rope.EnumerateRunes(_master.GetDisplayRope(), startIdx))
            {
                CheckDrawCursors(LineBreakBias.Top);

                if (lineBreakIndex < _master._lineBreaks.Count
                    && _master._lineBreaks[lineBreakIndex] == count)
                {
                    // Line break.
                    PostDrawLine();

                    baseLine = new Vector2(drawBox.Left, baseLine.Y + height);
                    lineBreakIndex += 1;

                    selectStartPos = selecting ? 0 : null;
                    selectEndPos = null;

                    imeStartPos = imeing ? 0 : null;
                    imeEndPos = null;

                    // Past the bottom of the visible area of the screen: no need to render anything else.
                    if (baseLine.Y - height > drawBox.Height)
                        break;
                }

                CheckDrawCursors(LineBreakBias.Bottom);

                baseLine.X += font.DrawChar(handle, rune, baseLine, scale, renderedTextColor);

                count += rune.Utf16SequenceLength;
            }

            // Also draw cursor if it's at the very end.
            CheckDrawCursors(LineBreakBias.Bottom);
            CheckDrawCursors(LineBreakBias.Top);
            PostDrawLine();

            void CheckDrawCursors(LineBreakBias bias)
            {
                var pos = new CursorPos(count, bias);

                if (_master.HasKeyboardFocus() && _master._cursorPosition == pos)
                {
                    var cursorColor = _master.CaretColor ?? renderedTextColor;
                    cursorColor.A *= _master._blink.Opacity;

                    handle.DrawRect(
                        new UIBox2(
                            baseLine.X,
                            baseLine.Y - height + descent,
                            baseLine.X + 1,
                            baseLine.Y + descent),
                        cursorColor);

                    if (UserInterfaceManager.KeyboardFocused == _master && Root?.Window is { } window)
                    {
                        var box = (UIBox2i) new UIBox2(
                            drawBox.Left,
                            baseLine.Y - height + descent,
                            drawBox.Right,
                            baseLine.Y + descent);
                        var cursorOffset = baseLine.X - drawBox.Left;

                        window.TextInputSetRect(box.Translated(GlobalPixelPosition), (int) cursorOffset);
                    }
                }

                if (selectionLower == pos)
                {
                    selecting = true;
                    selectStartPos = (int) baseLine.X;
                }

                if (selectionUpper == pos)
                {
                    selecting = false;
                    selectEndPos = (int) baseLine.X;
                }

                if (count == imeStartIndex)
                {
                    imeing = true;
                    imeStartPos = (int) baseLine.X;
                }

                if (count == imeEndIndex)
                {
                    imeing = false;
                    imeEndPos = (int) baseLine.X;
                }
            }

            void PostDrawLine()
            {
                if (selectStartPos != null)
                {
                    var rect = new UIBox2(
                        selectStartPos.Value,
                        baseLine.Y - height + descent,
                        selectEndPos ?? baseLine.X,
                        baseLine.Y + descent
                    );

                    handle.DrawRect(rect, _master.SelectionColor);
                }

                if (_master._imeData.HasValue && imeStartPos.HasValue)
                {
                    // Draw IME underline.
                    var y = baseLine.Y + font.GetDescent(scale);
                    var rect = new UIBox2(
                        imeStartPos.Value,
                        y - 1,
                        imeEndPos ?? baseLine.X,
                        y
                    );

                    handle.DrawRect(rect, renderedTextColor);
                }
            }
        }
    }

    public sealed class TextEditEventArgs(RfTextEdit control, Rope.Node textRope) : EventArgs
    {
        public RfTextEdit Control { get; } = control;
        public Rope.Node TextRope { get; } = textRope;
    }

    /// <summary>
    /// Specifies which line the cursor is positioned at when on a word-wrapping break.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When words get pushed to a new line due to word-wrapping, a line break is tracked.
    /// For various reasons, people want to be able to place their cursor on both the end of the "top" line,
    /// as well as the start of the "bottom" line. These are however the same position in the source text,
    /// going by raw string indices at least. To allow the code to differentiate between these two positions,
    /// this bias value is tracked in all cursor positions.
    /// </para>
    /// <para>
    /// This is only for word-wrapping line breaks however. For explicit line breaks created with a newline character,
    /// the cursor bias should always be "top" so that everything works correctly.
    /// </para>
    /// </remarks>
    public enum LineBreakBias : byte
    {
        Top = 0,
        Bottom = 1,
    }

    /// <summary>
    /// Stores the necessary data for a position in the cursor of the text.
    /// </summary>
    /// <param name="Index">The index of the cursor in the text contents.</param>
    /// <param name="Bias">Which direction to bias the cursor to.</param>
    public record struct CursorPos(int Index, LineBreakBias Bias) : IComparable<CursorPos>
    {
        public static CursorPos Min(CursorPos a, CursorPos b)
        {
            return a.CompareTo(b) < 0 ? a : b;
        }

        public static CursorPos Max(CursorPos a, CursorPos b)
        {
            return a.CompareTo(b) > 0 ? a : b;
        }

        public int CompareTo(CursorPos other)
        {
            var indexComparison = Index.CompareTo(other.Index);
            if (indexComparison != 0)
                return indexComparison;

            // If two positions are at the same index, the one with bias top is considered earlier.
            return ((byte) Bias).CompareTo((byte) other.Bias);
        }

        public static bool operator <(CursorPos left, CursorPos right) => left.CompareTo(right) < 0;

        public static bool operator >(CursorPos left, CursorPos right) => left.CompareTo(right) > 0;

        public static bool operator <=(CursorPos left, CursorPos right) => left.CompareTo(right) <= 0;

        public static bool operator >=(CursorPos left, CursorPos right) => left.CompareTo(right) >= 0;
    }

    [Flags]
    private enum MoveType
    {
        // @formatter:off
        Left = 1 << 0,
        Right = 1 << 1,
        LeftWord = 1 << 2,
        RightWord = 1 << 3,
        Up = 1 << 4,
        Down = 1 << 5,
        BeginOfLine = 1 << 6,
        EndOfLine = 1 << 7,

        ActionMask = (1 << 16) - 1,
        SelectFlag = 1 << 16,
        // @formatter:on
    }
}
