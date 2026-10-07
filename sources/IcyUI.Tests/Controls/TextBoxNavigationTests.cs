// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Input.Devices;
using Icy.Tests.UI;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TextBoxNavigationTests
    {
        [Fact]
        public void LeftAndRight_MoveTheCaret_AndEscapeOnlyAtTheEdges()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var textBox = new TextBox { Width = 100, Height = 20, Text = "ab" };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);

            // The caret starts at 0: Left is at the edge, Right moves.
            Assert.False(input.Events.Navigation.RaiseFocusChanging(-Vector2.UnitX).Handled);
            Assert.True(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX).Handled);
            input.Events.Text.RaiseTextInput("X");
            Assert.Equal("aXb", textBox.Text);

            Assert.True(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX).Handled);
            Assert.False(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX).Handled);
        }

        [Fact]
        public void UpAndDown_AreNeverClaimed()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var textBox = new TextBox { Width = 100, Height = 20, Text = "ab" };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);

            Assert.False(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitY).Handled);
            Assert.False(input.Events.Navigation.RaiseFocusChanging(-Vector2.UnitY).Handled);
        }

        [Fact]
        public void TheRawLeftKey_NoLongerMovesTheCaretOnItsOwn()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var textBox = new TextBox { Width = 100, Height = 20, Text = "ab" };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);
            input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX);

            // Only the navigation route moves the caret now, so a lone KeyDown (no navigation event) changes nothing.
            input.Keyboard.RaiseKeyDown(Keys.Left);
            input.Events.Text.RaiseTextInput("X");

            Assert.Equal("aXb", textBox.Text);
        }

        [Fact]
        public void ComboBoxTextBox_KeepsLeftRightForTheCaret_AndDownLeavesWhileClosed()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var comboBox = new ComboBox { Width = 120, Height = 24, ItemsSource = new List<object> { "Apple", "Banana" } };
            canvas.Add(comboBox);
            canvas.Render();
            TextBox textBox = comboBox.EnumerateVisualSubtree().OfType<TextBox>().First();
            canvas.Focus(textBox);
            // A committed selection gives the box text without leaving the popup open (closing re-syncs the text from it).
            comboBox.SelectedIndex = 0;
            Assert.False(comboBox.IsOpen);
            Assert.Equal("Apple", textBox.Text);

            Assert.True(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX).Handled);
            Assert.Same(textBox, canvas.FocusedElement);
            Assert.False(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitY).Handled);
        }
    }
}
