// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
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
        protected override Size MeasureContent()
        {
            Size chromeSize = base.MeasureContent();
            IFont? font = ResolveFont();
            if (font == null)
                return chromeSize;

            Vector2 textSize = font.MeasureString(Text.Length > 0 ? Text : " ", DefaultRenderingOptions(Vector2.Zero));
            return new Size(
                chromeSize.Width + (int)MathF.Ceiling(textSize.X),
                Math.Max(chromeSize.Height, (int)MathF.Ceiling(textSize.Y)));
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            base.OnDetached();
            Unsubscribe();
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            base.OnRender(context);

            IFont? font = ResolveFont();
            if (font == null)
                return;

            Vector2 textOrigin = new(Padding.Left + BorderThickness.Left, Padding.Top + BorderThickness.Top);
            font.DrawString(context, Text, DefaultRenderingOptions(textOrigin));

            if (!IsFocused)
                return;

            Vector2 caretOffset = font.MeasureString(Text[..caretIndex], DefaultRenderingOptions(Vector2.Zero));
            Rectangle caretRect = new(
                (int)(textOrigin.X + MathF.Ceiling(caretOffset.X)),
                (int)textOrigin.Y,
                1,
                (int)MathF.Ceiling(font.Metrics.Ascent - font.Metrics.Descent));
            new SolidColorBrush(Foreground).Draw(context, GetDefaultRenderOptions() with { Destination = caretRect });
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
                        Text = Text[..(caretIndex - 1)] + Text[caretIndex..];
                        caretIndex--;
                    }

                    break;
                case Keys.Delete:
                    if (caretIndex < Text.Length)
                        Text = Text[..caretIndex] + Text[(caretIndex + 1)..];
                    break;
                case Keys.Left:
                    caretIndex = Math.Max(0, caretIndex - 1);
                    break;
                case Keys.Right:
                    caretIndex = Math.Min(Text.Length, caretIndex + 1);
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

        private IFont? ResolveFont() =>
            FontFamily.Length == 0 ? null : Configuration?.Fonts.GetOrLoad(new FontInfo(FontFamily, FontSize, FontStyle.Regular));

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
