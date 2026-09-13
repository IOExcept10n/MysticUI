// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Icy.UI.Controls
{
    /// <summary>
    /// The result a <see cref="MessageBox"/> (or any <see cref="Dialog{TResult}"/> that chooses to reuse it)
    /// closes with.
    /// </summary>
    public enum DialogResult
    {
        /// <summary>No button was pressed - the dialog was dismissed some other way (e.g. Escape).</summary>
        None,

        /// <summary>The user pressed OK.</summary>
        OK,

        /// <summary>The user pressed Cancel.</summary>
        Cancel,

        /// <summary>The user pressed Yes.</summary>
        Yes,

        /// <summary>The user pressed No.</summary>
        No,
    }

    /// <summary>
    /// The set of buttons a <see cref="MessageBox.ShowAsync(Canvas, string, DialogButtons)"/> message box shows.
    /// </summary>
    public enum DialogButtons
    {
        /// <summary>A single OK button.</summary>
        OK,

        /// <summary>OK and Cancel buttons.</summary>
        OKCancel,

        /// <summary>Yes and No buttons.</summary>
        YesNo,

        /// <summary>Yes, No, and Cancel buttons.</summary>
        YesNoCancel,
    }

    /// <summary>
    /// Static convenience helpers that build a small content tree and show it via <see cref="Dialog{TResult}"/> -
    /// a message with a button row (<see cref="ShowAsync(Canvas, string, DialogButtons)"/>), or a single-line
    /// text prompt (<see cref="ShowInputAsync(Canvas, string, string?)"/>). A separate, non-generic class rather
    /// than static members on <see cref="Dialog{TResult}"/> itself, to avoid declaring statics on a generic type
    /// (CA1000) while keeping <see cref="Dialog{TResult}"/>'s own API fully type-safe.
    /// </summary>
    public static class MessageBox
    {
        /// <summary>
        /// Shows a modal message with a row of buttons, and returns the <see cref="DialogResult"/> of whichever
        /// one the user pressed.
        /// </summary>
        /// <param name="canvas">The canvas to show the message box on.</param>
        /// <param name="message">The message text to display.</param>
        /// <param name="buttons">Which buttons to show. Defaults to <see cref="DialogButtons.OK"/>.</param>
        /// <returns>A task that completes with the pressed button's <see cref="DialogResult"/>.</returns>
        public static Task<DialogResult> ShowAsync(Canvas canvas, string message, DialogButtons buttons = DialogButtons.OK)
        {
            var dialog = new Dialog<DialogResult>();
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 12) });

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (DialogResult result in ButtonsFor(buttons))
            {
                var button = new Button { Content = new TextBlock { Text = result.ToString() }, Margin = new Thickness(4, 0, 0, 0) };
                button.Click += (_, _) => dialog.Close(result);
                buttonRow.Children.Add(button);
            }

            panel.Children.Add(buttonRow);
            dialog.Content = panel;
            return dialog.ShowAsync(canvas);
        }

        /// <summary>
        /// Shows a modal single-line text prompt with OK/Cancel buttons.
        /// </summary>
        /// <param name="canvas">The canvas to show the prompt on.</param>
        /// <param name="prompt">The prompt text to display above the text box.</param>
        /// <param name="defaultValue">The text box's initial value. Defaults to an empty string.</param>
        /// <returns>
        /// A task that completes with the text box's value when OK is pressed, or <see langword="null"/> when
        /// Cancel is pressed.
        /// </returns>
        public static Task<string?> ShowInputAsync(Canvas canvas, string prompt, string? defaultValue = null)
        {
            var dialog = new Dialog<string?>();
            var textBox = new TextBox { Text = defaultValue ?? string.Empty, Margin = new Thickness(0, 0, 0, 12) };
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(textBox);

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = new TextBlock { Text = "OK" } };
            ok.Click += (_, _) => dialog.Close(textBox.Text);
            var cancel = new Button { Content = new TextBlock { Text = "Cancel" }, Margin = new Thickness(4, 0, 0, 0) };
            cancel.Click += (_, _) => dialog.Close(null);
            buttonRow.Children.Add(ok);
            buttonRow.Children.Add(cancel);

            panel.Children.Add(buttonRow);
            dialog.Content = panel;
            return dialog.ShowAsync(canvas);
        }

        private static IEnumerable<DialogResult> ButtonsFor(DialogButtons buttons) => buttons switch
        {
            DialogButtons.OK => [DialogResult.OK],
            DialogButtons.OKCancel => [DialogResult.OK, DialogResult.Cancel],
            DialogButtons.YesNo => [DialogResult.Yes, DialogResult.No],
            DialogButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
            _ => throw new ArgumentOutOfRangeException(nameof(buttons)),
        };
    }
}
