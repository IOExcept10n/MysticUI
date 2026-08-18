using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TextBoxTests
    {
        private static (Canvas Canvas, FakeInputSystem Input, TextBox TextBox) CreateFocusedTextBox()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            var textBox = new TextBox { Width = 100, Height = 20 };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);
            return (canvas, input, textBox);
        }

        [Fact]
        public void Text_SetNull_CoercesToEmptyString()
        {
            var textBox = new TextBox { Text = null! };

            Assert.Equal(string.Empty, textBox.Text);
        }

        [Fact]
        public void Focus_EnablesTextInput_Unfocus_DisablesIt()
        {
            var (canvas, input, textBox) = CreateFocusedTextBox();

            Assert.True(input.Events.Text.IsTextInputEnabled);

            canvas.Focus(null);

            Assert.False(input.Events.Text.IsTextInputEnabled);
        }

        [Fact]
        public void TextInput_WhileFocused_AppendsToText()
        {
            var (_, input, textBox) = CreateFocusedTextBox();

            input.Events.Text.RaiseTextInput("abc");

            Assert.Equal("abc", textBox.Text);
        }

        [Fact]
        public void TextInput_WhileNotFocused_IsIgnored()
        {
            var (canvas, input, textBox) = CreateFocusedTextBox();
            canvas.Focus(null);

            input.Events.Text.RaiseTextInput("abc");

            Assert.Equal(string.Empty, textBox.Text);
        }

        [Fact]
        public void LeftArrow_MovesCaretForSubsequentInsert()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("ac");

            input.Keyboard.RaiseKeyDown(Keys.Left);
            input.Events.Text.RaiseTextInput("b");

            Assert.Equal("abc", textBox.Text);
        }

        [Fact]
        public void Backspace_RemovesCharacterBeforeCaret()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("abc");

            input.Keyboard.RaiseKeyDown(Keys.Back);

            Assert.Equal("ab", textBox.Text);
        }

        [Fact]
        public void TextInput_ControlCharacter_IsIgnored()
        {
            // Regression: Windows' WM_CHAR (which MonoGame's Window.TextInput is backed by) fires for control
            // characters too, not just printable ones - Backspace produces '\b', Enter '\r', Tab '\t'. Those
            // already have dedicated handling via the raw KeyDown event; letting them through here as "typed
            // text" would splice a literal control character into the text and double-process a single key press.
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("ab");

            input.Events.Text.RaiseTextInput("\b");

            Assert.Equal("ab", textBox.Text);
        }

        [Fact]
        public void Backspace_Twice_RemovesTwoCharactersNotOne()
        {
            // Regression: Text's setter clamps caretIndex to the new (shorter) length as a side effect, and
            // Keys.Back used to decrement caretIndex again on top of that - a no-op bug at the end of the text
            // (the common case) drove the caret two positions left per backspace instead of one, so a second
            // backspace would silently miss a character instead of removing the next one.
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("abc");

            input.Keyboard.RaiseKeyDown(Keys.Back);
            input.Keyboard.RaiseKeyDown(Keys.Back);

            Assert.Equal("a", textBox.Text);
        }

        [Fact]
        public void Backspace_RepeatedlyToEmpty_DoesNotThrow()
        {
            // Regression: backspacing the last character used to drive caretIndex to -1 (crashed on Stride).
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("ab");

            input.Keyboard.RaiseKeyDown(Keys.Back);
            input.Keyboard.RaiseKeyDown(Keys.Back);
            input.Keyboard.RaiseKeyDown(Keys.Back);

            Assert.Equal(string.Empty, textBox.Text);
        }

        [Fact]
        public void Backspace_ThenInsert_InsertsAtEndNotOneShort()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("abc");

            input.Keyboard.RaiseKeyDown(Keys.Back);
            input.Events.Text.RaiseTextInput("z");

            Assert.Equal("abz", textBox.Text);
        }

        [Fact]
        public void Delete_RemovesCharacterAfterCaret()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("abc");
            input.Keyboard.RaiseKeyDown(Keys.Left);
            input.Keyboard.RaiseKeyDown(Keys.Left);
            input.Keyboard.RaiseKeyDown(Keys.Left);

            input.Keyboard.RaiseKeyDown(Keys.Delete);

            Assert.Equal("bc", textBox.Text);
        }

        [Fact]
        public void Home_MovesCaretToStartForSubsequentInsert()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("bc");

            input.Keyboard.RaiseKeyDown(Keys.Home);
            input.Events.Text.RaiseTextInput("a");

            Assert.Equal("abc", textBox.Text);
        }

        [Fact]
        public void End_MovesCaretToEndForSubsequentInsert()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("ab");
            input.Keyboard.RaiseKeyDown(Keys.Home);

            input.Keyboard.RaiseKeyDown(Keys.End);
            input.Events.Text.RaiseTextInput("c");

            Assert.Equal("abc", textBox.Text);
        }

        [Fact]
        public void TextChanged_FiresOnEdit()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            int raisedCount = 0;
            textBox.TextChanged += (_, _) => raisedCount++;

            input.Events.Text.RaiseTextInput("a");

            Assert.Equal(1, raisedCount);
        }
    }
}
