// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Icy.Data;
using Icy.Data.Markup.Attributes;
using Icy.Input.Devices;
using Icy.Input.Events;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.Rendering.Fonts;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A single-line editable text field. Builds on the existing <see cref="ITextEvents"/> character synthesizer
    /// for typed input and raw <see cref="IKeyboardInput"/> (only while focused) for caret movement/deletion, and
    /// draws its own caret.
    /// </summary>
    /// <remarks>
    /// No text selection (mouse-drag-to-select, Shift+arrow, clipboard cut/copy/paste) in v1 - just single-caret
    /// editing. No multi-line/wrapping support either, matching <see cref="TextBlock"/>.
    /// </remarks>
    public class TextBox : Control
    {
        private int caretIndex;
        private string fontFamily = string.Empty;
        private float fontSize = 16;
        private float scrollOffset;
        private IKeyboardInput? subscribedKeyboard;
        private ITextEvents? subscribedTextEvents;
        private string text = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextBox"/> class.
        /// </summary>
        public TextBox()
        {
            IsFocusable = true;
            FocusChanged += OnFocusChanged;
        }

        /// <summary>
        /// Occurs when <see cref="Text"/> changes.
        /// </summary>
        public event EventHandler? TextChanged;

        /// <summary>
        /// Gets or sets the font family to render <see cref="Text"/> with. See <see cref="TextBlock.FontFamily"/>
        /// for how the family is resolved.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue("")]
        [RegisterReference]
        [AffectsMeasure]
        public string FontFamily
        {
            get => fontFamily;
            set
            {
                if (SetProperty(ref fontFamily, value))
                {
                    InvalidateMeasure();
                }
            }
        }

        /// <summary>
        /// Gets or sets the font size, in pixels.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(16f)]
        [RegisterReference]
        [AffectsMeasure]
        public float FontSize
        {
            get => fontSize;
            set
            {
                if (SetProperty(ref fontSize, value))
                {
                    InvalidateMeasure();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current text.
        /// </summary>
        [Category("Content")]
        [DefaultValue("")]
        [RegisterReference]
        [AffectsMeasure]
        public string Text
        {
            get => text;
            set
            {
                string newValue = value ?? string.Empty;
                if (!SetProperty(ref text, newValue))
                    return;
                caretIndex = Math.Clamp(caretIndex, 0, text.Length);
                InvalidateMeasure();
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Left/Right move the caret by one character and claim the press.</description></item>
        /// <item><description>At the start (Left) or end (Right) of the text the press isn't claimed, so focus moves on.</description></item>
        /// <item><description>Up/Down are never claimed: the box is single-line.</description></item>
        /// </list>
        /// </remarks>
        protected internal override bool OnNavigate(Vector2 direction)
        {
            if (direction.X < 0 && caretIndex > 0)
                caretIndex--;
            else if (direction.X > 0 && caretIndex < Text.Length)
                caretIndex++;
            else
                return false;

            InvalidateVisual();
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Both axes SUM chrome (border + padding, from <see cref="Control.MeasureContent"/> measuring
        /// <see cref="Control.Chrome"/>) with the text's own size - never <see cref="Math.Max(int, int)"/>. The text
        /// is drawn at a fixed <see cref="UIElement.Padding"/>-derived offset from the top, so the box must reserve
        /// room for the padding/border above the text AND below it, on top of the text's own full line height -
        /// <c>Max</c>'ing them (as height alone used to) only reserves whichever of the two happens to be taller,
        /// so the text silently overflows past the box's own bottom edge by roughly the bottom padding/border
        /// whenever the text's line height (the common case for any real font) exceeds the chrome's own size.
        /// </remarks>
        protected override Size MeasureContent()
        {
            Size chromeSize = base.MeasureContent();
            IFont? font = ResolveFont();
            if (font == null)
                return chromeSize;

            Vector2 textSize = font.MeasureAdvance(Text.Length > 0 ? Text : " ", DefaultRenderingOptions(Vector2.Zero));
            return new Size(
                chromeSize.Width + (int)MathF.Ceiling(textSize.X),
                chromeSize.Height + (int)MathF.Ceiling(textSize.Y));
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            base.OnDetached();
            Unsubscribe();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// When <see cref="Text"/> is wider than the visible content area, this scrolls horizontally to keep the
        /// caret in view - like a WPF <c>TextBox</c>, never like <see cref="TextBlock"/> (which has no caret and
        /// simply overflows/clips). <see cref="scrollOffset"/> persists across renders and is only nudged when the
        /// caret would otherwise leave a small margin band near either edge, so scrolling doesn't jitter on every
        /// keystroke - it moves exactly as far as needed to bring the caret back into view, the same way typing
        /// past the edge of a real text field does.
        /// </remarks>
        protected override void OnRender(IRenderContext context)
        {
            base.OnRender(context);

            IFont? font = ResolveFont();
            if (font == null)
                return;

            Vector2 textOrigin = new(Padding.Left + BorderThickness.Left, Padding.Top + BorderThickness.Top);
            Rectangle contentBounds = new Rectangle(Point.Empty, ActualBounds.Size) - (Padding + BorderThickness);
            float caretX = font.MeasureAdvance(Text[..caretIndex], DefaultRenderingOptions(Vector2.Zero)).X;

            if (contentBounds.Width > 0)
            {
                // A margin so the caret never sits flush against the visible edge - the user can see the next
                // couple of characters they're about to type over/delete, not just the caret itself. Two "0"s is
                // a reasonably representative two-character width for most fonts; capped against a third of the
                // visible width so it never eats the whole box on a very narrow TextBox.
                float margin = MathF.Min(contentBounds.Width / 3f, font.MeasureAdvance("00", DefaultRenderingOptions(Vector2.Zero)).X);

                if (caretX - scrollOffset > contentBounds.Width - margin)
                    scrollOffset = caretX - contentBounds.Width + margin;
                else if (caretX - scrollOffset < margin)
                    scrollOffset = caretX - margin;

                float fullTextWidth = font.MeasureAdvance(Text, DefaultRenderingOptions(Vector2.Zero)).X;
                scrollOffset = Math.Clamp(scrollOffset, 0, MathF.Max(0, fullTextWidth - contentBounds.Width));
            }
            else
            {
                scrollOffset = 0;
            }

            // Narrows ClipToBounds's existing scissor (already set to this whole control's ActualBounds, chrome
            // included, by UIElement.Draw before OnRender runs) down to just the content area - otherwise scrolled
            // text can bleed left into the padding/border band instead of disappearing behind it.
            Rectangle screenContentBounds = context.Transform.Apply(contentBounds);
            Rectangle oldScissor = context.Options.Scissor;
            context.Options.Scissor = Rectangle.Intersect(oldScissor, screenContentBounds);

            Vector2 scrolledOrigin = textOrigin with { X = textOrigin.X - scrollOffset };
            font.DrawString(context, Text, DefaultRenderingOptions(scrolledOrigin));

            if (IsFocused)
            {
                Rectangle caretRect = new(
                    (int)(scrolledOrigin.X + MathF.Ceiling(caretX)),
                    (int)textOrigin.Y,
                    1,
                    (int)MathF.Ceiling(font.Metrics.Ascent - font.Metrics.Descent));
                new SolidColorBrush(Foreground).Draw(context, GetDefaultRenderOptions() with { Destination = caretRect });
            }

            context.Options.Scissor = oldScissor;
        }

        private FontRenderingOptions DefaultRenderingOptions(Vector2 position) => new(
            Position: position,
            Scale: null,
            Rotation: 0,
            Origin: Vector2.Zero,
            CharacterSpacing: 0,
            LineSpacing: 0,
            Color: Foreground,
            Depth: ZIndex,
            Effect: null);

        private void InsertText(string inserted)
        {
            if (string.IsNullOrEmpty(inserted))
                return;

            // Defends against a platform's text-input event leaking control characters as "typed text" - e.g.
            // Windows' WM_CHAR (which MonoGame.Window.TextInput is backed by) fires for Backspace ('\b'), Enter
            // ('\r'), Tab ('\t'), etc., not just printable characters. Those already have their own dedicated
            // handling via the raw KeyDown event (see OnKeyDown); inserting them here too as literal characters
            // would double-process a single key press (e.g. Backspace both deleting via KeyDown *and* splicing a
            // literal '\b' into the text here). Filtered here (not just at the MonoGame source) so any other
            // engine backend that leaks the same class of event is covered too.
            if (inserted.Any(char.IsControl))
                inserted = new string(inserted.Where(c => !char.IsControl(c)).ToArray());

            if (inserted.Length == 0)
                return;

            string current = Text;
            Text = current[..caretIndex] + inserted + current[caretIndex..];
            caretIndex += inserted.Length;
            InvalidateVisual();
        }

        private void OnFocusChanged(object? sender, EventArgs e)
        {
            if (IsFocused)
                Subscribe();
            else
                Unsubscribe();
        }

        private void OnKeyDown(object? sender, GenericEventArgs<Keys> e)
        {
            switch (e.Data)
            {
                case Keys.Back:
                    if (caretIndex > 0)
                    {
                        // Compute the target caret position from the pre-deletion index, then assign it (not
                        // decrement) after the Text setter runs - Text's setter already clamps caretIndex against
                        // the new, shorter length as a side effect (see its Math.Clamp call), so decrementing
                        // again here on top of that double-moved the caret whenever it sat at the end of the text
                        // (the common case), eventually driving it to -1 on the last character and crashing.
                        int newCaretIndex = caretIndex - 1;
                        Text = Text[..(caretIndex - 1)] + Text[caretIndex..];
                        caretIndex = newCaretIndex;
                    }

                    break;
                case Keys.Delete:
                    if (caretIndex < Text.Length)
                        Text = Text[..caretIndex] + Text[(caretIndex + 1)..];
                    break;
                case Keys.Home:
                    caretIndex = 0;
                    break;
                case Keys.End:
                    caretIndex = Text.Length;
                    break;
                default:
                    return;
            }

            InvalidateVisual();
        }

        private void OnTextInput(object? sender, GenericEventArgs<ITextInputEventInfo> e)
        {
            // Skip in-progress IME composition previews for v1 - only commit finalized input.
            if (e.Data.Type != TextInputEventType.Input)
                return;
            InsertText(e.Data.Text);
        }

        /// <summary>
        /// Resolves the font to measure and draw <see cref="Text"/> with.
        /// </summary>
        /// <returns>The resolved font, or <see langword="null"/> when nothing at all could be resolved.</returns>
        /// <remarks>
        /// Falls back in three steps, mirroring <see cref="TextBlock"/>'s own <c>ResolveFont</c>: this element's
        /// own <see cref="FontFamily"/>, then <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/>, then
        /// <see cref="Icy.Rendering.Fonts.FontSystem.FallbackFont"/>. Without this, a <see cref="TextBox"/> that
        /// never had <see cref="FontFamily"/> set explicitly - such as <see cref="ComboBox"/>'s internal text box,
        /// which never sets it - rendered no text, no caret, and measured as zero-sized, regardless of what font
        /// the rest of the document was using.
        /// </remarks>
        private IFont? ResolveFont()
        {
            if (Configuration?.Fonts is not { } fonts)
                return null;

            string family = FontFamily.Length > 0 ? FontFamily : fonts.DefaultFontFamily;
            return family.Length == 0 ? fonts.FallbackFont : fonts.GetOrLoad(new FontInfo(family, FontSize, FontStyle.Regular));
        }

        private void Subscribe()
        {
            if (Configuration == null)
                return;

            subscribedTextEvents = Configuration.Input.Events.Text;
            subscribedTextEvents.TextInput += OnTextInput;
            subscribedTextEvents.EnableTextInput();

            subscribedKeyboard = Configuration.Input.Keyboard;
            if (subscribedKeyboard != null)
                subscribedKeyboard.KeyDown += OnKeyDown;
        }

        private void Unsubscribe()
        {
            if (subscribedTextEvents != null)
            {
                subscribedTextEvents.TextInput -= OnTextInput;
                subscribedTextEvents.DisableTextInput();
                subscribedTextEvents = null;
            }

            if (subscribedKeyboard != null)
            {
                subscribedKeyboard.KeyDown -= OnKeyDown;
                subscribedKeyboard = null;
            }
        }
    }
}
