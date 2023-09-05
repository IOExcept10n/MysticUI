using MysticUI.Extensions.Text;
using Stride.Input;
using System.Globalization;
using System.Text;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a default <see cref="TextBoxBase"/> implementation.
    /// </summary>
    public class TextBox : TextBoxBase
    {
        /// <summary>
        /// Custom handler for text input events. It can transform input text if needed.
        /// </summary>
        public Func<TextInputEvent, string> InputHandler { get; set; }

        /// <summary>
        /// Creates a new instance of the <see cref="TextBox"/> class.
        /// </summary>
        public TextBox()
        {
            InputHandler = HandleTextInput!;
        }

        /// <inheritdoc/>
        protected internal override void OnTextInput(TextInputEvent e)
        {
            e.Text = InputHandler(e);
            base.OnTextInput(e);
        }

        /// <summary>
        /// Default input handling function. It's needed to fix the text encoding bug.
        /// </summary>
        /// <remarks>
        /// This bugfix related to issue https://github.com/stride3d/stride/issues/1728
        /// </remarks>
        /// <param name="inputEvent">An input event.</param>
        /// <returns>Recoded text from the <paramref name="inputEvent"/> or original text if the encoding shouldn't be changed.</returns>
        private string HandleTextInput(TextInputEvent inputEvent)
        {
            // If text was entered using IME or was pasted from the clipboard or current composition string is not empty, don't handle it.
            if (inputEvent.Type == TextInputEventType.Composition || inputEvent.Text.Length > 2 ||
                !string.IsNullOrEmpty(Composition))
                return inputEvent.Text;
            // Change text encoding for the current keyboard layout
            CultureInfo culture = KeyboardLayout.GetCurrentKeyboardLayout();
            if (culture.TextInfo.ANSICodePage != 0)
            {
                var bytes = Encoding.Unicode.GetBytes(inputEvent.Text);
                string text = Encoding.GetEncoding(culture.TextInfo.ANSICodePage).GetString(bytes).Replace("\0", "");
                return text;
            }
            return inputEvent.Text;
        }
    }
}