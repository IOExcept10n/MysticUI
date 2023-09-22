using FontStashSharp.RichText;
using MysticUI.Extensions;
using MysticUI.Extensions.Text;
using Stride.Core.Mathematics;
using Stride.Input;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents the basic functionality for the text input control.
    /// </summary>
    public abstract class TextBoxBase : Control
    {
        /// <summary>
        /// Defines the default limit of undo operations stack for the <see cref="TextBoxBase"/>.
        /// </summary>
        public const int DefaultUndoLimit = 100;

        private readonly RichTextLayout textLayout = new()
        {
            CalculateGlyphs = true,
            SupportsCommands = false
        };

        private UndoRedoStack UndoStack = new(DefaultUndoLimit);
        private UndoRedoStack RedoStack = new(DefaultUndoLimit);
        private bool suppressRedoStackReset;
        private Point internalScrollPosition;
        private Point lastCursorPosition;
        private bool isTouchDown;
        private float blinkDelay;
        private bool cursorOn;
        private bool acceptsReturn;
        private float blinkInterval = 0.5f;
        private string text = "";
        private string? placeholderText;
        private bool insertMode;
        private bool textWrapping;
        private int undoLimit = DefaultUndoLimit;
        private int cursorPosition;
        private bool isPassword;
        private char passwordChar = '*';
        private Range selectionRange;
        private bool supportsOvertype;
        private int lastSelectionStamp;
        private Keys lastPressedKey;
        private float keyRepeatDelay;
        private IBrush? imeSelectionBrush;
        private int maxLength;

        internal bool IsPlaceholderEnabled { get; set; }
        private bool ShouldEnableHintText => PlaceholderText != null && string.IsNullOrEmpty(Text) && !HasFocus;

        private Point TextStartPosition
        {
            get
            {
                var bounds = ActualBounds;
                var textSize = textLayout.Size;
                if (textSize.Y == 0) textSize.Y = textLayout.Font.LineHeight;
                Rectangle alignedBounds = textSize.Align(new(bounds.Width, bounds.Height), HorizontalAlignment.Left, VerticalTextAlignment);
                alignedBounds.Offset(bounds.Location);
                return new(alignedBounds.X, alignedBounds.Y);
            }
        }

        /// <summary>
        /// Gets or sets the value that indicates if the control accepts return input instead of handling it as command.
        /// </summary>
        [Category("Behavior")]
        public bool AcceptsReturn
        {
            get => acceptsReturn;
            set
            {
                if (acceptsReturn == value) return;
                acceptsReturn = value;
                NotifyPropertyChanged(nameof(AcceptsReturn));
            }
        }

        /// <summary>
        /// Gets or sets the value that determines the interval in seconds between cursor caret blinks.
        /// </summary>
        [Category("Behavior")]
        public float BlinkInterval
        {
            get => blinkInterval;
            set
            {
                blinkInterval = value;
                blinkDelay = 0;
                cursorOn = false;
            }
        }

        /// <summary>
        /// Gets or sets the brush of the caret.
        /// </summary>
        [Category("Appearance")]
        public IBrush? CaretBrush { get; set; }

        /// <summary>
        /// Gets the IME composition string.
        /// </summary>
        /// <remarks>
        /// Composition is the IME input preview. It doesn't represent the already entered text because user can cancel composition input.
        /// </remarks>
        public string Composition { get; private set; } = "";

        /// <summary>
        /// Gets or sets the value that determines the cursor render width.
        /// </summary>
        [Category("Appearance")]
        public int CursorWidth { get; set; } = 2;

        /// <summary>
        /// Gets or sets the position of the cursor in text.
        /// </summary>
        /// <remarks>
        /// Cursor position represents the index of the character before which the cursor is set.
        /// </remarks>
        [Category("Behavior")]
        public int CursorPosition
        {
            get => cursorPosition;
            set
            {
                if (cursorPosition == value) return;
                cursorPosition = value;
                OnCursorPositionChanged();
                NotifyPropertyChanged(nameof(CursorPosition));
            }
        }

        /// <summary>
        /// Gets the coordinates of the cursor in <see cref="TextBoxBase"/> render surface.
        /// </summary>
        public Point CursorCoordinates => GetRenderPositionByIndex(CursorPosition);

        /// <summary>
        /// Gets or sets the value that determines if the input field is prepared for the password input.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the input field should replace all text with <see cref="PasswordChar"/>. Also that means that the text can't be copied from the input field.
        /// <see langword="false"/> (default value) to input text without securing.
        /// </value>
        [Category("Behavior")]
        public bool IsPassword
        {
            get => isPassword;
            set
            {
                isPassword = value;
                UpdateRichTextLayout();
                NotifyPropertyChanged(nameof(IsPassword));
            }
        }

        /// <summary>
        /// Gets or sets the maximal text length in the <see cref="TextBoxBase"/>.
        /// </summary>
        /// <value>
        /// Length limit or <see langword="0"/> if text is limitless.
        /// </value>
        [Category("Behavior")]
        public int MaxLength
        {
            get => maxLength;
            set
            {
                if (maxLength == value) return;
                maxLength = value;
                if (maxLength > 0) Text = Text[..Math.Min(maxLength, Text.Length)];
            }
        }

        /// <summary>
        /// Gets or sets the character to replace all text with when the <see cref="IsPassword"/> mode is enabled.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue('*')]
        public char PasswordChar
        {
            get => passwordChar;
            set
            {
                passwordChar = value;
                UpdateRichTextLayout();
            }
        }

        /// <summary>
        /// Gets or sets the value that determines whether the <see cref="TextBoxBase"/> can't be edited by user.
        /// Regardless whether it's set to <see langword="true"/> or <see langword="false"/>, user can copy text from the field.
        /// </summary>
        [Category("Behavior")]
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the text that will be rendered in the <see cref="TextBoxBase"/> if it doesn't contain any other text.
        /// </summary>
        [Category("Appearance")]
        public string? PlaceholderText
        {
            get => placeholderText;
            set
            {
                placeholderText = value;
                if (string.IsNullOrEmpty(text))
                {
                    EnableHintText();
                }
            }
        }

        /// <summary>
        /// Gets or sets the brush for the text selection indication.
        /// </summary>
        [Category("Appearance")]
        public IBrush? SelectionBrush { get; set; }

        /// <summary>
        /// Gets or sets the brush for the IME text preview indication.
        /// </summary>
        [Category("Appearance")]
        public IBrush? IMESelectionBrush
        {
            get => imeSelectionBrush ?? SelectionBrush;
            set => imeSelectionBrush = value;
        }

        /// <summary>
        /// Gets or sets the text selection range.
        /// </summary>
        [Category("Behavior")]
        public Range SelectionRange
        {
            get => selectionRange;
            set
            {
                int firstPart = value.Start.GetOffset(Text.Length);
                int lastPart = value.End.GetOffset(Text.Length);
                int start = Math.Min(firstPart, lastPart);
                int end = Math.Max(firstPart, lastPart);
                selectionRange = start..end;
                OnSelectionChanged();
                NotifyPropertyChanged(nameof(SelectionRange));
            }
        }

        /// <summary>
        /// Gets the length of the text selection.
        /// </summary>
        public int SelectionLength => SelectionRange.GetOffsetAndLength(Text.Length).Length;

        /// <summary>
        /// Gets or sets the start position of the text selection.
        /// </summary>
        [Category("Behavior")]
        public int SelectionStart
        {
            get => SelectionRange.Start.GetOffset(Text.Length);
            set => SelectionRange = value..SelectionRange.End;
        }

        /// <summary>
        /// Gets or sets the end position of the text selection.
        /// </summary>
        [Category("Behavior")]
        public int SelectionEnd
        {
            get => SelectionRange.End.GetOffset(Text.Length);
            set => SelectionRange = SelectionRange.Start..value;
        }

        /// <summary>
        /// Gets the selected text part.
        /// </summary>
        public string SelectedText => Text[SelectionRange];

        /// <summary>
        /// Gets or sets the value that determines whether the control supports overtype mode.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if on <see cref="Keys.Insert"/> input overtype should be enabled, <see langword="false"/> otherwise.
        /// </value>
        [Category("Behavior")]
        public bool SupportsOvertype
        {
            get => supportsOvertype;
            set
            {
                supportsOvertype = value;
                insertMode = false;
            }
        }

        /// <summary>
        /// Gets or sets the text of the <see cref="TextBoxBase"/> control.
        /// </summary>
        [Category("Layout")]
        public string Text
        {
            get => text;
            set
            {
                if (MaxLength > 0) value = value[..Math.Min(maxLength, Text.Length)];
                SetText(value, true);
                DisableHintText();
            }
        }

        /// <summary>
        /// Gets or sets the value determines whether to wrap text in the <see cref="TextBoxBase"/>.
        /// </summary>
        [Category("Layout")]
        public bool TextWrapping
        {
            get => textWrapping;
            set
            {
                if (textWrapping == value) return;
                textWrapping = value;
                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets or sets the limit of undo operations in the current instance of the <see cref="TextBoxBase"/>.
        /// </summary>
        [Category("Behavior")]
        public int UndoLimit
        {
            get => undoLimit;
            set
            {
                if (undoLimit == value || suppressRedoStackReset) return;
                undoLimit = value;
                UndoStack = new(UndoStack, value);
                RedoStack = new(RedoStack, value);
            }
        }

        /// <summary>
        /// Gets or sets the vertical text alignment.
        /// </summary>
        [Category("Layout")]
        public VerticalAlignment VerticalTextAlignment { get; set; }

        /// <summary>
        /// Gets or sets the vertical line spacing for the <see cref="TextBoxBase"/>.
        /// </summary>
        [Category("Layout")]
        public int VerticalSpacing
        {
            get => textLayout.VerticalSpacing;
            set
            {
                textLayout.VerticalSpacing = value;
                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets or sets the timeout before starting handling last key press as input repeat.
        /// </summary>
        [Category("Behavior")]
        public float RepeatTimeout { get; set; } = 0.5f;

        /// <summary>
        /// Gets or sets the delay between handling two repeats of the key input when pressing.
        /// </summary>
        [Category("Behavior")]
        public float RepeatDelay { get; set; } = 0.1f;

        /// <summary>
        /// Gets or sets the function that filters out text from extra characters and text commands.
        /// </summary>
        public Func<string?, string?> FilterText { get; set; } = s => s?.Replace("\r", string.Empty);

        /// <summary>
        /// Occurs when the value of the <see cref="SelectionRange"/> property changes.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="CursorPosition"/> property changes.
        /// </summary>
        public event EventHandler? CursorPositionChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="Text"/> property is going to change.
        /// </summary>
        /// <remarks>
        /// The event supports cancel, if you cancel it, text won't be changed.
        /// </remarks>
        public event EventHandler<CancellableEventArgs<TextChangingData>>? TextChanging;

        /// <summary>
        /// Occurs when the value of the <see cref="Text"/> property changed.
        /// </summary>
        public event EventHandler? TextChanged;

        /// <summary>
        /// Occurs when the <see cref="Keys"/> command has been input in the <see cref="TextBoxBase"/>.
        /// </summary>
        /// <remarks>
        /// The event supports cancellation and acceptation. If you accept the event,
        /// that means the command was handled but <see cref="OnKeyDown(Keys)"/> event still will be raised.
        /// If you cancel the event, <see cref="OnKeyDown(Keys)"/> event will be cancelled too.
        /// </remarks>
        public event EventHandler<AcceptableEventArgs<Keys>>? TextCommandInput;

        /// <summary>
        /// Prepares initial data for the <see cref="TextBoxBase"/> instance.
        /// </summary>
        public TextBoxBase()
        {
            AcceptFocus = true;
            VerticalAlignment = VerticalAlignment.Top;
            ClipToBounds = true;
            AcceptsTextInput = true;
        }

        /// <summary>
        /// Inserts text string into the <see cref="Text"/> property.
        /// </summary>
        /// <param name="index">The index to start insertion from.</param>
        /// <param name="text">The text to insert.</param>
        public void Insert(int index, string? text)
        {
            text = FilterText(text);
            if (string.IsNullOrEmpty(text)) return;
            if (InsertText(index, text))
            {
                UndoStack.Insert(index, text.Length);
                CursorPosition += text.Length;
            }
        }

        /// <summary>
        /// Replaces all following text with new value.
        /// </summary>
        /// <param name="position">The index to start replacing from.</param>
        /// <param name="length">Maximal replacement length.</param>
        /// <param name="text">Text to replace.</param>
        public void Replace(int position, int length, string? text)
        {
            if (length <= 0) return;
            text = FilterText(text);
            if (string.IsNullOrEmpty(text))
            {
                Delete(position, length);
                return;
            }
            UndoStack.Replace(Text, position, length, text.Length);
            SetText(Text[..position] + text + (position + length >= Text.Length ? string.Empty : Text[(position + length)..]), false);
        }

        /// <summary>
        /// Replace all text with the new value.
        /// </summary>
        /// <param name="text">The text to replace.</param>
        public void ReplaceAllWith(string text)
        {
            if (string.IsNullOrEmpty(Text)) Replace(0, 0, text);
            else Replace(0, Text.Length, text);
        }

        /// <summary>
        /// Cancels last edit action and restores the text.
        /// </summary>
        public void Undo()
        {
            PerformUndoRedo(UndoStack, RedoStack);
        }

        /// <summary>
        /// Repeats last cancelled edit action and edits the text.
        /// </summary>
        public void Redo()
        {
            PerformUndoRedo(RedoStack, UndoStack);
        }

        /// <summary>
        /// Selects the whole text.
        /// </summary>
        public void SelectAll()
        {
            SelectionRange = Range.All;
        }

        /// <summary>
        /// Clears the text out.
        /// </summary>
        public void Clear()
        {
            Text = "";
        }

        /// <summary>
        /// Gets the width of the given character.
        /// </summary>
        /// <param name="index">Index of the character to get its width.</param>
        /// <returns>Width in pixels of the character with given index or <see langword="0"/> if the
        /// character width is unavailable or <paramref name="index"/> is more or equal to <see cref="Text"/> length.</returns>
        public float GetWidth(int index)
        {
            var glyph = textLayout.GetGlyphInfoByIndex(index);
            if (glyph == null) return 0;
            if (glyph.Value.Codepoint == '\n') return 0;
            return glyph.Value.Bounds.Width;
        }

        /// <inheritdoc/>
        protected internal override void OnKeyDown(Keys key)
        {
            if (HandleCommands(key))
            {
                base.OnKeyDown(key);
            }
            lastPressedKey = key;
        }

        /// <inheritdoc/>
        protected internal override void OnKeyUp(Keys key)
        {
            lastPressedKey = Keys.None;
            base.OnKeyUp(key);
        }

        /// <inheritdoc/>
        protected internal override void OnTextInput(TextInputEvent e)
        {
            base.OnTextInput(e);
            if (!Enabled || IsReadOnly) return;
            //var translate = Encoding.Convert(CurrentDefaultEncoding, CurrentEncoding, CurrentDefaultEncoding.GetBytes(e.Text));
            //string input = CurrentEncoding.GetString(translate);
            string input = e.Text;
            if (e.Type == TextInputEventType.Input)
            {
                // Clear the composition so it won't be inserted to the text.
                Composition = "";
                InputText(input);
            }
            else
            {
                // Update the composition.
                Composition = input;
                UpdateRichTextLayout();
                InvalidateMeasure();
            }
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchDown()
        {
            base.OnTouchDown();
            if (!Enabled) return false;
            if (Text.Length == 0) return true;
            SetCursorByTouch();
            isTouchDown = true;
            return true;
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchUp()
        {
            isTouchDown = false;
            return base.OnTouchUp();
        }

        /// <inheritdoc/>
        protected internal override void OnTouchMoved()
        {
            base.OnTouchMoved();
            SetCursorByTouch();
        }

        /// <inheritdoc/>
        protected internal override void OnDoubleClick()
        {
            base.OnDoubleClick();
            var position = CursorPosition;
            if (string.IsNullOrEmpty(Text) || position < 0 || position >= Text.Length || Desktop?.IsShiftPressed == true)
            {
                return;
            }
            if (char.IsWhiteSpace(Text[position]))
            {
                if (position == 0) return;
                position--;
                if (char.IsWhiteSpace(Text[position])) return;
            }
            int start = Text.LastIndexOf(' ', position, position);
            int end = Text.IndexOf(' ', position);
            if (start < 0) start = 0;
            if (end < 0) end = Text.Length;
            if (start == end) return;
            SelectionRange = start..end;
        }

        /// <inheritdoc/>
        protected internal override void OnGotFocus()
        {
            base.OnGotFocus();
            blinkDelay = 0;
            cursorOn = true;
            DisableHintText();
        }

        /// <inheritdoc/>
        protected internal override void OnLostFocus()
        {
            base.OnLostFocus();
            EnableHintText();
        }

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            float deltaTime = (float)DeltaTime.TotalSeconds;
            // I don't know where to update control logic so I do it there.
            if (lastPressedKey != Keys.None)
            {
                keyRepeatDelay += deltaTime;
                if (keyRepeatDelay > RepeatDelay + RepeatTimeout)
                {
                    HandleCommands(lastPressedKey);
                    keyRepeatDelay = RepeatTimeout;
                }
            }
            else
            {
                keyRepeatDelay = 0;
            }
            if (textLayout.Font == null) return;
            RenderSelection(context);
            var textColor = GetCurrentForegroundColor();
            var oldOpacity = context.Opacity;
            if (IsPlaceholderEnabled)
            {
                textColor = ForegroundColor;
                context.Opacity *= 0.5f;
            }
            var point = TextStartPosition;
            point.X -= internalScrollPosition.X;
            point.Y -= internalScrollPosition.Y;
            if (EnvironmentSettings.DebugOptions.DrawTextGlyphFrames)
            {
                foreach (var line in textLayout.Lines)
                {
                    foreach (TextChunk chunk in line.Chunks.Cast<TextChunk>())
                    {
                        foreach (var glyph in chunk.Glyphs)
                        {
                            context.DrawRectangle(glyph.Bounds, Color.White);
                        }
                    }
                }
            }

            context.DrawRichText(textLayout, (Vector2)point, textColor);
            context.Opacity = oldOpacity;

            // Skip cursor rendering if doesn't have the focus.
            if (!HasFocus) return;

            blinkDelay += deltaTime;
            if (blinkDelay >= BlinkInterval)
            {
                cursorOn = !cursorOn;
                blinkDelay = 0;
            }

            if (Enabled && cursorOn && CaretBrush != null)
            {
                point = GetRenderPositionByIndex(CursorPosition);
                point.X -= internalScrollPosition.X;
                point.Y -= internalScrollPosition.Y;
                Rectangle renderSurface = new(point.X, point.Y, CursorWidth, textLayout.Font.LineHeight);
                CaretBrush.Draw(context, renderSurface);
            }
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            if (Font == null) return Point.Zero;
            int width = availableSize.X;
            width -= CursorWidth;
            Point result = textLayout.Measure(TextWrapping ? width : null);
            if (result.Y > Font.LineHeight) result.Y = Font.LineHeight;
            else if (result.Y == 0) result.Y = Font.LineHeight;
            if (CaretBrush != null)
            {
                result.X += CursorWidth;
            }
            return result;
        }

        /// <inheritdoc/>
        protected override void OnFontChanged()
        {
            base.OnFontChanged();
            textLayout.Font = Font;
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            base.ArrangeInternal();
            int width = ActualBounds.Width;
            width -= CursorWidth;
            textLayout.Width = TextWrapping ? width : null;
        }

        /// <summary>
        /// Invokes the <see cref="CursorPositionChanged"/> event.
        /// </summary>
        protected internal virtual void OnCursorPositionChanged()
        {
            UpdateScrolling();
            CursorPositionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="SelectionChanged"/> event.
        /// </summary>
        protected internal virtual void OnSelectionChanged()
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles all default text edit commands.
        /// </summary>
        /// <param name="key">Key to handle commands with.</param>
        /// <returns><see langword="true"/> if the command was handled successfully, <see langword="false"/> otherwise.</returns>
        protected internal virtual bool HandleBuiltInCommands(Keys key)
        {
            if (Desktop == null) return false;
            // Handle all default keys and their combinations.
            switch (key)
            {
                // Ctrl+C (copy)
                case Keys.C:
                    if (Desktop.IsCtrlPressed)
                    {
                        Copy();
                    }
                    break;
                // Ctrl+V (paste)
                case Keys.V:
                    // Not necessary because input events handle clipboard paste as keyboard input.
                    //if (!IsReadOnly && Desktop.IsCtrlPressed)
                    //{
                    //    string clipboardText;
                    //    try
                    //    {
                    //        clipboardText = Clipboard.GetText();
                    //    }
                    //    catch
                    //    {
                    //        clipboardText = EnvironmentSettings.InternalClipboard;
                    //    }

                    //    if (!string.IsNullOrEmpty(clipboardText))
                    //    {
                    //        Paste(clipboardText);
                    //    }
                    //}
                    break;
                // Ctrl+X (cut)
                case Keys.X:
                    if (Desktop.IsCtrlPressed)
                    {
                        Copy();
                        if (!IsReadOnly && SelectionLength > 0)
                        {
                            DeleteSelection();
                        }
                    }
                    break;
                // Ctrl+D (duplicate)
                case Keys.D:
                    if (!IsReadOnly && Desktop.IsCtrlPressed)
                    {
                        // If nothing selected -> duplicate current line
                        if (SelectionLength <= 0)
                        {
                            // Get the start of the line.
                            int searchStart = Math.Max(0, SelectionStart - 1);
                            int lineStart = Text.LastIndexOf('\n', searchStart);
                            // Special case: cursor is on the first line.
                            if (lineStart == -1) lineStart = 0;
                            // Get the end of the line.
                            int lineEnd = Text.IndexOf('\n', lineStart);
                            // Special case: cursor is on the last line.
                            if (lineEnd == -1) lineEnd = Text.Length;
                            string line = Text[lineStart..lineEnd];
                            if (lineStart == 0)
                            {
                                line = "\n" + line;
                            }
                            Insert(lineEnd, line);
                        }
                        // Duplicate selection.
                        else
                        {
                            Insert(SelectionEnd, SelectedText);
                        }
                    }
                    break;
                // Ins (overtype mode)
                case Keys.Insert:
                    if (!IsReadOnly && SupportsOvertype)
                    {
                        insertMode = !insertMode;
                    }
                    break;
                // Ctrl+Z (undo)
                case Keys.Z:
                    if (Desktop.IsCtrlPressed)
                    {
                        Undo();
                    }
                    break;
                // Ctrl+Y (redo)
                case Keys.Y:
                    if (Desktop.IsCtrlPressed)
                    {
                        Redo();
                    }
                    break;
                // Ctrl+A (select all)
                case Keys.A:
                    if (Desktop.IsCtrlPressed)
                    {
                        SelectAll();
                    }
                    break;
                // ← (move cursor)
                case Keys.Left:
                    if (CursorPosition > 0)
                    {
                        if (Desktop.IsCtrlPressed)
                        {
                            int index = CursorPosition - 1;
                            while (index >= 0 && !char.IsPunctuation(Text, index) && !char.IsWhiteSpace(Text, index)) index--;
                            // Special case if we move before the punctuation char.
                            if (index + 1 == CursorPosition) index--;
                            CursorPosition = index + 1;
                        }
                        else
                        {
                            CursorPosition--;
                        }
                        UpdateKeyboardSelection();
                    }
                    break;
                // → (move cursor)
                case Keys.Right:
                    if (CursorPosition < Text.Length)
                    {
                        if (Desktop.IsCtrlPressed)
                        {
                            int index = CursorPosition;
                            while (index < Text.Length && !char.IsPunctuation(Text, index) && !char.IsWhiteSpace(Text, index)) index++;
                            // Special case if we move after the punctuation char.
                            if (index == CursorPosition) index++;
                            CursorPosition = index;
                        }
                        else
                        {
                            CursorPosition++;
                        }
                        UpdateKeyboardSelection();
                    }
                    break;
                // ↑ (move cursor)
                case Keys.Up:
                    if (Desktop.IsAltPressed)
                    {
                        // Alt+Up (Swap lines)
                        var line = textLayout.GetLineByCursorPosition(CursorPosition);
                        // If the current line is first, break.
                        if (line.LineIndex == 0) break;
                        // Get indices and strings of current and previous lines.
                        int lineCursorPosition = CursorPosition - line.TextStartIndex;
                        string lineText = Text[line.TextStartIndex..(line.TextStartIndex + line.Count)];
                        var previousLine = textLayout.Lines[line.LineIndex - 1];
                        string previousLineText = Text[previousLine.TextStartIndex..(previousLine.TextStartIndex + previousLine.Count)];
                        // Special case: current line is last.
                        if (previousLineText.EndsWith('\n') && !lineText.EndsWith('\n'))
                        {
                            previousLineText = previousLineText.TrimEnd('\n');
                            lineText += '\n';
                        }
                        Replace(previousLine.TextStartIndex, previousLine.Count + line.Count, lineText + previousLineText);
                        SetCursorPosition(lineCursorPosition + previousLine.TextStartIndex);
                    }
                    else
                    {
                        MoveLine(-1);
                    }
                    break;
                // ↓ (move cursor)
                case Keys.Down:
                    if (Desktop.IsAltPressed)
                    {
                        // Alt+Down (Swap lines)
                        var line = textLayout.GetLineByCursorPosition(CursorPosition);
                        // If the current line is last, break.
                        if (line.LineIndex == textLayout.Lines.Count - 1) break;
                        // Get indices and strings of current and next lines.
                        int lineCursorPosition = CursorPosition - line.TextStartIndex;
                        string lineText = Text[line.TextStartIndex..(line.TextStartIndex + line.Count)];
                        var nextLine = textLayout.Lines[line.LineIndex + 1];
                        string nextLineText = Text[nextLine.TextStartIndex..(nextLine.TextStartIndex + nextLine.Count)];
                        // Special case: next line is last.
                        if (lineText.EndsWith('\n') && !nextLineText.EndsWith('\n'))
                        {
                            lineText = lineText.TrimEnd('\n');
                            nextLineText += '\n';
                        }
                        Replace(line.TextStartIndex, line.Count + nextLine.Count, nextLineText + lineText);
                        SetCursorPosition(line.TextStartIndex + nextLine.Count + lineCursorPosition);
                    }
                    else
                    {
                        MoveLine(1);
                    }
                    break;
                // Backspace (delete previous character)
                case Keys.BackSpace:
                    if (!IsReadOnly)
                    {
                        if (SelectionLength == 0)
                        {
                            int index = CursorPosition - 1;
                            int length = 1;
                            // Delete previous word if Ctrl pressed.
                            if (Desktop.IsCtrlPressed)
                            {
                                while (index > 0 && !char.IsPunctuation(Text, index) && !char.IsWhiteSpace(Text, index))
                                {
                                    index--;
                                    length++;
                                }
                            }
                            // Special case if deleting the punctuation char.
                            if (length == 1)
                            {
                                length++;
                                index--;
                            }
                            int deleted = Delete(index + 1, length - 1);
                            if (deleted > 0)
                            {
                                SetCursorPosition(CursorPosition - deleted);
                                ResetSelection();
                            }
                        }
                        else
                        {
                            DeleteSelection();
                        }
                    }
                    break;

                case Keys.Delete:
                    if (!IsReadOnly)
                    {
                        if (SelectionLength == 0)
                        {
                            int length = 1;
                            // Delete next word if Ctrl pressed.
                            if (Desktop.IsCtrlPressed)
                            {
                                int index = CursorPosition + 1;
                                while (index < Text.Length && !char.IsPunctuation(Text, index) && !char.IsWhiteSpace(Text, index))
                                {
                                    index++;
                                    length++;
                                }
                            }
                            Delete(CursorPosition, length);
                        }
                        else
                        {
                            DeleteSelection();
                        }
                    }
                    break;
                // Tab (input 4 spaces)
                case Keys.Tab:
                    if (!AcceptsTab)
                    {
                        InputText("    ");
                    }
                    break;
                // PgUp (to the start of the text)
                case Keys.PageUp:
                    SetCursorPosition(0);
                    break;
                // Home (to the start of the line)
                case Keys.Home:
                    if (!Desktop.IsCtrlPressed && !string.IsNullOrEmpty(Text))
                    {
                        int newPosition = CursorPosition > 1 ? Text.LastIndexOf('\n', CursorPosition - 1, CursorPosition - 1) : 0;
                        SetCursorPosition(newPosition);
                    }
                    else
                    {
                        // Ctrl+Home (to the start of the text)
                        SetCursorPosition(0);
                    }
                    UpdateKeyboardSelection();
                    break;
                // PgDown (to the end of the text)
                case Keys.PageDown:
                    SetCursorPosition(Text.Length);
                    break;
                // End (to the end of the line)
                case Keys.End:
                    if (!Desktop.IsCtrlPressed)
                    {
                        int newPosition = CursorPosition < Text.Length - 1 ? Text.IndexOf('\n', CursorPosition + 1) : Text.Length;
                        if (newPosition == -1) newPosition = Text.Length;
                        SetCursorPosition(newPosition);
                    }
                    else
                    {
                        // Ctrl+End (to the end of the text).
                        SetCursorPosition(Text.Length);
                    }
                    UpdateKeyboardSelection();
                    break;

                case Keys.Escape:
                    ResetSelection();
                    break;
                // Enter (new line)
                case Keys.Enter or Keys.NumPadEnter:
                    if (!IsReadOnly)
                    {
                        InputChar('\n');
                    }
                    ResetSelection();
                    break;

                default:
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Handles all commands supported for this <see cref="TextBoxBase"/> instance.
        /// </summary>
        /// <param name="key">The key to handle commands with.</param>
        /// <returns><see langword="true"/> if commands were handled, <see langword="false"/> otherwise.</returns>
        private bool HandleCommands(Keys key)
        {
            if (!Enabled || Desktop == null)
            {
                // Just raise an event.
                return true;
            }
            if (Desktop.IsAltPressed || Desktop.IsCtrlPressed)
            {
                // Handle custom key commands
                var args = new AcceptableEventArgs<Keys>()
                {
                    Data = key
                };
                TextCommandInput?.Invoke(this, args);
                // Cancel input event at all.
                if (args.Cancel)
                {
                    return false;
                }
                // Handle only input event without default combinations.
                if (args.Handled)
                {
                    return true;
                }
            }
            return HandleBuiltInCommands(key);
        }

        private void SetCursorByTouch()
        {
            if (Desktop == null) return;
            var mousePosition = ToLocal(Desktop.TouchPosition);
            mousePosition.X += internalScrollPosition.X;
            mousePosition.Y += internalScrollPosition.Y;
            var line = textLayout.GetLineByY(mousePosition.Y) ?? textLayout.Lines.LastOrDefault();
            if (line != null)
            {
                int glyphIndex = line.GetGlyphIndexByX(mousePosition.X) ?? -1;
                if (glyphIndex != -1)
                {
                    if (mousePosition.X < line.Size.X && glyphIndex > 0) glyphIndex--;
                    SetCursorPosition(line.TextStartIndex + glyphIndex);
                    if (isTouchDown || Desktop.IsShiftPressed)
                    {
                        UpdateSelection();
                    }
                    else
                    {
                        ResetSelection();
                    }
                    return;
                }
            }
            CursorPosition = Text.Length;
        }

        private void Copy()
        {
            if (SelectionLength > 0)
            {
                string clipboardText = textLayout.Text[SelectionRange];
                try
                {
                    Clipboard.SetText(clipboardText);
                }
                catch
                {
                    EnvironmentSettings.InternalClipboard = clipboardText;
                }
            }
        }

        private bool SetText(string? value, bool resetCursor)
        {
            value = FilterText(value);
            if (value == Text)
            {
                return false;
            }
            string oldValue = Text;
            if (TextChanging != null)
            {
                var args = new CancellableEventArgs<TextChangingData>(new(oldValue, value));
                TextChanging(this, args);
                if (args.Cancel) return false;
            }
            text = value ?? "";
            UpdateRichTextLayout();
            if (resetCursor)
            {
                CursorPosition = 0;
                ResetSelection();
            }
            if (!suppressRedoStackReset)
            {
                RedoStack.Clear();
            }
            InvalidateMeasure();
            TextChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private void UpdateRichTextLayout()
        {
            if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(Composition))
            {
                textLayout.Text = text;
                EnableHintText();
                return;
            }
            DisableHintText();
            string displayText = IsPassword ? new string(PasswordChar, text.Length) : text;
            if (Composition.Length > 0)
            {
                int insertPosition = CursorPosition;
                if (SelectionLength > 0)
                {
                    // Replace the selection.
                    displayText = displayText.Remove(SelectionStart, SelectionLength);
                    if (insertPosition != 0)
                        insertPosition -= SelectionLength;
                }
                // Insert composition into text to display
                if (insertPosition >= displayText.Length)
                    displayText += Composition;
                else
                    displayText = displayText.Insert(insertPosition, Composition);
            }
            textLayout.Text = displayText;
        }

        private void DisableHintText()
        {
            if (PlaceholderText == null) return;
            textLayout.Text = text;
            IsPlaceholderEnabled = false;
        }

        private void EnableHintText()
        {
            if (ShouldEnableHintText)
            {
                textLayout.Text = PlaceholderText;
                IsPlaceholderEnabled = true;
            }
        }

        private void UpdateScrolling()
        {
            Point position = GetRenderPositionByIndex(CursorPosition);
            if (position == lastCursorPosition) return;
            var scrollViewer = Parent as ScrollViewer;
            Point size, maximum;
            Rectangle bounds = ActualBounds;
            if (scrollViewer != null)
            {
                size = new(scrollViewer.Width, scrollViewer.Height);
                size.X -= scrollViewer.VerticalScrollbarWidth;
                size.Y -= scrollViewer.HorizontalScrollbarHeight;
                maximum = scrollViewer.ScrollMaximum;
            }
            else
            {
                size = new Point(Width, Height);
                maximum = textLayout.Size;
                maximum.X = Math.Max(0, maximum.X + CursorWidth - size.X);
                maximum.Y = Math.Max(0, maximum.Y - size.Y);
            }
            if (maximum == Point.Zero)
            {
                internalScrollPosition = Point.Zero;
                lastCursorPosition = position;
                return;
            }
            position.X -= bounds.X;
            position.Y -= bounds.Y;
            int lineHeight = textLayout.Font.LineHeight;
            Point scrollPosition = scrollViewer?.ScrollPosition ?? internalScrollPosition;
            if (position.Y < scrollPosition.Y)
            {
                position.Y = scrollPosition.Y;
            }
            else if (position.Y + lineHeight > scrollPosition.Y + size.Y)
            {
                scrollPosition.Y = position.Y + lineHeight - size.Y;
            }

            if (position.X < scrollPosition.X)
            {
                position.X = scrollPosition.X;
            }
            else if (position.X + CursorWidth > scrollPosition.X + size.X)
            {
                scrollPosition.X = position.X + CursorWidth - size.X;
            }

            scrollPosition.X = Math.Clamp(scrollPosition.X, 0, maximum.X);
            scrollPosition.Y = Math.Clamp(scrollPosition.Y, 0, maximum.Y);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollPosition = scrollPosition;
            }
            else
            {
                internalScrollPosition = scrollPosition;
            }
            lastCursorPosition = position;
        }

        private int Delete(int position, int length)
        {
            if (position < 0 || position >= Text.Length || length <= 0) return 0;
            // If we're trying to delete one part
            // of a surrogate pair, delete both of them.
            if (length == 1)
            {
                if (char.IsSurrogate(Text[position])) length++;
                if (char.IsLowSurrogate(Text[position])) position--;
            }
            UndoStack.Delete(Text, position, length);
            DeleteText(position, length);
            return length;
        }

        private void DeleteSelection()
        {
            if (SelectionLength <= 0) return;
            int start = SelectionStart;
            int end = SelectionEnd;
            if (start > end) (start, end) = (end, start);
            Delete(start, end - start);
            SelectionStart = SelectionEnd = CursorPosition = start;
        }

        // Not necessary because input events handle paste as input.
        //private bool Paste(string? text)
        //{
        //    text = FilterText(text);
        //    DeleteSelection();
        //    if (InsertText(CursorPosition, text))
        //    {
        //        UndoStack.Insert(CursorPosition, text.Length);
        //        CursorPosition += text.Length;
        //        ResetSelection();
        //        return true;
        //    }
        //    return false;
        //}

        private void InputChar(char c)
        {
            if (!AcceptsReturn && c == '\n') return;
            if (insertMode && !(SelectionLength <= 0) && CursorPosition < Text.Length)
            {
                UndoStack.Replace(Text, CursorPosition, 1, 1);
                DeleteText(CursorPosition, 1);
                InsertChar(CursorPosition, c);
                SetCursorPosition(CursorPosition + 1);
            }
            else
            {
                DeleteSelection();
                InsertChar(CursorPosition, c);
                UndoStack.Insert(CursorPosition, 1);
                SetCursorPosition(CursorPosition + 1);
            }
            ResetSelection();
        }

        private void InputText(string text)
        {
            if (!AcceptsReturn && text.Contains('\n'))
            {
                text = text.Replace("\n", string.Empty);
            }
            if (insertMode && !(SelectionLength <= 0) && CursorPosition < Text.Length)
            {
                Replace(CursorPosition, text.Length, text);
            }
            else
            {
                DeleteSelection();
                Insert(CursorPosition, text);
            }
            SetCursorPosition(CursorPosition + text.Length);
            ResetSelection();
        }

        private void PerformUndoRedo(UndoRedoStack undo, UndoRedoStack redo)
        {
            if (undo.Count == 0) return;
            var record = undo.Stack.Pop();
            try
            {
                suppressRedoStackReset = true;
                switch (record.OperationType)
                {
                    case TextEditOperationType.Insertion:
                        {
                            redo.Delete(Text, record.Position, record.Length);
                            DeleteText(record.Position, record.Length);
                            SetCursorPosition(record.Position);
                            break;
                        }
                    case TextEditOperationType.Deletion:
                        {
                            if (InsertText(record.Position, record.Data))
                            {
                                redo.Insert(record.Position, record.Data.Length);
                                SetCursorPosition(record.Position);
                            }
                            break;
                        }
                    case TextEditOperationType.Replacement:
                        {
                            redo.Replace(Text, record.Position, record.Length, record.Data.Length);
                            DeleteText(record.Position, record.Length);
                            InsertText(record.Position, record.Data);
                            break;
                        }
                }
            }
            finally { suppressRedoStackReset = false; }
            ResetSelection();
        }

        private void SetCursorPosition(int newPosition)
        {
            CursorPosition = Math.Clamp(newPosition, 0, Text.Length);
        }

        private void ResetSelection()
        {
            SelectionRange = CursorPosition..CursorPosition;
            lastSelectionStamp = CursorPosition;
        }

        private void UpdateSelection()
        {
            SelectionRange = lastSelectionStamp..CursorPosition;
        }

        private void UpdateKeyboardSelection()
        {
            if (Desktop!.IsShiftPressed)
            {
                UpdateSelection();
            }
            else
            {
                ResetSelection();
            }
        }

        private void MoveLine(int delta)
        {
            var line = textLayout.GetLineByCursorPosition(CursorPosition);
            if (line == null) return;
            var newLine = line.LineIndex + delta;
            if (newLine < 0 || newLine >= textLayout.Lines.Count) return;
            var bounds = ActualBounds;
            var position = GetRenderPositionByIndex(CursorPosition);
            var preferredX = position.X - bounds.X;

            // Find closest glyph
            var newString = textLayout.Lines[newLine];
            var cursorPosition = newString.TextStartIndex;
            var glyphIndex = newString.GetGlyphIndexByX(preferredX);
            if (glyphIndex != null)
            {
                cursorPosition += glyphIndex.Value;
            }
            SetCursorPosition(cursorPosition);
            UpdateKeyboardSelection();
        }

        private void DeleteText(int position, int length)
        {
            if (length == 0) return;
            SetText(Text[..position] + Text[(position + length)..], false);
        }

        private bool InsertText(int position, [NotNullWhen(true)] string? s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (string.IsNullOrEmpty(Text))
            {
                SetText(s, false);
            }
            else
            {
                SetText(Text.Insert(position, s), false);
            }
            return true;
        }

        private void InsertChar(int position, char c)
        {
            if (string.IsNullOrEmpty(Text)) SetText(c.ToString(), false);
            else SetText(Text.Insert(position, c.ToString()), false);
        }

        private Point GetRenderPositionByIndex(int index)
        {
            Point result = TextStartPosition;
            if (!string.IsNullOrEmpty(textLayout.Text))
            {
                if (index < textLayout.Text.Length)
                {
                    var glyph = textLayout.GetGlyphInfoByIndex(index);
                    if (glyph != null)
                    {
                        result.X += glyph.Value.Bounds.Left;
                        result.Y += glyph.Value.LineTop;
                    }
                }
                else if (textLayout.Lines != null && textLayout.Lines.Count > 0)
                {
                    // Get after the last glyph
                    var lastLine = textLayout.Lines[^1];
                    if (lastLine.Count > 0)
                    {
                        var glyph = lastLine.GetGlyphInfoByIndex(lastLine.Count - 1);
                        result.X += glyph!.Value.Bounds.Right;
                        // If the last character is whitespace, add a few pixels (1/4 of the font size) to indicate the last whitespace
                        // (By default, font processor doesn't provide it any size because it is not rendered in the end of the line).
                        if (char.IsWhiteSpace(textLayout.Text[^1])) result.X += (int)Math.Ceiling(textLayout.Font.FontSize * 0.25);
                        result.Y += glyph!.Value.LineTop;
                        // If the last character is the new line, move it to  the new line
                        // (Because the font processor doesn't render new line as well).
                        if (textLayout.Text[^1] == '\n') result.Y += textLayout.Font.LineHeight + VerticalSpacing;
                    }
                    else
                    {
                        result.Y += textLayout.Font.LineHeight * Math.Max(textLayout.Lines.Count - 1, 0) + VerticalSpacing * Math.Max(textLayout.Lines.Count - 2, 0);
                    }
                }
            }
            return result;
        }

        private void RenderSelection(RenderContext context)
        {
            var bounds = ActualBounds;
            if (string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(Composition)) return;
            // Firstly, if the IME selection is on, render it.
            if (!string.IsNullOrEmpty(Composition) && IMESelectionBrush != null)
            {
                RenderSelectionInternal(context, bounds, CursorPosition, CursorPosition + Composition.Length, IMESelectionBrush);
                return;
            }
            // Otherwise render common selection.
            if (SelectionLength <= 0 || SelectionBrush == null) return;
            RenderSelectionInternal(context, bounds, SelectionStart, SelectionEnd, SelectionBrush);
        }

        private void RenderSelectionInternal(RenderContext context, Rectangle bounds, int selectionStart, int selectionEnd, IBrush selectionBrush)
        {
            var startGlyph = textLayout.GetGlyphInfoByIndex(selectionStart);
            if (startGlyph == null) return;
            int lineIndex = startGlyph.Value.TextChunk.LineIndex;
            int lineHeight = textLayout.Font.LineHeight;
            for (int i = selectionStart; ; i = textLayout.Lines[lineIndex].TextStartIndex)
            {
                startGlyph = textLayout.GetGlyphInfoByIndex(i);
                if (startGlyph == null)
                {
                    break;
                }
                Point startPosition = GetRenderPositionByIndex(i);
                var line = textLayout.Lines[startGlyph.Value.TextChunk.LineIndex];
                if (selectionEnd < line.TextStartIndex + line.Count)
                {
                    Point endPosition = GetRenderPositionByIndex(selectionEnd);
                    selectionBrush.Draw(context,
                                        new(startPosition.X - internalScrollPosition.X,
                                            startPosition.Y - internalScrollPosition.Y,
                                            endPosition.X - startPosition.X,
                                            lineHeight));
                    break;
                }
                selectionBrush.Draw(context,
                                    new(startPosition.X - internalScrollPosition.X,
                                        startPosition.Y - internalScrollPosition.Y,
                                        bounds.Left + startGlyph.Value.TextChunk.Size.X - startPosition.X,
                                        lineHeight));
                lineIndex++;
                if (lineIndex >= textLayout.Lines.Count) break;
            }
        }

        /// <summary>
        /// Represents the struct for the <see cref="TextChanging"/> event.
        /// </summary>
        /// <param name="OldValue"><see cref="Text"/> before editing.</param>
        /// <param name="NewValue"><see cref="Text"/> after editing.</param>
        public readonly record struct TextChangingData(string? OldValue, string? NewValue);
    }
}