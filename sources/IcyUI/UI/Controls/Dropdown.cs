// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input.Events;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="Selector"/> with a fixed-text closed-state display (<see cref="Selector.SelectedItem"/>'s
    /// display text) and letter-key typeahead - no free text entry.
    /// </summary>
    public class Dropdown : Selector
    {
        private ITextEvents? subscribedText;

        /// <summary>
        /// Initializes a new instance of the <see cref="Dropdown"/> class.
        /// </summary>
        public Dropdown()
        {
            IsFocusable = true;
            SelectionChanged += (_, _) => UpdateToggleContent();
            FocusChanged += Dropdown_FocusChanged;
        }

        private void Dropdown_FocusChanged(object? sender, EventArgs e)
        {
            if (IsFocused)
            {
                if (Configuration != null)
                {
                    subscribedText = Configuration.Input.Events.Text;
                    subscribedText.TextInput += OnTextInput;
                }
            }
            else if (subscribedText != null)
            {
                subscribedText.TextInput -= OnTextInput;
                subscribedText = null;
            }
        }

        private void UpdateToggleContent() =>
            Toggle.Content = new TextBlock { Text = SelectedItem == null ? string.Empty : GetDisplayText(SelectedItem) };

        private void OnTextInput(object? sender, GenericEventArgs<ITextInputEventInfo> e)
        {
            if (e.Data.Type != TextInputEventType.Input || e.Data.Text.Length == 0 || ItemCount == 0)
                return;

            char letter = e.Data.Text[0];
            if (char.IsControl(letter))
                return;
            string search = letter.ToString();

            int start = (SelectedIndex + 1) % ItemCount;
            for (int offset = 0; offset < ItemCount; offset++)
            {
                int index = (start + offset) % ItemCount;
                if (GetDisplayText(GetItemAt(index)).StartsWith(search, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedIndex = index;
                    return;
                }
            }
        }
    }
}
