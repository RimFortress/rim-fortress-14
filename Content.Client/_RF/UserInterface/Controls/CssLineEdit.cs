using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Content.Client._RF.CSS;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// Allows the user to input and modify a line of text, the <see cref="CssControl"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.LineEdit"/>.
/// </summary>
/// <remarks>
/// <para>
/// The text is drawn inside the padding and borders with the font, <c>color</c> and <c>line-height</c> of the
/// control. The cursor is <c>caret-color</c> (the text color by default), the selection is
/// <c>-rf-selection-color</c>. The control matches <c>:focus</c>, <c>:disabled</c>, <c>:read-only</c> and
/// <c>:placeholder-shown</c>, the latter is the way to style the placeholder (e.g. <c>color</c>).
/// </para>
/// <para>
/// <see cref="Text"/> hides <see cref="CssControl.Text"/>: it is the plain text the user edits, not markup.
/// Letter and word spacing don't apply to it.
/// </para>
/// </remarks>
[Virtual, UsedImplicitly]
public partial class CssLineEdit : CssControl
{
    [Dependency] private IConfigurationManager _cfgManager = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IClipboardManager _clipboard = null!;

    private const float MouseScrollDelay = 0.001f;

    // It is assumed that these two positions are NEVER inside a surrogate pair in the text buffer.
    private int _cursorPosition;
    private int _selectionStart;
    private string _text = "";
    private bool _editable = true;
    private string? _placeHolder;
    private bool _placeholderShown;

    private int _drawOffset;

    private CssTextEditShared.CursorBlink _blink;
    private readonly LineEditRenderBox _renderBox;

    private bool _mouseSelectingText;
    private float _lastMousePosition;

    private TimeSpan? _lastClickTime;
    private Vector2? _lastClickPosition;

    // The frame on which we got focus, to implement SelectAllOnFocus: otherwise there is no way to know whether
    // the KeyDown is the one that focused the control, and the text selection would stomp the behavior.
    private uint _focusedOnFrame;

    private (int start, int length)? _imeData;

    private bool IsPlaceHolderVisible
        => !(HidePlaceHolderOnFocus && HasKeyboardFocus()) && string.IsNullOrEmpty(_text) && _placeHolder != null;

    public event Action<LineEditEventArgs>? OnTextChanged;
    public event Action<LineEditEventArgs>? OnTextEntered;
    public event Action<LineEditEventArgs>? OnFocusEnter;
    public event Action<LineEditEventArgs>? OnFocusExit;
    public event Action<LineEditEventArgs>? OnTabComplete;
    public event Action<GUITextEnteredEventArgs>? OnTextTyped;
    public event Action<LineEditTextRemovedEventArgs>? OnTextRemoved;

    /// <summary>
    /// Determines whether the text gets changed by the input text.
    /// </summary>
    [PublicAPI]
    public Func<string, bool>? IsValid { get; set; }

    /// <summary>
    /// The text currently stored in the control. Setting this does not invoke <see cref="OnTextChanged"/>,
    /// use <see cref="SetText(string, bool)"/> for that.
    /// </summary>
    [ViewVariables, PublicAPI]
    public new string Text
    {
        get => _text;
        set => SetText(value);
    }

    /// <summary>
    /// Sets the current text, optionally invoking <see cref="OnTextChanged"/>.
    /// </summary>
    [PublicAPI]
    public void SetText(string value, bool invokeEvent = false)
    {
        if (value == _text)
            return;

        // Save cursor position or -1 for end.
        var cursorTarget = CursorPosition == _text.Length ? -1 : CursorPosition;

        if (!InternalSetText(value))
            return;

        var clamped = MathHelper.Clamp(cursorTarget == -1 ? _text.Length : cursorTarget, 0, _text.Length);
        while (clamped < _text.Length && !Rune.TryGetRuneAt(_text, clamped, out _))
        {
            clamped++;
        }

        _cursorPosition = clamped;
        _selectionStart = _cursorPosition;
        UpdatePseudoClass();

        if (invokeEvent)
            OnTextChanged?.Invoke(new LineEditEventArgs(this, _text));
    }

    [PublicAPI]
    public void ForceSubmitText()
    {
        OnTextEntered?.Invoke(new LineEditEventArgs(this, _text));
    }

    [PublicAPI]
    public string SelectedText => _text.AsSpan(SelectionLower, SelectionLength).ToString();

    [PublicAPI]
    public int SelectionLength => Math.Abs(_selectionStart - _cursorPosition);

    [ViewVariables, PublicAPI]
    public bool Editable
    {
        get => _editable;
        set
        {
            _editable = value;
            UpdateCursorShape();
            SetCssPseudoStyleClass(ReadOnlyPseudoClass, !value);
        }
    }

    [ViewVariables, PublicAPI]
    public string? PlaceHolder
    {
        get => _placeHolder;
        set
        {
            _placeHolder = value;
            UpdatePseudoClass();
        }
    }

    [PublicAPI]
    public int CursorPosition
    {
        get => _cursorPosition;
        set
        {
            var clamped = MathHelper.Clamp(value, 0, _text.Length);
            if (_text.Length != 0 && _text.Length != clamped && !Rune.TryGetRuneAt(_text, clamped, out _))
                throw new ArgumentException("Cannot set cursor inside surrogate pair.");

            _cursorPosition = clamped;
            _selectionStart = _cursorPosition;
        }
    }

    [PublicAPI]
    public int SelectionStart
    {
        get => _selectionStart;
        set
        {
            var clamped = MathHelper.Clamp(value, 0, _text.Length);
            if (_text.Length != 0 && _text.Length != clamped && !Rune.TryGetRuneAt(_text, clamped, out _))
                throw new ArgumentException("Cannot set cursor inside surrogate pair.");

            _selectionStart = clamped;
        }
    }

    [PublicAPI]
    public int SelectionLower => Math.Min(_selectionStart, _cursorPosition);

    [PublicAPI]
    public int SelectionUpper => Math.Max(_selectionStart, _cursorPosition);

    [PublicAPI]
    public bool HidePlaceHolderOnFocus { get; set; }

    [PublicAPI]
    public bool IgnoreNext { get; set; }

    /// <summary>
    /// If true, all the text will be automatically selected whenever the control is focused.
    /// </summary>
    [PublicAPI]
    public bool SelectAllOnFocus { get; set; }

    public CssLineEdit()
    {
        IoCManager.InjectDependencies(this);

        MouseFilter = MouseFilterMode.Stop;
        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;

        DefaultCursorShape = CursorShape.IBeam;

        AddChild(_renderBox = new LineEditRenderBox(this));
    }

    [PublicAPI]
    public void Clear()
    {
        Text = "";
    }

    [PublicAPI]
    public void InsertAtCursor(string text)
    {
        // Strip newlines.
        text = text.Replace("\n", "");

        var lower = SelectionLower;
        var newContents = _text[..lower] + text + _text[SelectionUpper..];

        if (!InternalSetText(newContents))
            return;

        _selectionStart = _cursorPosition = lower + text.Length;
        OnTextChanged?.Invoke(new LineEditEventArgs(this, _text));
        UpdatePseudoClass();
    }

    /// <remarks>
    /// Does not fix cursor positions, those will have to be adjusted manually.
    /// </remarks>
    private bool InternalSetText(string newText)
    {
        if (IsValid != null && !IsValid(newText))
            return false;

        _text = newText;
        return true;
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
        InvalidateMeasure();
    }

    private void UpdateCursorShape()
        => DefaultCursorShape = Disabled ? CursorShape.Arrow : _editable ? CursorShape.IBeam : CursorShape.Arrow;

    private void UpdatePseudoClass()
    {
        var shown = IsPlaceHolderVisible;
        if (shown == _placeholderShown)
            return;

        _placeholderShown = shown;
        SetCssPseudoStyleClass(PlaceholderShownPseudoClass, shown);
    }

    /// <summary>
    /// The area text is drawn in, in pixels relative to the control.
    /// </summary>
    private UIBox2 ContentBoxPx
    {
        get
        {
            var inset = ContentInset;
            var scale = UIScale;
            var left = inset.Left * scale;
            var top = inset.Top * scale;

            // The control can be smaller than its padding and borders.
            return new UIBox2(
                left,
                top,
                MathF.Max(left, PixelWidth - inset.Right * scale),
                MathF.Max(top, PixelHeight - inset.Bottom * scale));
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        IgnoreNext = false;

        _blink.FrameUpdate(args);

        if (!_mouseSelectingText)
            return;

        var contentBox = ContentBoxPx;

        if (_lastMousePosition < contentBox.Left)
            _drawOffset = Math.Max(0, _drawOffset - (int) Math.Ceiling(args.DeltaSeconds / MouseScrollDelay));
        else if (_lastMousePosition > contentBox.Right)
            _drawOffset += (int) Math.Ceiling(args.DeltaSeconds / MouseScrollDelay); // Clamped when drawing.

        _cursorPosition = GetIndexAtPos(MathHelper.Clamp(_lastMousePosition, contentBox.Left, contentBox.Right));
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inset = ContentInset;
        var scale = UIScale;

        return new Vector2(inset.Left + inset.Right, TextStyle.Font.GetHeight(scale) / scale + inset.Top + inset.Bottom);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var inset = ContentInset;
        _renderBox.Arrange(new UIBox2(
            inset.Left,
            inset.Top,
            MathF.Max(inset.Left, finalSize.X - inset.Right),
            MathF.Max(inset.Top, finalSize.Y - inset.Bottom)));

        return finalSize;
    }

    protected override void TextEntered(GUITextEnteredEventArgs args)
    {
        base.TextEntered(args);

        if (!Editable || Disabled)
            return;

        if (IgnoreNext)
        {
            IgnoreNext = false;
            return;
        }

        InsertAtCursor(args.Text);
        OnTextTyped?.Invoke(args);
    }

    protected override void TextEditing(GUITextEditingEventArgs args)
    {
        base.TextEditing(args);

        if (!Editable || Disabled)
            return;

        // TODO: this ignores IsValid, as the engine's LineEdit does.
        var ev = args.Event;
        var startChars = ev.GetStartChars();

        // Just break an active composition and build it anew to handle in-progress ones.
        AbortIme();

        if (ev.Text == "")
            return;

        // Delete the active text selection.
        if (_selectionStart != _cursorPosition)
            InsertAtCursor("");

        var startPos = _cursorPosition;
        _text = _text[..startPos] + ev.Text + _text[startPos..];

        _selectionStart = _cursorPosition = startPos + startChars;
        _imeData = (startPos, ev.Text.Length);

        UpdatePseudoClass();
    }

    private void AbortIme(bool delete = true)
    {
        if (!_imeData.HasValue)
            return;

        if (delete)
        {
            var (imeStart, imeLength) = _imeData.Value;

            _text = _text.Remove(imeStart, imeLength);
            _selectionStart = _cursorPosition = imeStart;

            UpdatePseudoClass();
        }

        _imeData = null;
    }

    /// <summary>
    /// Removes the selection or, if there is none, the text <paramref name="remove"/> gives (start, length).
    /// </summary>
    private void RemoveText(Func<(int Start, int Length)?> remove)
    {
        if (!Editable)
            return;

        var oldText = _text;
        var cursor = _cursorPosition;
        var selectStart = _selectionStart;

        if (_selectionStart != _cursorPosition)
        {
            _text = _text.Remove(SelectionLower, SelectionLength);
            _cursorPosition = SelectionLower;
        }
        else if (remove() is { Length: > 0 } range)
        {
            _text = _text.Remove(range.Start, range.Length);
            _cursorPosition = range.Start;
        }
        else
            return;

        _selectionStart = _cursorPosition;
        OnTextChanged?.Invoke(new LineEditEventArgs(this, _text));
        UpdatePseudoClass();
        OnTextRemoved?.Invoke(new LineEditTextRemovedEventArgs(oldText, _text, cursor, selectStart));
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (Disabled)
            return;

        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.TextCursorSelect)
        {
            if (!HasKeyboardFocus())
                return;

            if (!HandleKey(args))
                return;
        }
        // Double-clicking. Clicks delay should be <= 250ms and the distance < 10 pixels.
        else if (args.Function == EngineKeyFunctions.UIClick && _lastClickPosition != null && _lastClickTime != null
                 && _timing.RealTime - _lastClickTime <= TimeSpan.FromMilliseconds(_cfgManager.GetCVar(CVars.DoubleClickDelay))
                 && (_lastClickPosition.Value - args.PointerLocation.Position).IsShorterThan(_cfgManager.GetCVar(CVars.DoubleClickRange)))
        {
            _lastClickTime = _timing.RealTime;
            _lastClickPosition = args.PointerLocation.Position;

            _lastMousePosition = args.RelativePosition.X;

            var index = GetIndexAtPos(args.RelativePosition.X);
            _selectionStart = CssTextEditShared.PrevWordPosition(_text, index);
            _cursorPosition = CssTextEditShared.EndWordPosition(_text, index);

            args.Handle();
        }
        else if (!(SelectAllOnFocus && _focusedOnFrame == _timing.CurFrame))
        {
            _lastClickTime = _timing.RealTime;
            _lastClickPosition = args.PointerLocation.Position;

            _mouseSelectingText = true;
            _lastMousePosition = args.RelativePosition.X;

            // Find closest cursor position under mouse.
            var index = GetIndexAtPos(args.RelativePosition.X);

            _cursorPosition = index;

            if (args.Function != EngineKeyFunctions.TextCursorSelect)
                _selectionStart = index;

            args.Handle();
        }

        // Reset this so the cursor is always visible immediately after a keybind is pressed.
        _blink.Reset();
    }

    /// <returns>False if the blink must not be reset (the focus was released).</returns>
    private bool HandleKey(GUIBoundKeyEventArgs args)
    {
        var function = args.Function;

        if (function == EngineKeyFunctions.TextBackspace)
        {
            RemoveText(() =>
            {
                if (_cursorPosition == 0)
                    return null;

                // If this is a low surrogate remove two chars to remove the whole pair.
                var amount = char.IsLowSurrogate(_text[_cursorPosition - 1]) ? 2 : 1;
                return (_cursorPosition - amount, amount);
            });
        }
        else if (function == EngineKeyFunctions.TextDelete)
        {
            RemoveText(() =>
            {
                if (_cursorPosition >= _text.Length)
                    return null;

                return (_cursorPosition, char.IsHighSurrogate(_text[_cursorPosition]) ? 2 : 1);
            });
        }
        else if (function == EngineKeyFunctions.TextWordBackspace)
        {
            RemoveText(() =>
            {
                var start = CssTextEditShared.PrevWordPosition(_text, _cursorPosition);
                return (start, _cursorPosition - start);
            });
        }
        else if (function == EngineKeyFunctions.TextWordDelete)
        {
            RemoveText(() => (_cursorPosition, CssTextEditShared.EndWordPosition(_text, _cursorPosition) - _cursorPosition));
        }
        else if (function == EngineKeyFunctions.TextCursorLeft)
        {
            if (_selectionStart != _cursorPosition)
            {
                _cursorPosition = _selectionStart = SelectionLower;
            }
            else
            {
                ShiftCursorLeft();
                _selectionStart = _cursorPosition;
            }
        }
        else if (function == EngineKeyFunctions.TextCursorRight)
        {
            if (_selectionStart != _cursorPosition)
            {
                _cursorPosition = _selectionStart = SelectionUpper;
            }
            else
            {
                ShiftCursorRight();
                _selectionStart = _cursorPosition;
            }
        }
        else if (function == EngineKeyFunctions.TextCursorWordLeft)
        {
            _selectionStart = _cursorPosition = CssTextEditShared.PrevWordPosition(_text, _cursorPosition);
        }
        else if (function == EngineKeyFunctions.TextCursorWordRight)
        {
            _selectionStart = _cursorPosition = CssTextEditShared.EndWordPosition(_text, _cursorPosition);
        }
        else if (function == EngineKeyFunctions.TextCursorBegin)
        {
            _selectionStart = _cursorPosition = 0;
        }
        else if (function == EngineKeyFunctions.TextCursorEnd)
        {
            _selectionStart = _cursorPosition = _text.Length;
        }
        else if (function == EngineKeyFunctions.TextCursorSelectLeft)
        {
            ShiftCursorLeft();
        }
        else if (function == EngineKeyFunctions.TextCursorSelectRight)
        {
            ShiftCursorRight();
        }
        else if (function == EngineKeyFunctions.TextCursorSelectWordLeft)
        {
            _cursorPosition = CssTextEditShared.PrevWordPosition(_text, _cursorPosition);
        }
        else if (function == EngineKeyFunctions.TextCursorSelectWordRight)
        {
            _cursorPosition = CssTextEditShared.EndWordPosition(_text, _cursorPosition);
        }
        else if (function == EngineKeyFunctions.TextCursorSelectBegin)
        {
            _cursorPosition = 0;
        }
        else if (function == EngineKeyFunctions.TextCursorSelectEnd)
        {
            _cursorPosition = _text.Length;
        }
        else if (function == EngineKeyFunctions.TextSubmit)
        {
            if (Editable)
                ForceSubmitText();
        }
        else if (function == EngineKeyFunctions.TextPaste)
        {
            if (Editable)
                Paste();
        }
        else if (function == EngineKeyFunctions.TextCut)
        {
            if (Editable || SelectionLower != SelectionUpper)
            {
                Copy();
                InsertAtCursor("");
            }
        }
        else if (function == EngineKeyFunctions.TextCopy)
        {
            Copy();
        }
        else if (function == EngineKeyFunctions.TextSelectAll)
        {
            _cursorPosition = _text.Length;
            _selectionStart = 0;
        }
        else if (function == EngineKeyFunctions.TextReleaseFocus)
        {
            ReleaseKeyboardFocus();
            args.Handle();
            return false;
        }
        else if (function == EngineKeyFunctions.TextTabComplete)
        {
            if (Editable)
                OnTabComplete?.Invoke(new LineEditEventArgs(this, _text));
        }
        else
        {
            return true;
        }

        args.Handle();
        return true;

        void ShiftCursorLeft()
        {
            if (_cursorPosition == 0)
                return;

            _cursorPosition -= 1;

            if (char.IsLowSurrogate(_text[_cursorPosition]))
                _cursorPosition -= 1;
        }

        void ShiftCursorRight()
        {
            if (_cursorPosition == _text.Length)
                return;

            _cursorPosition += 1;

            // We ALREADY moved, so a low surrogate here means the cursor is between a surrogate pair.
            if (_cursorPosition != _text.Length && char.IsLowSurrogate(_text[_cursorPosition]))
                _cursorPosition += 1;
        }
    }

    private async Task Paste()
    {
        var text = await _clipboard.GetText();
        InsertAtCursor(text);
    }

    private void Copy()
    {
        var text = SelectedText;

        if (!string.IsNullOrWhiteSpace(text))
            _clipboard.SetText(text);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.TextCursorSelect)
            _mouseSelectingText = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _lastMousePosition = args.RelativePosition.X;
    }

    private int GetIndexAtPos(float horizontalPos)
    {
        var contentBox = ContentBoxPx;
        var scale = UIScale;

        var clickPosX = horizontalPos * scale;

        var font = TextStyle.Font;
        var index = 0;
        var chrPosX = contentBox.Left - _drawOffset;
        var lastChrPostX = contentBox.Left - _drawOffset;
        foreach (var rune in _text.EnumerateRunes())
        {
            if (!font.TryGetCharMetrics(rune, scale, out var metrics))
            {
                index += rune.Utf16SequenceLength;
                continue;
            }

            if (chrPosX > clickPosX)
                break;

            lastChrPostX = chrPosX;
            chrPosX += metrics.Advance;
            index += rune.Utf16SequenceLength;

            if (chrPosX > contentBox.Right)
                break;
        }

        // Distance between the right side of the glyph overlapping the mouse and the mouse.
        var distanceRight = chrPosX - clickPosX;
        // Same but left side.
        var distanceLeft = clickPosX - lastChrPostX;
        // If the mouse is closer to the left of the glyph we lower the index one, so we select before that glyph.
        if (index > 0 && distanceRight > distanceLeft)
        {
            index -= 1;

            if (char.IsLowSurrogate(_text[index]))
                index -= 1;
        }

        return index;
    }

    /// <summary>
    /// Get offset from the left of the control to the left edge of the text glyph at the specified index.
    /// </summary>
    /// <remarks>
    /// The returned value can be outside the bounds of the control if the glyph is currently clipped off.
    /// </remarks>
    public float GetOffsetAtIndex(int index)
    {
        var contentBox = ContentBoxPx;
        var scale = UIScale;

        var font = TextStyle.Font;
        var i = 0;
        var chrPosX = contentBox.Left - _drawOffset;
        foreach (var rune in _text.EnumerateRunes())
        {
            if (i >= index)
                break;

            if (font.TryGetCharMetrics(rune, scale, out var metrics))
                chrPosX += metrics.Advance;

            i += rune.Utf16SequenceLength;
        }

        return chrPosX / scale;
    }

    protected override void KeyboardFocusEntered()
    {
        base.KeyboardFocusEntered();

        // Reset this so the cursor is always visible immediately after gaining focus.
        _blink.Reset();
        UpdatePseudoClass();
        OnFocusEnter?.Invoke(new LineEditEventArgs(this, _text));

        if (Editable)
            Root?.Window?.TextInputStart();

        _focusedOnFrame = _timing.CurFrame;
        if (SelectAllOnFocus)
        {
            CursorPosition = _text.Length;
            SelectionStart = 0;
        }
    }

    protected override void KeyboardFocusExited()
    {
        base.KeyboardFocusExited();

        UpdatePseudoClass();
        OnFocusExit?.Invoke(new LineEditEventArgs(this, _text));

        Root?.Window?.TextInputStop();

        AbortIme(delete: false);
    }

    public sealed class LineEditEventArgs(CssLineEdit control, string text) : EventArgs
    {
        public CssLineEdit Control { get; } = control;
        public string Text { get; } = text;
    }

    public sealed class LineEditTextRemovedEventArgs(
        string oldText,
        string newText,
        int oldCursorPosition,
        int oldSelectionStart) : EventArgs
    {
        public string OldText { get; } = oldText;
        public string NewText { get; } = newText;
        public int OldCursorPosition { get; } = oldCursorPosition;
        public int OldSelectionStart { get; } = oldSelectionStart;
    }

    /// <summary>
    /// Use a separate control to do the rendering to make use of RectClipContent,
    /// so that we can clip characters in half.
    /// </summary>
    private sealed class LineEditRenderBox : Control
    {
        private readonly CssLineEdit _master;

        public LineEditRenderBox(CssLineEdit master)
        {
            _master = master;

            RectClipContent = true;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            var contentBox = PixelSizeBox;
            var style = _master.TextStyle;
            var font = style.Font;
            var renderedTextColor = style.Color;

            var uiScale = UIScale;
            var offsetY = (contentBox.Height - font.GetHeight(uiScale)) / 2;

            var renderedText = _master.IsPlaceHolderVisible ? _master._placeHolder! : _master._text;
            DebugTools.AssertNotNull(renderedText);

            ref var drawOffset = ref _master._drawOffset;

            // Go through the entire text once to find length/positional data of cursor.
            var count = 0;
            var posX = 0;
            var actualCursorPosition = 0;
            var actualSelectionStartPosition = 0;

            var actualImeStartPosition = 0;
            var actualImeEndPosition = 0;

            var imeStartIndex = -1;
            var imeEndIndex = -1;

            if (_master._imeData.HasValue)
            {
                (imeStartIndex, var length) = _master._imeData.Value;
                imeEndIndex = imeStartIndex + length;
            }

            foreach (var chr in renderedText.EnumerateRunes())
            {
                if (!font.TryGetCharMetrics(chr, uiScale, out var metrics))
                {
                    count += 1;
                    continue;
                }

                posX += metrics.Advance;
                count += chr.Utf16SequenceLength;

                // NOTE: Due to the way this code works, these don't get triggered if the relevant positions are all
                // the way at the left of the control. That's fine, the position is 0 then, which is the default.
                if (count == _master._cursorPosition)
                    actualCursorPosition = posX;

                if (count == _master._selectionStart)
                    actualSelectionStartPosition = posX;

                if (count == imeStartIndex)
                    actualImeStartPosition = posX;

                if (count == imeEndIndex)
                    actualImeEndPosition = posX;
            }

            var totalLength = posX;

            // Shift drawOffset around to fill as much as possible.
            var end = totalLength - drawOffset;
            if (end + 1 < contentBox.Width)
                drawOffset = Math.Max(0, drawOffset - (contentBox.Width - end));

            // Shift drawOffset around so that cursor is always visible.
            if (actualCursorPosition < drawOffset)
                drawOffset -= drawOffset - actualCursorPosition;
            else if (actualCursorPosition >= contentBox.Width + drawOffset)
                drawOffset += actualCursorPosition - (contentBox.Width + drawOffset - 1);

            // Apply drawOffset to positional data.
            actualCursorPosition -= drawOffset;
            actualSelectionStartPosition -= drawOffset;

            // Actually render.
            var baseLine = (-drawOffset, offsetY + font.GetAscent(uiScale)) + contentBox.TopLeft;

            foreach (var rune in renderedText.EnumerateRunes())
            {
                if (!font.TryGetCharMetrics(rune, uiScale, out var metrics))
                    continue;

                // Past the right edge, not gonna render anything anymore.
                if (baseLine.X > contentBox.Right)
                    break;

                // Make sure we're not off the left edge of the box.
                if (baseLine.X + metrics.BearingX + metrics.Width >= contentBox.Left)
                    font.DrawChar(handle, rune, baseLine, uiScale, renderedTextColor);

                baseLine += (metrics.Advance, 0);
            }

            // Draw cursor/selection.
            if (_master.HasKeyboardFocus())
            {
                var selectionLower = Math.Min(actualSelectionStartPosition, actualCursorPosition);
                var selectionUpper = Math.Max(actualSelectionStartPosition, actualCursorPosition);

                if (selectionLower != selectionUpper)
                {
                    handle.DrawRect(
                        new UIBox2(selectionLower, contentBox.Top, selectionUpper, contentBox.Bottom),
                        _master.SelectionColor);
                }

                var cursorColor = _master.CaretColor ?? renderedTextColor;
                cursorColor.A *= _master._blink.Opacity;

                handle.DrawRect(
                    new UIBox2(actualCursorPosition, contentBox.Top, actualCursorPosition + 1, contentBox.Bottom),
                    cursorColor);

                if (Root?.Window is { } window)
                {
                    // Update IME position.
                    var imeBox = new UIBox2(contentBox.Left, contentBox.Top, contentBox.Right, contentBox.Bottom);

                    window.TextInputSetRect((UIBox2i) imeBox.Translated(GlobalPixelPosition), actualCursorPosition);
                }
            }

            // Draw IME underline if necessary.
            if (_master._imeData.HasValue)
            {
                var y = baseLine.Y + font.GetDescent(uiScale);
                var rect = new UIBox2(actualImeStartPosition, y - 1, actualImeEndPosition, y);

                handle.DrawRect(rect, renderedTextColor);
            }
        }
    }
}
